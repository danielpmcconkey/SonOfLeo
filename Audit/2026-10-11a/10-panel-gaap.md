# GAAP domain expert (ledger, periods, journal, ingestion posting path)

## GAAP-FP-1 — ambiguity
- **Location:** Specs/Behavioral/FiscalPeriodCrud.md REQ-FP-2.7; Specs/Behavioral/Positions.md REQ-POS-15.7; Src/Business.CrossDomainOrchestration/FiscalPeriodCreation.fs ensureFiscalPeriods (fold added in 94978e6); Src/Ui.InterfaceBridge/Routes/FiscalPeriodRoutes.fs ensure; Tests/Tests.Integrated/CrossDomainOrchestration/PreLedgerBalanceGuards.fs:208
- **Summary:** No spec says whether 'ensure fiscal periods' over a range is all-or-nothing when REQ-POS-15.7 refuses one of its months. The code and a REQ-POS-15.7 test pin all-or-nothing, but the governing requirements are silent and point the other way.
- **Resolution:** fix-spec

REQ-FP-2.7 says missing periods in the range 'are created' and the operation 'returns the periods it created'. Its only stated failure is a start month later than the end month. REQ-POS-15.7 refuses the creation of a single fiscal period that starts on or before a pre-ledger balance date. Neither says what ensure does when some missing months in the range are refused and others are not. That mixed case is reachable. Take existing periods from 2025-03 on, a pre-ledger balance on 2025-02-15, and ensure 2025-01..2025-12. The 2025-01 period would strand the balance and is refused. A missing month after the ledger's existing start (say 2025-04 if absent) strands nothing. FiscalPeriodCreation.ensureFiscalPeriods (94978e6) folds in month order and stops at the first refusal. The route's comment says 'one transaction, so a failure part way through leaves none of the range behind'. The test at PreLedgerBalanceGuards.fs:208 asserts 'no fiscal period in the range is created'. So the all-or-nothing behavior is real and tested, but the REQ it cites (15.7) speaks only of creating a single period. The FP spec, which is where a reader of fiscal-period behavior looks, does not mention the pre-ledger refusal at all: REQ-FP-2.4 says only 'if all validations pass'. A competent developer reading REQ-FP-2.7 ('creates missing periods, returns those it created') could reasonably create the harmless months and report the refused one. That differs observably from the current code. The route description for FiscalPeriod Ensure also promises creation with no mention of refusal.

**Action:** Amend REQ-FP-2.7, or add a sub-bullet under REQ-POS-15.7, to state that ensure is all-or-nothing: if any month in the range would be refused under REQ-POS-15.7, nothing is created and the operation fails with that typed error. Add a one-line pointer from FiscalPeriodCrud §2 to REQ-POS-15.7, so the fiscal period spec shows every reason creation can fail.

**Why:** The period model is what closing will be built on. The rules for when a period can come into existence should be in one place a reader of the fiscal period spec can find. A test currently asserts behavior (no partial range) that no requirement states. If someone 'fixes' ensure to create the harmless months, they would break a test whose cited REQ never asked for that behavior, and the spec would not settle which side is right.

---


