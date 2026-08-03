#!/usr/bin/env bash
# Enforces: every active REQ is tested, waived, or unenforceable (invariant 2),
# and no test cites a nonexistent or withdrawn REQ (invariant 1).
set -u
exec bash Skills/SonOfLeoRequirementsAudit/traceability-audit.sh "$(dirname "$0")/.."
