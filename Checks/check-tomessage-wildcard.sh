#!/usr/bin/env bash
# Enforces: no IAppError implementation's ToMessage grows a wildcard arm.
# Each error DU's exhaustive match is the compiler-enforced guarantee that every case has a message.
# Scans every file that implements ToMessage; the member body ends at the next column-0 line.
set -u
cd "$(dirname "$0")/.."

files=$(grep -rl --include='*.fs' 'member this\.ToMessage()' Src)
[[ -z "$files" ]] && { echo 'No ToMessage implementations found — check is stale.'; exit 1; }

# shellcheck disable=SC2086
awk '
    FNR == 1 { inside = 0 }
    /member this\.ToMessage\(\)/ { inside = 1; next }
    inside && /^[^[:space:]]/ { inside = 0 }
    inside && /^[[:space:]]*\| _/ { print FILENAME ":" FNR ": " $0; bad = 1 }
    END { exit bad ? 1 : 0 }
' $files || {
    echo 'Wildcard arm in a ToMessage match — the exhaustive match is the guarantee.'
    exit 1
}
