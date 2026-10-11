# test-efficacy-DataAccessLayer

_No findings._

## Reasoning

Scope: DataAccessLayer.md has 19 active IDs. 14 are in the Waived table (1.14-1.20, 2.1, 2.3, 3.1, 3.2, 3.4, 3.5, 3.7) and 3 are in the Unenforceable table (3.2.1, 3.3, 3.6). Under precedent DAL-EFFICACY, none of those 17 is in scope for efficacy findings. That leaves REQ-DAL-2.2 and REQ-DAL-2.4, cited by 11 tests in /home/claude/SonOfLeo/Tests/Tests.Integrated/DataAccessLayer/DalTests.fs plus the assembly-wide guard in /home/claude/SonOfLeo/Tests/Tests.Integrated/ConnectionLeakGuard.fs. I read the spec, the specimens doc, Tests/README.md, the audit-conduct catalog and the precedent ledger (AMB-5, CON-DAL-02, DAL-EFFICACY). I read each test next to the DAL source (DalError.fs, ExecuteReader.fs, ExecuteNonQuery.fs, ExecuteScalar.fs, DbTransaction.fs, DbConnection.fs, LookupCache.fs), Ui.InterfaceBridge/CommandRoute.fs and App.Session/Context.fs.

REQ-DAL-2.2 (rows-affected verification): confirmNumRows has three non-Ok outcomes. Each one is tested with a typed match that checks the payload, not just the case (Specimen 4 does not apply).
- Read, ExactlyOne, two rows (lines 40-46): DalResultantRowsDidntMatchExpectation ("ExactlyOne", 2).
- Read, ExactlyOne, zero rows (line 52): DalNoOp, checked through isCorrectErrorEmpty, which matches on the case.
- Update, ExactlyOne, zero rows (line 63): DalNoOp.
- Update, ExactlyOne, two rows (lines 72-83): ("ExactlyOne", 2). The 2 comes from the SQL's `limit 2`. If the fixture had fewer accounts the test would go red, not pass falsely, so this is not Specimen 1.
- whenNoRows (lines 86-96): tested both ways. DalNoOp is swapped for the caller's error, and the wrong-count error passes through.

Insert and delete go through the same executeNonQuery/confirmNumRows path as update, so they are not untested branches. AnyQuantityIsAcceptable on set-based non-queries (e.g. AccountSnapshotLot.fs:109) is sanctioned by CON-DAL-02. I found no executeScalar used for writes and no Npgsql command outside App.DataAccessLayer, so no code path dodges the verification.

REQ-DAL-2.4 (commit or rollback plus release on every path): runWithAutoCompleteTransaction has three branches, and each is tested from outside the transaction boundary, which is what Specimen 9 requires.
- Success (line 199): the probe row is absent first and present after, with no session idle in a transaction and the pool's in-use count where it started.
- Typed error (line 234): the probe row is absent (rollback, not commit).
- Exception (line 242): wrapped as DalErrorDuringAutoCompleteTransactionRun with the exact message, and rolled back.
- Pool exhaustion (line 250): runs the pool maximum plus 5 failing operations; a leak would block or throw.
- The rollback runner's re-raise path (line 222) is tested, as is the LookupCache full-load transaction (line 164).

I checked that the probe reader (FiscalPeriod.fetchIdByKey) reads the database directly, not through a cache, so the probe assertions cannot be satisfied by stale cache state. I also checked that the hard-coded list of 9 caches in the line-164 test matches all 9 production caches in Src (Account x3, FiscalPeriod x2, MasterAgreement x2, PaymentAgreement x2).

The rollback runner's Ok and typed-error paths (used in production for shadow post and shadow reconcile) have no dedicated test. I did not raise this. The assembly-wide ConnectionLeakGuard (xUnit 2.9.3 BeforeAfterTestAttribute, so a throw in After fails the test) checks connection release after every integrated test, and thousands of tests run through that runner. Npgsql also ends any open transaction when a connection goes back to the pool. Whether the rollback runner rolls back rather than commits on success belongs to REQ-STG-8.x, which is outside this scope.

Negative coverage: 2.2's rejection behavior (a wrong count fails) is proven with typed errors. 2.4 has no rejection semantics.

Uncited behavior: in App.DataAccessLayer, the connection-string validation in DbConnection.fs maps to waived 1.14-1.19, containsPattern is cited via REQ-SYS-1.4, and buildReadQuery/RowReader are plumbing exercised by every domain test. None of it is behavior missing from a spec.

Dan's statement makes no DAL-specific claim, so there is no statement-delta in this scope.
