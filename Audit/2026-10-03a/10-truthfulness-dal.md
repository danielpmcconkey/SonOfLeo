# code-truthfulness-auditor:App.DataAccessLayer

## ARCH-DAL-1 — statement-delta
- **Location:** Src/App.DataAccessLayer/LookupCache.fs:10, 83-91; Architecture/SonOfLeo.archimate principle id-ac5c0f586fba42409935f772e5c9a768 (line ~1634); Src/README.md infrastructure table (LookupCache row)
- **Summary:** LookupCache sits in the lowest tier (App.DataAccessLayer) but hardcodes Ledger and CashFlow table and column names. That breaks the rule that no lower tier knows an upper tier's labels, which Dan's statement says the refactor enforced.
- **Resolution:** dan-decides

Dan's statement says the refactor's goal was "that no lower tier should have any insight into an upper tier's domain". The archimate principle "This application is structured in domain tiers..." says: "we *do* strictly enforce that no lower tier has any understanding of higher tier modules, types, or even labels." LookupCache.fs lines 83-91 hardcode business-domain table and column names inside the App tier: `ledger.account`/`code`/`account_name`, `ledger.fiscal_period`/`period_key`, `cashflow.master_agreement`/`agreement_name` and `cashflow.payment_agreement`/`payment_agreement_name`. The file admits this at line 10: `// todo: make this an interface so that lower tier doesn't need to have higher tier awareness`. Git history (`git log -S masterAgreementNameToId`) shows the two CashFlow caches were added in a1bc950 (2026-09-27), during the cash-flow slice. So the violation grew during the refactor that was meant to remove it. Consumers: Business.FinancialServices.DataIngestion/StageEntryLine.fs:79, CrossDomainOrchestration AgreementOrchestration.fs:39, StageEntryOrchestration.fs:102, ClassificationOrchestration.fs:31 (the last two use accountIdToCode only to check that an account exists), and the Ui.InterfaceBridge BoundaryConverters. I found no Dan-approved exception for LookupCache in the archimate model; its element at line ~1122 carries no documentation. The "Out-of-Order Is a Design Smell" principle says such cases must be reported to Dan and never quietly accommodated. A related drift: the Src/README.md inventory lists LookupCache as "Account code ↔ ID and fiscal period key ↔ ID" only and omits the master- and payment-agreement caches. An agent that consults the inventory will not know those exist.

**Action:** Dan decides one of two things. Either record an explicit, scoped exception for LookupCache in the archimate model, or have the caches moved out of App.DataAccessLayer: keep the generic Cache type in the DAL and define the per-table instances in each owning domain. In either case, update the Src/README.md LookupCache row to list all nine caches.

**Why:** Dan's statement presents tier isolation as achieved. In the lowest tier, a self-acknowledged todo shows otherwise, and the slice under audit widened it. The archimate model and Dan's statement both say such reach-ups must go to Dan rather than be accommodated. In a fully agentic workflow, an unflagged exception becomes a precedent for the next agent.

---

## STALE-DAL-1 — stale-reference
- **Location:** CompoundedLearnings/articles/architecture/dal-errors-are-backstops.md ("What works" code block, lines ~50-56); Skills/SonOfLeoSrcDeveloper/SKILL.md lines ~531-539; Src/App.DataAccessLayer/ExecuteReader.fs:20-28; Src/App.DataAccessLayer/DalError.fs:97-102
- **Summary:** The DAL-backstop learning, and the Src developer skill that cites it, teach code to match `DalResultantRowsDidntMatchExpectation` with `actual = 0` to detect "not found". The DAL no longer produces that error for zero rows.
- **Resolution:** fix-spec

`confirmNumRows` (ExecuteReader.fs:26-28) now returns `DalNoOp(expectation, 0)` whenever an `ExactlyOne` or `OneOrMany` query returns zero rows. It returns `DalResultantRowsDidntMatchExpectation` only for non-zero mismatches. The current translation mechanism is `DalError.whenNoRows` (DalError.fs:97-102), which swaps `DalNoOp` for a domain error and is used at more than 35 call sites in Src. A Src grep finds no remaining use of the old `actual = 0` pattern. The learning's "What works" block still prescribes `| Error (DalResultantRowsDidntMatchExpectation(expected, actual)) -> if actual = 0 then Error (EntityIdDoesntExist ...)`. SonOfLeoSrcDeveloper/SKILL.md repeats it: "only `actual = 0` becomes a domain 'doesn't exist' error". Neither mentions `DalNoOp` or `whenNoRows`: a grep of CompoundedLearnings and Skills for either name returns nothing. An agent that follows the learning writes an `actual = 0` arm that can never fire, so the raw `DalNoOp` reaches the operator untranslated. That is the exact failure the learning exists to prevent. Both documents also name layers that no longer exist (`Model/`, `ModelOrchestrator/`; Src now has Business.FinancialServices.* and Business.CrossDomainOrchestration).

**Action:** Rewrite the learning's "What works" example and the matching SonOfLeoSrcDeveloper/SKILL.md paragraph so they use `whenNoRows <domain error>` for the zero-row case. State that `DalResultantRowsDidntMatchExpectation` now means only a non-zero mismatch, which is integrity trouble. Replace the Model/ModelOrchestrator layer names with the current project names.

**Why:** Dan no longer writes or reviews code. CompoundedLearnings and the developer skill are the guidance the implementing agent reads, and here they contradict the code. A learning that teaches dead code is worse than none, because the agent follows it confidently.

---

## ENF-DAL-1 — enforcement-gap
- **Location:** Checks/check-apperror-coverage.sh:9-10; Src/App.DataAccessLayer/DalError.fs
- **Summary:** The error-case coverage check still reads the deleted monolithic `Src/Utilities/AppError.fs`. As a result, DalError (and every other per-domain error DU) is untracked, and the check reports a vacuous 0/0 PASS.
- **Resolution:** fix-code

Dan's statement says the monolithic error was split into domain-specific implementations of IAppError. The check's awk still targets `/^type AppError/` in `Src/Utilities/AppError.fs`, which no longer exists, so run-all prints `awk: cannot open` followed by `0/0` and PASS. I ran the check's logic by hand against DalError. These cases have no reference anywhere in Tests: DalCantFetchTransactionOfNone, DalConnectionStringConfigRetrievalError, DalConnectionStringEnvVarContainsConnectionString, DalConnectionStringEnvVarNotFound, DalConnectionStringIsEmpty, DalErrorDuringTransactionCommit, DalErrorDuringTransactionCreation, DalErrorDuringTransactionRollback, ReaderFailedToConvertRawRows. The four connection-string cases match the REQ-DAL-1.14..1.18 waivers, so they are expected to be untested. The others are not covered by any waiver. ReaderFailedToConvertRawRows is also never constructed in Src (see MAINT-DAL-1). The check is report-only (`exit 0`), but it is the only tooling that tracks this coverage goal, and right now it measures nothing.

**Action:** Point check-apperror-coverage.sh at every `type *Error =` DU that implements IAppError across Src (for example DalError.fs, LedgerError.fs, CashFlowError.fs). Make it fail loudly when its input file is missing instead of passing on 0/0.

**Why:** A check that passes on empty input is false assurance. The audit is Dan's main backstop, and a green row in run-all.sh tells him error-case coverage is being tracked when it is not.

---

## MAINT-DAL-1 — maintainability
- **Location:** Src/App.DataAccessLayer/DbTransaction.fs:22-25; Src/App.DataAccessLayer/DalError.fs:45, 91
- **Summary:** The DAL has public items nothing uses: the `ManualTransactionResult` type (Failed / Success / TransactionCreateFail) and the error case `ReaderFailedToConvertRawRows`, which is never constructed.
- **Resolution:** fix-code

A grep of Src and Tests finds no reference to `ManualTransactionResult`, `TransactionCreateFail`, `Failed of DalError * DbTransaction` or `Success of 'T * DbTransaction` outside DbTransaction.fs. `ReaderFailedToConvertRawRows` is declared and has a ToMessage arm, but no code creates it. ExecuteReader.fs:181/194 returns `constructFromRaw` errors directly, and conversion exceptions surface as `DalErrorDuringReaderQueryExecution`. `ManualTransactionResult` describes a manual transaction lifecycle, while every real transaction runs through `runWithAutoCompleteTransaction` or `runCommandRouteAndAutoRollback`, the brackets that REQ-DAL-2.4's tests verify. A public type that suggests another way to drive a transaction is a hint to the next agent that the manual path is supported.

**Action:** Delete `ManualTransactionResult` and the `ReaderFailedToConvertRawRows` case together with its ToMessage arm, or wire `ReaderFailedToConvertRawRows` in if it was meant to wrap `constructFromRaw` failures.

**Why:** In the lowest tier, public types and error cases are taken as API by agent developers. Unused API that implies an unguarded transaction lifecycle works against the REQ-DAL-2.4 guarantee. It also inflates the error-coverage denominator once ENF-DAL-1 is fixed.

---


