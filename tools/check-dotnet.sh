#!/usr/bin/env bash
# Runs the .NET checks of the Unity-free code. It builds dotnet/CoveredLot.Core.Tests in Release, which builds
# dotnet/CoveredLot.Core from the same files Unity compiles (Assets/CoveredLot/Core) against the engine DLLs in
# Assets/Generated, and runs the tests twice: under tr-TR, then under sv-SE. The tests require
# PUZZLE_TEST_CULTURE and fail without it. It runs from dotnet/, so dotnet/global.json pins the SDK.
# TRX results go to artifacts/local/dotnet/<culture>/. No Unity editor is needed.
#
# Usage: tools/check-dotnet.sh
# Exit codes: 0 when both runs pass with at least one test each, 1 otherwise.
set -euo pipefail

readonly CULTURES=(tr-TR sv-SE)
readonly TRX_NAME=CoveredLot.Core.Tests.trx

repo_root="$(git rev-parse --show-toplevel)"
cd "$repo_root/dotnet"

# run_tests <culture>: one test run; prints why and returns 1 when it fails or reports no test.
run_tests() {
  local culture="$1"
  local results="../artifacts/local/dotnet/$culture"
  # errexit is off inside a function called from an if, so every step checks its own status.
  rm -rf "$results" || return 1
  mkdir -p "$results" || return 1

  if ! PUZZLE_TEST_CULTURE="$culture" dotnet test CoveredLot.Core.Tests -c Release -nologo \
      --logger "trx;LogFileName=$TRX_NAME" --results-directory "$results"; then
    echo "check-dotnet: the tests failed under $culture" >&2
    return 1
  fi

  local trx="$results/$TRX_NAME"
  if [ ! -f "$trx" ]; then
    echo "check-dotnet: no TRX result was written under $culture" >&2
    return 1
  fi
  local total
  total="$(grep -o '<Counters [^>]*' "$trx" | sed -n 's/.* total="\([0-9]*\)".*/\1/p')"
  if [ -z "$total" ] || [ "$total" -eq 0 ]; then
    echo "check-dotnet: the run under $culture reported no test" >&2
    return 1
  fi
  echo "check-dotnet: $total tests passed under $culture"
}

failures=0
for culture in "${CULTURES[@]}"; do
  if ! run_tests "$culture"; then
    failures=$((failures + 1))
  fi
done

if [ "$failures" -gt 0 ]; then
  echo "check-dotnet: $failures of ${#CULTURES[@]} culture runs failed" >&2
  exit 1
fi
echo "check-dotnet: green under ${CULTURES[*]}"
