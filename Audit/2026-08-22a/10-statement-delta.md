# statement-delta-auditor

## SD-COUNT-1 — statement-delta
- **Location:** Audit/2026-08-21a/action-items.md
- **Summary:** Dan says the 2026-08-21a audit breakdown was 4 fixed / 8 overruled / 17 accepted, but the itemized action-items list records only 6 overruled items and 19 accepted items.
- **Resolution:** dan-decides

Dan's statement: '4 fixed during, 8 overruled, 17 accepted and completed.' The action-items.md itemized list explicitly marks 6 items as overruled (#1 AC-EFF-1, #14 STALE-HOOK-1, #16 IDIOM-TZ-1, #17 IDIOM-FMC-1, #25 SD-AUDIT-1, #29 CT-STG-2) and 19 items as accepted (#2-7, #9-11, #13, #15, #19-24, #26-27). The 4 done items (#8, #12, #18, #28) match Dan's claim. Confirmed via grep: 6 lines contain 'overruled' in the status column; 23 lines contain 'accepted' (19 main items plus 4 sub-items 27a-d). The file's own summary line at the bottom also states '8 overruled, 17 accepted' -- the summary has been inconsistent with its own itemized data since the original disposition commit (325bb71). Dan is quoting the file's summary, but the itemized record -- the authoritative data -- shows 6 and 19. The discrepancy has persisted across both commits that touched this file (325bb71 original, 9cb6ab9 migration updates to #21 and #27 only; neither changed any item's status).

**Action:** Reconcile the action-items.md summary line with the itemized statuses. Either correct the summary to '6 overruled, 19 accepted' or, if 2 items were truly overruled, update those 2 items' status fields to say so.

**Why:** The audit record is the permanent ledger of what was accepted versus dismissed. If 2 items are miscategorized -- accepted when they should be overruled, or vice versa -- that distorts both the remediation scope and the precedent record for future audits.

---
