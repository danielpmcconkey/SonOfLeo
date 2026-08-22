# fp-efficacy

## FP-COV-1 — test-gap
- **Location:** Tests/Tests.Integrated/Model/Ledger/FiscalPeriod.fs, lines 89-98 (REQ-FP-3.1)
- **Summary:** REQ-FP-3.1 test asserts 2 of 7 FiscalPeriod properties; five are unverified on the read path at any layer.
- **Resolution:** fix-test

REQ-FP-3.1 states: "the system must return a FiscalPeriod type with all fiscal period properties." The citing test (`REQ-FP-3.1 fetchById happy path`) fetches a fixture period by ID and asserts only `fiscalPeriodId` (the locator) and `isOpen`. It never checks `periodKey`, `startDate`, `endDate`, `createdAt`, or `modifiedAt`.

No other test at any layer fills this gap. The route-level tests (FetchByKey, FetchAll, Close, Reopen) deserialize into `FiscalPeriodReturn` (which carries periodKey, startDate, endDate, isOpen, createdAt, modifiedAt) but only ever assert on `periodKey` and/or `isOpen`. Dates and timestamps are asserted only on the in-memory return value of `constructNewAndSaveToDb` (creation path), never on a DB-read fiscal period.

Smell test: if `fetchById` returned a FiscalPeriod with the correct ID and `isOpen=true` but garbage in periodKey, startDate, endDate, createdAt, and modifiedAt, every citing test passes green. The DB round-trip for those five properties is unverified.

The fixture's `fiscalPeriods` list holds FiscalPeriod objects created by `constructNewAndSaveToDb` (in-memory constructed, never re-read from DB). Comparing `fetchById` output against one of these would be a genuine round-trip check — expected from in-memory construction, actual from DB read — and would not be Specimen 6.

**Action:** Expand the REQ-FP-3.1 test to find the matching period in `fixture.Data.fiscalPeriods` and assert all properties (periodKey, startDate, endDate, createdAt, modifiedAt) against it, in addition to the existing ID and isOpen checks.

**Why:** The REQ explicitly says 'all fiscal period properties.' Five of seven properties have no read-path verification anywhere in the suite. A mapping bug in `reconstitute` or `mapRawForDbRead` — swapping startDate and endDate, truncating timestamps, garbling the key — would be invisible to the current tests.

---
