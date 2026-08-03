#!/usr/bin/env bash
# Enforces: unit-returning checks are named confirmX; validateX is retired.
# Allowlist: empty since #123a sweep (2026-08-03).
set -u
cd "$(dirname "$0")/.."

allow=''

status=0
while IFS= read -r line; do
    [[ -z "$line" ]] && continue
    file=$(basename "${line%%:*}")
    fname=$(sed -E 's/.*let (private )?(validate[A-Za-z0-9]*).*/\2/' <<<"$line")
    if ! grep -qxF "$file:$fname" <<<"$allow"; then
        echo "$line"
        status=1
    fi
done <<<"$(grep -rn --include='*.fs' -E 'let (private )?validate[A-Z]' Src)"

if [[ $status -ne 0 ]]; then
    echo 'New validateX definition — the canon is confirmX.'
fi
exit $status
