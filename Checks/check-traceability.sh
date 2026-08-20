#!/usr/bin/env bash
# SLOW
# Enforces: every active REQ is tested, waived, or unenforceable (invariant 2),
# and no test cites a nonexistent or withdrawn REQ (invariant 1).
# Only enforced on main — feature branches may have specs or tests in-flight.
set -u
current_branch="$(git rev-parse --abbrev-ref HEAD 2>/dev/null)"
if [[ "$current_branch" != "main" ]]; then
    exit 0
fi
exec bash Skills/SonOfLeoRequirementsAudit/traceability-audit.sh "$(dirname "$0")/.."
