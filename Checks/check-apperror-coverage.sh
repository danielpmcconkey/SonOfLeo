#!/usr/bin/env bash
# SLOW
# Tracks the coverage goal: every error case of every IAppError implementation in Src/ is
# referenced by at least one test. REPORT-ONLY for missing cases — exits 0 however many are
# untested. Flip to gating by replacing the final `exit 0` with `exit $((missing > 0))`.
# Fails (exit 1) when it discovers no error DUs or no cases: that means the discovery below
# has gone stale, not that coverage is perfect.
# Discovery: every Src file containing `interface IAppError with`; the cases are the `| Case`
# lines of the column-0 `type` declaration that precedes that interface.
set -u
cd "$(dirname "$0")/.."

files=$(grep -rl --include='*.fs' 'interface IAppError with' Src)
[[ -z "$files" ]] && { echo 'No IAppError implementations found — check is stale.'; exit 1; }

# shellcheck disable=SC2086
cases=$(awk '
    FNR == 1 { buf = ""; intype = 0 }
    /^type / { buf = ""; intype = 1; next }
    intype && /interface IAppError with/ { printf "%s", buf; buf = ""; intype = 0; next }
    intype && /^[[:space:]]*\| [A-Z][A-Za-z0-9]*/ {
        match($0, /\| [A-Z][A-Za-z0-9]*/); buf = buf substr($0, RSTART + 2, RLENGTH - 2) "\n"
    }
' $files | sort -u)

[[ -z "$cases" ]] && { echo 'No error cases found in IAppError implementations — check is stale.'; exit 1; }

test_files=$(git ls-files -- 'Tests/*.fs')

total=0
missing=0
for c in $cases; do
    total=$((total + 1))
    # shellcheck disable=SC2086
    if ! grep -qw "$c" $test_files; then
        echo "untested: $c"
        missing=$((missing + 1))
    fi
done

echo "AppError coverage: $((total - missing))/$total cases referenced in Tests across $(wc -w <<< "$files") error types (report-only)"
exit 0
