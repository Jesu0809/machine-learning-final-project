#!/usr/bin/env bash
# Commits every change in the project (new, modified and deleted files) and pushes it.
# Usage (from anywhere):   tools/sync.sh "short description of the change"
# Works on macOS and in Git Bash on Windows.
set -euo pipefail

cd "$(dirname "$0")/.."

message="${1:-}"
if [ -z "$message" ]; then
    echo 'Usage: tools/sync.sh "short description of the change"'
    exit 1
fi

git add -A

# Never commit Unity caches, local builds or the raw art folder.
blocked=$(git diff --cached --name-only | grep -E '(^|/)(Library|Temp|Logs|UserSettings|builds|art-source)/|\.DS_Store$' || true)
if [ -n "$blocked" ]; then
    echo "These files must not be committed (check .gitignore):"
    echo "$blocked"
    git reset -q
    exit 1
fi

# GitHub rejects files over 100 MB; stop well before that.
too_big=""
while IFS= read -r file; do
    if [ -f "$file" ] && [ "$(wc -c < "$file")" -gt 50000000 ]; then
        too_big="$too_big$file"$'\n'
    fi
done < <(git diff --cached --name-only)
if [ -n "$too_big" ]; then
    echo "These files are larger than 50 MB:"
    echo "$too_big"
    git reset -q
    exit 1
fi

if git diff --cached --quiet; then
    echo "Nothing to commit."
    exit 0
fi

echo "Files in this commit:"
git diff --cached --stat | tail -n 15

git commit -q -m "$message"
git pull --rebase -q
git push -q
echo "Done: \"$message\" pushed to $(git rev-parse --abbrev-ref HEAD)."
