#!/usr/bin/env bash
# Audits the REQ- traceability index. Invariants are documented in Specs/README.md.
# BdsNotes/ is an archaeological record and is never scanned.
#
# Usage: traceability-audit.sh [repo-root]
#   repo-root defaults to $(git rev-parse --show-toplevel)
#
# Exit code: 1 if phantom references exist (invariant 1), 0 otherwise.
# Everything else is a report, not a failure.
set -euo pipefail

REPO_ROOT="${1:-$(git rev-parse --show-toplevel)}"
cd "$REPO_ROOT"

ID_RE='REQ-[A-Z]+-[0-9]+(\.[0-9]+)*'
SPEC_FILES=(Specs/Behavioral/*.md)
# Source and migrations carry no REQ annotations (retired 2026-07-31 — see
# CompoundedLearnings/articles/architecture/no-req-annotations-in-source.md). Tests are
# the only destination, so they are the only thing scanned.
TEST_DIRS=(Tests)

tmp=$(mktemp -d); trap 'rm -rf "$tmp"' EXIT

# ---- parse specs -----------------------------------------------------------
# Active definitions: "- **REQ-...**" bullets above the Withdrawn / Waived /
# Promotion-candidates sections. Only the leading bolded ID on the line is a
# definition; IDs later in the line are cross-references.
for f in "${SPEC_FILES[@]}"; do
    awk '/^## (Withdrawn|Waived from testing|Promotion candidates)/{exit} {print}' "$f" \
        | grep -oE "^[[:space:]]*- \*\*$ID_RE" | grep -oE "$ID_RE" || true
done | sort -u > "$tmp/active"

section_ids() {  # $1 = section heading; first table column only
    for f in "${SPEC_FILES[@]}"; do
        awk -v h="^## $1" '$0 ~ h {on=1; next} /^## /{on=0} on' "$f" \
            | grep -E '^\|' | cut -d'|' -f2 | grep -oE "$ID_RE" || true
    done | sort -u
}
section_ids "Withdrawn"           > "$tmp/withdrawn"
section_ids "Waived from testing" > "$tmp/waived"

# ---- scan destinations ------------------------------------------------------
grep -rhoE "$ID_RE" "${TEST_DIRS[@]}" 2>/dev/null | sort > "$tmp/test_all" || true
sort -u "$tmp/test_all" > "$tmp/test_refs"
cp "$tmp/test_refs" "$tmp/all_refs"

# ---- invariant 1: no phantoms (HARD FAIL) -----------------------------------
comm -23 "$tmp/all_refs" "$tmp/active" > "$tmp/phantoms"
comm -12 "$tmp/phantoms" "$tmp/withdrawn" > "$tmp/ph_withdrawn"
comm -23 "$tmp/phantoms" "$tmp/withdrawn" > "$tmp/ph_unknown"

echo "=== Invariant 1: phantom references (tests -> nonexistent or withdrawn requirement) ==="
if [[ -s "$tmp/phantoms" ]]; then
    show_refs() {  # exact-ID match: not followed by another digit or sub-number
        local esc; esc=$(sed 's/\./\\./g' <<< "$1")
        grep -rnE "${esc}([^.0-9]|\$)" "${TEST_DIRS[@]}" 2>/dev/null | sed 's/^/    /'
    }
    while read -r id; do
        echo "WITHDRAWN: $id is referenced but withdrawn:"
        show_refs "$id"
    done < "$tmp/ph_withdrawn"
    while read -r id; do
        echo "UNKNOWN: $id is referenced but defined nowhere:"
        show_refs "$id"
    done < "$tmp/ph_unknown"
else
    echo "clean"
fi

# ---- invariant 2: every active requirement tested or waived ------------------
echo ""
echo "=== Invariant 2: active requirements with no test and no waiver ==="
comm -23 "$tmp/active" <(sort -u "$tmp/test_refs" "$tmp/waived") > "$tmp/untested"
if [[ -s "$tmp/untested" ]]; then
    cat "$tmp/untested"
    echo "($(wc -l < "$tmp/untested") of $(wc -l < "$tmp/active") active requirements)"
else
    echo "clean"
fi

# ---- consistency: waived but tested anyway -----------------------------------
comm -12 "$tmp/waived" "$tmp/test_refs" > "$tmp/stale_waivers"
if [[ -s "$tmp/stale_waivers" ]]; then
    echo ""
    echo "=== Stale waivers: waived from testing but tests exist ==="
    cat "$tmp/stale_waivers"
fi

# ---- invariant 3: bullshit-sniffer feed ---------------------------------------
# (the old invariant 3 — active requirements with no code annotation — was retired
#  2026-07-31 along with source annotations themselves)
echo ""
echo "=== Invariant 3: test annotations per requirement, descending ==="
if [[ -s "$tmp/test_all" ]]; then
    uniq -c "$tmp/test_all" | sort -rn
else
    echo "(no test annotations yet)"
fi

[[ -s "$tmp/phantoms" ]] && exit 1 || exit 0
