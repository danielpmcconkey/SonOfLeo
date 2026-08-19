#!/usr/bin/env bash
# Installs the pre-commit hook: refuses when the working tree isn't what is about
# to be committed, then runs `Checks/run-all.sh --quick`; a failing check refuses
# the commit. Run once per clone. The host and container are separate clones;
# install in each.
#
# History: this hook was disabled 2026-07-26 because check-format produced
# intermittent false FAILs. Fantomas was dropped 2026-07-31 and check-format.sh
# deleted with it, which removed the only unreliable check. Re-enabled the same day.
set -eu
cd "$(dirname "$0")/.."

cat > .git/hooks/pre-commit <<'EOF'
#!/usr/bin/env bash
set -eu
root="$(git rev-parse --show-toplevel)"
cd "$root"

# Every check reads files from the working tree, but a commit records the index.
# Stage a file, edit it again, and the checks would validate content that is not
# being committed — a false pass. Refuse instead of guessing which one you meant.
overlap="$(comm -12 \
    <(git diff --cached --name-only --diff-filter=ACM | sort -u) \
    <(git diff --name-only | sort -u))"

if [ -n "$overlap" ]; then
    echo 'REFUSED  working tree does not match what you are committing'
    echo
    echo 'These staged files have further unstaged edits, so the checks below would'
    echo 'validate content that is not in your commit:'
    echo
    echo "$overlap" | sed 's/^/    /'
    echo
    echo "Stage them ('git add -A') or stash them, then commit again."
    exit 1
fi

exec bash "$root/Checks/run-all.sh" --quick
EOF
chmod +x .git/hooks/pre-commit
echo 'pre-commit hook installed at .git/hooks/pre-commit'
