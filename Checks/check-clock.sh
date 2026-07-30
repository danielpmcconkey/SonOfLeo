#!/usr/bin/env bash
# Enforces: all time comes from Utilities.Clock / Utilities.Calendar.
# DateTime.Now / DateTime.UtcNow / DateTimeOffset.*Now / SystemClock are banned everywhere else.
# Allowlist: Src/Utilities/Clock.fs and Src/Utilities/Calendar.fs — they ARE the time boundary.
set -u
cd "$(dirname "$0")/.."

hits=$(grep -rn --include='*.fs' -E 'DateTime\.Now|DateTime\.UtcNow|DateTimeOffset\.Now|DateTimeOffset\.UtcNow|SystemClock' Src Tests |
    grep -v '^Src/Utilities/Clock\.fs:' |
    grep -v '^Src/Utilities/Calendar\.fs:')

if [[ -n "$hits" ]]; then
    echo "$hits"
    echo 'Banned time API outside Clock/Calendar.'
    exit 1
fi
