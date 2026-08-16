# Test Data Guide

**No static JSONL file.** Build the `BaseStageRawRow` list directly in F# with
relative dates (`today.PlusDays(-3)`, etc.) so the data doesn't go stale. If you
need to test JSON deserialization, serialize the domain objects to a JSONL string
at runtime and feed that to the route.

## Entries and expected outcomes

| Group | Description | Lines | Expected outcome |
|---|---|---|---|
| grp-001 | DD DoorDash Order 8431927 | 1 null code, 1 parser-assigned (F-1270) | OneMatch: DoorDash rule (priority 100) beats generic TestBank rule (priority 1000) → F-5350 |
| grp-002 | MARATHON PETRO 7218 CONCORD NC | 1 null code, 1 parser-assigned | OneMatch: generic TestBank rule only → F-5300 |
| grp-003 | HARRIS TEETER 0381 CONCORD NC | 1 null code, 1 parser-assigned | OneMatch: generic TestBank rule only → F-5300 |
| grp-004 | SPECTRUM SOUTHEAST 800-892-2253 | 1 null code, 1 parser-assigned | OneMatch: generic TestBank rule only → F-5300 |
| grp-005 | TOTALLY UNKNOWN MERCHANT NOWHERE | 1 null code, 1 parser-assigned | NoMatch: no description-specific rule, but generic TestBank matches. See note below. |
| grp-006 | ALLSTATE INS AUTOPAY | 1 null code, 1 parser-assigned | Conflict: two rules at priority 500 both match (ALLSTATE→F-5300 and ALLSTATE→F-5650). Needs new rules. |
| grp-007 | PAYROLL DEPOSIT ACME CORP | 4 lines, ALL parser-assigned | Skips classification entirely → auto-Classified (REQ-STG-5.8) |
| grp-008 | Fixture JE with reference | fi_reference = "TXN-001" | Ledger dedup: matches jeWithRef's external reference. Never reaches classification. |
| grp-009 | DD DoorDash Order 9917223 | 1 null code, 1 parser-assigned | OneMatch: DoorDash rule → F-5350 |
| grp-010 | DD DoorDash Order 9917223 | fi_reference = "REF-DD-002" (same as grp-009) | Stage-vs-stage dedup: same source + ref as grp-009. Never reaches classification. |

## Problem: grp-005 NoMatch

The generic TestBank rule (Source = "TestBank" → F-5300, priority 1000) matches ALL TestBank entries including unknowns. That means grp-005 would get a match, not NoMatch. To get a genuine NoMatch, either:

1. Use a source that has no generic catch-all rule (e.g., a new "TestSavings" source with no rules)
2. Or accept that NoMatch testing needs a separate fixture entry not in this file

**Recommendation:** Add a new source "TestSavings" with no rules. Change grp-005 to `"fiSource":"TestSavings"`. BD would need to add that source to the fixture.

## Rules to add for Conflict testing (grp-006)

Add two rules at the SAME priority (500) that both match "ALLSTATE":

```
Rule: "Allstate Insurance → F-5300" 
  Source = TestBank, Description matches "^ALLSTATE"
  Priority 500, codeAtMatch = F-5300

Rule: "Allstate Insurance → F-5650"
  Source = TestBank, Description matches "^ALLSTATE"  
  Priority 500, codeAtMatch = F-5650
```

These deliberately conflict. The entry should end up with status Conflict.

## Summary of entry-level statuses after full pipeline

| Group | Status |
|---|---|
| grp-001 | Classified |
| grp-002 | Classified |
| grp-003 | Classified |
| grp-004 | Classified |
| grp-005 | NoMatch (if source changed to TestSavings) |
| grp-006 | Conflict |
| grp-007 | Classified (fully parser-assigned, skips classification) |
| grp-008 | Duplicate (ledger dedup) |
| grp-009 | Classified |
| grp-010 | Duplicate (stage-vs-stage dedup) |
