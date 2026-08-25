# FiscalPeriodCrud Test Efficacy Auditor

## FP-LEAP-1 — test-gap
- **Location:** Tests/Tests.Integrated/ModelOrchestrator/FiscalPeriodCreation.fs, REQ-FP-1.5
- **Summary:** REQ-FP-1.5 explicitly describes February end-date derivation including leap years, but no test across the FP suite exercises a February period key.
- **Resolution:** fix-test

REQ-FP-1.5 states: 'Fiscal period end date is derived from the key as the last day of the indicated month (e.g., key "2026-07" -> end date 2026-07-31; key "2026-02" -> end date 2026-02-28 or 2026-02-29 in a leap year).' The spec author chose February as an illustrative example precisely because it is the variable-length month with a leap-year edge.

The test citing REQ-FP-1.5 (line 12 of FiscalPeriodCreation.fs) uses key "1974-06" (June, 30 days). The REQ-FP-2.4 model test uses "2050-10" (October, 31 days). Generic fixture keys use "2050-01" (January, 31 days). Other hard-coded keys in route tests: "1993-06", "1992-05", "2048-11". Grep for any YYYY-02 period key string across all of Tests/ returns zero results.

Of the three distinct month-end lengths (28/29, 30, 31), the test suite exercises only two (30 and 31). A bug in the derivation specific to February -- for instance, hardcoding 28 instead of using NodaTime's calendar-aware arithmetic -- would be invisible to the current suite. The implementation uses `startDate.PlusMonths(1).PlusDays(-1)` (Src/ModelOrchestrator/FiscalPeriodCreation.fs line 24), which is correct, but the test suite does not confirm it for the one month the spec singled out.

**Action:** Add a Theory to the REQ-FP-1.4/1.5/2.3 test (or alongside it) parameterized across month-end archetypes: a 31-day month, a 30-day month, a non-leap-year February (e.g. "2025-02", expect end day 28), and a leap-year February (e.g. "2024-02", expect end day 29). The existing Fact for June can become one row of the Theory.

**Why:** The spec explicitly calls out February and leap years as a distinguishing case for end-date derivation. A test suite that never exercises the one month the requirement highlighted leaves the spec-described edge case asserted only by trust in the date library, not by observation.

---
