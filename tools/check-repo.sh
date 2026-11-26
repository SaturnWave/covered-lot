#!/usr/bin/env bash
# Checks the repository's ignore and attribute rules against a fixed table, so a change to
# .gitignore or .gitattributes cannot silently start tracking build output or secrets, or
# stop tracking source. Exit code 0 when every expectation holds.
set -euo pipefail

repo_root="$(git rev-parse --show-toplevel)"
cd "$repo_root"

failures=0
fail() {
  echo "FAIL  $1"
  failures=$((failures + 1))
}

must_be_ignored=(
  Library/x
  Temp/x
  Obj/x
  Logs/x
  UserSettings/x
  Builds/Android/a.apk
  Build/x
  artifacts/local/x
  dotnet/CoveredLot.Core/bin/x
  dotnet/CoveredLot.Core/obj/x
  dotnet/CoveredLot.Core.Tests/bin/Release/x.dll
  CoveredLot.Core.csproj
  Assembly-CSharp.csproj
  covered-lot.sln
  covered-lot.slnx
  Assets/InitTestScene123.unity
  .env
  .vs/x
  .idea/x
  .vscode/x
  x.pdb
  x.log
)
# Signing files, built here so that this file does not trip the scan's keystore pattern.
for extension in keystore jks p12 p8 pfx pem mobileprovision; do
  must_be_ignored+=("signing/a.$extension")
done
must_be_tracked=(
  dotnet/CoveredLot.Core/CoveredLot.Core.csproj
  dotnet/CoveredLot.Core.Tests/CoveredLot.Core.Tests.csproj
  Assets/CoveredLot/Editor/Build/Probe.cs
  Assets/CoveredLot/Logs/Probe.cs
  Assets/Generated/PuzzleCore/PuzzleCore.dll
  Assets/Generated/Resources/Levels/campaign-jam-0000.txt
  Assets/Generated/engine-sync.txt
  tools/check.sh
  .gitattributes
  .editorconfig
)

for path in "${must_be_ignored[@]}"; do
  if ! git check-ignore -q --no-index -- "$path"; then
    fail "$path should be ignored"
  fi
done
for path in "${must_be_tracked[@]}"; do
  if git check-ignore -q --no-index -- "$path"; then
    fail "$path should not be ignored"
  fi
done

# attribute_is <attribute> <expected value> <path>...
attribute_is() {
  local attribute="$1" expected="$2" path value
  shift 2
  for path in "$@"; do
    value="$(git check-attr "$attribute" -- "$path" | sed 's/^.*: //')"
    if [ "$value" != "$expected" ]; then
      fail "$path has $attribute=$value, expected $expected"
    fi
  done
}
attribute_is eol lf x.sh x.cs x.txt x.json x.md x.asmdef x.uss x.unity x.meta x.asset
attribute_is binary set x.dll x.png x.ttf

# Every tracked text file is stored with LF line endings.
while IFS= read -r line; do
  index_eol="$(awk '{ print $1 }' <<< "$line")"
  if [ "$index_eol" != "i/lf" ] && [ "$index_eol" != "i/-text" ] && [ "$index_eol" != "i/none" ]; then
    fail "stored with $index_eol: ${line##*$'\t'}"
  fi
done < <(git ls-files --eol)

if [ "$failures" -gt 0 ]; then
  echo "check-repo: $failures failure(s)" >&2
  exit 1
fi
echo "check-repo: every ignore and attribute expectation holds"
