# code-outward-coverage-DAL

## ARCH-DAL-1 — statement-delta
- **Location:** Src/App.DataAccessLayer/LookupCache.fs:10, 83-91
- **Summary:** The App-tier DAL's LookupCache hardcodes Business-tier tables, columns and entity names (ledger.account, ledger.fiscal_period, cashflow.master_agreement, cashflow.payment_agreement). That contradicts Dan's statement that no lower tier has any insight into an upper tier's domain.
- **Resolution:** dan-decides

Dan's statement for this run: "I broke out the monolithic error and auditable actions into domain-specific implementations of an interface. This was to further my principle that no lower tier should have any insight into an upper tier's domain." Architecture/SonOfLeo.archimate line 1634 (Principle 'This application is structured in domain tiers...') says: "we *do* strictly enforce that no lower tier has any understanding of higher tier modules, types, or even labels." Line 1613 ('Tiers Build Upward') says: "App references nothing in Business or Ui."

App.DataAccessLayer is in the App tier, but LookupCache.fs lines 83-91 define nine public values that are named after Business-tier entities and carry their schema and column names:
- accountCodeToId / accountIdToCode / accountIdToName ("ledger.account", "code", "account_name")
- fiscalPeriodKeyToId / fiscalPeriodIdToKey ("ledger.fiscal_period", "period_key")
- masterAgreementNameToId / masterAgreementIdToName ("cashflow.master_agreement", "agreement_name")
- paymentAgreementNameToId / paymentAgreementIdToName ("cashflow.payment_agreement", "payment_agreement_name")

The file admits this at line 10: "// todo: make this an interface so that lower tier doesn't need to have higher tier awareness". The cash-flow slice added two more entities (master and payment agreement) to this lower-tier list, so the deviation grew during the same refactor Dan describes as removing this kind of awareness (the error and auditable-action split).

These are not just UI boundary helpers. Business-tier modules call the cache directly for existence checks: StageEntryLine.fs:79, AgreementOrchestration.fs:39, StageEntryOrchestration.fs:102 and ClassificationOrchestration.fs:31. The UI boundary converters (AccountFieldConverters, FiscalPeriodFieldConverters, CashFlowLookupConverters, SharedContractConverters) call it as well. I found no ruling about LookupCache in resolved-findings.md.

**Action:** Dan decides one of two things. (a) Make the generic Cache<'K,'V> the only thing App.DataAccessLayer exposes, and move each named instance (table, key column, entity label) into the lowest tier of its own business domain, as the line-10 todo proposes. (b) Record LookupCache as an approved exception to the tier principle in the archimate model.

**Why:** Dan relies on this audit as the backstop for a fully agentic workflow. The tier principle exists to stop agents from putting components in the wrong place. A sanctioned-looking example in the lowest tier, one that grew during this slice, is exactly the precedent a future agent will copy.

---

## TEST-DAL-2.4-1 — test-gap
- **Location:** Src/App.DataAccessLayer/DbTransaction.fs:42-57, 59-78, 100-109; REQ-DAL-2.4; Tests/Tests.Integrated/DataAccessLayer/DalTests.fs:87-116
- **Summary:** No test reaches three REQ-DAL-2.4 branches in DbTransaction: transaction-creation failure (connection disposed, then DalErrorDuringTransactionCreation), commit failure (DalErrorDuringTransactionCommit, then disposal in finally), and rollback failure (DalErrorDuringTransactionRollback, which runWithAutoCompleteTransaction returns in place of the operation's own error).
- **Resolution:** dan-decides

REQ-DAL-2.4 reads: "Every database transaction the system opens is committed or rolled back, and its connection released, on every path — success, typed error, and exception."

DbTransaction.fs has dedicated code for three failure paths:
- Creation failure (lines 48-56). If BeginTransaction throws after OpenConnection succeeds, the connection is explicitly disposed (comment at line 52: "nothing else would ever release it"), and the result is Error DalErrorDuringTransactionCreation.
- Commit failure (lines 66-78). The exception becomes DalErrorDuringTransactionCommit, and the finally block disposes the transaction and the connection. In runWithAutoCompleteTransaction (lines 106-109), the Ok result is dropped and commitError is returned.
- Rollback failure (line 75, plus lines 102-105). runWithAutoCompleteTransaction returns rollbackError and discards the operation's original funcError.

What the tests cover:
- The DalTests Theory (lines 87-115) lists 28 error cases. It does not include DalErrorDuringTransactionCreation, DalErrorDuringTransactionCommit or DalErrorDuringTransactionRollback.
- A grep of Src and Tests finds those three cases only where DbTransaction.fs constructs them; no test references them.
- The four REQ-DAL-2.4 tests (DalTests.fs:266-320) cover the lookup-cache load, typed-error rollback, thrown-exception rollback, and pool-exhaustion paths.
- The assembly-wide ConnectionLeakGuard (Tests/Tests.Integrated/ConnectionLeakGuard.fs) covers the success path for every integrated test.

No test drives the commit-throws, rollback-throws or begin-throws branches. Nothing in the spec says which error the caller gets when both the operation and the rollback fail.

The DalTests comment at lines 85-86 waives only errors that are "impossible to provoke from a buildable, functioning code base (e.g. DalEnvVarNotSet)". REQ-DAL-2.4 has no waiver, and the DataAccessLayer.md Waived table does not cover these branches. A commit failure can likely be provoked without corrupting the environment, for example by having a second connection terminate the backend mid-transaction. Whether the test role has permission to do that would need checking.

**Action:** Either add integrated tests that make commit (and, if feasible, begin and rollback) throw, and assert the typed error plus zero idle-in-transaction sessions plus an unchanged pool count. Or have Dan record that these sub-paths of REQ-DAL-2.4 cannot be provoked, in a note or an amendment to the REQ-DAL-2.4 test-coverage stance.

**Why:** REQ-DAL-2.4 is the one test-required DAL requirement, and its own text says "every path". The untested branches are the ones that hold hand-written disposal logic (the explicit connection.Dispose() at line 53, and the finally block at lines 76-78). That is where a regression would quietly leak pooled connections again, which is the failure the REQ was written (2026-09-26) to prevent.

---

## MAINT-DAL-1 — maintainability
- **Location:** Src/App.DataAccessLayer/DbTransaction.fs:22-25, 27-28, 33-35; Src/App.DataAccessLayer/ExecuteScalar.fs:174-177; Src/App.DataAccessLayer/DalError.fs:8, 45
- **Summary:** Some DAL code has no reachable path, so no test can ever cover it: the ManualTransactionResult type, the internal isSome, ReaderFailedToConvertRawRows, the DalCantFetchTransactionOfNone branch, and the failwith fallback in executeScalar.
- **Resolution:** fix-code

Evidence:
- ManualTransactionResult<'T> (Failed / Success / TransactionCreateFail) is declared at DbTransaction.fs:22-25. A grep of Src, Tests, DevDataStage and DbMigration finds no use outside that declaration.
- `let internal isSome` (DbTransaction.fs:27-28) has no caller. App.DataAccessLayer's fsproj has no InternalsVisibleTo.
- DalCantFetchTransactionOfNone (DalError.fs:8) is produced only by transactionAndConnection (DbTransaction.fs:35). Its only callers are ExecuteNonQuery.fs:33, ExecuteReader.fs:183 and ExecuteScalar.fs:175, and each one reaches it only in the `false` arm of `match dbTransaction |> isNone`. So the None case can never get there.
- ExecuteScalar.fs:177 (`Result.defaultWith(fun e -> failwith(toMessage e))`) is unreachable for the same reason.
- ReaderFailedToConvertRawRows (DalError.fs:45) is never constructed. The grep across Src and Tests finds it only in its declaration and its ToMessage arm. executeReaderQuery passes constructFromRaw errors through unchanged.

The DalTests Theory leaves DalCantFetchTransactionOfNone out, consistent with it being unreachable.

**Action:** Remove ManualTransactionResult, isSome and ReaderFailedToConvertRawRows. Either drop DalCantFetchTransactionOfNone and the executeScalar failwith fallback, or restructure the three Execute* functions so they rely on transactionAndConnection's Result alone instead of checking isNone first.

**Why:** In a code-inward coverage audit, code that cannot be reached is permanently uncovered and makes coverage harder to read. An agent developer who sees an error case such as ReaderFailedToConvertRawRows will assume the DAL produces it and may write handling or tests against a path that does not exist.

---

