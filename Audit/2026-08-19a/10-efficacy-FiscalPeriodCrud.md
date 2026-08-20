# FiscalPeriodCrud Efficacy Auditor

## FP-AQ-1 — test-gap
- **Location:** Tests/Tests.Integrated/InterfaceBridge/FiscalPeriodRoutes.fs line 95, REQ-FP-3.4
- **Summary:** REQ-FP-3.4 route test uses a cowardly inequality with a hard-wired floor (Specimen 2 + Specimen 1).
- **Resolution:** fix-test

The route-level test for REQ-FP-3.4 (FetchAll happy path) asserts `Assert.True(returned |> List.length >= 9)`. The `>= 9` is textbook Specimen 2 (cowardly inequality) and the `9` is Specimen 1 (hard-wired count). The fixture creates 10 fiscal periods (9 open + 1 closed) and exposes `fixture.Data.totalFiscalPeriods` for exactly this purpose, but the route test does not use the fixture at all. A fetch returning every row in the database plus duplicates would pass. The model-level test for the same REQ (Tests.Integrated/Model/Ledger/FiscalPeriod.fs lines 98-110) does proper membership testing against fixture IDs, so the behavior IS verified at a lower layer, but the route test's sole assertion verifies nothing about the route's correctness.

**Action:** Replace `Assert.True(returned |> List.length >= 9)` with an expected count derived from `fixture.Data.totalFiscalPeriods`, or perform membership verification against the fixture period keys (converting through the FiscalPeriodReturn contract). The class already accepts the fixture in its constructor.

**Why:** A `>=` assertion tolerates duplicates, leaked rows, and broken filters. It is an assertion that gave up. The test README explicitly says: 'I want to know that you know you should have 6 and expect exactly 6.' The model-layer test does this correctly; the route test does not.

---

## FP-AQ-2 — test-gap
- **Location:** Tests/Tests.Integrated/ModelOrchestrator/FiscalPeriodCreation.fs lines 13-31 (REQ-FP-1.4, REQ-FP-1.5, REQ-FP-2.3); Tests/Tests.Integrated/Model/Ledger/FiscalPeriod.fs lines 47-72 (REQ-FP-2.4, REQ-FP-2.5)
- **Summary:** Date-derivation tests assert month and day but never assert the year component of the derived start and end dates.
- **Resolution:** fix-test

REQ-FP-1.4 says 'start date is derived from the key as the first day of the indicated month (e.g., key 2026-07 -> start date 2026-07-01)' and REQ-FP-1.5 parallels for end date. Both spec examples include the year as part of the derivation. The orchestrator test uses key '1974-06' and asserts `startDate.Month = 6`, `startDate.Day = 1`, `endDate.Day = 30` but never asserts `startDate.Year = 1974` or `endDate.Year = 1974`. The model test (REQ-FP-2.4/2.5) uses key '2050-10' and asserts months and days for both dates but likewise never asserts the year. Smell test: if the derivation hardcoded year 2000 but correctly extracted month/day from the key, both tests would pass.

**Action:** Add year assertions to the orchestrator test (e.g., `Assert.Equal(1974, startDate.Year)` and `Assert.Equal(1974, endDate.Year)`) and to the model test's date assertions. Derive the expected year from the key string to stay consistent with the fixture-derived-values principle.

**Why:** The year is half the information in the period key. A derivation that produces the correct month/day but wrong year would create fiscal periods whose dates do not correspond to their keys. No test currently catches this.

---
