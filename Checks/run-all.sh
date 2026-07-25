#!/usr/bin/env bash
# SonOfLeo guardrails — deterministic checks runner.
# Usage: run-all.sh [--quick]
#   --quick skips checks marked "# SLOW" (used by the pre-commit hook).
# Exit codes per check script: 0 = pass, 1 = fail, 2 = skipped.
set -u
cd "$(dirname "$0")/.."

quick="${1:-}"
pass=0 fail=0 skip=0

for check in Checks/check-*.sh; do
    name=$(basename "$check" .sh)
    if [[ "$quick" == "--quick" ]] && grep -q '^# SLOW' "$check"; then
        printf 'SKIP  %s (slow — skipped in --quick)\n' "$name"
        skip=$((skip + 1))
        continue
    fi
    out=$(bash "$check" 2>&1)
    rc=$?
    case $rc in
        0) printf 'PASS  %s\n' "$name"; [[ -n "$out" ]] && printf '%s\n' "$out"; pass=$((pass + 1)) ;;
        2) printf 'SKIP  %s\n%s\n' "$name" "$out"; skip=$((skip + 1)) ;;
        *) printf 'FAIL  %s\n%s\n' "$name" "$out"; fail=$((fail + 1)) ;;
    esac
done

echo '----'
echo "$pass passed, $fail failed, $skip skipped"
[[ $fail -eq 0 ]]
