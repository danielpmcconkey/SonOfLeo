# test-efficacy-DataAccessLayer

## EFF-DAL-2.2-1 — test-gap
- **Location:** REQ-DAL-2.2; Tests/Tests.Integrated/DataAccessLayer/DalTests.fs:163-205; Src/App.DataAccessLayer/ExecuteReader.fs:20-28 (confirmNumRows), Src/App.DataAccessLayer/ExecuteNonQuery.fs:39
- **Summary:** REQ-DAL-2.2 names reads, inserts, updates and deletes, but the citing tests check only reads (0 and 2 rows) and one update (0 rows). No test shows that a write touching more rows than expected is rejected.
- **Resolution:** fix-test

REQ-DAL-2.2 says: "All non-scalar queries (set-based read, insert, update, and delete) must verify against expected rows affected." Four tests cite it.
(a) Line 163: a read expecting ExactlyOne that returns two rows gets DalResultantRowsDidntMatchExpectation("ExactlyOne", 2). This is a strong test with a typed match and payload asserts.
(b) Line 175: a read expecting ExactlyOne that returns zero rows gets DalNoOp.
(c) Line 186: an update (`update ledger.account set code = code where 1 = 2`) expecting ExactlyOne that touches zero rows gets DalNoOp.
(d) Line 195: tests whenNoRows on hand-built Error values. It never touches the database.

The gaps:
1. Over-count on the write path is never tested. The only DalResultantRowsDidntMatchExpectation test is a read (executeReaderQuery). executeNonQuery gets its count from `command.ExecuteNonQuery()`, a different source from the reader's `rows.Length`, and no test checks that this count is compared on the over-count side. Smell test: change ExecuteNonQuery.fs:39 from `confirmNumRows numRows expectedRows` to a check that rejects only `numRows = 0` and all four tests still pass. An update or delete that hits two rows where the caller declared ExactlyOne is the failure 2.2 exists to catch.
2. Inserts and deletes are named in the REQ but never exercised by a citing test.
3. confirmNumRows has four expectations (Zero, ExactlyOne, OneOrMany, AnyQuantityIsAcceptable). Only ExactlyOne's failure arms are reached. Nothing tests that `Zero` with rows present gives DalResultantRowsDidntMatchExpectation, or that `OneOrMany` with zero rows gives DalNoOp. A mutation of `| Zero when numRows = 0 -> Ok()` to `| Zero -> Ok()` survives. (Context: grep finds no production caller passing `Zero`. `OneOrMany` appears only in the duplicated row check in JournalEntryOrchestration.fetchHeadersFromFilter, lines 239-246.)
Test (d) is a sound test, but it tests whenNoRows (closer to REQ-SYS-6.2's typed not-found rule) rather than the row verification itself, so it adds nothing to 2.2's write-path coverage.
Precedent: CON-DAL-02 rules that 2.2 means "verify against the caller's declared expectation". This finding accepts that reading and asks that the declared expectation actually be tested on the write path. DAL-EFFICACY says 2.2 is in scope once its tests are cited, which they now are.

**Action:** Add a REQ-DAL-2.2 test that runs executeNonQuery inside a rolled-back transaction (form 3). The statement is an update (and/or a delete) whose predicate matches two fixture rows (e.g. `update ledger.account set code = code where unique_id in (@a, @b)` with two fixture account IDs), declared ExactlyOne. Assert `Error (AsError (DalResultantRowsDidntMatchExpectation ("ExactlyOne", 2)))`. Optionally add a Theory over the remaining (expectation, rowCount) cells of confirmNumRows (Zero with 1 row, OneOrMany with 0 rows, plus the success cells), run through executeReaderQuery with literal SQL.

**Why:** A row-count guard on writes is a backstop against an update or delete whose predicate is wider than intended. The only over-count test is on reads, so the write-side comparison could be weakened to a zero-row check and the suite would stay green. That is the "function returns garbage of the right shape" failure.

---

## EFF-DAL-2.4-1 — test-gap
- **Location:** REQ-DAL-2.4; Tests/Tests.Integrated/DataAccessLayer/DalTests.fs:273-299; Src/App.DataAccessLayer/LookupCache.fs:25-40, 53-63
- **Summary:** Whether the REQ-DAL-2.4 lookup-cache test reaches the transaction it was written to check depends on test order. The caches are process-wide and load only once, so if an earlier test already loaded them, this test opens no transaction and passes without checking anything.
- **Resolution:** fix-test

The test at line 273 is named "fetching through every lookup cache leaves no session idle in a transaction and the pool's in-use count where it started". Its comment (line 275) says "each cache loads in full on its first fetch".

In the source, `Cache.fetch` (LookupCache.fs:28-40) calls `loadAll()` only while the module-level mutable `cache` is None. `loadAll` (fetchAll, lines 53-63) is the only code here that opens a transaction (`createDbTransaction()`) and relies on `finally tran |> rollback` to release it. The nine caches (Account.codeToId/idToCode/idToName, FiscalPeriod.keyToId/idToKey, MasterAgreement and PaymentAgreement nameToId/idToName) are process-wide statics that are never invalidated. Route and orchestrator tests load them during a run, e.g. AccountFieldConverters (5 call sites), CashFlowLookupConverters (4), FiscalPeriodFieldConverters, StageEntryOrchestration, AccountRoutes.fs and FiscalPeriodRoutes.fs tests. ConnectionReleaseTests is one of 86 classes in the SharedTestData collection, so whether it runs before or after them depends on xUnit's case ordering.

When it runs after them, every fetch skips loadAll and goes straight to `loadOne` under the NoTransaction context the test passes (line 276). No transaction is opened, so `Assert.Equal(0L, idle)` (line 296) and `Assert.Equal(inUseBefore, connectionsInUse())` (line 297) hold no matter what fetchAll does. Deleting the `finally ... rollback` in fetchAll would not turn this test red in that ordering. The miss arm `Error (AsError (DalNoOp _))` (line 292) also cannot tell "loaded, then missed" from "already loaded, missed", so the test cannot detect that it skipped the load.
The assembly-wide ConnectionLeakGuard (ConnectionLeakGuard.fs) partly backstops this by checking pool in-use on whichever test first loads each cache. But the idle-in-transaction check, the server-side half of 2.4, exists only in this dedicated test, and it is order-dependent.

**Action:** Make the test independent of process state. Either (a) build fresh `LookupCache.stringToIdCache`/`idToStringCache` instances for each table inside the test, so the first fetch always runs fetchAll, or (b) assert directly that the load ran (e.g. fetch a key that exists in fixture data through a newly constructed cache) before asserting release. Then the 2.4 claim covers the transaction fetchAll opens.

**Why:** Specimen 9 / smell test #2: a test of a release mechanism has to make sure the mechanism actually ran. If an earlier test already loaded the caches, this test measures a code path that opens no transaction and is green whether or not fetchAll leaks.

---

## EFF-DAL-UNCITED-1 — test-gap
- **Location:** Tests/Tests.Integrated/DataAccessLayer/DalTests.fs:85-160 (``DAL errors surface when they should``); Src/App.DataAccessLayer/DalError.fs, ExecuteScalar.fs, DbTransaction.fs
- **Summary:** A 27-case Theory in the DAL test file cites no REQ ID. It tests DAL error and unboxing behavior (null and wrong-type unboxing, transaction-of-None misuse, malformed-SQL execution errors) that no behavioral spec describes, and one case repeats a REQ-DAL-2.4 test.
- **Resolution:** dan-decides

The Theory at lines 87-160 has no REQ prefix. Its InlineData rows cover: DalCantCompleteTransactionOfNone, DalCantUseTransactionOfNoneInAutoCommit, seven `*UnboxingReturnedNull` cases, fourteen `DalErrorDuring*Unboxing`/`*OptionUnboxing` cases, DalErrorDuringNonQueryExecution / ReaderQueryExecution / ScalarExecution (malformed SQL `SEL ECT`), and DalErrorDuringAutoCompleteTransactionRun.
No REQ in DataAccessLayer.md covers typed unboxing failures, typed SQL-execution failures, or rejecting a no-transaction context in commit / auto-commit. Section 1's typed errors are all waived, and section 2 covers only parameterization, row counts and transaction release. SystemWide.md has no requirement for these either. Tests/README.md, "Bullshit test practices": "Do not write tests unless you have a behavioral REQ to cite. If the code you are testing does something uncited by the REQs, stop and point that out. Likely an REQ needs to be added."
Separately, the `DalErrorDuringAutoCompleteTransactionRun` row (lines 125-128: raise inside runWithAutoCompleteTransaction on a NewTransaction context) covers the same case as ``REQ-DAL-2.4 an operation that throws mid-transaction rolls back...`` (line 345), which already asserts the typed `DalErrorDuringAutoCompleteTransactionRun` with its message. The Theory row adds no rollback or release check (the leak guard aside), so it tests the same thing twice.
The assertions themselves are typed (isCorrectErrorString matches the unique DU case name, per SadPath.fs), so this is not a Specimen 4 issue. The problem is that the tests have no REQ behind them. No resolved-findings entry covers this Theory.

**Action:** Dan decides: either add a DAL REQ (e.g. "Every DAL failure, including a value that cannot be read as its requested type and a transaction operation on a context with no transaction, fails with a typed error naming the failure") and prefix the Theory with it, or delete the Theory. In either case, drop the DalErrorDuringAutoCompleteTransactionRun row, since REQ-DAL-2.4's throw test already covers that case.

**Why:** Tests are meant to trace to requirements. Behavior the code guarantees but no spec states can be changed or removed without anyone noticing a broken requirement, and the traceability gate cannot see 27 cases that verify behavior with no REQ.

---

