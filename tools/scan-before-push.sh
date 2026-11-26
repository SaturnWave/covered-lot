#!/usr/bin/env bash
# Scans what a push would publish. This repository is public, so a branch push publishes
# as much as a push to master: run this before every push.
#
# It checks every commit that the remote does not have yet:
#   - the author and committer emails must be GitHub noreply addresses;
#   - the commit message, the names of added or changed files and their bytes (text and
#     binary alike) must hold no secret and no personal data.
# Then it checks every file in HEAD the same way.
#
# A hit names the commit, the file and the pattern, never the matched text.
# Exit codes: 0 clean, 1 hits found, 2 usage error.
#
# Usage: tools/scan-before-push.sh [<remote>]      (default: origin)
#
# Some pattern strings below are split in two ("one""drive"), so that this file does not
# match its own patterns when HEAD is scanned. tools/scan-self-test.sh plants one of each
# kind of hit in a throwaway repository and checks that this script reports it.
set -euo pipefail

if [ "$#" -gt 1 ]; then
  echo "usage: tools/scan-before-push.sh [<remote>]" >&2
  exit 2
fi
remote="${1:-origin}"

repo_root="$(git rev-parse --show-toplevel)"
cd "$repo_root"

work_dir="$(mktemp -d)"
trap 'rm -rf "$work_dir"' EXIT

# name | i (ignore case) or s (case-sensitive) | extended regular expression
content_patterns=(
  'private-key|s|-----BEGIN ([A-Z]+ )*PRIVATE KEY-----'
  'github-token|s|(gh[opusr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{30,})'
  'aws-access-key|s|AKIA[0-9A-Z]{16}'
  'google-api-key|s|AIza[0-9A-Za-z_-]{35}'
  'slack-token|s|xox[abposr]-[A-Za-z0-9-]{10,}'
  'stripe-live-key|s|sk_live_[0-9A-Za-z]{16,}'
  "password-assignment|i|(password|passwd|storepass(word)?|keypass(word)?|keystorepass|keyaliaspass)[\"']?[[:space:]]*[:=][[:space:]]*[\"']?[^\"'[:space:],;}]"
  'keystore-file|i|[A-Za-z0-9_-]\.(keystore|jks)([^A-Za-z0-9]|$)'
  'drive-letter-path|s|(^|[^A-Za-z0-9])[A-Za-z]:[\\/][A-Za-z0-9._$ -]'
  "users-folder|s|/User""s/"
  "one-drive-folder|i|one""drive"
  "app-data-folder|i|app""data"
)

# Personal data of whoever runs the scan, read from the environment at run time and never
# written into this file.
escape_regex() {
  printf '%s' "$1" | sed 's/[][\.*^$+?(){}|/]/\\&/g'
}
user_name="${USERNAME:-${USER:-}}"
if [ -n "$user_name" ]; then
  content_patterns+=("user-name|i|(^|[^A-Za-z0-9])$(escape_regex "$user_name")([^A-Za-z0-9]|$)")
fi
home_folders=()
for folder in "${USERPROFILE:-}" "${HOME:-}"; do
  if [ -n "$folder" ]; then
    home_folders+=("$folder" "${folder//\\//}")
  fi
done

email_re='[A-Za-z0-9._%+-]+@[A-Za-z0-9-]+(\.[A-Za-z0-9-]+)*\.[A-Za-z]{2,}'
allowed_email_re='(^([0-9]+\+)?[A-Za-z0-9-]+@users\.noreply\.github\.com$)|(^noreply@(anthropic|github)\.com$)'
commit_email_re='^([0-9]+\+)?[A-Za-z0-9-]+@users\.noreply\.github\.com$'
secret_file_re='(\.(keystore|jks|p12|p8|pfx|pem|mobileprovision)$)|((^|/)\.env$)'

hits=0
report() {
  # report <where> <file> <pattern>; never the matched text.
  printf 'HIT  %s  %s  %s\n' "$1" "$2" "$3"
  hits=$((hits + 1))
}

# Splits one content_patterns entry into the globals pattern_name, pattern_regex and
# pattern_case (an array: empty, or -i).
split_pattern() {
  pattern_name="${1%%|*}"
  pattern_regex="${1#*|*|}"
  local flag="${1#*|}"
  flag="${flag%%|*}"
  pattern_case=()
  if [ "$flag" = i ]; then
    pattern_case=(-i)
  fi
}

# scan_tree <where> <dir>: every file under <dir>, by its name and by its bytes.
scan_tree() {
  local where="$1" dir="$2" entry file relative found
  # File names: one grep per pattern over the list of names, because starting a process
  # per file and pattern is slow on Windows.
  local names="$work_dir/names.txt"
  (cd "$dir" && find . -type f) | sed 's#^\./##' > "$names"
  while IFS= read -r relative; do
    report "$where" "$relative" "secret-file-name"
  done < <(grep -E -i -e "$secret_file_re" "$names" || true)
  for entry in "${content_patterns[@]}"; do
    split_pattern "$entry"
    while IFS= read -r relative; do
      report "$where" "$relative" "$pattern_name (in the file name)"
    done < <(grep -E "${pattern_case[@]+"${pattern_case[@]}"}" -e "$pattern_regex" "$names" || true)
  done

  for entry in "${content_patterns[@]}"; do
    split_pattern "$entry"
    while IFS= read -r -d '' file; do
      report "$where" "${file#"$dir"/}" "$pattern_name"
    done < <(grep -r -l -Z -a -E "${pattern_case[@]+"${pattern_case[@]}"}" -e "$pattern_regex" "$dir" || true)
  done

  local folder
  for folder in "${home_folders[@]+"${home_folders[@]}"}"; do
    while IFS= read -r -d '' file; do
      report "$where" "${file#"$dir"/}" "home-folder"
    done < <(grep -r -l -Z -a -F -i -e "$folder" "$dir" || true)
  done

  while IFS= read -r -d '' file; do
    # Kept in a variable, never printed: only whether a non-noreply address is left counts.
    found="$(grep -o -a -E -e "$email_re" "$file" | grep -v -E -e "$allowed_email_re" || true)"
    if [ -n "$found" ]; then
      report "$where" "${file#"$dir"/}" "email-address"
    fi
  done < <(grep -r -l -Z -a -E -e "$email_re" "$dir" || true)
}

commits=()
while IFS= read -r commit; do
  commits+=("$commit")
done < <(git rev-list --reverse HEAD --not --remotes="$remote")

for commit in "${commits[@]+"${commits[@]}"}"; do
  short="$(git rev-parse --short "$commit")"
  for email in "$(git log -1 --format=%ae "$commit")" "$(git log -1 --format=%ce "$commit")"; do
    if ! grep -E -q -e "$commit_email_re" <<< "$email"; then
      report "$short" "(commit metadata)" "author-or-committer-email"
    fi
  done

  commit_dir="$work_dir/$short"
  mkdir -p "$commit_dir/files" "$commit_dir/message"
  git log -1 --format=%B "$commit" > "$commit_dir/message/commit-message"
  scan_tree "$short" "$commit_dir/message"

  while IFS= read -r -d '' path; do
    mkdir -p "$commit_dir/files/$(dirname "$path")"
    git cat-file blob "$commit:$path" > "$commit_dir/files/$path"
  done < <(git diff-tree -r --root --no-commit-id --diff-filter=ACMRT --name-only -z "$commit")
  scan_tree "$short" "$commit_dir/files"
  rm -rf "$commit_dir"
done

head_dir="$work_dir/HEAD"
mkdir -p "$head_dir"
git archive HEAD | tar -x -C "$head_dir"
scan_tree "HEAD" "$head_dir"

if [ "$hits" -gt 0 ]; then
  echo "scan: $hits hit(s) in ${#commits[@]} new commit(s) and HEAD; push nothing until they are gone." >&2
  exit 1
fi
echo "scan: clean (${#commits[@]} new commit(s) and HEAD)"
