#!/usr/bin/env bash
# Enforces P2.1 (PATTERNS.md): TestingError exists solely for test plumbing and is banned in Src/.
# Allowlist: Src/Utilities/AppError.fs — the case's own definition and toMessage arm.
set -u
cd "$(dirname "$0")/.."

hits=$(grep -rn --include='*.fs' 'TestingError' Src |
    grep -v '^Src/Utilities/AppError\.fs:')

if [[ -n "$hits" ]]; then
    echo "$hits"
    echo 'TestingError used in Src/ (P2.1).'
    exit 1
fi
