# code-outward-coverage-business-tier

## COV-CF-1 — missing-requirement
- **Location:** Src/Business.FinancialServices.CashFlow/Payment.fs:74-107 (applyFieldUpdates), 359-408 (update); Src/Business.FinancialServices.CashFlow/Instance.fs:55-58, 183-216 (instanceDateUpdate branch)
- **Summary:** Payment.update can change a Payment's memo, posted-to-FI date and staged-line pointer, and can clear its journal entry line, and Instance.update can change an Instance's date. No REQ describes these updates, no contract or route reaches them, and no test runs them.
- **Resolution:** dan-decides

Payment.update builds SET clauses for four independent FieldUpdates: journal_entry_line_id, stage_entry_line_id, posted_to_fi_date and memo. Payment.applyFieldUpdates also has an error branch (CashflowInvalidPaymentTransactionPointerRow) for the case where both pointer ids end up None.

The only production caller that builds a PaymentFieldUpdates is CashFlowOps.transitionOneInstancesPaymentsToPosted (CashFlowOps.fs:852-858). It sets journalEntryLineIdUpdate = SetTo(Some ...) and leaves the other three at NoChange. That is the REQ-CF-10.3 path. Every other construction passes paymentUpdates = [] (CashFlowOps.fs:339, 683; CashFlowFieldConverters.fs:477, 495).

In Tests/, grep finds no reference to Payment.update, stageEntryLineIdUpdate or postedToFiDateUpdate, and no paymentUpdates list that is not empty.

REQ-CF-14.5 grants only "add a Payment to an existing Invoice" and updates of Invoice fields. No REQ grants editing a Payment's memo, posted-to-FI date or staged pointer. REQ-CF-9.10 mentions operations that "re-point" a Payment, but no such operation exists outside the posted transition.

Instance.update has the same problem. Its instanceDateUpdate branch is NoChange at every construction site (CashFlowOps.fs:346, 689, 871; CashFlowFieldConverters.fs:422; Tests InstanceDataStates.fs:150). InstanceOrchestration.updateInstanceComposite:513 and :715 still branch on it. If that branch were ever wired up, REQ-CF-4.6 (the date fits the cadence), REQ-CF-4.7 (the date is later than every existing Instance) and REQ-CF-4.8 (the next-instance date advances) are written only for creation, so nothing says whether they apply to a date change.

**Action:** Dan decides one of two options for Payment memo, posted-to-FI date and staged-pointer edits, and for Instance date edits. Either (a) spec them (REQ-CF-14.x), expose them and test them, or (b) remove the unreachable FieldUpdate fields and branches from PaymentFieldUpdates and InstanceFieldUpdates so the update shape matches the specified surface.

**Why:** These write paths have no spec and no test, and they edit data that matching (REQ-CF-13.2) and derived state (REQ-CF-9.8–9.10) depend on. If an agent developer wires one up, nothing defines what it must validate, and the roughly 1600 tests would not notice.

---

## COV-CF-2 — missing-requirement
- **Location:** Src/Business.FinancialServices.CashFlow/PaymentAgreement.fs:266-325 (update); Src/Business.CrossDomainOrchestration/AgreementOrchestration.fs:377-404; Src/Ui.InterfaceBridge/Routes/CashFlowRoutes.fs:78
- **Summary:** PaymentAgreement.update can change a leg's name, debit and credit accounts, expected amount, days-due and memo. No REQ gives the operator a way to update a Payment Agreement, and the only route passes an empty leg-update list.
- **Resolution:** dan-decides

AgreementOrchestration.updateAgreement accepts a PaymentAgreementFieldUpdates list. It calls PaymentAgreement.update for every leg update and then validates the composite.

The only production caller is CashFlowRoutes.updateAgreement, and it calls `AgreementOrchestration.updateAgreement context []` (CashFlowRoutes.fs:78). Leg updates are therefore unreachable from any interface.

REQ-CF-14.2 grants updates to the Master Agreement's name, direction, cadence, counterparty, start date, end date and memo only. No REQ in §14 grants a means to update a Payment Agreement.

Tests still exercise the path directly. AgreementUpdate.fs runs `REQ-CF-3.6 updating a leg's credit account to its debit account is rejected` through updateAgreement with a non-empty leg list, and SweepBehaviour.fs:148 calls PaymentAgreement.update for setup.

Changing a leg's debit or credit account after Payments exist would invalidate REQ-CF-6.9 (the Payment's line must sit on the agreement's credit or debit account) for those existing Payments. Nothing in the spec says whether such a change is allowed, is rejected, or triggers revalidation.

**Action:** Dan decides one of two options. Either (a) add a REQ granting leg updates, saying which fields may change and how existing Invoices, Payments and links are revalidated, and expose it through a route, or (b) remove the leg-update parameter from updateAgreement, leave PaymentAgreement.update unused, and re-scope the REQ-CF-3.6 update-path test.

**Why:** A tested code path that no operator can reach gives false assurance. The REQ-CF-3.6 update test passes against behavior the product does not offer. If the path is ever exposed, it carries account-change semantics nobody has specced.

---

## COV-CF-3 — missing-requirement
- **Location:** Src/Business.FinancialServices.CashFlow/PaymentAgreementLink.fs:185-231 (update, delete); Src/Ui.InterfaceBridge/Routes/ClassificationRoutes.fs:140-158; REQ-CF-12.7, REQ-CF-14.6
- **Summary:** Deleting or re-pointing a Payment Agreement Link succeeds without checks when a Payment already references the linked staged line. No REQ says what should happen to that Payment or whether the change should be refused.
- **Resolution:** dan-decides

PaymentAgreementLink.update and delete check only no-op and not-found. The UpdatePaymentAgreementLink and DeletePaymentAgreementLink routes call them directly, with no orchestration (ClassificationRoutes.fs:146, 157).

REQ-CF-14.6 defines the opposite direction: deleting a Payment also deletes its link. The *Why* note there says leaving the link would recreate the Payment. REQ-CF-12.7 says only that the operator can re-point and delete links. REQ-STG-6.5 says "the link or Payment must be removed first" for line edits, so either may exist without the other.

Nothing covers a link delete or re-point while a Payment references the line. Today a re-point leaves the line linked to agreement Y while its Payment sits on an Invoice of agreement X. A delete leaves a Payment with no link. The next ClassifyPaymentAgreements run then re-links the line, because REQ-CF-12.3 counts unlinked lines as candidates, but REQ-CF-13.2 and 13.7 skip it because a Payment references it.

The REQ-CF-12.7 tests in LinkageAndMatching.fs:776-813 cover re-point and delete only on lines with no Payment.

**Action:** Dan decides one of two options and states it in §12. Either (a) reject a link delete or re-point while a Payment references the line, with a typed error that names the Payment, mirroring REQ-STG-6.5, or (b) define the cascade. Then add tests for the chosen behavior.

**Why:** Links and Payments are the obligation-matching record for the Saturday review. Today an operator action can make a Payment disagree with its link without any error, and nothing in the spec or tests says whether that is allowed.

---

## COV-GEN-1 — test-gap
- **Location:** Src/Business.General/Cadence.fs:243-256 (confirmAnnually), 425-438 (incrementAnnually NthWeekDay and Last branches); REQ-CF-2.10, REQ-CF-2.11, REQ-CF-2.25, REQ-CF-4.8, REQ-CF-7.3
- **Summary:** Annually cadences that use an nth-weekday or Last month day are never exercised by any test, neither for date-fit validation nor for advancing the next-instance date.
- **Resolution:** fix-test

REQ-CF-2.11 requires an Annually cadence to carry a month and a month day specification. REQ-CF-2.10 defines a month day as DateInMonth, NthWeekDay or Last. Cadence.incrementAnnually has three branches: DateInMonth (PlusYears 1), NthWeekDay (LocalDate.FromYearMonthWeekAndDay in the following year) and Last (GetDaysInMonth of the new year, the leap-February case).

Every Annually cadence anywhere in Tests/ uses DateInMonth:
- Isolated MasterAgreementDataStates.fs:178 and :203: Annually(March, DateInMonth 1)
- Integrated InstanceDataStates.fs:178, SweepBehaviour.fs:59, LinkageAndMatching.fs:268, MasterAgreementDataStates.fs:427-447: all DateInMonth

A grep for an Annually cadence built with Cadence.Last or Cadence.NthWeekDay returns nothing. The Monthly Last leap-year test (MasterAgreementDataStates.fs ~190) covers confirmLastDayOfMonth for Monthly only.

So confirmAnnually with a non-DateInMonth month day, and the NthWeekDay and Last increments (for example Annually(February, Last) going from 2027-02-28 to 2028-02-29), have no test.

**Action:** Add Annually(…, NthWeekDay …) and Annually(February, Last) cases to the REQ-CF-2.25 theory, and to the REQ-CF-7.3 sweep theory or a REQ-CF-4.8 advancement test. Include a leap-year crossing for Last.

**Why:** Advancing the next-instance date drives sweep idempotence (REQ-CF-4.8, 7.7). A wrong increment on an untested branch would silently mis-date or skip an annual obligation, such as an insurance premium due the last day of February.

---

## COV-DEAD-1 — maintainability
- **Location:** Src/Business.FinancialServices.Classification/RuleMatch.fs:105 (fetchById), :133 (fetchByRunIdAndClaimantType); Src/Business.FinancialServices.CashFlow/PaymentAgreement.fs:227 (fetchByName); Src/Business.FinancialServices.CashFlow/CashFlowComponent.fs:153-159 (Blocker.toString); Src/Business.General/Cadence.fs:118 (Month.toAbbreviation); Src/Business.FinancialServices.CashFlow/PaymentAgreementLink.fs:135, :144; CashFlowError.fs, LedgerError.fs, BizFinServError.fs (error cases never raised)
- **Summary:** Several public business-tier functions have no caller in Src and no test, and several error cases are declared but never raised.
- **Resolution:** fix-code

A grep of Src, Tests, DevDataStage and DbMigration, checking both qualified and unqualified (via `open`) references, gives the following.

No caller anywhere:
- RuleMatch.fetchById
- RuleMatch.fetchByRunIdAndClaimantType
- PaymentAgreement.fetchByName
- CashFlowComponent.Blocker.toString
- Cadence.Month.toAbbreviation

Called only from Tests, never from Src:
- PaymentAgreementLink.fetchByPaymentAgreementId (LinkageAndMatching.fs)
- PaymentAgreementLink.fetchByPaymentAgreementIdList (InvoiceMatching.fs:499)

Declared and given messages but never constructed in Src:
- CashflowInvoiceCompositeUpdateNoOp
- CashflowInvoiceNotUnderMasterAgreement
- CashflowPaymentNotUnderMasterAgreement
- JournalEntryExternalReferenceIsEmpty and JournalEntryExternalReferenceTooLong (REQ-JE-1.44/1.45 are actually enforced through JournalEntryReferenceTextIsEmpty/TooLong in JournalEntryComponent.fs:57-59)
- FromDecimalListFailedConversion

One more detail: Blocker.toString interpolates the private BlockerNote union directly (`$"NeedsDecision: {note}"`), so if anything ever called it, it would print the union's structural form instead of the note text.

**Action:** Delete the uncalled functions and the error cases that are never raised. Alternatively, if any is meant to back a REQ (for example a run-by-claimant-type retrieval for REQ-CF-12.8), wire it to its operation and test it.

**Why:** With fully agentic development, unused public surface looks like supported API. The next agent may reuse a function that has no spec and no test, or match on an error case that can never occur. Error cases that are never raised also make the error vocabulary disagree with what the system actually reports.

---

## SD-CLS-1 — statement-delta
- **Location:** Src/Business.FinancialServices.DataIngestion/DataIngestionError.fs:11-60; Src/Business.FinancialServices.Classification/{ClassificationComponent,ClassificationRule,Classifier}.fs (open DataIngestionError); Architecture/SonOfLeo.archimate (no 'Define classification errors' function)
- **Summary:** Dan says Classification is now its own domain, that errors were split into domain-specific implementations, and that no lower tier knows an upper tier's domain. In fact Classification has no error type: its errors live in DataIngestionError, a lower tier, which also names CashFlow concepts.
- **Resolution:** dan-decides

Dan's statement says classification "now stands as its own domain". It also says he "broke out the monolithic error and auditable actions into domain-specific implementations ... no lower tier should have any insight into an upper tier's domain."

The Classification project compiles after DataIngestion and CashFlow, and it has no ClassificationError file. Every classification failure is a DataIngestionError case:
- IngestionClassificationRuleGroupsEmpty
- IngestionClassificationRuleIdDoesntExist
- IngestionClassificationRuleInvalidClaimant
- IngestionClassificationRuleName*
- IngestionClassificationRuleUpdateNoOp
- IngestionFieldMatchChainEmpty
- IngestionInvalidClassificationClaimantType
- IngestionInvalidClassificationGroupConnector
- IngestionInvalidNumericSearchOperator
- IngestionSearchPattern*
- IngestionClassificationRuleStoredPatternInvalid
- IngestionClassificationRulePatternTimedOut

ClassificationComponent.fs, ClassificationRule.fs and Classifier.fs all `open Business.FinancialServices.DataIngestion.DataIngestionError`.

DataIngestionError.StageLineProtection (lines 11-21) also names LinkedToPaymentAgreement, ReferencedByPayment and RecordedInClassificationRun. DataIngestion is a lower tier, and these are CashFlow and Classification concepts.

Auditable actions were split correctly: ClassificationAuditableAction.fs exists. The archimate lists 'Define cash flow errors', 'Define data ingestion errors' and others, but has no classification-errors function, which matches the code rather than the statement.

**Action:** Dan decides one of two options. Either (a) add Classification/ClassificationError.fs implementing IAppError, move the classification cases into it, and update the archimate, or (b) record that classification errors intentionally stay in DataIngestionError and amend the stated principle accordingly.

**Why:** The error split exists to keep lower tiers ignorant of upper-tier domains, so that agent developers cannot put components in the wrong place. Here the lower tier owns the upper tier's whole error vocabulary, which is exactly the coupling the refactor was meant to prevent.

---

