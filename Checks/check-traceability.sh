#!/usr/bin/env bash
# SLOW
# Enforces: every active REQ is tested, waived, or unenforceable (invariant 2),
# and no test cites a nonexistent or withdrawn REQ (invariant 1).
# Only enforced on main — feature branches may have specs or tests in-flight.
# Off main it reports SKIP (exit 2) rather than a silent pass, so nobody reads a
# branch run as "traceability is clean". Run it on main after merge (README step 13).
set -u
current_branch="$(git rev-parse --abbrev-ref HEAD 2>/dev/null)"
if [[ "$current_branch" != "main" ]]; then
    echo "on branch '$current_branch', not main: traceability is enforced on main after merge (README step 13)"
    exit 2
fi
exec bash Skills/SonOfLeoRequirementsAudit/traceability-audit.sh "$(dirname "$0")/.."
