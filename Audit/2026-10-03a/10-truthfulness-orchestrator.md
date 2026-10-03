# code-truthfulness-auditor:Business.CrossDomainOrchestration

## CDO-RPT-1 — contradiction
- **Location:** Src/Business.CrossDomainOrchestration/PrePostingReview.fs:52-82 (ruleNameForLine); REQ-RPT-7.3, REQ-CR-1.6; Tests/Tests.Integrated/CrossDomainOrchestration/PrePostingReview.fs:272-288
- **Summary:** When more than one rule in the latest run names the line's account, the pre-posting review names the lowest-priority rule (largest priority number), not the rule that gave the line its account.
- **Resolution:** fix-code

ruleNameForLine filters to the most recent run's matches that carry the line's current account and then does `List.sortBy (fun rule -> -(rule |> ClassificationRule.priority), name) |> List.tryHead`. Sorting ascending on the negated priority puts the LARGEST priority value first. REQ-CR-1.6 says lower values are higher priority, and the classifier (REQ-STG-5.5) gives the account to the rule with the lowest value. REQ-RPT-7.3 requires 'the name of the classification rule that gave it its account'. So with rules of priority 10 and 20 that both claim account X, the classifier assigns X through the priority-10 rule, but the review names the priority-20 rule. The code comment ('the highest priority wins') states the right intent; the sort direction contradicts it. The test `REQ-RPT-7.3 a line carries the name of the rule recorded against it ...` encodes the same inversion: it calls the priority-10 rule `lowerPriority`, calls the priority-20 rule `winner`, and asserts the priority-20 name. Test and code agree with each other, but both contradict CR-1.6 and the purpose of RPT-7.3 ('a wrong account is usually a wrong rule ... fixing it once').

**Action:** Sort by ascending priority (lowest value first), then by name, in ruleNameForLine. Fix the REQ-RPT-7.3 test so the lower-numbered rule is the expected winner.

**Why:** The report exists so the operator fixes the right rule. Naming a rule that lost classification sends the operator to edit a rule that did not cause the wrong account, so the actual cause stays in place every Saturday.

---

## CDO-SYS-1 — contradiction
- **Location:** Src/Business.FinancialServices.CashFlow/{MasterAgreement.fs:233, PaymentAgreement.fs:266, Instance.fs:183, Invoice.fs:318, Payment.fs:359} update functions, called from Src/Business.CrossDomainOrchestration/AgreementOrchestration.fs:377 (updateAgreement), InstanceOrchestration.fs:677 (updateInstanceComposite), InstanceOrchestration.fs:831 (MasterAgreement.updateCadence); REQ-SYS-3.3
- **Summary:** None of the cash-flow entity update paths sets modified_at, so updates to Master Agreements, Payment Agreements, Instances, Invoices and Payments never stamp the operation's instant.
- **Resolution:** fix-code

REQ-SYS-3.3: 'Every successful update to a record must set its modified at timestamp to the initiation instant of the operation performing the update.' These are all entities with created_at/modified_at NOT NULL columns (202609071125-CreateCashFlowTables.sql). The UPDATE statements built in MasterAgreement.update, PaymentAgreement.update, Instance.update, Invoice.update and Payment.update contain only the changed columns plus `WHERE unique_id = @unique_id`. There is no `modified_at = @modified` clause and no @modified parameter. Compare PaymentAgreementLink.fs:205-212, AccountDeactivation.updateActiveEnd, JournalEntryVoiding.voidById and ClassificationOrchestration.updateClassificationRule, which all set it. The CDO orchestrators drive these updates constantly: every agreement update, every instance composite update (invoice derived states and is_fulfilled), every sweep-created Instance (updateCadence moves next_instance on the Master Agreement), every match-created Payment (invoice state re-derivation), and every payment-to-posted transition (Payment.update and Invoice.update). The REQ-SYS-3.3 tests (OperationInstantAndAtomicity.fs:98-103, Account.fs:329, JournalEntryComment.fs:190, JournalEntryExternalReference.fs:51) cover only ledger records and payment agreement links. No test covers any cash-flow entity, so the gap is invisible to the 1600-test back-stop.

**Action:** Add `modified_at = @modified` (the context's initiation instant) to the five cash-flow update statements. Add the cash-flow updates (agreement update, invoice update, payment transition, sweep cadence advance) to the REQ-SYS-3.3 theory in OperationInstantAndAtomicity.fs.

**Why:** modified_at is the only per-record audit trail these entities have. With it frozen at creation, nobody can tell when an invoice's derived state, a payment's ledger pointer, or an agreement's cadence last changed. That is the history the Saturday review and the future planning engine will rely on.

---

## CDO-STG-1 — contradiction
- **Location:** Src/Business.CrossDomainOrchestration/StageEntryOrchestration.fs:340-362 (deduplicateStagedEntries); REQ-STG-6.7, REQ-STG-7.2, REQ-STG-7.5.1; Tests/Tests.Integrated/CrossDomainOrchestration/StagingIngestionRules.fs:801
- **Summary:** Dedup skips paid duplicates without reporting them: a paid duplicate whose status is Classified, NoMatch or Conflict disappears from the result, and nothing says it was left unflagged because of a Payment.
- **Resolution:** fix-code

REQ-STG-6.7: an entry with a line referenced by a Payment cannot become Duplicate; the attempt 'is reported without flagging (dedup)'. deduplicateStagedEntries quietly removes paid headers from the flag list and returns `[Ingested] |> fetchByStatusList` (REQ-STG-7.5.1). The code comment claims the paid entry 'stays at its status and so appears in the result below'. That holds only when the paid entry is Ingested. REQ-STG-7.2 lets dedup flag Classified, NoMatch and Conflict entries too (fetchDuplicates excludes only Duplicate/Posted/Ignored/Reviewed). Linkage and matching (REQ-CF-12.3) create Payments on lines of Classified/NoMatch/Conflict entries, so a paid duplicate in one of those statuses is never returned and never reported. Even for an Ingested entry, the result is a plain StageEntry list with nothing that marks the entry as 'not flagged because of the Payment'. The REQ-STG-6.7 dedup test name claims the result 'lists it ... as not flagged because of the Payment', but it only asserts that the Ingested entry is present in the list.

**Action:** Return paid-but-unflagged duplicates explicitly in the dedup result (for example, a separate list with the reason), whatever their status. Extend the REQ-STG-6.7 test to cover a Classified paid duplicate and to assert on the reason.

**Why:** A repeated FI row that already carries a Payment is exactly the case the operator must look at: either the Payment is on the wrong copy or the bill is being paid twice. Leaving it unflagged without telling anyone hides it until it posts.

---

## CDO-JE-1 — enforcement-gap
- **Location:** Src/Business.CrossDomainOrchestration/JournalEntryVoiding.fs:93-108 (confirmNoPaymentReferencesEntry); CashFlowOps.fs:886-936 (transitionPaymentsToPosted); InstanceOrchestration.fs:79-128 (confirmPayment); REQ-JE-4.14, REQ-JE-4.13, REQ-CF-10.2/10.3, REQ-CF-9.6/9.9
- **Summary:** Between batch post and the payment-to-posted transition, the void guard can be bypassed, and the transition then attaches the Payment to a line of the voided entry and can derive PostedToLedger.
- **Resolution:** dan-decides

StageEntryOrchestration.post writes JEs and records journal_entry_line_id on the staged lines (REQ-STG-9.10), but it does not touch Payments. Those still point Staged until CashFlowOps.transitionPaymentsToPosted runs, and that is a separate command (CashFlowRoutes.fs:29; no caller of post invokes it). JournalEntryVoiding.confirmNoPaymentReferencesEntry looks only at Payment.journal_entry_line_id, so during that window a void of the freshly posted JE passes REQ-JE-4.14. By REQ-JE-4.13 the staged line keeps its journal_entry_line_id after the void. transitionPaymentsToPosted then sets the Payment's pointer to that voided line, and nothing checks voided_at: neither the transition nor confirmPayment nor the derived-state code does. The Invoice can then derive PostedToLedger and the Instance can become fulfilled on cash the ledger no longer records. That is exactly the outcome the JournalEntryVoiding doc comment says the guard exists to prevent. Read literally, REQ-JE-4.14 ('any Payment's transaction pointer references a line of the entry') is satisfied, because the pointer is still Staged. So this is a gap in the spec as well as in the code.

**Action:** Dan to decide one of two fixes: (a) extend the void guard to Payments whose staged line records a journal entry line of the entry being voided, or (b) have the payment-to-posted transition refuse or report JE lines whose entry is voided. Then amend REQ-JE-4.14 or §10 to match.

**Why:** Payment state and posted state feed every Saturday decision (REQ-CF-9.10 rationale). A Payment that lands on a voided line makes an obligation read as paid and posted when the ledger says the cash never moved.

---

## CDO-CF-1 — contradiction
- **Location:** Src/Business.CrossDomainOrchestration/InstanceOrchestration.fs:677-759 (updateInstanceComposite), 356-381 (confirmInstanceComposite); Src/Business.FinancialServices.CashFlow/Instance.fs:183; REQ-CF-4.6, REQ-CF-4.7, REQ-SYS-2.1
- **Summary:** updateInstanceComposite accepts an instance-date change and persists it without checking that the date fits the cadence (REQ-CF-4.6) or that it is later than the agreement's other Instances (REQ-CF-4.7).
- **Resolution:** fix-code

InstanceCompositeUpdate.instanceUpdates.instanceDateUpdate is applied in memory, passed through confirmInstanceComposite, and written by Instance.update. confirmInstanceComposite checks cohesion, the diamond relation, one invoice per Payment Agreement, fulfilment and the invoice composites. It never calls Cadence.confirmDateFitsCadenceType or the latest-instance ordering check, which only createInstanceCompositeAndSaveToDb (lines 801-804) performs. REQ-CF-4.6 is a 'valid and invalid data states' rule, and REQ-SYS-2.1 requires every update to enforce them. Today no interface path sets instanceDateUpdate (every bridge and CashFlowOps caller passes NoChange), so this is latent. updateInstanceComposite is still the public 'single door for editing an Instance', and an agent extending the maintenance routes (REQ-CF-14.x) would inherit the hole.

**Action:** When instanceDateUpdate is SetTo, validate the new date against the Master Agreement's cadence (REQ-CF-4.6) and against the agreement's other Instances (REQ-CF-4.7). Alternatively, remove instanceDateUpdate from InstanceFieldUpdates if instance dates are meant to be immutable.

**Why:** The sweep's idempotence depends on Instances sitting on cadence dates in strictly increasing order (REQ-CF-4.7, 4.8, 7.7). An off-cadence or back-dated Instance written through the update door breaks that without any error.

---

## CDO-FILTER-1 — other
- **Location:** Src/Business.CrossDomainOrchestration/FetchFilterAndSort.fs:457-478 (createTemporalPredicateAndParameters); used by CashFlowCompositeFetcher.fs:77-89,119-121
- **Summary:** The temporal predicate references `@<prefix>_end_inclusive`, but the parameter it binds is named `@<prefix>_end`, so any non-None temporal filter on an AgreementFilter fails at the database.
- **Resolution:** fix-code

The predicate text is `({col} >= @{prefix}_begin and {col} <= @{prefix}_end_inclusive)`, but the parameter list binds `@{prefix}_begin` and `@{prefix}_end`. Npgsql resolves `@ins_instance_date_end_inclusive` as a whole identifier, finds no parameter of that name, and leaves it in the SQL. PostgreSQL then reads it as the `@` (absolute value) operator applied to a nonexistent column, and the query errors. This breaks four AgreementFilter fields: instanceTemporalFilter, invoiceDateTemporalFilter, invoiceDueTemporalFilter and paymentPostedToLedgerTemporalFilter. They feed AgreementOrchestration.fetchFiltered and InstanceOrchestration.fetchFiltered, which are public CDO functions. Every in-repo caller (AgreementOrchestration.fs:305-317 and 335-347, and the tests in MasterAgreementDataStates, PaymentAgreementDataStates and CashFlowMaintenance) passes None, and the bridge exposes none of these fields. The code is therefore untested and unreachable from the interface today.

**Action:** Make the predicate placeholder and the bound parameter name identical, and add a test that runs fetchFiltered with a non-None temporal filter.

**Why:** These are general-purpose filter builders in the shared fetch module. The next agent to expose an invoice-due-date or instance-date filter (an obvious Saturday query) will get a database error rather than results, and no current test will tell them why.

---

## CDO-FILTER-2 — other
- **Location:** Src/Business.CrossDomainOrchestration/FetchFilterAndSort.fs:434-442 (createAmountPredicateAndParameters, AmountRange branch)
- **Summary:** The AmountRange filter compares against the ceiling with `>=` instead of `<=`, so a range filter returns every amount at or above the ceiling.
- **Resolution:** fix-code

The AmountRange branch builds `{col} >= @{prefix}_min` and `{col} >= @{prefix}_max`, joined with `and`. That reduces to `col >= ceiling`: values inside the range are excluded and values above it are included. It affects paymentAgreementExpectedAmount, invoiceAmount and paymentAmount on AgreementFilter (CashFlowCompositeFetcher.fs:74-76, 90-92, 116-118). As with CDO-FILTER-1, no in-repo caller or test supplies an AmountFilter (grep for `AmountRange` and `invoiceAmount = Some` in Tests and Src/Ui.InterfaceBridge finds nothing), so the code is latent and unverified.

**Action:** Change the ceiling predicate to `{col} <= @{prefix}_max` and add a test that exercises AmountRange and ExactAmount.

**Why:** This fails silently: the query succeeds and returns plausible-looking but wrong rows, so whoever wires it up later has no signal that the filter is wrong.

---

## CDO-DELTA-1 — statement-delta
- **Location:** Dan's statement ('A Payment is created pointing at its staged line, and gains its journal entry line linkage when the staged entry eventually posts'); Src/Business.CrossDomainOrchestration/StageEntryOrchestration.fs:803-835 (post); CashFlowOps.fs:886 (transitionPaymentsToPosted); Src/Ui.InterfaceBridge/Routes/CashFlowRoutes.fs:29, IngestionRoutes.fs:164
- **Summary:** Posting a staged entry does not give its Payments their journal entry line. That happens only when the separate payment-to-posted transition command runs.
- **Resolution:** dan-decides

StageEntryOrchestration.post constructs the JEs, records journal_entry_header_id and journal_entry_line_id on the staged entry and lines, and moves the status to Posted. It never reads or updates cashflow.payment. The Payment's journal_entry_line_id is set only by CashFlowOps.transitionPaymentsToPosted, which is a separate command route (CashFlowRoutes.fs:29/162) that the post route (IngestionRoutes.fs:164) does not call. This matches CashFlow §10 ('runs after staged entries have been posted', Phase 7). It differs from the mental model that posting itself links the Payment. Until the transition runs, Payments stay Staged and invoice posted states stay stale, and CDO-JE-1 describes the void hazard that window opens.

**Action:** Dan to confirm that the two-step design is intended, and make sure the Saturday state machine always runs the transition immediately after batch post.

**Why:** If the transition step is skipped or reordered in the planned Saturday state machine, Payments and invoice posted states drift from the ledger, and the void guard has a gap during that time.

---

## CDO-DELTA-2 — statement-delta
- **Location:** Dan's statement ('classification ... now stands as its own domain'; 'broke out the monolithic error ... into domain-specific implementations'); Src/Business.FinancialServices.DataIngestion/DataIngestionError.fs:27-45; Src/Business.CrossDomainOrchestration/ClassificationOrchestration.fs:58,73,247,353,356; CashFlowOps.fs:226
- **Summary:** Classification has no error type of its own. Its failures are still DataIngestionError cases named 'Ingestion*', so the Classification domain reports its errors through the DataIngestion domain.
- **Resolution:** dan-decides

Business.FinancialServices.Classification contains no error module (its files are ClassificationAuditableAction, ClassificationComponent, ClassificationRule, ClassificationRuleGroup, Classifier, FieldMatch, FieldMatchChain, RuleMatch). Rule-level errors live in DataIngestionError: IngestionClassificationRuleGroupsEmpty, IngestionClassificationRuleIdDoesntExist, IngestionClassificationRuleUpdateNoOp, IngestionFieldMatchChainEmpty, IngestionInvalidClassificationClaimantType, IngestionClassificationRuleStoredPatternInvalid, IngestionClassificationRulePatternTimedOut and others. CDO raises them for classification operations, including the payment-agreement path, which has nothing to do with ingestion: ClassificationOrchestration.confirmFieldMatchChain, confirmRuleGroups, fetchRunMatchesWithRules and updateClassificationRule, and CashFlowOps.selectLegsOfClaimedEntries line 226. This is allowed by the dependency direction (Classification references DataIngestion), so it is not a tier violation. It does contradict the stated outcome that classification is a standalone domain with its own error implementation.

**Action:** Dan to decide whether to create a ClassificationError implementing IAppError and move the classification cases into it, or to accept the current placement and adjust the mental model.

**Why:** Error ownership is one of the structural cues Dan set up to keep agents placing code correctly. Classification errors living in DataIngestion invites the next agent to put more classification logic there too.

---


