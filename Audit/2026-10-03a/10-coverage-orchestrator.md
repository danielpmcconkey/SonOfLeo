# code-outward-coverage-CrossDomainOrchestration

## CDO-COV-1 — missing-requirement
- **Location:** Src/Business.CrossDomainOrchestration/CashFlowCompositeFetcher.fs:50-154 (createPredicateAndParameters), FetchFilterAndSort.fs:97-117 (AgreementFilter), :184-238 (createAmountPredicateAndParameters, createTemporalPredicateAndParameters); InstanceOrchestration.fs:407-419 (fetchFiltered)
- **Summary:** 15 of the 19 AgreementFilter predicates in the cash flow composite fetcher are not described by any REQ, are not reachable from any route, and have no test. The Invoice-target fetch (InstanceOrchestration.fetchFiltered) is called only from tests.
- **Resolution:** dan-decides

In production, fetchCompositeFiltered is reached only through AgreementOrchestration.fetchByMasterAgreementId (sets agreementIds only) and fetchAllActiveAgreements (sets activeAgreementsOnly only). FetchAgreementSummary turns the name into an ID before it calls fetchByMasterAgreementId. Tests add agreementNames and invoiceBlocker (CashFlowMaintenance.fs:170,199, under REQ-SYS-1.4). That leaves 15 predicates with no REQ, no caller and no test: direction, accountIds (debit OR credit), paymentAgreementExpectedAmount, instanceTemporalFilter, externalInvoiceId, invoiceDateTemporalFilter, invoiceDueTemporalFilter, invoiceAmount, invoiceState, invoicePaymentState, invoicePostedState, journalEntryLineId, stageEntryLineId, paymentAmount and paymentPostedToLedgerTemporalFilter. A grep of Tests/ for '<field> = Some' finds none of them used on an AgreementFilter. createAmountPredicateAndParameters (exact or inclusive range) and createTemporalPredicateAndParameters are used only by these predicates. CashFlow.md has no filtered-retrieval requirement: REQ-CF-14.3 asks only for one agreement fetched by name. Compare REQ-STG-10.x, REQ-AC-3.12.1 and REQ-CR-5.3, which each specify their filter sets. InstanceOrchestration.fetchFiltered, the only caller of the TargetComposite.Invoice branch and invoicesSelectAndJoinInsideDistinct, has no Src caller. Only CashFlowMaintenance.fs:200,330 calls it.

**Action:** Dan decides one of two paths. Either write a REQ-CF filtered-retrieval section listing the supported criteria and add a citing test per predicate, or delete the unused predicates and InstanceOrchestration.fetchFiltered.

**Why:** These are dozens of lines of SQL predicate construction: amount ranges, temporal resolution through fiscal periods, and OR-joins across debit and credit accounts. They will run against real data as soon as a route uses them, and no spec or test defines what correct output is. Under a fully agentic workflow, an unspecced and untested query surface is where wrong answers can ship unnoticed.

---

## CDO-COV-2 — missing-requirement
- **Location:** Src/Business.CrossDomainOrchestration/AgreementOrchestration.fs:44-70, 360-371, 389, 393-399 (updateAgreement payment-agreement branch); Src/Ui.InterfaceBridge/Routes/CashFlowRoutes.fs:78
- **Summary:** updateAgreement can update Payment Agreements (name, debit/credit account, expected amount, days-due, memo), but no REQ describes that, no route reaches it, and its cohesion check that a leg belongs to the agreement being updated has no test.
- **Resolution:** dan-decides

The only production caller, the UpdateAgreement route, always passes an empty Payment Agreement update list (CashFlowRoutes.fs:78: `AgreementOrchestration.updateAgreement context []`). No other Src code builds PaymentAgreementFieldUpdates. REQ-CF-14.2 covers only Master Agreement fields (name, flow direction, cadence, counterparty, start/end date, memo), and no REQ-CF describes updating a Payment Agreement. Even so, the orchestrator has a full update path: isThereAPaymentAgreementUpdate, confirmAuthorityAndCohesion (which returns CashflowPaymentAgreementNotUnderMasterAgreement when a leg ID belongs to a different agreement), and PaymentAgreement.update. One test reaches it, and only by calling the orchestrator directly: AgreementUpdate.fs:45 'REQ-CF-3.6 updating a leg's credit account to its debit account is rejected'. No test references CashflowPaymentAgreementNotUnderMasterAgreement (0 hits in Tests/), so the cohesion rejection is untested. Updating a leg's debit or credit account would also change which account REQ-CF-6.9 checks existing Payments against and which lines REQ-CF-12.4 leg selection keeps. No spec covers that.

**Action:** Dan decides one of two paths. Either spec Payment Agreement updates in CashFlow §14, with a route and tests that include the cross-agreement leg rejection, or remove the Payment Agreement branch from updateAgreement.

**Why:** This is a latent write path to accounts, the field that drives payment validation and linkage. It sits one route-wiring change away from production, with no requirement and no tested guard.

---

## CDO-COV-3 — missing-requirement
- **Location:** Src/Business.CrossDomainOrchestration/InstanceOrchestration.fs:480-520, 579-607, 677-759 (updateInstanceComposite: instanceDateUpdate, paymentUpdates); Src/Ui.InterfaceBridge/BoundaryConverters/CashFlowFieldConverters.fs:420-505
- **Summary:** updateInstanceComposite can change an Instance's date and a Payment's memo, posted-to-FI date and staged-line pointer, but no REQ describes these updates, no route reaches them, no test exercises them, and the date update skips the REQ-CF-4.6 and 4.7 checks that Instance creation applies.
- **Resolution:** dan-decides

Every converter that builds an InstanceCompositeUpdate sets instanceUpdates through noChangeInstanceUpdates (CashFlowFieldConverters.fs:420-423) and paymentUpdates to []. The only Src code that fills paymentUpdates is CashFlowOps.transitionOneInstancesPaymentsToPosted, which sets journalEntryLineIdUpdate and nothing else. Even so, the orchestrator accepts instanceDateUpdate (writes through Instance.update at lines 715-717) and payment memo, postedToFiDate and stageEntryLineId updates (lines 579-591, 727-732). REQ-CF-14.5/14.6 list the supported Instance-composite edits: add or update an Invoice, add or delete a Payment. Neither changing an Instance date nor editing Payment fields is among them. A grep of Tests/ for instanceDateUpdate or paymentUpdates set to anything other than NoChange or [] returns nothing. createInstanceCompositeAndSaveToDb enforces REQ-CF-4.6 (Cadence.confirmDateFitsCadenceType, line 803) and REQ-CF-4.7 (confirmInstanceDateIsAfterLatestInstance, line 804). updateInstanceComposite runs neither on a changed date, so an Instance could be moved off-cadence or before an earlier Instance. The 'Payment belongs to another Invoice' branch at lines 604-605 is also untested: CashFlowMaintenance.fs:268 covers only the nonexistent-ID branch.

**Action:** Dan decides one of two paths. Either remove instanceDateUpdate and the unused payment-field updates from the orchestrator contract, or spec them in CashFlow §14 with the REQ-CF-4.6/4.7 checks applied to a changed date, and add tests.

**Why:** The orchestrator's comment calls this function 'the single door for editing an Instance'. Part of that door has no spec, no test, and invariant checks that are weaker than on creation. The first route that uses it would ship those gaps.

---

## CDO-COV-4 — test-gap
- **Location:** Tests/Tests.Integrated/CrossDomainOrchestration/InstanceDataStates.fs:393-441 (three REQ-CF-4.9 tests); Src/Business.CrossDomainOrchestration/InstanceOrchestration.fs:512-520, 682-684, 708-710
- **Summary:** The three REQ-CF-4.9 tests pass because updateInstanceComposite rejects them as no-op updates (CashflowInstanceCompositeUpdateNoOp), not because of any is-fulfilled rule. The no-op path itself is never asserted by error type.
- **Resolution:** fix-test

Each test builds instanceUpdate (all NoChange, no invoice updates, no new invoices) and sets only isFulfilledUpdate = SetTo true/false. isThereACompositeUpdate (lines 512-520) ignores isFulfilledUpdate, so the call fails at line 684 with CashflowInstanceCompositeUpdateNoOp before any 4.9 logic runs. Line 710 also overwrites the caller's isFulfilledUpdate with the derived value, so a caller-supplied flag is discarded silently. Each test asserts only `attempt |> Result.isError` plus the unchanged flag, so the wrong error goes unnoticed. confirmFulfilledInstanceHasInvoices and confirmFulfilledInstanceInvoicesAreFullyPaid (lines 316-339) cannot fail by construction once isFulfilled is derived. The no-op rejection that UpdateInvoice reaches when its payload changes no field has no test of its own: CashflowInstanceCompositeUpdateNoOp appears 0 times in Tests/. REQ-SYS-6.1's waiver says 'Testing should be enforced by every individual write operation with a no-op possibility'.

**Action:** Rewrite the REQ-CF-4.9 tests to assert on the error type and to reach the derivation through a real change (for example adding a Payment). Add a REQ-SYS-6.1 test showing that an UpdateInvoice payload naming no field fails with CashflowInstanceCompositeUpdateNoOp. Consider removing isFulfilledUpdate from InstanceFieldUpdates, since REQ-CF-9.11 says no contract carries it.

**Why:** Three tests named for REQ-CF-4.9 exercise a different code path. Deleting every 4.9-related line would leave them green, so the traceability matrix shows 4.9 as covered when it is not.

---

## CDO-COV-5 — test-gap
- **Location:** Src/Business.CrossDomainOrchestration/StageEntryOrchestration.fs:71-83 (confirmLinesAreAllPositive), :120, :670; REQ-STG-1.6, REQ-STG-2.12
- **Summary:** No test covers the only check that rejects a staged line amount of zero or less, on ingest or on manual update.
- **Resolution:** fix-test

REQ-STG-1.6 says amount must be 'positive' and REQ-STG-2.12 says 'greater than zero'. Money.fromDecimal accepts values down to minMoney = -9999999999.99 (Money.fs:11, 29), and ingestion.staged_entry_line.amount is `numeric(12,2) NOT NULL` with no CHECK (202609071115-CreateIngestionTables.sql:62). confirmLinesAreAllPositive (error IngestionStageLineNonPositiveAmount) is therefore the only enforcement, and it runs for ingest (constructGroup → createStageEntry) and manual update (updateStageEntry line 670). IngestionStageLineNonPositiveAmount appears 0 times in Tests/. The ingest validation theory (IngestionRoutes.fs:275-289) covers excess precision and over-max for amount, but not zero or negative. StageEntryUpdate.fs only ever updates amounts to positive values.

**Action:** Add tests citing REQ-STG-1.6/REQ-STG-2.12. One should cover an ingest file with a 0.00 and a -1.00 amount record (rejected, nothing staged). Another should cover an UpdateStageEntry setting a line amount to 0.00 or a negative value (rejected with IngestionStageLineNonPositiveAmount, entry unchanged).

**Why:** Deleting the only guard on sign-convention integrity would leave every test green. A negative staged amount would post with its direction flipped.

---

## CDO-COV-6 — test-gap
- **Location:** Src/Business.CrossDomainOrchestration/AccountCreation.fs:70-79, 110 (confirmTypeAndSubtypeAreValid); REQ-AC-1.29, 1.31, 1.32, 1.34, 1.36
- **Summary:** Account type/subtype combinations are enforced only when an account is created through orchestration, and no test exercises that rejection. Only the boolean predicate has tests.
- **Resolution:** fix-test

AccountSubtype.validTypeSubtypeCombination is called from exactly one place in Src: AccountCreation.fs:71. ledger.account.account_subtype has no CHECK constraint (202609071105-CreateLedgerTables.sql:9). The ~45 REQ-AC-1.28–1.36 tests in Tests.Isolated/Model/Ledger/AccountComponent.fs assert the predicate's true/false result directly. No integrated or route test creates an account with an invalid combination (for example Equity/Cash). AccountInvalidTypeSubtypeCombo appears 0 times in Tests/. Deleting line 110 would leave every test green while letting invalid combinations persist.

**Action:** Add an integrated test citing REQ-AC-1.29–1.36: CreateAccount with a disallowed type/subtype pair (for example Equity + Cash, Asset + CurrentLiability) fails with AccountInvalidTypeSubtypeCombo and stores no account.

**Why:** The tested predicate is not the enforcement point. Coverage that stops at the helper does not prove the system rejects the input.

---

## CDO-COV-7 — test-gap
- **Location:** Src/Business.CrossDomainOrchestration/StageEntryOrchestration.fs:921-929 (fetchFiltered journalEntryHeaderId / journalEntryLineId predicates); REQ-STG-10.2
- **Summary:** No test covers the staged-entry fetch filters by journal entry ID and journal entry line ID, though REQ-STG-10.2 lists both. Every other REQ-STG-10.2 criterion has a dedicated test.
- **Resolution:** fix-test

StageEntryFetching.fs has one REQ-STG-10.2 test per criterion: id, source file, date range, fiscal period, description, ingestion source, fi reference, status, line id, amount, line type, account and memo (lines 142-448). Nothing tests journalEntryHeaderId or journalEntryLineId. A grep of Tests/ for 'journalEntryHeaderId = Some' or 'journalEntryLineId = Some' returns nothing. The contract carries both fields (IngestionContracts.fs:143-144). The spec's rationale says these filters were added 2026-09-26, replacing the classification-rule-ID filter, to trace a posted ledger entry back to the staged data it came from. A commented-out test for the old classificationRuleId filter remains at StageEntryFetching.fs:462-482.

**Action:** Add two REQ-STG-10.2 tests. Post a staged entry, then fetch by its journal entry ID and by one of its journal entry line IDs. Each should return exactly that staged entry with all its lines (REQ-STG-10.3), and nothing for an unposted entry.

**Why:** The ledger-to-staging trace back is the stated purpose of these filters and of REQ-STG-9.10 provenance, and it is the path the reconciliation and ML-fidelity story depends on. Right now a wrong column name in either predicate would go unnoticed.

---

## CDO-COV-8 — missing-requirement
- **Location:** Src/Business.CrossDomainOrchestration/CashFlowOps.fs:329-342 (createPaymentForInvoice), :489-494; REQ-CF-13.4, REQ-CF-6.6
- **Summary:** Payments created by invoice matching get their posted-to-FI date set to the staged entry's entry date. No REQ states this and no test checks it.
- **Resolution:** dan-decides

createPaymentForInvoice builds `CashFlowComponent.Staged lineId, amount, Some { localDate = entryDate }, None, None`, so matching sets postedToFiDate to the staged entry's entry date. REQ-CF-13.4 says only that 'a Payment is created against it with a Staged pointer to that line'. REQ-CF-6.6 says posted-to-FI date 'represent[s] when the financial institution processed the payment' and may be null. Nothing says the system derives it, or from which date. The only posted-to-FI tests (PaymentDataStates.fs:446, REQ-CF-6.6) cover a caller-supplied date on CreatePayment. No LinkageAndMatching or InvoiceMatching test asserts it, so the value could change to None or another date without any test failing.

**Action:** Add a clause to REQ-CF-13.4 (or a new REQ) stating the posted-to-FI date of a matching-created Payment, then add a test asserting it. If the date should stay null, change the code instead.

**Why:** Dan's vision says the ledger must capture data at the fidelity the retirement-planning engine will need. The date the FI processed a payment is exactly that kind of field. Matching is how most Payments will be created, and right now it writes that field by unspecced, unverified convention.

---

## CDO-COV-9 — contradiction
- **Location:** Src/Business.CrossDomainOrchestration/JournalEntryOrchestration.fs:195-238, 278-298 (fetchFiltered source and unVoidedOnly branches); REQ-JE-3.1.1
- **Summary:** The public JournalEntryOrchestration.fetchFiltered offers a whole-entry non-voided filter and a source filter. REQ-JE-3.1.1 says whole-entry reads offer no non-voided filter, and no caller or test uses either branch.
- **Resolution:** dan-decides

REQ-JE-3.1.1: 'Journal entry reads return voided entries alongside active ones; the void marker distinguishes them. Only the line-level reads (REQ-JE-3.4, REQ-JE-3.9.1) offer a non-voided filter.' Yet JournalEntryFetchFilter has unVoidedOnly (adds 'and je.voided_at is null', line 207) and source (line 224-227), and fetchFiltered is public. Every Src caller sets source = None and unVoidedOnly = false: fetchById, fetchByPeriod, fetchByDateRange, fetchByReference (lines 300-377). No route or test calls fetchFiltered directly (grep finds none), so both branches have no REQ and no test. The source filter on whole entries also has no REQ: REQ-JE-3.x has no retrieve-by-source requirement.

**Action:** Remove the source and unVoidedOnly fields from JournalEntryFetchFilter, or make fetchFiltered private. If Dan wants either capability, amend REQ-JE-3.1.1 and add tested REQs.

**Why:** Code contradicts a spec rule. The next agent to wire up the public function would ship a read that silently drops voided entries, and REQ-JE-3.1.1 exists to rule that out.

---

## CDO-COV-10 — statement-delta
- **Location:** Src/Business.CrossDomainOrchestration/StageEntryOrchestration.fs:803-835 (post); CashFlowOps.fs:886-936 (transitionPaymentsToPosted); Src/Ui.InterfaceBridge/Routes/CashFlowRoutes.fs:29-32, 158-162
- **Summary:** Dan's statement says a Payment 'gains its journal entry line linkage when the staged entry eventually posts'. Posting does not do this. The link is written only when the operator separately runs TransitionPaymentsToPosted.
- **Resolution:** dan-decides

StageEntryOrchestration.post writes journal entries, records journal_entry_line_id on each staged line, and sets status Posted. It never touches cashflow.payment. CashFlowOps.transitionPaymentsToPosted, the only code that sets Payment.journalEntryLineId from a posted staged line, is called only by the Cashflow TransitionPaymentsToPosted route (grep finds no other Src caller, and the PostStageEntries route does not call it). Until that second verb runs, Payments on posted lines stay Staged. Invoices keep posted state NotHandled or PartiallyPosted, and Payments derive no posted-to-ledger date (REQ-CF-6.10). The spec agrees with the code: REQ-CF-10 describes a separate transition operation. So the gap is between Dan's mental model and the repo, not between spec and code.

**Action:** Dan confirms that the two-step model (post, then TransitionPaymentsToPosted) is intended for the planned Saturday state machine. If posting should carry the link forward itself, amend REQ-CF-10 and have post invoke the transition.

**Why:** Dan relies on this audit as his back-stop. A mental model that assumes posting closes the cash-flow loop could lead to a Saturday sequence that skips the transition and leaves invoices showing NotHandled after the cash posted.

---

