#!/usr/bin/env bash
# Installs the pre-commit hook: runs `Checks/run-all.sh --quick`; a failing check
# refuses the commit. Run once per clone. Host and BD's container share this clone
# through the mounted workspace, so one install covers both sides — the hook uses
# only repo-relative paths and self-skips any check whose tooling is absent.
set -eu
cd "$(dirname "$0")/.."

cat > .git/hooks/pre-commit <<'EOF'
#!/usr/bin/env bash
exec bash "$(git rev-parse --show-toplevel)/Checks/run-all.sh" --quick
EOF
chmod +x .git/hooks/pre-commit
echo 'pre-commit hook installed at .git/hooks/pre-commit'
