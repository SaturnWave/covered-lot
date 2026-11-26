#!/usr/bin/env bash
# Proves that tools/scan-before-push.sh catches what it must. It builds two throwaway
# repositories under artifacts/local (ignored): one with a planted hit per commit, one
# with a single clean commit. The planted values are put together at run time, so this
# file itself holds none of them and stays clean when HEAD is scanned.
#
# Usage: tools/scan-self-test.sh       Exit code 0 when every expectation holds.
set -euo pipefail

repo_root="$(git rev-parse --show-toplevel)"
scan="$repo_root/tools/scan-before-push.sh"
base="$repo_root/artifacts/local/scan-self-test"
rm -rf "$base"
mkdir -p "$base"
trap 'rm -rf "$base"' EXIT

noreply_email="$(printf '%s+%s@%s' 1 tester users.noreply.github.com)"
outside_email="$(printf '%s@%s' someone example.com)"

new_repo() {
  git init -q "$1"
  git -C "$1" config user.name tester
  git -C "$1" config user.email "$noreply_email"
  git -C "$1" config core.autocrlf false
}

# plant <repo> <file> <content> [<author email>]: one commit holding one planted file.
plant() {
  local repo="$1" file="$2" content="$3" email="${4:-$noreply_email}"
  printf '%s\n' "$content" > "$repo/$file"
  git -C "$repo" add "$file"
  git -C "$repo" -c user.email="$email" commit -q -m "plant $file"
  git -C "$repo" rev-parse --short HEAD
}

failures=0
expect_hit() {
  # expect_hit <scan output> <commit> <pattern>
  if grep -F -q -e "HIT  $2  " <<< "$1" && grep -F -q -e "$3" <<< "$(grep -F -e "HIT  $2  " <<< "$1")"; then
    echo "ok    $3 reported for commit $2"
  else
    echo "FAIL  $3 not reported for commit $2"
    failures=$((failures + 1))
  fi
}

dirty="$base/dirty"
new_repo "$dirty"
c_key="$(plant "$dirty" key.txt "$(printf -- '-----BEGIN %s PRIVATE KEY-----' RSA)")"
c_token="$(plant "$dirty" token.txt "token = $(printf '%s%s' gh p_)$(printf 'a%.0s' $(seq 1 36))")"
c_path="$(plant "$dirty" path.txt "$(printf '%s%s%s' C ':' '\Users\someone\notes')")"
user_name="${USERNAME:-${USER:-}}"
c_user=""
if [ -n "$user_name" ]; then
  c_user="$(plant "$dirty" user.txt "written by $user_name")"
fi
c_email="$(plant "$dirty" plain.txt "nothing to see" "$outside_email")"

set +e
output="$(cd "$dirty" && "$scan" 2>&1)"
status=$?
set -e

if [ "$status" -eq 1 ]; then
  echo "ok    the scan exits 1 on the planted repository"
else
  echo "FAIL  the scan exited $status on the planted repository"
  failures=$((failures + 1))
fi
expect_hit "$output" "$c_key" "private-key"
expect_hit "$output" "$c_token" "github-token"
expect_hit "$output" "$c_path" "drive-letter-path"
if [ -n "$c_user" ]; then
  expect_hit "$output" "$c_user" "user-name"
else
  echo "skip  no USERNAME or USER in the environment, so the user-name case is not planted"
fi
expect_hit "$output" "$c_email" "author-or-committer-email"
if grep -F -q -e "$outside_email" <<< "$output"; then
  echo "FAIL  the scan printed a matched value"
  failures=$((failures + 1))
else
  echo "ok    the scan names patterns, never matched values"
fi

clean="$base/clean"
new_repo "$clean"
plant "$clean" readme.txt "A clean file." > /dev/null
set +e
clean_output="$(cd "$clean" && "$scan" 2>&1)"
clean_status=$?
set -e
if [ "$clean_status" -eq 0 ]; then
  echo "ok    the scan exits 0 on a clean repository"
else
  echo "FAIL  the scan exited $clean_status on a clean repository:"
  printf '%s\n' "$clean_output"
  failures=$((failures + 1))
fi

if [ "$failures" -gt 0 ]; then
  echo "scan self-test: $failures failure(s)" >&2
  exit 1
fi
echo "scan self-test: all expectations hold"
