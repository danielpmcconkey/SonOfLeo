#!/usr/bin/env bash
# Enforces formatting via Fantomas, configured by the repo-root
# .editorconfig. Self-skips (exit 2) until the Fantomas pilot is adopted — adoption
# means the .editorconfig exists at repo root. Self-skips likewise where fantomas
# isn't installed (e.g. a container without the dotnet tool) — the review contract
# still catches formatting host-side.
set -u
cd "$(dirname "$0")/.."
export PATH="$PATH:$HOME/.dotnet/tools"

if [[ ! -f .editorconfig ]]; then
    echo 'no .editorconfig at repo root — Fantomas not yet adopted (pilot pending)'
    exit 2
fi
if ! command -v fantomas >/dev/null; then
    echo 'fantomas not installed (dotnet tool install -g fantomas) — check skipped'
    exit 2
fi

status=0
fantomas --check Src || status=1
fantomas --check Tests || status=1
exit $status
