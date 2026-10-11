# spec-quality-AccountCrud

## AC-RANGE-1 — insufficient-elaboration
- **Location:** Specs/Behavioral/AccountCrud.md REQ-AC-3.12.1 (and REQ-AC-3.12.3); Src/Ui.InterfaceBridge/BoundaryConverters/SharedContractConverters.fs:19; Src/Business.CrossDomainOrchestration/FetchFilterAndSort.fs:25,95-107; Src/Business.CrossDomainOrchestration/AccountActivity.fs:137-140
- **Summary:** REQ-AC-3.12.1's 'inclusive date range on entry date' filter does not say what happens when the end date is earlier than the begin date. The code quietly returns nothing, and the corpus rule added for the same gap in Positions says to reject it.
- **Resolution:** dan-decides

REQ-AC-3.12.1: 'a temporal filter given as either a fiscal period key or an inclusive date range on entry date'. Nothing says what happens to a reversed range. A fiscal period key always resolves to a valid range (FetchFilterAndSort.getDateRangeFromTemporalFilter), but a caller-supplied DateRange is not checked anywhere. SharedContractConverters.fs:19 copies beginDate and endInclusive straight into TemporalFilter.DateRange. FilterDateRange is a plain record with no constructor check. AccountActivity.fs:137-140 passes the pair into the SQL BETWEEN. So a caller who swaps the dates gets an empty result. Under REQ-AC-3.12.3 the date range is a journal-entry filter, so even the line-less Accounts drop out, and the operator sees 'no activity' when the real cause is an input mistake. Two reasonable developers would split: one rejects the range, the other returns empty. Audit 2026-10-04a raised this exact point against REQ-POS-7.5 (10-quality-Positions.md: 'Two reasonable developers would differ: one rejects the reversed range ... the other returns an empty list'). It was accepted, and REQ-POS-7.5 now carries '(End-date rule added 2026-10-05)': 'The end date may not be earlier than the begin date; otherwise the listing fails with a typed error naming both dates.' Slice 2 repeated that rule in REQ-POS-13.5 and REQ-POS-15.6. The ledger's own activity filter, the one operators use most, is the one left unspecified. The same gap exists in REQ-JE-3.7 and REQ-STG-10.2, which are outside this scope but share the TemporalFilter path. resolved-findings.md has no matching ruling.

**Action:** Amend REQ-AC-3.12.1 to add: 'A date range whose end date is earlier than its begin date fails with a typed error naming both dates.' (or, if Dan prefers, state outright that a reversed range returns no rows). Then test the chosen behaviour.

**Why:** A reversed range that returns nothing reads as 'this account had no activity', which is the silent-surprise outcome REQ-SYS-6.1's rationale warns against. The corpus has already settled this question for Positions date ranges, so the ledger spec is now the inconsistent one, and the next developer cannot tell which convention applies.

---

## AC-WD-2 — stale-reference
- **Location:** Specs/Behavioral/AccountCrud.md Withdrawn table: REQ-AC-3.8, REQ-AC-3.5.1, REQ-AC-3.6.1
- **Summary:** The reasons for withdrawing REQ-AC-3.8 ('Changed to system run time') and REQ-AC-3.5.1/3.6.1 ('No longer needed due to how active accounts are thought of now') use outdated wording or point to nothing a reader can check.
- **Resolution:** fix-spec

REQ-AC-3.8 (active Accounts relative to a passed-in reference time) is withdrawn as 'Changed to system run time'. Its replacement, REQ-AC-3.9 (amended 2026-10-03), uses 'the current date (the calendar date (REQ-SYS-7.1) of the operation's initiation instant (REQ-SYS-3.4))'. That is a Calendar Date taken from the initiation instant, not 'system run time', and REQ-SYS-3.4 was written to stop operations reading the clock more than once. resolved-findings.md IE-AC-1 is retired precisely because 'a read that takes a fresh Calendar.today() is now a finding'. REQ-AC-3.5.1 and 3.6.1 (an all-or-active-only choice on the child and by-type reads) are withdrawn as 'No longer needed due to how active accounts are thought of now'. That points to no requirement. The real reason is that activity became date-relative (REQ-AC-1.48/1.50), so a bare 'active only' flag has no meaning without a reference date (REQ-AC-1.48.1), and the reason does not say so. A reader cannot tell from the reason whether REQ-AC-3.5/3.6 now return deactivated Accounts. They do: they return all of them. Audit 2026-10-04a #138 (AC-WD-1) accepted the same kind of correction for REQ-AC-1.24/2.5, whose reasons used 'timestamps' for Calendar Dates, and REQ-AC-1.38's reason was corrected on 2026-10-03. These three were not touched.

**Action:** Reword the reasons. REQ-AC-3.8: 'Replaced by REQ-AC-3.9: the reference date is the current date (REQ-SYS-3.4/7.1), not caller-supplied.' REQ-AC-3.5.1/3.6.1: 'Activity is relative to a Calendar Date (REQ-AC-1.48/1.50, 1.48.1); an all-or-active flag names none. REQ-AC-3.5/3.6 return every matching Account; active-only reads are REQ-AC-3.9.'

**Why:** A withdrawal reason is the only record of why a requirement stopped binding. When it uses retired wording or points to nothing, a later reader cannot tell whether the withdrawal left a gap, which is exactly what checklist item 6 asks. Dan has corrected reasons of this kind before (AC-WD-1, REQ-AC-1.38).

---


