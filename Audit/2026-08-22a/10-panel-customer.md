# customer-gap

## SD-REMEDIATION-1 — statement-delta
- **Location:** Audit/2026-08-21a/action-items.md; HobsonsNotes/wakeup-2026-08-22b.md
- **Summary:** Dan says all 17 accepted items are "completed" but the wakeup from the same day shows 10 BD test items remain open.
- **Resolution:** dan-decides
- **Prior ruling:** Related to SD-COUNT-1 from the 2026-08-22a audit (unresolved, addresses count mismatch in the same sentence). This finding addresses a different aspect -- the 'completed' status claim vs. open work items.

Dan's statement: 'All 29 findings from that audit have been resolved (4 fixed during, 8 overruled, 17 accepted and completed).' The wakeup-2026-08-22b.md (written in Dan's session, timestamped 16:13 today) says: 'Of the accepted items: all Hobson spec fixes are done, all Dan code changes are done, 10 BD test items remain open.' It then enumerates the open items: '#2-7, #9-11 plus the REQ-STG-3.7 test relocation [from #19].' The action-items.md itself shows these items at status 'accepted' (not 'done'). The spec and code remediation is finished; the test remediation -- BD's lane -- is not. This is related to but distinct from SD-COUNT-1 (from today's earlier 2026-08-22a audit), which flagged the count mismatch (6 vs 8 overruled, 19 vs 17 accepted). SD-COUNT-1 addresses the arithmetic; this finding addresses the temporal claim that the work is finished. The open items are test-quality improvements (more specific assertions, fixture reorganization, coverage additions), not missing tests for core functionality -- the practical risk is low, but the statement is inaccurate.

**Action:** Clarify the statement. If 'completed' means 'dispositioned and assigned,' say that. If it means 'all remediation work is done,' update to reflect that 10 BD test items are in progress.

**Why:** The audit record is the permanent ledger of what happened. 'Accepted and completed' vs 'accepted and in progress' are materially different claims. As the customer who will rely on this system for financial operations, knowing the test coverage state -- even for improvement items, not missing coverage -- informs confidence in the built domains.

---
