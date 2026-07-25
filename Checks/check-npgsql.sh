#!/usr/bin/env bash
# Enforces P2.3 (PATTERNS.md): Utilities.DAL is the only place Npgsql is touched.
# Allowlist: Src/Utilities/DAL.fs (the boundary itself) and
# Tests/Tests.Integrated/_TestDataStage.fs (fixture TRUNCATE teardown — sanctioned test infrastructure).
set -u
cd "$(dirname "$0")/.."

hits=$(grep -rn --include='*.fs' 'Npgsql' Src Tests |
    grep -v '^Src/Utilities/DAL\.fs:' |
    grep -v '^Tests/Tests\.Integrated/_TestDataStage\.fs:')

if [[ -n "$hits" ]]; then
    echo "$hits"
    echo 'Npgsql reference outside the DAL (P2.3).'
    exit 1
fi
