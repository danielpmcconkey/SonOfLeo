#!/usr/bin/env bash
# Enforces: fixtures move forward through time — integrated-test
# data derives from Calendar.today() offsets, never hard-wired near-present dates.
# Distant sentinel years (2040+) are allowed per the TestWriter skill ("2050-01" keys
# deliberately outside the fixture's range). Scope is Tests.Integrated only —
# isolated parse/validation tests legitimately use literal date strings.
set -u
cd "$(dirname "$0")/.."

hits=$(grep -rn --include='*.fs' -E '"20[0-3][0-9]-|LocalDate ?\( ?20[0-3][0-9]' Tests/Tests.Integrated)

if [[ -n "$hits" ]]; then
    echo "$hits"
    echo 'Hard-wired near-present date in an integrated test. Derive from Calendar.today() or use a 2040+ sentinel.'
    exit 1
fi
