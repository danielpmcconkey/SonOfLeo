#!/usr/bin/env bash
# Enforces: all time comes from App.Utility.Clock / App.Utility.Calendar.
# DateTime.Now / DateTime.UtcNow / DateTimeOffset.*Now / SystemClock are banned everywhere else.
# Allowlist: Src/App.Utility/Clock.fs and Src/App.Utility/Calendar.fs — they ARE the time boundary.
set -u
cd "$(dirname "$0")/.."

hits=$(grep -rn --include='*.fs' -E 'DateTime\.Now|DateTime\.UtcNow|DateTimeOffset\.Now|DateTimeOffset\.UtcNow|SystemClock' Src Tests |
    grep -v '^Src/App\.Utility/Clock\.fs:' |
    grep -v '^Src/App\.Utility/Calendar\.fs:')

if [[ -n "$hits" ]]; then
    echo "$hits"
    echo 'Banned time API outside Clock/Calendar.'
    exit 1
fi
