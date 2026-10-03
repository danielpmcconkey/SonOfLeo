# code-truthfulness-auditor (CashFlow / Classification / DataIngestion / Ledger / FinServ / General)

## SYS-3.3-CF-1 — contradiction
- **Location:** Src/Business.FinancialServices.CashFlow/MasterAgreement.fs:290-306 (update, updateCadence); PaymentAgreement.fs:266-325; Instance.fs:183-216; Invoice.fs:318-387; Payment.fs:359-408; REQ-SYS-3.3
- **Summary:** Five cash-flow update functions never write modified_at, so every successful update to a Master Agreement, Payment Agreement, Instance, Invoice or Payment leaves the timestamp stale.
- **Resolution:** fix-code

REQ-SYS-3.3: "Every successful update to a record must set its 'modified at' timestamp to the initiation instant of the operation performing the update." The SET clauses in MasterAgreement.update, PaymentAgreement.update, Instance.update, Invoice.update and Payment.update come only from the FieldUpdate list. None of them adds `modified_at = @modified`. Compare PaymentAgreementLink.update:209-213, Account.fs:252-254, FiscalPeriod.fs:151-153 and JournalEntryVoiding.voidById, which all add `modified_at = @modified` from Context.getInitiationInstant. The cashflow tables have no triggers (DbMigration/Scripts/202609071125-CreateCashFlowTables), so nothing else moves the column. These are live paths: AgreementOrchestration.fs:391/396 (REQ-CF-14.2 update), InstanceOrchestration.fs:717/726/730 (REQ-CF-14.5 invoice/payment updates and REQ-CF-9.10 re-derivation of payment_state, posted_state and is_fulfilled), InstanceOrchestration.fs:831 (REQ-CF-4.8 next-instance advance on every Instance creation) and CashFlowOps.transitionPaymentsToPosted (REQ-CF-10.3). Tests do not catch it. The REQ-SYS-3.3 Theory in OperationInstantAndAtomicity.fs:103 covers comments, voids and link re-points only. The cash-flow tests that look at modifiedAt (RevisedRequirementsCashFlow.fs:328, PaymentsToPosted.fs:246) assert it is UNCHANGED after a no-op, which passes trivially because the column never changes.

**Action:** Add `modified_at = @modified` (from Context.getInitiationInstant) to the SET clause of all five update functions. Then add cash-flow cases to the REQ-SYS-3.3 Theory: agreement update, invoice update, payment transition and next-instance advance.

**Why:** Dan relies on modified_at as the audit signal for what each Saturday operation touched. Today every derived-state recompute, cadence advance and posting transition is invisible in it. The existing tests pass for the wrong reason.

---

## CF-VOID-WINDOW-1 — missing-requirement
- **Location:** Src/Business.CrossDomainOrchestration/JournalEntryVoiding.fs:93-108 (confirmNoPaymentReferencesEntry); CashFlowOps.fs:886-934 (transitionPaymentsToPosted); Src/Business.FinancialServices.CashFlow/Payment.fs:243-255; REQ-JE-4.14, REQ-CF-10.2/10.3
- **Summary:** A journal entry can be voided while a still-Staged Payment covers it. The next payment-to-posted run then re-points that Payment at the voided entry's line, which is the exact state REQ-JE-4.14 exists to prevent.
- **Resolution:** dan-decides

Sequence: (1) matching creates a Payment with a Staged pointer to staged line L. (2) Batch post records on L the journal entry line J it produced (REQ-STG-9.10). Posting does not touch Payments. (3) The operator voids the JE. confirmNoPaymentReferencesEntry looks only at Payment.fetchByJournalEntryLineIdList, i.e. `pmt.journal_entry_line_id in (...)`. The Payment's journal_entry_line_id is still null, so the void goes through. (4) CashFlowOps.transitionPaymentsToPosted reads L.journalEntryLineId = J and sets the Payment's journal_entry_line_id = J. Nothing checks voided_at. Payment.fetchAny (Payment.fs:245-246) then derives amount and posted_to_ledger_date from the voided line, and Invoice posted state can become PostedToLedger. REQ-JE-4.14's Why reads: "Voiding it would leave the Invoice reading 'FullyPaid' and 'PostedToLedger' on cash the ledger no longer records." The letter of REQ-JE-4.14 ("transaction pointer references a line") does not cover a Staged pointer whose staged line has posted, and REQ-CF-10.3 has no voided-entry exclusion. The REQ-JE-4.14 test (Tests.Integrated/CrossDomainOrchestration/JournalEntryVoiding.fs:276) always runs transitionPaymentsToPosted before voiding (line 119), so this window is never exercised.

**Action:** Dan decides which guard closes it. Option A: widen REQ-JE-4.14 to also refuse a void when a Payment's staged line records a line of the entry. Option B: make REQ-CF-10.3 skip or reject a staged line whose JE line belongs to a voided entry. Add a test that posts, voids without transitioning, then transitions.

**Why:** This is the one sequence where the Saturday routine can leave the cash-flow books asserting the ledger holds cash that was voided. It slips past both the void guard and the transition because each assumes the other ran first.

---

## STMT-CF-POSTLINK-1 — statement-delta
- **Location:** Dan's statement (cash flow paragraph); CashFlow.md §10; Src/Ui.InterfaceBridge/Routes/CashFlowRoutes.fs:29-32,162; CashFlowOps.fs:886
- **Summary:** Dan says a Payment 'gains its journal entry line linkage when the staged entry eventually posts'. In the code that linkage happens only when a separate payment-to-posted command is run.
- **Resolution:** dan-decides

Batch post (StageEntryOrchestration) records JE line IDs on staged lines (REQ-STG-9.10) but never touches cashflow.payment. The only caller of CashFlowOps.transitionPaymentsToPosted is the CashFlowRoutes handler at CashFlowRoutes.fs:29-32, a distinct operator command matching CashFlow.md §10 ("runs after staged entries have been posted ... Phase 7"). Until that command runs, the Payment stays Staged: amount comes from the staged line, posted_to_ledger_date is null, the Invoice stays NotHandled/PartiallyPosted, and the JE-void guard does not see the Payment (see CF-VOID-WINDOW-1).

**Action:** Dan to confirm the mental model: either posting should also run the transition in the same operation, or the statement should read 'when the payment-to-posted transition runs after posting'.

**Why:** Dan's model implies there is no window between posting and Payment linkage. There is one, and the void-guard gap depends on it.

---

## ARCH-CLS-ERR-1 — architecture
- **Location:** Src/Business.FinancialServices.DataIngestion/DataIngestionError.fs:12-21,27-45; Src/Business.FinancialServices.Classification/* (no error module); Architecture/SonOfLeo.archimate principles 'This application is structured in domain tiers...' and 'Foundations Are the Base of Their Container'
- **Summary:** Classification has no error vocabulary of its own. Its errors, and labels for cash flow and classification concepts, live in DataIngestionError, a lower tier. That contradicts Dan's statement and the tier principle that no lower tier knows higher-tier labels.
- **Resolution:** fix-code

Dan's statement says he broke the monolithic error into domain-specific implementations so that 'no lower tier should have any insight into an upper tier's domain'. The archimate principle reads: "we do strictly enforce that no lower tier has any understanding of higher tier modules, types, or even labels". The domain order is data ingestion < cash flow < classification. Yet DataIngestionError (compiled in Business.FinancialServices.DataIngestion) declares IngestionClassificationRuleGroupsEmpty, IngestionClassificationRuleIdDoesntExist, IngestionClassificationRuleInvalidClaimant (whose message names 'payment agreement'), IngestionClassificationRuleNameIsEmpty/TooLong, IngestionClassificationRuleUpdateNoOp, IngestionFieldMatchChainEmpty, IngestionInvalidClassificationClaimantType, IngestionInvalidClassificationGroupConnector, IngestionInvalidNumericSearchOperator, IngestionSearchPattern*, IngestionClassificationRuleStoredPatternInvalid and IngestionClassificationRulePatternTimedOut. Classification modules raise these: ClassificationComponent.fs (8 uses), ClassificationRule.fs, Classifier.fs. DataIngestionError.StageLineProtection (lines 12-21) also names LinkedToPaymentAgreement, ReferencedByPayment and RecordedInClassificationRun, which are cash flow and classification concepts. Classification has ClassificationAuditableAction but no ClassificationError, so the 'Foundations' principle ("Every container has ... its error vocabulary and its auditable actions") is half met. The archimate 'Error management functions' list also has no 'Define classification errors' entry, consistent with the gap.

**Action:** Create Business.FinancialServices.Classification/ClassificationError.fs implementing IAppError, compiled first in that project. Move the Ingestion*Classification*/SearchPattern/NumericSearchOperator/ClassificationGroupConnector/ClaimantType cases into it. Have Dan rule on whether StageLineProtection's cash-flow/classification labels are an approved exception or should be reported by an upper tier.

**Why:** The error split was meant to enforce downward-only knowledge. Leaving classification's vocabulary in DataIngestion means an agent that follows the existing pattern will keep adding upper-tier labels to a lower tier, which is exactly what the refactor was meant to stop.

---

## ARCH-FOUNDATION-ORDER-1 — architecture
- **Location:** Src/Business.FinancialServices.{Ledger,DataIngestion,CashFlow,Classification}/*.fsproj (AuditableAction.fs listed last); Architecture/SonOfLeo.archimate 'Foundations Are the Base of Their Container'; Src/README.md; Checks/check-compile-order.sh
- **Summary:** In all four FinancialServices projects the AuditableAction file compiles last, but the archimate principle says auditable actions are a container foundation that compiles first. The check that README says guards order only checks membership.
- **Resolution:** fix-code

Principle text: "Every container has foundations that sit below everything else in it: Its error vocabulary and its auditable actions (e.g. LedgerError and LedgerAuditableAction in Ledger ...). Nothing else in the container is below them. ... Foundations compile first in their container". The .fsproj Compile Include order is: Ledger: LedgerError ... JournalEntryComment, LedgerAuditableAction (last). DataIngestion: DataIngestionError ... StageEntryLine, DataIngestionAuditableAction (last). CashFlow: CashFlowError ... PaymentAgreementLink, CashFlowAuditableAction (last). Classification: ClassificationComponent ... Classifier, ClassificationAuditableAction (last). The errors are correctly first. Src/README.md says "Checks/check-compile-order.sh guards the hand-maintained <Compile Include> order", but the script header says "This verifies MEMBERSHIP both ways ... Compile ORDER itself is verified by dotnet build". dotnet build cannot detect a foundation placed too late, because nothing depends on it.

**Action:** Move each *AuditableAction.fs entry to directly after the container's error file in its .fsproj. Correct the Src/README.md sentence to say the check verifies membership, or extend the check to enforce foundation-first order.

**Why:** Dan's explicit structural decision is not reflected in any of the four projects, and the README tells agents a guard exists that does not.

---

## STALE-CF-COMMENTS-1 — stale-reference
- **Location:** Src/Business.FinancialServices.CashFlow/Payment.fs:145-151,182-185; PaymentAgreementLink.fs:222-225; CompoundedLearnings/articles/architecture/orchestration-layer.md ('A read-only join across domains' example)
- **Summary:** Several comments and one learning describe a superseded Payment workflow and query: manual payment creation, a flow-direction/account-pinned amount join, and 'the only hard delete in Src/'.
- **Resolution:** fix-code

(a) Payment.fs:145-151 says the operator 'will add a new payment record ... with the link to stage' and 'will close the loop by updating the payment record'. Per CashFlow.md §12-§13 and §10, matching creates Payments automatically and transitionPaymentsToPosted sets the JE line. (b) Payment.fs:182-185 error text: 'no matching journal_entry_line/staged_entry_line was found for this payment's flow direction and account'. The query (Payment.fs:243-254) now joins purely on the line-level pointer columns. There is no flow-direction or account filtering, consistent with REQ-CF-6.5's 2026-09-26 revision. (c) PaymentAgreementLink.fs:222-225: 'This is the only hard delete in Src/'. Payment.delete (Payment.fs:410) and StageEntryLine.delete (StageEntryLine.fs:318) also hard-delete. The same comment says 'The classification diagnostic that produced it survives and is where the trail lives', which is false for operator-created links (REQ-CF-12.7). Those have no classification run, so deleting one leaves no trail. (d) orchestration-layer.md says Payment.fs's `fetchGenericRead` joins 'pinned by payment_agreement's debit/credit account and master_agreement's flow direction so exactly one line matches'. No such function or pinning exists (Payment.fs uses fetchAny with a line-id join). The learning outranks code in the authority order, so an agent reading it would reintroduce the old join.

**Action:** Rewrite the Payment.fs comment and the CashflowInvalidPaymentAmountRow text to describe the line-level pointer. Fix the PaymentAgreementLink.delete comment. Update the orchestration-layer.md example to the current line-level join, or replace it.

**Why:** Dan does not read the code. Agents treat comments and learnings as ground truth, so stale descriptions of the Payment lifecycle feed directly into future wrong implementations.

---

## SYS-2.1-READPATH-1 — contradiction
- **Location:** Src/Business.General/Cadence.fs:322-357 (reconstitute); Src/Business.FinancialServices.Ledger/Account.fs:66-110; Src/Business.FinancialServices.CashFlow/PaymentAgreement.fs:137-172; Invoice.fs:185-232; REQ-SYS-2.1, REQ-CF-2.25, REQ-AC-1.29..1.36, REQ-CF-3.6, REQ-CF-3.7, REQ-CF-5.6
- **Summary:** Several reconstitute functions skip cross-field data-state rules that REQ-SYS-2.1 says must be enforced on read-from-persistence as well as on create.
- **Resolution:** dan-decides
- **Prior ruling:** No resolved-findings entry covers this. The REQ-CR-1.21 waiver says 'read-path validation is not performed by design (see Journaling slice precedent)', but that waiver applies only to the money-search-pattern amount in classification rules, not to these entities, so it is raised here.

REQ-SYS-2.1: every operation that '...reconstitutes an entity — create, update, and read-from-persistence alike — must enforce that entity's legal data-state rules'. Cadence.reconstitute builds the record directly and never calls confirmNextInstance, which Cadence.create does at line 281. A stored next_instance that does not fit the cadence (REQ-CF-2.25) reads back as valid. Stray columns are also silently ignored for Daily/Weekly/EveryOtherWeek: a Weekly row with cadence_date_in_month set is accepted. MonthDayFromColumns only rejects inconsistency for Monthly/Annually. Account.reconstitute validates each field but never applies AccountSubtype.validTypeSubtypeCombination, so REQ-AC-1.29..1.36 are unchecked on read; only AccountCreation.fs:71 checks them. PaymentAgreement.reconstitute does not reject debit = credit (REQ-CF-3.6) or a non-positive expected_amount (REQ-CF-3.7). Invoice.reconstitute does not reject a non-positive amount (REQ-CF-5.6). Those checks exist only in AgreementOrchestration.fs:122/133 and InstanceOrchestration.confirmInvoiceAmountIsPositive. The schema has no CHECK constraints for any of these (CreateCashFlowTables migration).

**Action:** Dan to rule. Either run the existing cross-field validators (Cadence.confirmNextInstance, validTypeSubtypeCombination, debit<>credit, amount>0) inside each reconstitute, or record a read-path exception (as the REQ-CR-1.21 waiver does for money-pattern amounts) in SystemWide.md.

**Why:** REQ-SYS-2.1's read-path clause is either a real rule or not. The code currently applies it to single-field types only, and nothing documents the line.

---

## IDIOM-AP-INSIST-1 — idiom
- **Location:** Src/Business.General/ActivityPeriod.fs:65-75 (insistBeginValidationBehavior); called from Account.fs:52-54 and MasterAgreement.fs:63-66 inside entity create
- **Summary:** Entity create functions go through ActivityPeriod.insistBeginValidationBehavior, which re-runs validation and calls failwith on error. That puts a throwing branch inside constructors that the archimate principle declares infallible.
- **Resolution:** fix-code

Archimate principle 'Infallible Create, Orchestrator Validates': "Create (construct) functions in Entity-level type modules never fail ... No exceptions in constructors." Account.create and MasterAgreement.create both call insistBeginValidationBehavior. It rebuilds the period via ActivityPeriod.create and then calls `Result.defaultWith(fun e -> failwith(e.ToMessage()))`. Its own comment admits the failure is impossible, because only the behaviour flag changes. The function lives in the module that owns the private record, so `{ ap with beginValidationBehavior = b }` gives the same result without a fallible path or an exception.

**Action:** Replace the body of insistBeginValidationBehavior with a record copy-and-update of beginValidationBehavior, removing the create/failwith round trip.

**Why:** FP principle: a total function should be total by construction, not total because a branch is believed unreachable. Using failwith to make a Result infallible hides partiality, and agents copy that pattern into the next constructor.

---

## MAINT-DEAD-CASES-1 — maintainability
- **Location:** CashFlowError.fs:43,53,89; BizFinServError.fs:6; LedgerError.fs:209-210; CashFlowComponent.fs:153-159 (Blocker.toString); AccountComponent.fs:113 (AccountSubtype.validFor); Checks/check-apperror-coverage.sh
- **Summary:** Several in-scope error cases and helpers are never referenced anywhere in Src, and one of them (Blocker.toString) would print the private wrapper if anything called it. The check meant to surface this reads a deleted file.
- **Resolution:** fix-code

No reference outside their own declaring file (grep -w across Src): CashflowInvoiceCompositeUpdateNoOp, CashflowInvoiceNotUnderMasterAgreement, CashflowPaymentNotUnderMasterAgreement (CashFlowError); FromDecimalListFailedConversion (BizFinServError); JournalEntryExternalReferenceIsEmpty/TooLong (LedgerError; JournalExternalReferenceText uses JournalEntryReferenceTextIsEmpty/TooLong instead); Blocker.toString; AccountSubtype.validFor. Blocker.toString interpolates `{note}`, where note is the private single-case union BlockerNote. F#'s generated ToString would render 'NeedsDecision: BlockerNote "..."' rather than the note text. check-apperror-coverage.sh reads Src/Utilities/AppError.fs, which no longer exists, and reports a vacuous 0/0 PASS. Nothing in Checks/ can detect unreferenced error cases under the per-domain error split.

**Action:** Delete the unreferenced cases and helpers, or fix Blocker.toString to use BlockerNote.value if it is meant to be kept. Repoint check-apperror-coverage.sh at the per-domain *Error.fs files.

**Why:** Agents treat the error vocabulary as the menu of failures the system can produce. Dead cases suggest checks that do not exist, such as diamond checks on Payments, and the broken coverage check gives false comfort.

---

