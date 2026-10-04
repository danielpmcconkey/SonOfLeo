# code-inward-coverage-DataAccessLayer

## COV-DAL-1 — test-gap
- **Location:** Src/App.DataAccessLayer/DbTransaction.fs lines 56-62, 87-93; Src/App.DataAccessLayer/DalError.fs (DalErrorDuringTransactionCommit, DalErrorDuringTransactionRollback); REQ-DAL-2.4
- **Summary:** No test exercises the branches where the commit or rollback itself fails. DalErrorDuringTransactionCommit and DalErrorDuringTransactionRollback appear nowhere in Tests/, even though a test can trigger both.
- **Resolution:** fix-test

commitOrRollbackAndDispose (DbTransaction.fs 50-62) catches an exception from npgTran.Commit()/Rollback(), maps it to DalErrorDuringTransactionCommit or DalErrorDuringTransactionRollback, and disposes the transaction and connection in `finally`. runWithAutoCompleteTransaction (87-93) passes the commit error through on the Ok branch (line 93). On the Error branch it returns the rollback error in place of the function's own error (line 89). A grep of every DalError case against Tests/**/*.fs shows these two cases have no references at all. The other unreferenced cases are the connection-string ones (DalConnectionString*, DalEnvVarNotSet), which REQ-DAL-1.14 to 1.18 waive, and DalErrorDuringTransactionCreation, which is hard to provoke. DalTests.fs lines 85-86 justify leaving some errors out because they are "impossible to provoke from a buildable, functioning code base". That does not hold for these two. createDbTransaction and commit/rollback are public, and completing the same DbTransaction twice makes the second Commit()/Rollback() run against a transaction that is already disposed. That throws inside the try and lands in exactly these arms. REQ-DAL-2.4 says every transaction is committed or rolled back and its connection released "on every path — success, typed error, and exception". The DAL-2.4 tests in DalTests.fs (lines 272-362) cover success, a typed error from func and a throw from func. None covers the path where the DAL's own commit or rollback fails, which is the path that depends on the `finally` dispose at lines 60-62. ConnectionLeakGuard would catch a leak only if some test reached that path, and none does.

**Action:** Add DAL tests that commit, and separately roll back, an already-completed DbTransaction. Each should assert the typed DalErrorDuringTransactionCommit / DalErrorDuringTransactionRollback and that ConnectionPool.connectionsInUse is back to its starting value. Cite REQ-DAL-2.4.

**Why:** These are the only DAL paths where releasing the connection depends entirely on a `finally` block. They are also the paths most likely to run when the database is unhealthy. Without a test, a refactor that moves the dispose into the try would leak connections on commit failure and every check would still pass.

---

## COV-DAL-2 — test-gap
- **Location:** Src/App.DataAccessLayer/ExecuteReader.fs lines 14-28 (confirmNumRows; AcceptableExpectedRows.Zero and .OneOrMany); REQ-DAL-2.2
- **Summary:** No test runs the Zero and OneOrMany row checks in the DAL's confirmNumRows, and no production code passes either expectation to the DAL.
- **Resolution:** dan-decides

confirmNumRows handles four expectations. ExactlyOne is tested by DalTests.fs 163-192, and AnyQuantityIsAcceptable runs on every read. The Zero arms (line 22 Ok; the fall-through at line 28 when rows exist) and the OneOrMany arms (line 24 Ok; line 27 DalNoOp) are never reached. Across Src, outside App.DataAccessLayer, `Zero` and `OneOrMany` appear only in JournalEntryOrchestration.fs 239-246. That code re-implements the same match over its own deduplicated count after calling JournalEntryHeader.query with AnyQuantityIsAcceptable. No caller in Src constructs Zero or OneOrMany as a value, so no executeReaderQuery or executeNonQuery call ever receives them. In Tests, DalTests.fs lines 55 and 64 pass Zero only alongside the malformed SQL "SEL ECT ...". That throws before confirmNumRows runs, so the Zero arms stay unexecuted. REQ-DAL-2.2 is marked tested, but its tests cover only the ExactlyOne branch. Two of the four expectations in the public AcceptableExpectedRows type have no caller and no test.

**Action:** Dan decides: either remove Zero and OneOrMany from the DAL's expectation type, or add REQ-DAL-2.2 tests that run executeReaderQuery/executeNonQuery with Zero against 0 and 2 rows and with OneOrMany against 0 and 2 rows. JournalEntryOrchestration.fs 239-246 depends on the type and needs the same decision.

**Why:** A row-count check that no test or caller exercises gives REQ-DAL-2.2's 'verify against expected rows' no protection on that branch. The duplicate copy in JournalEntryOrchestration can drift from the DAL's version without either one failing a test.

---

## COV-DAL-3 — test-gap
- **Location:** Src/App.DataAccessLayer/ExecuteScalar.fs lines 13-150; Src/App.DataAccessLayer/ExecuteReader.fs line 84 (RowReader.getBoolOption); Src/App.DataAccessLayer/QueryParameter.fs lines 23-24, 77-86 (NullableBoolean, NullableJsonb)
- **Summary:** 13 of the 14 public scalar-unboxing functions, plus RowReader.getBoolOption and the NullableBoolean/NullableJsonb parameter cases, have no production caller. No test exercises their success branch.
- **Resolution:** dan-decides

The only executeScalar call in Src is AccountDeactivation.fs:86, and it uses longUnboxing. The other 13 unboxing functions have no caller in Src: stringUnboxing, stringOptionUnboxing, intUnboxing, intOptionUnboxing, longOptionUnboxing, decimalUnboxing, decimalOptionUnboxing, localDateUnboxing, localDateOptionUnboxing, instantUnboxing, instantOptionUnboxing, uuidUnboxing and uuidOptionUnboxing. Tests reference them only in the DalTests.fs theory at 87-160, which drives just two error branches: the null branch through "select 'burp' where 1 = 0" and the wrong-type branch through "SELECT 'hello'" / "SELECT 1". The success branches never run anywhere: `Ok unboxed` in the non-option functions, and `Ok None` / `Ok(Some unboxed)` in the option functions. Only longUnboxing's success branch runs, through AccountDeactivation and the DalTests helper sessionsIdleInTransaction. Likewise, RowReader.getBoolOption has no reference in Src or Tests, and the QueryParameterValue cases NullableBoolean and NullableJsonb are never built in Src or Tests. Each is part of the DAL's public surface and has no caller, no test and no REQ. These are not internal helpers covered through a tested API; nothing reaches them.

**Action:** Dan decides: delete the uncalled unboxing functions, getBoolOption, NullableBoolean and NullableJsonb, or keep them and add one success-path test each. For example, executeScalar "select 1.50::numeric" with decimalUnboxing returns 1.50M, and "select null::uuid" with uuidOptionUnboxing returns Ok None.

**Why:** Public DAL functions that are tested only on their error path give no evidence that they decode Npgsql's types correctly. The first real caller would be the first to find out, for example whether intUnboxing can unbox a Postgres bigint count. Code with no caller and no test cannot be verified and cannot be traced to a requirement.

---

