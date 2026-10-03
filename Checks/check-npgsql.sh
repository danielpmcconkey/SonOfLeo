#!/usr/bin/env bash
# Enforces: the App.DataAccessLayer project is the only place Npgsql is
# touched (extracted from Utilities.DAL 2026-07-25).
# Allowlist: Src/App.DataAccessLayer/ (the boundary itself) and
# Tests/Tests.Integrated/ConnectionPool.fs (test code reading the driver's connection-pool metrics by meter name).
set -u
cd "$(dirname "$0")/.."

hits=$(grep -rn --include='*.fs' 'Npgsql' Src Tests |
    grep -v '^Src/App\.DataAccessLayer/' |
    grep -v '^Tests/Tests\.Integrated/ConnectionPool\.fs:')

if [[ -n "$hits" ]]; then
    echo "$hits"
    echo 'Npgsql reference outside the App.DataAccessLayer project.'
    exit 1
fi
