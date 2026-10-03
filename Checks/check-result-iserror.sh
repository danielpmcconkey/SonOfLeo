#!/usr/bin/env bash
# Enforces: no test uses Result.isError (Specimen 4, Tests/README). A rejection test matches
# the specific error case it expects; isError passes on any error, including the wrong one.
# REPORT-ONLY until the audit 2026-10-03a test work (#149) rewrites the existing sites —
# exits 0 with the list. Flip to gating by replacing the final `exit 0` with `exit 1`.
set -u
cd "$(dirname "$0")/.."

# shellcheck disable=SC2046
hits=$(grep -n 'Result\.isError' $(git ls-files -- 'Tests/*.fs'))

if [[ -n "$hits" ]]; then
    echo "$hits"
    echo "$(wc -l <<< "$hits") Result.isError sites in Tests/ — match the expected error case instead (report-only until #149 lands)."
    exit 0
fi
