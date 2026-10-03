#!/usr/bin/env bash
# Enforces: no test creates, alters or drops a database object (table, function, trigger, schema, ...).
# Tests work only with the objects the migrations create. A test that needs its own objects to provoke a failure is
# testing a state the application cannot produce: question the requirement instead (Tests/README.md).
# Allowlist: none.
set -u
cd "$(dirname "$0")/.."

hits=$(grep -rniE --include='*.fs' \
    '\b(create|alter|drop)\s+(or\s+replace\s+)?(constraint\s+)?(table|function|procedure|trigger|schema|view|index|sequence|type|extension|role)\b' \
    Tests | grep -vE '^Tests/[^:]*/(bin|obj)/')

if [[ -n "$hits" ]]; then
    echo "$hits"
    echo 'Test code creates, alters or drops a database object.'
    exit 1
fi
