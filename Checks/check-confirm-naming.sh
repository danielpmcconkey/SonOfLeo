#!/usr/bin/env bash
# Enforces: unit-returning checks are named confirmX.
# 1. validateX is retired: any validateX definition fails.
# 2. A checkX definition fails when its signature returns Result<unit, ...>, or carries no
#    return annotation (so the check can't tell it isn't one).
# Allowlist: empty since #123a sweep (2026-08-03).
set -u
cd "$(dirname "$0")/.."

allow=''

status=0
while IFS= read -r line; do
    [[ -z "$line" ]] && continue
    file=$(basename "${line%%:*}")
    fname=$(sed -E 's/.*let (private )?(rec )?((validate|check)[A-Za-z0-9]*).*/\3/' <<<"$line")
    if ! grep -qxF "$file:$fname" <<<"$allow"; then
        echo "$line"
        status=1
    fi
done <<<"$(
    grep -rn --include='*.fs' -E 'let (private )?(rec )?validate[A-Z]' Src
    for f in $(grep -rl --include='*.fs' -E 'let (private )?(rec )?check[A-Z]' Src); do
        # print a checkX definition line when its signature, up to the line ending in '=',
        # returns Result<unit or has no return annotation
        awk -v file="$f" '
            /let (private )?(rec )?check[A-Z]/ { start = FNR; first = $0; sig = ""; open = 1 }
            open { sig = sig " " $0
                   if ($0 ~ /=[ \t]*$/ || $0 ~ /=[ \t]+[^=>]/) {
                       if (sig ~ /\)[ \t]*:[ \t]*Result<unit/ || sig !~ /\)[ \t]*:/) print file ":" start ":" first
                       open = 0 } }' "$f"
    done
)"

if [[ $status -ne 0 ]]; then
    echo 'Unit-returning check named validateX or checkX — the canon is confirmX.'
fi
exit $status
