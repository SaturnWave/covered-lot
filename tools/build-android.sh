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
#   - the build changed what git sees (each file's status, the diff against HEAD and the untracked files' contents, as
#     tools/check.sh compares them), because the CLI's guard would refuse the next build. A successful build changes
#     files when Unity writes settings on a target's first build: review them, then commit or revert them. A failed
#     build can leave temporary entries, such as the Input System's preloaded actions asset in ProjectSettings.asset,
#     which Unity removes only after a successful build: revert those.
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

# repository_state: what git sees of the working tree, as tools/check.sh records it: every file's status, untracked
# files included, the diff against HEAD, and the content hashes of the untracked files, which the diff leaves out.
repository_state() {
  git status --porcelain=v1 --untracked-files=all || return 1
  echo "--- diff against HEAD"
  git diff HEAD --binary || return 1
  echo "--- untracked contents"
  git -c core.quotePath=false ls-files --others --exclude-standard | git hash-object --stdin-paths
}

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
# The state files hold the whole diff against HEAD, so they stay in the ignored build folder.
state_before="$BUILD_DIR/state-before.txt"
state_after="$BUILD_DIR/state-after.txt"
repository_state > "$state_before"

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
repository_state > "$state_after"
if ! cmp -s "$state_before" "$state_after"; then
  if [ "$status" -ne 0 ]; then
    echo "build-android: the failed build changed what git sees; a failed build can leave temporary entries, so revert" >&2
    echo "build-android: what it changed (git checkout -- <file>) rather than commit it:" >&2
  else
    echo "build-android: the build changed what git sees; review what it changed, then commit or revert it before the" >&2
    echo "build-android: next build:" >&2
  fi
  status_lines_before="$(sed '/^--- diff against HEAD$/q' "$state_before")"
  status_lines_after="$(sed '/^--- diff against HEAD$/q' "$state_after")"
  if [ "$status_lines_before" = "$status_lines_after" ]; then
    echo "build-android: a file that already differed from HEAD, or an untracked file, changed; one of these:" >&2
    git status --short --untracked-files=all >&2 || true
  else
    echo "build-android: git status before (<) and after (>) the build:" >&2
    diff <(echo "$status_lines_before") <(echo "$status_lines_after") >&2 || true
  fi
  failures=1
fi

if [ "$failures" -ne 0 ]; then
  exit 1
fi
size="$(wc -c < "$APK" | tr -d ' ')"
sha="$(sha256sum "$APK" | cut -d ' ' -f 1)"
echo "build-android: built $APK ($size bytes, sha256 $sha)"
