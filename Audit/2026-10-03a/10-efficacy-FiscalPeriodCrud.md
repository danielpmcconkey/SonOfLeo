# test-efficacy-FiscalPeriodCrud

## EFF-FP-1 — test-gap
- **Location:** REQ-FP-1.3, REQ-FP-2.2; Tests/Tests.Integrated/Model/Ledger/FiscalPeriod.fs:45-56
- **Summary:** The only duplicate-key test passes on any non-query DAL failure. It never shows that the unique-key constraint caused the rejection.
- **Resolution:** fix-test

`REQ-FP-1.3 REQ-FP-2.2 Period Key must be unique` asserts `isCorrectError (... constructNewAndPersist ...) DalErrorDuringNonQueryExecution None` (line 51-54). `DalErrorDuringNonQueryExecution of exn` (Src/App.DataAccessLayer/DalError.fs:27) is the catch-all that ExecuteNonQuery.fs:43 returns for any exception during an insert. Examples: a renamed column, a type mismatch, a permission revoked by the migrator-ownership change, or a dropped connection. `isCorrectError` (Tests.Helpers/SadPath.fs:79-92) compares DomainName and CaseName only, so it never inspects the exception. This is Specimen 4 in typed clothing: the case is named, but the case does not identify the failure. Elsewhere the suite already narrows this error to the constraint. StageEntryIngestion.fs:841-842 matches `DalErrorDuringNonQueryExecution ex` and asserts `Assert.Contains("source_source_name_key", ex.Message)`. The FP test should do the same for `fiscal_period_period_key_unq` (DbMigration/Scripts/202609071105-CreateLedgerTables, line 47). Separately, REQ-FP-2.2 says the system must reject a duplicated key, and the system has no FP domain error for it. A caller who creates an existing period through the `FiscalPeriod Create` route gets a raw Postgres message plus a stack trace (DalError.fs:73). Compare DataIngestion, which has `IngestionSourceNameAlreadyExists`. No route-level test of the duplicate-create interaction exists.

**Action:** Change the test to match `Error (AsError (DalErrorDuringNonQueryExecution ex))` and assert that ex.Message names `fiscal_period_period_key_unq`. If Dan wants a typed domain rejection for REQ-FP-2.2, the alternative is a LedgerError case such as FiscalPeriodKeyAlreadyExists, asserted at the route.

**Why:** A rejection test that names only a catch-all case cannot tell a uniqueness violation from a broken insert statement. A test that passes for the wrong reason gives exactly the false assurance this audit exists to catch.

---

## EFF-FP-2 — test-gap
- **Location:** REQ-FP-2.4; Tests/Tests.Integrated/Model/Ledger/FiscalPeriod.fs:59-87; Tests/Tests.Integrated/InterfaceBridge/FiscalPeriodRoutes.fs:62-84, 216-226
- **Summary:** Neither citing test of REQ-FP-2.4 checks persistence: one asserts only the in-memory return value, and the other reads back by key and then asserts only that key.
- **Resolution:** fix-test

REQ-FP-2.4 requires creation to 'persist the fully validated record in the database and return a fiscal period record with the created ID, computed dates, and created/modified timestamps.' (1) The model test (FiscalPeriod.fs:59-87) asserts only on the value returned by `constructNewAndPersist`. That function returns the record it built in memory (Src/Business.CrossDomainOrchestration/FiscalPeriodCreation.fs:27-32), not a read-back. Nothing it asserts would fail if `FiscalPeriod.persist` wrote wrong dates or a wrong is_open value. Its ID assertion is `Assert.NotEqual(uuid, Guid.Empty)` (line 76), which is Specimen 2. (2) The route test (FiscalPeriodRoutes.fs:62-84) resolves the ID by key through `LookupCache.fiscalPeriodKeyToId` (line 72), calls `fetchById`, and then asserts `Assert.Equal(expected, FiscalPeriodKey.value(FiscalPeriod.periodKey fetched))` (line 77). That is Specimen 8: the row was located by the key that is then asserted. The persisted start date, end date, is_open, created_at and modified_at are never asserted after the round trip, and neither are the corresponding fields of the route's return payload. Persisting every column is checked only incidentally, by the REQ-FP-3.1 test (FiscalPeriod.fs:101-124), which compares fixture-built periods against fetchById. (3) `REQ-FP-2.4 Fiscal Period Create rejects invalid period key string` (FiscalPeriodRoutes.fs:216-226) asserts only the typed `FiscalPeriodInvalidKeyString` error. It verifies the REQ-FP-1.2 vector at the route but is credited to REQ-FP-2.4, and it does not check that nothing was persisted.

**Action:** In the route create test, after the route commits, assert fetched.startDate, endDate, isOpen and createdAt against literal or fixture-derived expectations instead of the key. Recite the invalid-key route test to REQ-FP-1.2, or add a post-condition that no row exists.

**Why:** The REQ's central obligation is that the record is persisted. As written, the citing tests would stay green if persist wrote garbage into every non-key column. Today only a test citing another REQ prevents that, so the coverage is thinner than traceability reports.

---

## EFF-FP-3 — test-gap
- **Location:** REQ-FP-2.1; Tests/Tests.Integrated/Model/Ledger/FiscalPeriod.fs:34-42
- **Summary:** The test for REQ-FP-2.1 asserts only that the generated ID is not Guid.Empty (Specimen 2). It never checks that the ID is unique or that the persisted row carries it.
- **Resolution:** fix-test

REQ-FP-2.1: 'the system must generate a unique UUID for the ID.' The test's only assertion is `Assert.NotEqual(unique_id, Guid.Empty)` (line 39). A constructor that returned the same non-empty Guid on every call would pass. It would be caught only incidentally, when the TestDataStage fixture hit the primary-key constraint while creating its ten periods, not by this test. The test also never shows that the ID returned to the caller is the ID stored in ledger.fiscal_period, because constructNewAndPersist returns its in-memory record. REQ-FP-1.7 (uniqueness) is waived on the grounds that 'constructNew always creates the UUID at runtime'. That waiver assumes a 2.1 test that actually shows runtime generation.

**Action:** Create two periods with distinct distant-year keys in one rolled-back transaction. Assert their IDs differ, and assert that `FiscalPeriod.fetchIdByKey` on each key returns the ID the creation returned.

**Why:** `<> Guid.Empty` is a cowardly inequality: it fails only on one degenerate value and checks no property of the generation the REQ requires.

---

## EFF-FP-4 — test-gap
- **Location:** REQ-FP-3.5 (and REQ-FP-3.4 model-layer test); Tests/Tests.Integrated/Model/Ledger/FiscalPeriod.fs:126-154
- **Summary:** The only test of REQ-FP-3.5 uses one-sided membership checks: the fixture's open IDs are present and one named closed ID is absent. It never asserts the exact set of open periods.
- **Resolution:** fix-test

`REQ-FP-3.5 fetchAll with open only filters out closed periods` asserts `List.exists(... = fixture.Data.closedFiscalPeriodId) |> Assert.False` and `openFiscalPeriodIds |> List.forall(... List.exists ...) |> Assert.True`. Two kinds of broken fetchAll would pass. One returns duplicates. The other returns any closed period other than the one named fixture ID, for example if the predicate became `unique_id <> @closedId` or if `is_open` were dropped while some other filter happened to exclude that row. The test never asserts that every returned period is open, or that the result equals the fixture's open periods. No route-level test of `FetchAll` with openOnly=true exists either. The REQ-FP-3.4 model test at lines 126-139 has the same superset-only shape, but the route test `REQ-FP-3.4 FiscalPeriod FetchAll happy path` (FiscalPeriodRoutes.fs:110-123) asserts exact key equality against fixture.Data.fiscalPeriods. 3.4 is therefore covered properly at one layer, and 3.5 at none.

**Action:** Derive the expected value as `fixture.Data.fiscalPeriods |> List.filter FiscalPeriod.isOpen |> List.map (periodKey >> value) |> List.sort` and assert exact equality with the fetched keys, as the route 3.4 test does.

**Why:** Specimen 2 and the hollow-names tell 'states only one side of a filter'. The test forbids one known row and permits everything else, including returning closed periods the test happens not to name.

---

## EFF-FP-5 — test-gap
- **Location:** REQ-FP-1.2; Tests/Tests.Isolated/Model/Ledger/FiscalPeriod.fs:16-31
- **Summary:** No test probes the 'four-digit year' clause of REQ-FP-1.2, and the happy-path test never inspects the key it constructed.
- **Resolution:** fix-test

REQ-FP-1.2: key must be `YYYY-MM` 'where YYYY is a four-digit year and MM is a two-digit month (01–12)'. The negative theory covers a missing hyphen (`202006`), month 00, month 13, and alphabetic input (`Sep-2025`). No case has a year that is not four digits, such as `226-01` or `20266-01`, and no case has a one-digit or three-digit month (`2026-1`, `2026-011`). Those cases are what would catch a regex that lost its `^`/`$` anchors or its `{4}` quantifier (Src/Business.FinancialServices.Ledger/FiscalPeriodComponent.fs:18). The happy-path test `REQ-FP-1.2 PeriodKey.fromString happy path` (lines 17-20) asserts only that the result is not an Error (`| _ -> ()`). It never asserts `FiscalPeriodKey.value` equals "2026-06", even though fromString transforms its input by trimming before it stores it (line 21).

**Action:** Add InlineData rows `226-01`, `20266-01`, `2026-1` and `2026-011` to the negative theory. In the happy path, assert `FiscalPeriodKey.value key = "2026-06"`.

**Why:** REQ-FP-1.2 defines several distinct rejection criteria. The tests exercise the month range and separator but not the year width or the anchoring, so part of the rule is cited but unproven.

---


