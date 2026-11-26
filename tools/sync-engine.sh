#!/usr/bin/env bash
# Copies the rules engine into the Unity project, from one commit of the engine repository
# (SaturnWave/puzzle-engine):
#   - the Release builds of PuzzleCore.dll and PuzzleCore.Jam.dll (no PDBs);
#   - the canonical level texts and the presentation file that the engine's own gen writes
#     for the campaign pack;
#   - the brief's covered 6x6 board, the Example.
# It also writes a provenance record (engine-sync.txt) and sha256sum-format checksums of
# every staged file (engine-sync.sha256). Nothing is generated on the phone.
#
# Usage:
#   tools/sync-engine.sh <engine-dir> <commit>     copy from that commit into Assets/Generated
#   tools/sync-engine.sh <engine-dir> --check      rebuild at the recorded commit and compare
#
# The commit must be an ancestor of the engine's origin/master; this script never fetches.
# It works on a `git archive` of the commit, so the engine checkout (which another session
# shares) is never touched. Close the Unity editor first: Unity must not import half a sync.
# Exit codes: 0 done (or, with --check, identical), 1 refused or different, 2 usage error.
set -euo pipefail

readonly ENGINE_REPOSITORY=https://github.com/SaturnWave/puzzle-engine
readonly PACK=campaign-jam
readonly SLOTS=100
readonly EXAMPLE=packs/jam/golden/six-by-six.pc-lot

usage() {
  echo "usage: tools/sync-engine.sh <engine-dir> <commit> | tools/sync-engine.sh <engine-dir> --check" >&2
  exit 2
}
fail() {
  echo "sync-engine: $1" >&2
  exit 1
}

[ "$#" -eq 2 ] || usage
engine="$1"
repo_root="$(git rev-parse --show-toplevel)"
cd "$repo_root"

readonly GENERATED=Assets/Generated
readonly RECORD="$GENERATED/engine-sync.txt"
readonly CHECKSUMS="$GENERATED/engine-sync.sha256"

if [ "$2" = --check ]; then
  mode=check
  [ -f "$RECORD" ] || fail "no $RECORD to check against"
  commit_arg="$(sed -n 's/^engine-commit //p' "$RECORD")"
  [ -n "$commit_arg" ] || fail "$RECORD names no engine commit"
else
  mode=sync
  commit_arg="$2"
fi

# 1. The commit must be on the engine's published master.
commit="$(git -C "$engine" rev-parse --verify --quiet "$commit_arg^{commit}")" ||
  fail "$commit_arg is not a commit of the engine repository"
if ! git -C "$engine" merge-base --is-ancestor "$commit" origin/master; then
  fail "$commit is not on the engine's origin/master (fetch the engine first if it should be)"
fi

work=artifacts/local/engine-sync
archive="$work/$commit"
stage="$work/$commit-stage"
rm -rf "$archive" "$stage"
mkdir -p "$archive" "$stage/gen" "$stage/out/PuzzleCore" "$stage/out/Resources/Levels"
trap 'rm -rf "$archive" "$stage"' EXIT

# 2. Unpack the commit. The archive is read from stdin: GNU tar would take a "D:" file
#    name for a remote host.
git -C "$engine" archive "$commit" | (cd "$archive" && tar -x -f -)

# 3. Build the CLI in Release. That builds PuzzleCore and PuzzleCore.Jam under the engine's
#    own build guards, with the engine's global.json and nuget.config from the archive.
if ! (cd "$archive" && dotnet build src/PuzzleCore.Cli/PuzzleCore.Cli.csproj -c Release -nologo) > "$stage/build.log" 2>&1; then
  tail -n 40 "$stage/build.log" >&2
  fail "the engine build failed"
fi
sdk_version="$(cd "$archive" && dotnet --version)"

# 4. Generate the campaign with the engine's own gen. The CLI is already built, so exit
#    code 1 can only mean rejected slots, which gen leaves out of the pack.
gen_dir="$repo_root/$stage/gen"
set +e
(cd "$archive" && dotnet src/PuzzleCore.Cli/bin/Release/net10.0/PuzzleCore.Cli.dll gen "$PACK" "$SLOTS" "$gen_dir") > "$stage/gen.log" 2>&1
gen_status=$?
set -e
if [ "$gen_status" -ne 0 ] && [ "$gen_status" -ne 1 ]; then
  tail -n 20 "$stage/gen.log" >&2
  fail "gen exited with $gen_status"
fi

presentation="$stage/gen/$PACK.presentation.json"
[ -f "$presentation" ] || fail "gen wrote no $PACK.presentation.json"
level_ids=()
while IFS= read -r id; do
  level_ids+=("$id")
done < <(grep -o -E '"levelId"[[:space:]]*:[[:space:]]*"[^"]+"' "$presentation" | sed -E 's/.*"([^"]+)"$/\1/')
[ "${#level_ids[@]}" -gt 0 ] || fail "the presentation file lists no level"
for id in "${level_ids[@]}"; do
  [[ "$id" =~ ^[a-z0-9-]+$ ]] || fail "unexpected level id in the presentation file"
  [ -f "$stage/gen/$id.pc-lot" ] || fail "gen wrote no level file for $id"
done
written="$(find "$stage/gen" -maxdepth 1 -name '*.pc-lot' | wc -l)"
[ "$written" -eq "${#level_ids[@]}" ] || fail "gen wrote $written level files for ${#level_ids[@]} listed levels"

# 5. Stage exactly the files the game ships, with their bytes unchanged.
out="$stage/out"
cp "$archive/src/PuzzleCore/bin/Release/netstandard2.1/PuzzleCore.dll" "$out/PuzzleCore/"
cp "$archive/src/PuzzleCore.Jam/bin/Release/netstandard2.1/PuzzleCore.Jam.dll" "$out/PuzzleCore/"
for id in "${level_ids[@]}"; do
  cp "$stage/gen/$id.pc-lot" "$out/Resources/Levels/$id.txt"
done
cp "$presentation" "$out/Resources/Levels/presentation.json"
cp "$archive/$EXAMPLE" "$out/Resources/Levels/six-by-six.txt"

# 6. The provenance record: no time and no local path, so a re-sync is a no-op.
printf '%s\n' \
  "engine-repository $ENGINE_REPOSITORY" \
  "engine-commit $commit" \
  "dotnet-sdk $sdk_version" \
  "configuration Release" \
  "gen $PACK $SLOTS" \
  "example $EXAMPLE" \
  "levels ${#level_ids[@]}" > "$out/engine-sync.txt"

# 7. One sha256sum line per staged file, sorted by path, relative to Assets/Generated.
#    Checked with: cd Assets/Generated && sha256sum -c engine-sync.sha256
(
  cd "$out"
  find . -type f ! -name 'engine-sync.*' | sed 's#^\./##' | LC_ALL=C sort | while IFS= read -r file; do
    printf '%s  %s\n' "$(sha256sum < "$file" | cut -d' ' -f1)" "$file"
  done
) > "$out/engine-sync.sha256"

if [ "$mode" = check ]; then
  different=0
  for record in engine-sync.txt engine-sync.sha256; do
    if ! cmp -s "$out/$record" "$GENERATED/$record"; then
      echo "sync-engine: $GENERATED/$record differs from a rebuild at $commit" >&2
      diff "$GENERATED/$record" "$out/$record" >&2 || true
      different=1
    fi
  done
  if ! (cd "$GENERATED" && sha256sum -c --quiet engine-sync.sha256); then
    echo "sync-engine: files under $GENERATED do not match their checksums" >&2
    different=1
  fi
  [ "$different" -eq 0 ] || exit 1
  echo "sync-engine: $GENERATED matches a rebuild at $commit (${#level_ids[@]} levels)"
  exit 0
fi

# 8. Copy into Assets/Generated. Files with unchanged bytes are left alone, so Unity does
#    not re-import them, and Unity's .meta files are never rewritten. A file this script no
#    longer ships (a level that disappeared) is deleted together with its .meta.
mkdir -p "$GENERATED"
while IFS= read -r -d '' existing; do
  relative="${existing#"$GENERATED"/}"
  case "$relative" in
    *.meta) continue ;;
  esac
  if [ ! -f "$out/$relative" ]; then
    rm -f "$existing" "$existing.meta"
    echo "removed $relative"
  fi
done < <(find "$GENERATED" -type f -print0)

copied=0
while IFS= read -r -d '' staged; do
  relative="${staged#"$out"/}"
  target="$GENERATED/$relative"
  if ! cmp -s "$staged" "$target"; then
    mkdir -p "$(dirname "$target")"
    cp "$staged" "$target"
    copied=$((copied + 1))
  fi
done < <(find "$out" -type f -print0)

(cd "$GENERATED" && sha256sum -c --quiet engine-sync.sha256) || fail "the copied files do not match their checksums"
echo "sync-engine: $GENERATED holds engine $commit with ${#level_ids[@]} levels ($copied file(s) changed)"
