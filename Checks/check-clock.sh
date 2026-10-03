#!/usr/bin/env bash
# Enforces two rules.
# 1. All time comes from App.Utility.Clock / App.Utility.Calendar.
#    DateTime.Now / DateTime.UtcNow / DateTimeOffset.*Now / SystemClock are banned everywhere else.
#    Allowlist: Src/App.Utility/Clock.fs and Src/App.Utility/Calendar.fs — they ARE the time boundary.
# 2. REQ-SYS-3.4: one operation, one moment. Src code derives the current instant or date from the
#    operation's initiation instant (Context.getInitiationInstant), never from a fresh
#    Clock.now() / Calendar.today() read.
#    Allowlist: Clock.fs, Calendar.fs, and AuditEnvelope.fs (which captures the initiation instant).
set -u
cd "$(dirname "$0")/.."

rc=0

hits=$(grep -rn --include='*.fs' -E 'DateTime\.Now|DateTime\.UtcNow|DateTimeOffset\.Now|DateTimeOffset\.UtcNow|SystemClock' Src Tests |
    grep -v '^Src/App\.Utility/Clock\.fs:' |
    grep -v '^Src/App\.Utility/Calendar\.fs:')

if [[ -n "$hits" ]]; then
    echo "$hits"
    echo 'Banned time API outside Clock/Calendar.'
    rc=1
fi

fresh=$(grep -rn --include='*.fs' -E 'Clock\.now|Calendar\.today' Src |
    grep -v '^Src/App\.Utility/Clock\.fs:' |
    grep -v '^Src/App\.Utility/Calendar\.fs:' |
    grep -v '^Src/App\.Operation/AuditEnvelope\.fs:')

if [[ -n "$fresh" ]]; then
    echo "$fresh"
    echo 'Fresh clock read in Src. Derive from the operation initiation instant (REQ-SYS-3.4).'
    rc=1
fi

exit $rc
