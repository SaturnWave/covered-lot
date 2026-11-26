#!/usr/bin/env bash
# Runs every check of the repository, Unity's included (design section 10.5), and reports each step's result:
#   1. tools/check-repo.sh: the ignore, attribute, script and .editorconfig rules;
#   2. tools/check-dotnet.sh: the .NET tests of the Unity-free Core, under tr-TR and under sv-SE;
#   3. the Unity tests, EditMode and PlayMode, each under tr-TR and under sv-SE, through `unity test`, which waits for
#      the editor and never passes -quit. Each run writes its NUnit report and editor log to
#      artifacts/local/unity/<mode>-<culture>.xml and .log. This script first deletes the old reports and logs, so one
#      of an earlier run can never stand in for a run that wrote none, and the CoveredLot DLLs and PDBs in
#      Library/ScriptAssemblies, which the first run writes again from Unity's build of the current sources. Unity
#      compiles by content: a source whose time changed but whose content did not (a touch, a checkout) leaves its DLL
#      as it was, older than the source, so without the deletion UnityChecks would refuse a DLL that is current;
#   4. the repository after those runs: no *.meta file is untracked, and git status, the diff against HEAD and the
#      untracked files are as they were before the runs (a session that does not quit at once saves the upgrades that
#      packages make on load);
#   5. dotnet/CoveredLot.UnityChecks, under tr-TR and under sv-SE: the IL rules over Unity's build of the CoveredLot
#      assemblies, and the reports and logs of the four runs.
# It needs the Unity CLI (`unity`) and the .NET SDK that dotnet/global.json pins, and the Unity editor closed: it
# refuses to start while Temp/UnityLockfile exists. Once the project is imported, a full run takes about two minutes,
# most of it the four Unity runs; in a fresh clone the first run imports the whole project first. It checks the
# repository it belongs to, from whatever folder it is started.
#
# Usage: tools/check.sh
# Exit codes: 0 when every step passed; 1 when any step failed; 2 when Temp/UnityLockfile exists at the start.
export LC_ALL=C LANG=C
set -euo pipefail

readonly MODES=(EditMode PlayMode)
readonly CULTURES=(tr-TR sv-SE)
readonly UNITY_TIMEOUT_SECONDS=1800
readonly LOCK_FILE=Temp/UnityLockfile
readonly RUNS_DIR=artifacts/local/unity
readonly CHECKS_DIR=artifacts/local/unity-checks
readonly SCRIPT_ASSEMBLIES_DIR=Library/ScriptAssemblies
readonly TRX_NAME=CoveredLot.UnityChecks.trx

cd "$(dirname "${BASH_SOURCE[0]}")/.."

if [ -e "$LOCK_FILE" ]; then
  echo "check: $LOCK_FILE exists, so a Unity editor may have this project open; close it and run check.sh again." >&2
  echo "check: if no editor is running, an editor that was ended (a crash, a timeout, a run stopped at a compile" >&2
  echo "check: error) left the file behind: delete the Temp folder and run check.sh again." >&2
  exit 2
fi

# The editor wants absolute paths. Git Bash's pwd -W gives the Windows form; elsewhere pwd is the absolute path.
repo_root="$(pwd -W 2>/dev/null || pwd)"
work_dir="$(mktemp -d)"
trap 'rm -rf "$work_dir"' EXIT

summary=()
failures=0
total_started=$SECONDS

# step <name> <command> [<argument>...]: runs one step, reports it and records its result and duration. A failed step
# never stops the script, so every step reports. errexit is off inside a command run as an if condition, so each step
# function checks its own commands' status.
step() {
  local name="$1" started=$SECONDS outcome=PASS
  shift
  echo "check: --- $name"
  if ! "$@"; then
    outcome=FAIL
    failures=$((failures + 1))
  fi
  summary+=("$outcome  $name ($((SECONDS - started)) s)")
  echo "check: $outcome  $name"
}

# unity_tests <mode> <culture>: one Unity test run. The CLI's own output goes to <run>.cli.txt next to the report. A run
# that the CLI ended (exit code 6) can leave Temp/UnityLockfile behind; the editor of the next run starts all the same.
unity_tests() {
  local mode="$1" culture="$2"
  local run="$mode-$culture" status=0
  PUZZLE_TEST_CULTURE="$culture" unity test . --mode "$mode" --output "$repo_root/$RUNS_DIR/$run.xml" \
    --timeout "$UNITY_TIMEOUT_SECONDS" --no-banner -- -logFile "$repo_root/$RUNS_DIR/$run.log" \
    > "$RUNS_DIR/$run.cli.txt" 2>&1 || status=$?
  case "$status" in
    0) echo "check: the $run run ended with exit code 0; UnityChecks reads its report" ;;
    8) echo "check: tests failed in the $run run; see $RUNS_DIR/$run.xml" >&2 ;;
    6) echo "check: the $run run gave no verdict (a compile error, the licence, a crash or the timeout); see $RUNS_DIR/$run.log" >&2 ;;
    *) echo "check: the $run run ended with exit code $status; see $RUNS_DIR/$run.cli.txt" >&2 ;;
  esac
  return "$status"
}

# repository_state: what git sees of the working tree: every file's status, untracked files included, the diff
# against HEAD, and the content hashes of the untracked files, which the diff leaves out.
repository_state() {
  git status --porcelain=v1 --untracked-files=all || return 1
  echo "--- diff against HEAD"
  git diff HEAD --binary || return 1
  echo "--- untracked contents"
  git -c core.quotePath=false ls-files --others --exclude-standard | git hash-object --stdin-paths
}

# repository_unchanged <state before the runs>: fails when a .meta file is untracked or the runs changed what git sees.
repository_unchanged() {
  local before="$1" after="$work_dir/state-after.txt" status=0 untracked_metas
  repository_state > "$after" || return 1
  if ! cmp -s "$before" "$after"; then
    local status_before="$work_dir/status-before.txt" status_after="$work_dir/status-after.txt"
    sed '/^--- diff against HEAD$/q' "$before" > "$status_before"
    sed '/^--- diff against HEAD$/q' "$after" > "$status_after"
    if cmp -s "$status_before" "$status_after"; then
      echo "check: the Unity runs changed a file that already differed from HEAD, or an untracked file; one of these:" >&2
      git status --short --untracked-files=all >&2 || true
    else
      echo "check: the Unity runs changed git status (< before, > after):" >&2
      diff "$status_before" "$status_after" >&2 || true
    fi
    status=1
  fi
  untracked_metas="$(git ls-files --others --exclude-standard -- '*.meta')" || return 1
  if [ -n "$untracked_metas" ]; then
    echo "check: these .meta files are untracked; commit each with the file it describes:" >&2
    echo "$untracked_metas" >&2
    status=1
  fi
  return "$status"
}

# unity_checks <culture>: dotnet/CoveredLot.UnityChecks in Release; fails when it fails, runs no test or skips one.
unity_checks() {
  local culture="$1"
  local results="$CHECKS_DIR/$culture"
  rm -rf "$results" || return 1
  mkdir -p "$results" || return 1
  if ! (cd dotnet && PUZZLE_TEST_CULTURE="$culture" dotnet test CoveredLot.UnityChecks -c Release -nologo \
      --logger "trx;LogFileName=$TRX_NAME" --results-directory "../$results"); then
    echo "check: UnityChecks failed under $culture" >&2
    return 1
  fi

  local trx="$results/$TRX_NAME" counters total passed
  if [ ! -f "$trx" ]; then
    echo "check: UnityChecks wrote no TRX result under $culture" >&2
    return 1
  fi
  counters="$(grep -o '<Counters [^>]*' "$trx")" || counters=""
  total="$(sed -n 's/.* total="\([0-9]*\)".*/\1/p' <<< "$counters")"
  passed="$(sed -n 's/.* passed="\([0-9]*\)".*/\1/p' <<< "$counters")"
  if [ -z "$total" ] || [ "$total" -eq 0 ]; then
    echo "check: UnityChecks ran no test under $culture" >&2
    return 1
  fi
  if [ "$passed" != "$total" ]; then
    echo "check: ${passed:-no} of $total UnityChecks tests passed under $culture; a skipped test fails the check" >&2
    return 1
  fi
  echo "check: $total UnityChecks tests passed under $culture"
}

step "tools/check-repo.sh" tools/check-repo.sh
step "tools/check-dotnet.sh" tools/check-dotnet.sh

mkdir -p "$RUNS_DIR"
state_before="$work_dir/state-before.txt"
repository_state > "$state_before"
for mode in "${MODES[@]}"; do
  for culture in "${CULTURES[@]}"; do
    rm -f "$RUNS_DIR/$mode-$culture.xml" "$RUNS_DIR/$mode-$culture.log" "$RUNS_DIR/$mode-$culture.cli.txt"
  done
done
rm -f "$SCRIPT_ASSEMBLIES_DIR"/CoveredLot*.dll "$SCRIPT_ASSEMBLIES_DIR"/CoveredLot*.pdb
for mode in "${MODES[@]}"; do
  for culture in "${CULTURES[@]}"; do
    step "Unity $mode tests under $culture" unity_tests "$mode" "$culture"
  done
done
step "the repository after the Unity runs" repository_unchanged "$state_before"
for culture in "${CULTURES[@]}"; do
  step "UnityChecks under $culture" unity_checks "$culture"
done

echo "check: --- summary"
for line in "${summary[@]}"; do
  echo "check: $line"
done
seconds=$((SECONDS - total_started))
if [ "$failures" -gt 0 ]; then
  echo "check: $failures of ${#summary[@]} steps failed ($seconds s)" >&2
  exit 1
fi
echo "check: green: all ${#summary[@]} steps passed ($seconds s)"
