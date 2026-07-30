#!/usr/bin/env bash
# Enforces: AppError.toMessage never grows a wildcard arm.
# Its exhaustive match is the compiler-enforced guarantee that every case has a message.
set -u
cd "$(dirname "$0")/.."

awk '
    /let toMessage/ { inside = 1; next }
    inside && /^[[:space:]]*\| _/ { print FILENAME ":" FNR ": " $0; bad = 1 }
    END { exit bad ? 1 : 0 }
' Src/Utilities/AppError.fs || {
    echo 'Wildcard arm in AppError.toMessage — the exhaustive match is the guarantee.'
    exit 1
}
