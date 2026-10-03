# test-efficacy-DataAccessLayer

## DAL-EFF-1 — test-gap
- **Location:** Specs/Behavioral/DataAccessLayer.md REQ-DAL-2.4; Tests/Tests.Integrated/DataAccessLayer/DalTests.fs:265-320; Tests/Tests.Integrated/ConnectionLeakGuard.fs:11-21
- **Summary:** REQ-DAL-2.4 names three paths (success, typed error, exception), but none of the four REQ-DAL-2.4 tests runs a successful operation. The success path, where the transaction must commit and release its connection, has no test under the REQ's ID.
- **Resolution:** fix-test

REQ-DAL-2.4 says every transaction is "committed or rolled back, and its connection released, on every path — success, typed error, and exception." The four tests that cite it are:
(1) the lookup-cache test at :266, which uses NoTransaction contexts and the cache's internal rollback-only load;
(2) the typed-error test at :295 (failWithTypedError);
(3) the exception test at :303 (failByThrowing);
(4) the pool-exhaustion test at :311, which alternates failWithTypedError and failByThrowing only.

No test sends an Ok-returning func through runCommandRouteAndAutoCompleteTransaction and then checks three things: from a fresh NoTransaction context the probe write exists (it committed); sessionsIdleInTransaction() = 0; and connectionsInUse() is back to the baseline. The helper confirmReleased (:251-258) can only assert that the probe does NOT exist (Assert.False(probeExists, ...)), so the commit half of the REQ has no assertion anywhere in DalTests.

The assembly-wide ConnectionLeakGuard does check that connections are released after every integrated test, and many tests commit successfully through runCommandRouteAndAutoCompleteTransaction. So release-on-success is guarded indirectly. But the guard compares only the pool's in-use count. It never checks for sessions left idle in a transaction, and it never checks that a commit happened. It is also not a test whose name cites the REQ.

Smell test: change DbTransaction.fs:106-109 so that the Ok branch calls rollback instead of commit, and every REQ-DAL-2.4 test stays green.

**Action:** Add a REQ-DAL-2.4 success-path test. It runs runCommandRouteAndAutoCompleteTransaction with a func that writes the probe fiscal period and returns Ok. It then asserts that idle-in-transaction = 0, that the in-use count equals the baseline, and that the probe exists when read from a fresh NoTransaction context. Finally it deletes the probe through the Cleanup helper.

**Why:** The REQ lists three paths, and its tests cover only two. The one left out (success, the common path) is where a commit-versus-rollback mix-up would silently lose every write while every REQ-DAL-2.4 test still passed. A REQ that lists several behaviors is only partly covered when the tests cited under its ID skip one of them.

---

## DAL-EFF-2 — test-gap
- **Location:** REQ-DAL-2.4; Src/Ui.InterfaceBridge/CommandRoute.fs:35-52; Src/Ui.InterfaceBridge/Routes/IngestionRoutes.fs:179,199; Tests/Tests.Integrated/DataAccessLayer/DalTests.fs:294-320
- **Summary:** Production code also opens transactions through a second runner, runCommandRouteAndAutoRollback (used for shadow post and shadow reconcile). No test drives its exception path, so whether that transaction is rolled back and released when the operation throws is never checked.
- **Resolution:** fix-test

REQ-DAL-2.4 covers "every database transaction the system opens." There are two runners that open route transactions:
- runCommandRouteAndAutoCompleteTransaction (CommandRoute.fs:28-31, which delegates to DbTransaction.runWithAutoCompleteTransaction);
- runCommandRouteAndAutoRollback (CommandRoute.fs:35-52). It has its own exception handling: `with _ -> tran |> rollback |> ignore; reraise()`.

The second runner is not test-only. IngestionRoutes.fs:179 uses it for the production shadow post (isShadow = true), and IngestionRoutes.fs:199 uses it for IngestShadowReconcile.

The dedicated exception tests (DalTests.fs:303 and :311) only go through runCommandRouteAndAutoCompleteTransaction. A grep of Tests/ for throwing funcs found none that throw inside runCommandRouteAndAutoRollback; in tests that runner is only ever reached through Ok and Error returns. The ConnectionLeakGuard can only see a leak on a path that some test actually runs, so it cannot cover this one either.

Smell test: delete the `tran |> rollback |> ignore` line at CommandRoute.fs:42. A thrown exception during a shadow post would then leave a connection checked out with its transaction still open, and every test would still pass.

**Action:** Add a REQ-DAL-2.4 test that runs failByThrowing through runCommandRouteAndAutoRollback, using the shadow-post action (for example IngestShadowPostStageEntries). It asserts that the exception propagates (Assert.Throws), and then calls confirmReleased inUseBefore.

**Why:** This REQ is about every transaction the system opens, not just the main runner. Testing one transaction runner does not show that a second runner with its own hand-written cleanup is correct.

---

## DAL-EFF-3 — test-gap
- **Location:** REQ-DAL-2.4; Tests/Tests.Integrated/DataAccessLayer/DalTests.fs:265-292; Src/App.DataAccessLayer/LookupCache.fs:30-33,53-64
- **Summary:** The REQ-DAL-2.4 lookup-cache test only exercises the cache's transaction-opening load if it happens to be the first code in the test process to touch each cache. Whether that happens depends on test order, so in practice it probably runs only the no-transaction single-row lookup.
- **Resolution:** fix-test

In LookupCache.fs, the only code that opens a transaction is fetchAll (:53-64): createDbTransaction, then a read, then `finally tran |> rollback |> ignore`. Cache.fetch calls loadAll only when `cache` is None (:30-33). The caches are process-wide and never reset; the test's own comment at :260-261 says so.

Several Src modules fetch from these caches on ordinary operation paths: AccountFieldConverters, FiscalPeriodFieldConverters, CashFlowLookupConverters, SharedContractConverters, StageEntryLine, AgreementOrchestration, StageEntryOrchestration and ClassificationOrchestration. Any integrated test that ran earlier in the same process and used one of them has already loaded that cache. In that case the fetches at :273-281 only reach loadOne (fetchOne with a NoTransaction context), which opens no transaction.

The test's assertions (DalNoOp for each fetch; idle = 0; inUseBefore = connectionsInUse()) hold whether or not fetchAll ran. Collections run sequentially (xunit.runner.json), but the order in which tests run is not something this test controls.

Smell test: remove the rollback at LookupCache.fs:62. When this test runs after any test that loaded a cache, it passes. The ConnectionLeakGuard would blame whichever test loaded the cache first, so the suite as a whole still fails, but this REQ-DAL-2.4 test proves nothing about the cache load it is named for ("after every lookup cache has loaded").

**Action:** Either make the test force a fresh load of each cache (this needs an internal reset hook on Cache, which is a Src change), or drop the cache-load claim from this test's name and rely openly on ConnectionLeakGuard for fetchAll. Dan to pick.

**Why:** A test whose effect depends on execution order can be green for a reason unrelated to what its name claims. The "garbage of the right shape" test fails here whenever the test is not the first to touch the caches.

---

## DAL-EFF-4 — stale-ruling
- **Location:** Skills/SonOfLeoRequirementsAudit/resolved-findings.md:165-169 (DAL-EFFICACY); Specs/Behavioral/DataAccessLayer.md REQ-DAL-2.4
- **Summary:** The DAL-EFFICACY ruling says all 19 active REQ-DAL requirements are waived or unenforceable, so a DAL test-efficacy pass will always find nothing. REQ-DAL-2.4 (added 2026-09-26) is tested and is not waived, so the ruling's premise is no longer true.
- **Resolution:** dan-decides
- **Prior ruling:** DAL-EFFICACY (overruled, 2026-08-20/22). It is raised again because its premise ("all 19 waived or unenforceable") no longer holds now that REQ-DAL-2.4 exists and is tested.

The ruling (dated 2026-08-20, wording corrected 2026-08-22) reads: "all 19 are either waived from testing or classified as unenforceable... A test-efficacy auditor scoped to the DAL will always return 'no findings' because there are no tested REQ IDs to audit against... Do not flag the absence of DAL-specific efficacy findings."

The spec now has 19 active REQs, but REQ-DAL-2.4 is in neither the Waived table nor the Unenforceable table. Four tests cite it (DalTests.fs:266, :295, :303, :311), and Tests/Tests.Integrated/ConnectionLeakGuard.fs exists for it. The ruling would tell future auditors to skip a scope that now contains real test-efficacy gaps (DAL-EFF-1 to DAL-EFF-3). The findings above are not suppressed by it, because it does not match exactly: it is about there being no tested REQ IDs, which is no longer true.

**Action:** Withdraw or rewrite DAL-EFFICACY so it says REQ-DAL-2.4 is the one tested DAL requirement and is in scope for test-efficacy audits.

**Why:** Precedent rulings tell later audit runs what to skip. A ruling whose factual premise is gone will push future auditors to drop real findings about the transaction-hygiene requirement that protects the connection pool.

---

