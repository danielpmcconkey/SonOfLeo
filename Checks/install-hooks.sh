#!/usr/bin/env bash
# Installs the pre-commit hook: refuses when the working tree isn't what is about
# to be committed, then runs `Checks/run-all.sh --quick`; a failing check refuses
# the commit. Run once per clone. Host and BD's container share this clone through
# the mounted workspace, so one install covers both sides — the hook uses only
# repo-relative paths and self-skips any check whose tooling is absent.
#
# CURRENTLY DISABLED — requires --force. See the block below for why.
set -eu
cd "$(dirname "$0")/.."

# ── DISABLED 2026-07-26 (Dan's call) ──────────────────────────────────────────────
# check-format intermittently reports a false FAIL, naming a correctly-formatted file
# that is byte-identical to HEAD. Observed three times in one session, on commits
# touching no F# at all, always via this hook; fourteen consecutive direct runs of
# run-all.sh passed. Cause undiagnosed. See
# CompoundedLearnings/articles/process/a-check-verdict-is-evidence-not-truth.md
#
# A gate that can refuse a commit you need to make, for a violation that does not
# exist, is worse than no gate — it teaches everyone the --no-verify reflex, which is
# how the earlier false-PASS defect survived undetected. Switched off deliberately
# rather than routed around habitually.
#
# THE CHECKS THEMSELVES ARE UNAFFECTED. `bash Checks/run-all.sh` still works and is
# still mandatory before presenting work (Skills/CodeReviewer/SKILL.md, Gate 0). Only
# the automatic pre-commit enforcement is off.
#
# To reinstate once the flake is diagnosed: bash Checks/install-hooks.sh --force
# ──────────────────────────────────────────────────────────────────────────────────
if [[ "${1:-}" != "--force" ]]; then
    echo 'pre-commit hook installation is DISABLED pending the check-format flake.'
    echo 'The checks still run: bash Checks/run-all.sh'
    echo 'To install anyway: bash Checks/install-hooks.sh --force'
    exit 0
fi

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
