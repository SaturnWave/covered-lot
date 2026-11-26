#!/usr/bin/env bash
# Checks the repository's ignore and attribute rules against a fixed table, so a change to
# .gitignore or .gitattributes cannot silently start tracking build output or secrets, or
# stop tracking source. It also pins what the scripts and .editorconfig promise: every
# tools/*.sh is executable and fixes its locale first, and the level texts keep no final
# newline. It checks the repository it belongs to, from whatever folder it is started.
# Exit code 0 when every expectation holds.
export LC_ALL=C LANG=C
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/.."

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

# Every tracked script is stored executable, so it runs on macOS and in CI as it does here.
while IFS= read -r entry; do
  mode="${entry%% *}"
  if [ "$mode" != 100755 ]; then
    fail "${entry##*$'\t'} is stored with mode $mode, expected 100755 (git update-index --chmod=+x)"
  fi
done < <(git ls-files -s -- 'tools/*.sh')

# Every script fixes its locale before anything else, so sorting and character classes do not
# depend on the shell that starts it.
for script in tools/*.sh; do
  first_command="$(awk 'NR > 1 && NF > 0 && !/^#/ { print; exit }' "$script")"
  if [ "$first_command" != "export LC_ALL=C LANG=C" ]; then
    fail "$script does not start with export LC_ALL=C LANG=C"
  fi
done

# The level texts must stay byte-identical to the engine's output, which has no final newline,
# so .editorconfig must not ask editors to add one or to trim lines under Assets/Generated.
generated_section="$(awk '/^\[/ { in_section = ($0 == "[Assets/Generated/**]"); next } in_section' .editorconfig)"
for setting in "insert_final_newline = false" "trim_trailing_whitespace = false"; do
  if ! grep -q -x -F -e "$setting" <<< "$generated_section"; then
    fail ".editorconfig does not set $setting for [Assets/Generated/**]"
  fi
done

if [ "$failures" -gt 0 ]; then
  echo "check-repo: $failures failure(s)" >&2
  exit 1
fi
echo "check-repo: every ignore, attribute, script and .editorconfig expectation holds"
