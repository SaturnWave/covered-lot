#!/usr/bin/env bash
# Builds the Android development APK (design section 11, piece 12) through the Unity CLI:
#   unity build . --target Android --execute-method CoveredLot.EditorTools.DevelopmentBuild.Android
#     --output-path Builds/Android/CoveredLot-dev.apk --android-export-type apk --timeout 3600
#     --log-file artifacts/local/build/android.log
# The CLI refuses uncommitted changes, so a build comes from a commit; --allow-dirty-build, passed on to the CLI, skips
# that guard. With --execute-method the CLI returns the method's exit code, and it writes its provenance manifest next to
# the APK (Builds/ is ignored). The CLI's own output goes to artifacts/local/build/android.cli.txt. The script fails when:
#   - the CLI ends with an exit code other than 0;
#   - the APK is missing;
#   - the editor log holds a compiler error or warning (error CS or warning CS) for a file under Assets/CoveredLot. The
#     log holds only what this build compiled: when Bee serves the player's scripts from its cache, the log shows no
#     compile and this check has nothing to read; csc.rsp's -warnaserror+ stays the guard;
#   - the build changed what git sees: Unity writes some settings on a target's first build, and the CLI's guard would
#     refuse the next build until they are committed or reverted.
# It refuses to start while Temp/UnityLockfile exists. Unity's Gradle and Bee caches follow GRADLE_USER_HOME and
# BEE_CACHE_DIRECTORY in the environment the editor starts with (piece 12 checked both by path), so the script prints
# both. It builds the repository it belongs to, from whatever folder it is started.
#
# Usage: tools/build-android.sh [--allow-dirty-build]
# Exit codes: 0 when the APK was built cleanly; 1 when the build or a check failed; 2 when Temp/UnityLockfile exists or
# an argument is unknown.
export LC_ALL=C LANG=C
set -euo pipefail

readonly APK=Builds/Android/CoveredLot-dev.apk
readonly BUILD_DIR=artifacts/local/build
readonly LOG=$BUILD_DIR/android.log
readonly CLI_OUTPUT=$BUILD_DIR/android.cli.txt
readonly LOCK_FILE=Temp/UnityLockfile
# The first IL2CPP build compiles the engine's code for ARM64 and fills the Gradle cache: 29 minutes on this project's
# first build, against a timeout of an hour.
readonly TIMEOUT_SECONDS=3600
readonly METHOD=CoveredLot.EditorTools.DevelopmentBuild.Android

cd "$(dirname "${BASH_SOURCE[0]}")/.."

cli_options=()
for argument in "$@"; do
  case "$argument" in
    --allow-dirty-build) cli_options+=(--allow-dirty-build) ;;
    *)
      echo "build-android: unknown argument $argument; usage: tools/build-android.sh [--allow-dirty-build]" >&2
      exit 2
      ;;
  esac
done

if [ -e "$LOCK_FILE" ]; then
  echo "build-android: $LOCK_FILE exists, so a Unity editor may have this project open; close it and build again." >&2
  echo "build-android: if no editor is running, an editor that was ended left the file behind: delete the Temp folder." >&2
  exit 2
fi

# No apostrophe may stand in these defaults: inside a double-quoted expansion, bash reads one as a quote.
echo "build-android: GRADLE_USER_HOME=${GRADLE_USER_HOME:-<unset: the default Gradle home>}"
echo "build-android: BEE_CACHE_DIRECTORY=${BEE_CACHE_DIRECTORY:-<unset: the default Bee cache>}"

# The editor wants absolute paths. Git Bash's pwd -W gives the Windows form; elsewhere pwd is the absolute path.
repo_root="$(pwd -W 2>/dev/null || pwd)"
mkdir -p "$BUILD_DIR" "$(dirname "$APK")"
rm -f "$APK" "$LOG" "$CLI_OUTPUT"
status_before="$(git status --porcelain=v1 --untracked-files=all)"

started=$SECONDS
status=0
unity build . --target Android --execute-method "$METHOD" --output-path "$repo_root/$APK" --android-export-type apk \
  --timeout "$TIMEOUT_SECONDS" --log-file "$repo_root/$LOG" --no-tail --no-banner ${cli_options[@]+"${cli_options[@]}"} \
  > "$CLI_OUTPUT" 2>&1 || status=$?
echo "build-android: the CLI ended with exit code $status after $((SECONDS - started)) s"

failures=0
if [ "$status" -ne 0 ]; then
  echo "build-android: the build failed; see $LOG and $CLI_OUTPUT" >&2
  failures=1
fi
if [ ! -f "$APK" ]; then
  echo "build-android: there is no APK at $APK" >&2
  failures=1
fi
if [ ! -f "$LOG" ]; then
  echo "build-android: the editor wrote no log at $LOG" >&2
  failures=1
else
  # Unity writes compiler paths with backslashes or with slashes. -a reads the log as text even if it holds a NUL byte,
  # which would make grep print only "Binary file matches".
  compiler_lines="$(grep -a -E '(error|warning) CS[0-9]+' "$LOG" | grep -a -E 'Assets[\\/]CoveredLot[\\/]' || true)"
  if [ -n "$compiler_lines" ]; then
    echo "build-android: the compiler reported on our code:" >&2
    echo "$compiler_lines" >&2
    failures=1
  fi
fi
status_after="$(git status --porcelain=v1 --untracked-files=all)"
if [ "$status_after" != "$status_before" ]; then
  echo "build-android: the build changed what git sees; commit or revert these before the next build:" >&2
  echo "$status_after" >&2
  failures=1
fi

if [ "$failures" -ne 0 ]; then
  exit 1
fi
size="$(wc -c < "$APK" | tr -d ' ')"
sha="$(sha256sum "$APK" | cut -d ' ' -f 1)"
echo "build-android: built $APK ($size bytes, sha256 $sha)"
