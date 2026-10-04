# test-efficacy-FiscalPeriodCrud

## EFF-FP-1 — test-gap
- **Location:** Tests/Tests.Integrated/Model/Ledger/FiscalPeriod.fs:54-65 (REQ-FP-1.3, REQ-FP-2.2)
- **Summary:** The duplicate-period-key test passes on any DalErrorDuringNonQueryExecution, which covers every database failure, and it never checks that the existing period was left alone.
- **Resolution:** fix-test

REQ-FP-1.3 says no two fiscal period records may share a period key. REQ-FP-2.2 says create must reject a duplicated key. The only test citing either asserts `isCorrectError (existingKey |> FiscalPeriodCreation.constructNewAndPersist context) DalErrorDuringNonQueryExecution None` (lines 59-63). DalErrorDuringNonQueryExecution is `of exn` (Src/App.DataAccessLayer/DalError.fs:26), and ExecuteNonQuery.fs:38 wraps every exception from an insert in it. isCorrectError compares only DomainName and CaseName (Tests/Tests.Helpers/SadPath.fs:63-72). So the test passes for an FK violation, a NOT NULL violation, a misspelled column or a lost connection, not just for a breach of `fiscal_period_period_key_unq` (DbMigration/Scripts/202609071105-CreateLedgerTables.sql:47). This is Specimen 4 (untyped failure): the case it names is the DAL's catch-all for 'something went wrong in the database'. Specimen 4 records that exact catch-all hiding four broken tests in August 2026. REQ-SYS-2.1.2 does allow this rejection to fall through to the database constraint, and the suite already has a pattern for pinning that: PaymentAgreementDataStates.fs:108-117 (`constraintViolation "23505" "payment_agreement_payment_agreement_name_key"`) matches the DbException's SqlState and constraint name. The sibling Account uniqueness test (Tests/Tests.Integrated/Model/Ledger/Account.fs:64-70) also reads the data back so that an overwrite would fail it, with the comment 'The error alone would also be satisfied by an implementation that resolved the collision by overwriting the account already holding the code.' This test does neither. If the period insert were broken for any reason, or an upsert replaced the existing period, the test would still pass. It is also the only test anywhere that cites REQ-FP-1.3 or REQ-FP-2.2.

**Action:** Change the assertion to match DalErrorDuringNonQueryExecution(:? DbException as db) with db.SqlState = "23505" and the constraint name fiscal_period_period_key_unq (reuse the constraintViolation shape from PaymentAgreementDataStates.fs). Then re-fetch the existing period by key and assert it equals the fixture's period (same id, dates, isOpen, timestamps).

**Why:** A sad-path test has to show the specific rejection fired. Matching a catch-all error case is Result.isError under another name: a regression that breaks every insert, or one that quietly overwrites, still passes, and nothing else in the suite covers this requirement.

---

## EFF-FP-2 — test-gap
- **Location:** Tests/Tests.Integrated/Model/Ledger/FiscalPeriod.fs:136-148 (REQ-FP-3.4)
- **Summary:** The model-layer 'fetchAll without filter happy path' test checks only that some fixture IDs are present, using Assert.True over List.forall/List.exists, never the exact result set.
- **Resolution:** fix-test

REQ-FP-3.4: 'The system must be able to retrieve all fiscal period records without filter.' The model test pipes `fixture.Data.openFiscalPeriodIds |> List.forall(... List.exists ...)` into `Assert.True` (lines 140-142), and does the same for `closedFiscalPeriodId` (lines 143-145). This is a one-sided membership check written as a boolean truth assertion, not an exact comparison: Specimen 2 (an assertion that gave up on exactness) and the 'states only one side of a filter' tell from the hollow-names table. It passes if fetchAll(false) returns every row twice, returns extra rows, or returns rows whose key, dates or flags are garbage, because only IDs are inspected. The REQ-FP-3.5 test immediately below (lines 151-164) already does an exact `Assert.Equal<Set<string>>` against keys derived from the fixture, and the route test FiscalPeriodRoutes.fs:113-126 does an exact sorted-key-list equality, so the model test is the odd one out. The behavior is not uncovered (the route test pins it exactly), but under Tests/README.md every happy path is tested at each layer, and this layer's test cannot fail for over-returning.

**Action:** Replace the two Assert.True(List.forall/List.exists) assertions with Assert.Equal<string list>(fixture.Data.fiscalPeriods keys sorted, fetched keys sorted), mirroring FiscalPeriodRoutes.fs:115-123. Optionally rename the test to state the property, e.g. 'fetchAll without filter returns exactly the fixture's periods, open and closed'.

**Why:** Asserting with Assert.True on a membership predicate when an exact equality is available lets duplicates and extra rows through. Tests/README.md says 'Asserting equality is preferred to asserting truth.'

---

## EFF-FP-3 — test-gap
- **Location:** Tests/Tests.Integrated/Model/Ledger/FiscalPeriod.fs:167-175 (REQ-FP-4.1), 198-205 (REQ-FP-4.2)
- **Summary:** The model-layer close and reopen happy-path tests assert only the returned isOpen flag, never that the returned period is the one that was targeted.
- **Resolution:** fix-test

REQ-FP-4.1 and REQ-FP-4.2 require a means to close or reopen *a* fiscal period. The model tests assert only `Assert.False(FiscalPeriod.isOpen closed)` (line 172) and `Assert.True(FiscalPeriod.isOpen reopened)` (line 202). Smell test from the specimens doc: if closeFiscalPeriod returned some other already-closed period (the fixture has one, closedFiscalPeriodId) or reopenFiscalPeriod returned any open period, both tests would pass, because neither compares the returned period's id or key with the input id. The production code returns `fpId |> fetchById context` (Src/Business.FinancialServices.Ledger/FiscalPeriod.fs:169), so a defect in the update's WHERE clause, such as toggling the wrong row, combined with the re-fetch would go unnoticed at this layer. The route tests (FiscalPeriodRoutes.fs:142 and 169) do assert the returned key, so the behavior has some coverage, but only at a layer where the period is resolved by key rather than by the id the model function takes.

**Action:** In both model tests, also assert Assert.Equal(id, closed |> FiscalPeriod.fiscalPeriodId) (and likewise for reopened), and assert that the returned key equals the fixture period's key.

**Why:** An assertion on a flag alone is satisfied by any record of the right shape. The test has to tie the result back to the input it was given, or it does not show the operation acted on the intended period.

---

