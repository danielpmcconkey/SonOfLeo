# ai-maintainability

## AIM-1 — enforcement-gap
- **Location:** HobsonsNotes/plan-saturday-readiness-2026-09-26.md §9 (commit f07f667); Specs/Behavioral/SystemWide.md line 48 (REQ-SYS-6.1.1) and Waived table (commit 366b75d); Checks/check-traceability.sh
- **Summary:** The slice's final report says the traceability audit's Invariants 1 and 2 are clean, but the commit it reported on fails Invariant 2 (REQ-SYS-6.1.1). The slice merged to main with traceability failing, and nothing mechanical caught the false report.
- **Resolution:** dan-decides

Commit 366b75d ("stale waivers on tested requirements removed") deleted the waiver row `| REQ-SYS-6.1.1 | simply untestable | Dan, 2026-07-06 |` from SystemWide.md. No test has ever cited REQ-SYS-6.1.1: `git grep SYS-6.1.1 366b75d^ -- Tests` and the same grep at HEAD both return nothing. The row was not a stale waiver, and removing it left an active requirement with no test, no waiver and no Unenforceable entry. Three hours later f07f667 (Plan §9 final report, written by an agent) says, under "Test status": "`Checks/run-all.sh` passes 9 of 9" and "The traceability audit shows Invariants 1 and 2 clean." Running Skills/SonOfLeoRequirementsAudit/traceability-audit.sh against the tree at f07f667 prints `REQ-SYS-6.1.1 (1 of 666 active requirements)` and exits 1. The same failure is on main now, and it is the only FAIL in Checks/run-all.sh. The report was written on the `cash-flow` branch, where check-traceability.sh exits 0 without running (see AIM-2), so "passes" was true of the runner and false of the invariant. Under README step 12, Dan merges on the strength of that final report.

**Action:** Dan decides where REQ-SYS-6.1.1 belongs; it reads as a spec-authoring policy, so most likely the Unenforceable table. Then require every final report to paste the raw output of traceability-audit.sh (which runs on any branch) rather than an agent's paraphrase of it.

**Why:** This audit is Dan's main backstop, and the agent-written final report is what he merges on. Here an agent's spec-table edit broke the invariant, and the agent's own verification summary then said it was intact. In a fully agentic loop, a self-attested "clean" that nobody re-runs is exactly how an untested requirement, or later an untested ledger rule, reaches main without anyone noticing.

---

## AIM-2 — contradiction
- **Location:** Checks/check-traceability.sh lines 7-10; Checks/run-all.sh lines 3-4; Specs/README.md lines 92-102 (Commit gate); README.md steps 8, 12, 13; Skills/SonOfLeoSrcDeveloper/SKILL.md lines 624-625
- **Summary:** The commit gate in Specs/README says a new REQ ships in the same commit as a citing test and that check-traceability enforces this through the pre-commit hook. The hook is retired, the check exits 0 (shown as PASS) on every branch except main, and the README's slice loop says the invariant cannot hold mid-slice.
- **Resolution:** fix-spec

Specs/README.md:96-102 says: "A new REQ ships in the same commit as a citing test... Enforced by `Checks/check-traceability.sh`... the pre-commit hook (`Checks/run-all.sh --quick`) skips it, and it only enforces on `main`." The pre-commit hook was retired in 4008d58. README.md's slice loop has Hobson commit the spec at step 1 and the test agent commit placeholders only at step 8, and says the invariant "*cannot* hold mid-slice: the spec lands before the tests exist." That contradicts the same-commit gate. The SonOfLeoSrcDeveloper skill (lines 624-625) still tells implementing agents that the pre-commit hook "runs it automatically", and run-all.sh:4 still says `--quick` is "used by the pre-commit hook". Off main, check-traceability.sh does `exit 0`, not the runner's skip code `exit 2`, so run-all.sh prints `PASS  check-traceability` for a check that never ran. That is how AIM-1's "9 of 9" was true. In practice, nothing enforces traceability between Hobson's spec commit and Dan's merge, and the runner tells agents it passed.

**Action:** Make check-traceability.sh exit 2 (SKIP, with a one-line reason) off main. Rewrite the Specs/README commit-gate paragraph to describe the current gate (traceability checked on main after merge, step 13). Remove the pre-commit hook text from run-all.sh and Skills/SonOfLeoSrcDeveloper/SKILL.md.

**Why:** Agents read forward and trust what they read (Specs/README's own corollary: "Warnings before the step"). Two documents state opposite gates, the skill promises a safety net that no longer exists, and the runner shows PASS for a no-op. An agent cannot work out the real rule from the repo, and a green run-all.sh misreports what was checked.

---

## AIM-3 — test-gap
- **Location:** Tests/README.md line 102; Tests/Tests.Integrated/CrossDomainOrchestration/{InvoiceDataStates,PaymentDataStates,MasterAgreementDataStates,InstanceDataStates,PaymentAgreementDataStates,StagingIngestionRules,MaintenanceOperations}.fs
- **Summary:** 70 test assertions use `Assert.True(attempt |> Result.isError)`, which Tests/README forbids. 60 of them sit under test names that say the payload is "rejected with a typed error". Most are in the agent-written CashFlow data-state suites, and no check catches the pattern.
- **Resolution:** fix-test

Tests/README.md:98-102 requires a sad path to match the typed DU case with both escape arms and says "Never `Result.isError`." Skills/TestWriter/references/bullshit-test-specimens.md:91-96 lists the pattern as worthless because "`isError` passes for *any* failure." Counts by file (isError uses / uses under a name saying "typed error"): InvoiceDataStates 24/24, PaymentDataStates 11/11, MasterAgreementDataStates 10/10, InstanceDataStates 8/8, StagingIngestionRules 5/4, MaintenanceOperations 4/0, PaymentAgreementDataStates 3/2, plus 5 elsewhere. One example is MaintenanceOperations.fs:570, ``REQ-CF-14.5 REQ-CF-9.3 a CreatePayment payload ... that would make a blocked Invoice FullyPaid is rejected``. It asserts only `attempt |> Result.isError` and that nothing was written. If an agent deleted the 9.3 blocker check and the payload were rejected for any other reason (pointer resolution, CF-6.9 account mismatch, a fixture slip), the test would stay green. That matches what a grep for error cases shows: only 28 of 82 CashFlowError cases are named anywhere in Tests, and the unnamed ones include CashflowInvoicePostedToLedgerRequiresFullyPaid, CashflowInvoiceFullyPaidAmountMismatch, CashflowInvoicePartiallyPaidWithNoPayments and CashflowInstanceFulfilledWithUnpaidInvoice, the Invoice lifecycle invariants of REQ-CF-9.x. Constraint 3 ("seen to fail") does not catch this: perturbing an `isError` assertion still shows it failing on success.

**Action:** Add Checks/check-result-iserror.sh, which fails on `Result.isError` anywhere under Tests/, and have the test agent rewrite the 70 sites to match the specific CashFlowError or DataIngestionError case.

**Why:** This is the textbook failure-amplification path. A small wrong edit to an Invoice lifecycle guard passes build and all ~1600 tests because the assertions accept any rejection. The test standard already names the defect, but it is prose with no check, and the agents who wrote these suites did not follow it.

---

## AIM-4 — enforcement-gap
- **Location:** DbMigration/Migrator/Program.fs lines 46-64, 136-146; DbMigration/Scripts/2026090711{00,05,10,15,20,25,30,35}-*.sql (commit 8695bf3); Tests/Tests.Integrated/setup-throwaway-test-db.sh
- **Summary:** The lockdown of runtime-role privileges was written by editing eight already-registered migration scripts in place. The migrator tracks scripts only by file path, with no content hash, so any environment that already recorded those scripts will never get the change. The tests cannot see the gap because the test database is rebuilt from raw scripts every time.
- **Resolution:** dan-decides

The migrator registers each file by `up_file` path (registerFile) and treats a migration as applied when `migration.history` has its dml_id (fetchAppliedIds). Nothing compares file contents with what was applied. Commit 8695bf3 (2026-10-03) changed `OWNER to sonofleo_{ENV}` to `OWNER to sonofleo_migrator` and replaced `GRANT ALL ... TO sonofleo_{ENV}` with SELECT/INSERT/UPDATE/DELETE inside 202609071100 through 202609071135. Those scripts were added on 2026-09-27 (a1bc950). The same commit did add a new migration, 202610031500-TestRoleTruncate, for the truncate grant, so the agent knew the new-file pattern but applied it to only one of the two changes. In any dev or prod database where the 09-07 scripts were recorded before 10-03, the runtime role still owns the tables and still has ALL. That contradicts Dan's statement that the standard application users are no longer allowed create or alter. setup-throwaway-test-db.sh pipes every script through psql as superuser on a fresh database, bypassing the migrator, so the suite always sees the edited version. No rule in CompoundedLearnings, Src/README or the skills forbids editing an applied migration; the only migration rule is process.md's "migration review is always Dan's/Hobson's job".

**Action:** Dan decides whether dev and prod already recorded the 09-07 scripts. If so, add a new migration that re-states ownership and grants. Separately, store a content hash in migration.dml and have the migrator refuse to run when a registered file's hash has changed.

**Why:** Privilege separation is one of the few guardrails that stops a misbehaving agent's code or SQL from altering schema in the real books. When the guardrail exists only in a test database rebuilt from scratch, the protection seen in testing is not the protection in production.

---

## AIM-5 — architecture
- **Location:** Src/Business.FinancialServices.DataIngestion/DataIngestionError.fs lines 12-35, 84; Src/Business.FinancialServices.Classification/ClassificationComponent.fs line 8, ClassificationRule.fs line 12; Architecture/SonOfLeo.archimate principles 'Domains Build Upward', 'This application is structured in domain tiers...', 'Each domain defines its own errors'; Tests/Tests.Isolated/Model/DataIngestion/
- **Summary:** Classification has no error type of its own: it raises cases from DataIngestionError (IngestionClassificationRule* and others). DataIngestion sits below both cash flow and classification, yet its error file names their concepts (LinkedToPaymentAgreement, ReferencedByPayment, RecordedInClassificationRun). The architecture model's own principles forbid both.
- **Resolution:** dan-decides

The model orders the concepts "account < fiscal period < journal entry < data ingestion < cash flow < classification" and states "we *do* strictly enforce that no lower tier has any understanding of higher tier modules, types, or even labels." It also says "Each domain defines its own errors." DataIngestionError.fs defines StageLineProtection = LinkedToPaymentAgreement | ReferencedByPayment | RecordedInClassificationRun, with messages naming payment agreements, Payments and classification runs. It also defines IngestionClassificationRuleGroupsEmpty, ...RuleIdDoesntExist, ...RuleInvalidClaimant (which carries a payment-agreement Guid), ...RuleNameIsEmpty/TooLong, ...RuleUpdateNoOp, IngestionFieldMatchChainEmpty, IngestionInvalidClassificationClaimantType, IngestionInvalidClassificationGroupConnector, IngestionInvalidNumericSearchOperator and ...RuleStoredPatternInvalid. Classification raises these by opening `Business.FinancialServices.DataIngestion.DataIngestionError`. The Business.FinancialServices.Classification project has no ClassificationError.fs, while every other business container has one. Classification's isolated tests also still live in Tests/Tests.Isolated/Model/DataIngestion/ (ClassificationRuleComponent, Classifier, FieldMatch*, ...). This contradicts Dan's statement that classification now stands as its own domain and that the monolithic error was broken into per-domain implementations. No check enforces label direction, because the compiler only sees project references, and those point the right way.

**Action:** Hobson decides whether this goes into a plan as a work item: create ClassificationError in the Classification project, move the Classification* and FieldMatch* cases into it, and move the classification tests to Model/Classification. Dan decides whether StageLineProtection's higher-concept labels are an approved exception under 'Out-of-Order Is a Design Smell'.

**Why:** Agents copy the nearest example. When the newest domain puts its errors in a lower domain's DU and its tests in a lower domain's folder, the next agent working on classification or cash flow will do the same, and the boundary that the fsproj split was built to make hard to cross erodes without any build failure.

---

## AIM-6 — stale-reference
- **Location:** Checks/check-apperror-coverage.sh line 9; Checks/check-testingerror.sh lines 3, 8
- **Summary:** Two checks still point at Src/Utilities/AppError.fs, which no longer exists. The error-coverage tracker reports 0/0 and passes, which hides that 156 of 266 error cases across the ten error DUs are never named in Tests.
- **Resolution:** fix-code

check-apperror-coverage.sh reads cases from `Src/Utilities/AppError.fs` with awk. The file is gone (errors now live in ten per-domain DUs that implement App.Utility.IAppError), so the check prints "AppError coverage: 0/0" and exits 0. check-tomessage-wildcard.sh fails when it finds nothing ("check is stale"); this check has no such guard. Re-running its intent across the current DUs gives the number of cases named anywhere in Tests: CashFlowError 28/82, DalError 30/39, DataIngestionError 37/48, LedgerError 59/69, UtilityError 1/7, BridgeError 0/1, BizGeneralError 10/12, BizFinServError 4/6, OperatorCliError 1/1, ReportCliError 1/1. check-testingerror.sh allowlists the same missing path; TestingError now lives in Tests/Tests.Helpers/TestError.fs, so that check is harmless, but its stated allowlist is fiction.

**Action:** Point check-apperror-coverage.sh at every file that implements IAppError (the same discovery check-tomessage-wildcard.sh uses), have it fail when it finds no cases, and fix the allowlist comment and path in check-testingerror.sh.

**Why:** A check that silently passes after its target moves is worse than no check: run-all.sh reports green and agents and Dan read that as coverage. The real number, about a third of CashFlow error cases named in any test, is the coverage signal that would have flagged AIM-3.

---

## AIM-7 — contradiction
- **Location:** CompoundedLearnings/articles/process/release-candidate-merge-flow.md; CompoundedLearnings/catalogs/process.md line 14; README.md 'Who does what' and slice loop
- **Summary:** The process learning on merge flow, still listed in the process catalog, tells agents to use one narrowly scoped branch per task and says Dan reviews each diff in Rider and runs the tests. The README's agentic loop says several agents share one branch, rebase before each commit, and Dan does not review code.
- **Resolution:** fix-spec

release-candidate-merge-flow.md says: "One task, one branch, one hand-off... A second worthwhile finding becomes its own branch, never a second commit on this one", "Dan reviews the narrow diff in Rider against `main`", "Dan runs the tests before merging to `main`", and that BD's container and Dan's host both point at `Host=172.18.0.1 / sonofleo_test`, so simultaneous integrated runs corrupt each other. README.md says "Several can work on one branch at once, so each pulls with `--rebase` before every commit", Dan "reviews it only when there is a strong reason to", and step 12 merges when tests and checks pass and the final report is in. Cloud agents use a throwaway local database (setup-throwaway-test-db.sh). The catalog row tells agents to read this article whenever they are "about to branch for a new task" or "found a second thing worth doing mid-task", which is exactly when an agent will act on the outdated rule. process.md:14 also still assigns the green-checks obligation to "BD".

**Action:** Rewrite release-candidate-merge-flow.md and the process.md standing rule to match the README's agentic loop (shared slice branch, rebase discipline, throwaway DB), or retire the article to history.

**Why:** CompoundedLearnings ranks above code in the authority order, and agents load these articles by catalog trigger. A fresh agent given only the repo gets two incompatible branching procedures and one of them tells it a human reviews its diff, which is the wrong assumption for an agent deciding how careful to be.

---

