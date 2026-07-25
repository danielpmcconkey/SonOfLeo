#!/usr/bin/env bash
# SLOW
# Tracks #125a (PATTERNS.md P2.1 goal): every AppError case exercised by at least one
# test asserting that specific case. REPORT-ONLY until #125a lands — always exits 0.
# Flip to gating by replacing the final `exit 0` with `exit $((missing > 0))`.
set -u
cd "$(dirname "$0")/.."

cases=$(awk '/^type AppError/ { f = 1; next } /^module/ { f = 0 } f' Src/Utilities/AppError.fs |
    grep -oE '^[[:space:]]*\| [A-Z][A-Za-z0-9]*' | sed 's/.*| //')

total=0
missing=0
for c in $cases; do
    total=$((total + 1))
    if ! grep -rq --include='*.fs' "$c" Tests; then
        echo "untested: $c"
        missing=$((missing + 1))
    fi
done

echo "AppError coverage: $((total - missing))/$total cases referenced in Tests (report-only until #125a)"
exit 0
