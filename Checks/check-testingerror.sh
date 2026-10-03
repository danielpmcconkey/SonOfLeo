#!/usr/bin/env bash
# Enforces: TestingError exists solely for test plumbing and is banned in Src/.
# It is defined in Tests/Tests.Helpers/TestError.fs, outside Src/, so Src/ has no allowlist.
set -u
cd "$(dirname "$0")/.."

hits=$(grep -rn --include='*.fs' 'TestingError' Src)

if [[ -n "$hits" ]]; then
    echo "$hits"
    echo 'TestingError used in Src/ — it is test plumbing only.'
    exit 1
fi
