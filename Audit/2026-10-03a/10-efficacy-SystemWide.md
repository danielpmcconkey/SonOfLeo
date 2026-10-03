# test-efficacy-SystemWide

## EFF-SYS-3.3-1 — enforcement-gap
- **Location:** REQ-SYS-3.3; Src/Business.FinancialServices.CashFlow/{MasterAgreement.fs:233-300, PaymentAgreement.fs:266-324, Instance.fs:183-216, Invoice.fs:318-386, Payment.fs:359-407}; Tests/Tests.Integrated/CrossDomainOrchestration/OperationInstantAndAtomicity.fs:98-152; RevisedRequirementsCashFlow.fs:291-296
- **Summary:** Five CashFlow update functions never write modified_at, and none of the REQ-SYS-3.3 tests exercise any of them, so the violation goes unnoticed.
- **Resolution:** fix-code

REQ-SYS-3.3: 'Every successful update to a record must set its "modified at" timestamp to the initiation instant of the operation performing the update.' The UPDATE statements in MasterAgreement.update (MasterAgreement.fs:291 `UPDATE cashflow.master_agreement set {setClauses} WHERE unique_id = @unique_id`), PaymentAgreement.update (:315), Instance.update (:207), Invoice.update (:376) and Payment.update (:397) build setClauses only from the caller's field updates. None of them adds `modified_at = @modified`, and no DB trigger supplies it (the CreateCashFlowTables migration only declares `modified_at ... NOT NULL`). Compare PaymentAgreementLink.update (:209-212), Account (Account.fs:252), FiscalPeriod (:151) and ClassificationOrchestration.updateClassificationRule (:351), which all set it. So the stored modified-at of a master agreement, payment agreement, instance, invoice or payment stays frozen at its creation instant after any update. The REQ-SYS-3.3 theory at OperationInstantAndAtomicity.fs:98-102 covers four updates (comment text, comment secondary, void, link re-point). The other citing tests are Account.fs:329, JournalEntryComment.fs:190 and JournalEntryExternalReference.fs:51. None of these touches a CashFlow entity other than the link. RevisedRequirementsCashFlow.fs:291-296 does update an agreement under an advanced instant (`let updating = s.Context |> Context.updateInitiationInstant`), but it asserts only `fieldsOf after`, never modified-at. Related: no test anywhere asserts the REQ-SYS-3.2 created/modified instants for MasterAgreement, PaymentAgreement, Instance, Invoice or Payment. The creation code does read Context.getInitiationInstant (AgreementOrchestration.fs:215, InstanceOrchestration.fs:608/646/806), so only the coverage is missing there, not the behavior.

**Action:** Add `modified_at = @modified` (from Context.getInitiationInstant) to the five CashFlow update statements. Then add the master-agreement, payment-agreement, instance, invoice and payment updates as rows of the REQ-SYS-3.3 theory in OperationInstantAndAtomicity.fs, asserting modified-at equals the updating instant and created-at is unchanged.

**Why:** Dan relies on this audit as the backstop for agent-written code. This is a REQ violation in five entities that the current suite cannot see, because the cited theory samples only ledger records and the link.

---

## EFF-SYS-6.2-1 — enforcement-gap
- **Location:** REQ-SYS-6.2; Src/Business.FinancialServices.CashFlow/MasterAgreement.fs:298; Src/Ui.InterfaceBridge/BoundaryConverters/CashFlowFieldConverters.fs:560; Src/Ui.InterfaceBridge/Routes/CashFlowRoutes.fs:72-81
- **Summary:** Updating a master agreement by an ID that does not exist fails with a generic DAL row-count error, not a typed not-found error, and no REQ-SYS-6.2 test covers agreement update.
- **Resolution:** fix-code

REQ-SYS-6.2: an update by an ID no record holds 'fails with a typed not-found error naming the kind of record and the ID. It must not surface as a generic database or row-count error.' The UpdateAgreement route takes `agreementId: Guid` from the payload (CashFlowContracts.fs:95). There are two failure paths. (1) MasterAgreement.update runs `executeNonQuery ... ExactlyOne` (MasterAgreement.fs:298) with no `whenNoRows`, so zero affected rows surface as DalNoOp. (2) When the activity-period dates change, the converter first calls `MasterAgreement.fetchById` (CashFlowFieldConverters.fs:560), which is `fetchAny ... ExactlyOne` with no `whenNoRows` (MasterAgreement.fs:216). The typed case CashflowMasterAgreementIdDoesntExist exists, but only fetchByMasterAgreementId (AgreementOrchestration.fs:322) raises it, after the write. PaymentAgreement.update has the same gap: PaymentAgreement.fs:322 has no whenNoRows, and confirmPaymentAgreementBelongsToAgreement calls PaymentAgreement.fetchById without one (AgreementOrchestration.fs:50). Grepping Tests/ for CashflowMasterAgreementIdDoesntExist returns zero hits. The REQ-SYS-6.2 tests cover comment, external reference, classification rule, staged entry and line, invoice, link and payment, but not master or payment agreement update.

**Action:** Wrap the MasterAgreement.update / PaymentAgreement.update non-query and their fetchById pre-reads with whenNoRows(CashflowMasterAgreementIdDoesntExist / CashflowPaymentAgreementIdDoesntExist). Add a REQ-SYS-6.2 test that sends UpdateAgreement for a fresh Guid and matches the typed case, asserting its payload equals that Guid.

**Why:** The REQ forbids exactly this generic row-count error. Without a negative test the leak is invisible, which is the same failure mode Specimen 4 records for the ingestion 'Resultant rows didn't match expectation' leak.

---

## EFF-SYS-6.2-2 — test-gap
- **Location:** REQ-SYS-6.2 / REQ-SYS-6.3; Tests/Tests.Integrated/CrossDomainOrchestration/CashFlowMaintenance.fs:213-231, 249-256; Src/Ui.InterfaceBridge/BoundaryConverters/CashFlowFieldConverters.fs:444, 490; Src/Ui.InterfaceBridge/Routes/ClassificationRoutes.fs:156
- **Summary:** The invoice-update and link-delete REQ-SYS-6.2 tests bypass the interface path, where a missing ID actually surfaces as a generic DalNoOp before the tested code is reached.
- **Resolution:** fix-code

The test at CashFlowMaintenance.fs:213 builds an InstanceCompositeUpdate around a valid instance and a missing invoice, then calls InstanceOrchestration.updateInstanceComposite directly. Through the interface, UpdateInvoice first runs `let! invoice = invoiceId |> Invoice.fetchById context` (CashFlowFieldConverters.fs:444) to find the instance. Invoice.fetchById is `fetchAny ... ExactlyOne` with no whenNoRows (Invoice.fs:286), so a caller updating a missing invoice gets DalNoOp ('Resultant rows was either ExactlyOne or OneOrMany...'), never CashflowInvoiceIdDoesntExist. Likewise, the test at :249 calls PaymentAgreementLink.delete directly, but the DeletePaymentAgreementLink route first calls `PaymentAgreementLink.fetchById` (ClassificationRoutes.fs:156; PaymentAgreementLink.fs:126-133, no whenNoRows), so a caller deleting a missing link also gets DalNoOp. The same converter pattern affects CreatePayment against a missing invoice (CashFlowFieldConverters.fs:490), a REQ-SYS-6.3 missing-referent case with no test. Per Tests/README.md, a caller sending a bad ID is a distinct failure vector whose lowest layer is the route. Each of these tests passes while the REQ fails for every real caller.

**Action:** Add whenNoRows mappings to Invoice.fetchById and PaymentAgreementLink.fetchById, or use them at the converter and route call sites. Then add route-level tests (routeUiCommandForTesting "CashFlow" "UpdateInvoice" / "CreatePayment" and "Classification" "DeletePaymentAgreementLink") that send a fresh Guid and match the typed not-found case and its ID.

**Why:** A test that calls a function the interface never reaches with that input proves the REQ for a path no caller uses. Here the smell test fails outright: the real caller-facing behavior is the forbidden generic error.

---

## EFF-SYS-8.1-1 — enforcement-gap
- **Location:** REQ-SYS-8.1; Src/Ui.InterfaceBridge/Routes/FiscalPeriodRoutes.fs:25-34; Src/Business.CrossDomainOrchestration/FiscalPeriodCreation.fs:61
- **Summary:** FiscalPeriod Ensure makes one insert per missing month with no transaction, and the REQ-SYS-8.1 tests cover only JE PostNew and PostStageEntries.
- **Resolution:** fix-code

REQ-SYS-8.1: 'An operation that makes more than one write performs them in a single database transaction that commits only when the whole operation succeeds.' The Ensure route builds `Context.create NoTransaction FiscalPeriodEnsure` (FiscalPeriodRoutes.fs:26), and ensureFiscalPeriods runs `missingKeys |> List.map (constructNewAndPersist context)` (FiscalPeriodCreation.fs:61), one INSERT per missing month, each auto-committed. A failure partway through leaves the earlier periods in place. REQ-FP-2.7 makes Ensure a multi-month operation that the Saturday run calls weekly. The three REQ-SYS-8.1 tests (OperationInstantAndAtomicity.fs:248, 297, 316) cover only JournalEntry PostNew and Ingestion PostStageEntries. No test or check covers the other interface operations that write more than once.

**Action:** Run the Ensure route under runCommandRouteAndAutoCompleteTransaction. Also decide whether REQ-SYS-8.1 coverage should include a check or test that enumerates every write route and confirms multi-write ones run in a transaction; the current two-operation sample did not catch this one.

**Why:** A half-applied Ensure is exactly the illegal partial state the REQ's rationale names. Sampling two operations leaves the rest unverified.

---

## EFF-SYS-8.1-2 — test-gap
- **Location:** REQ-SYS-8.1; Tests/Tests.Integrated/CrossDomainOrchestration/OperationInstantAndAtomicity.fs:296-311
- **Summary:** The exception-rollback test asserts only `Result.isError` and an empty result set, so it also passes when the write before the raise never happened.
- **Resolution:** fix-test

The body is `result { let! _ = createTestJournalEntryFromPrimitives ...; return raise (InvalidOperationException ...) }`, and the assertions are `Assert.True(outcome |> Result.isError)` (line 310) and `Assert.Empty(written)` (line 311). If createTestJournalEntryFromPrimitives returns Error (bad fixture account, period closed, any validation), the result builder short-circuits. Then nothing is written, the raise never runs, outcome is Error, written is empty, and the test passes without proving rollback after an exception. This is Specimen 4 (`Result.isError` passes for any failure). It also fails the smell test: delete the write and the test still passes. The test never confirms the exception was what it caught. The catch wraps it as `TestError.TestingError ex.Message`, but nothing matches that case or the message.

**Action:** Match the outcome to the specific exception path, e.g. require `Error (AsError (TestingError msg))` with msg = "raised after the write". Also assert that the write was issued before the raise, for example by capturing the created header ID from the inner result and asserting it is absent afterwards via a fresh context.

**Why:** Rollback is invisible from inside the boundary (Specimen 9), so the test's only evidence is that the write happened and then vanished. If it does not prove the write happened, it proves nothing.

---

## EFF-SYS-3.3-2 — test-gap
- **Location:** REQ-SYS-3.3; Tests/Tests.Integrated/CrossDomainOrchestration/OperationInstantAndAtomicity.fs:154-172
- **Summary:** 'a rejected update leaves the record's modified-at unchanged' checks the rejection with `Assert.True(attempt |> Result.isError)` (Specimen 4).
- **Resolution:** fix-test

Line 166: `Assert.True(attempt |> Result.isError)`. The comment says the intended rejection is REQ-JE-1.53 (secondary equals primary), whose typed case is JournalEntryCommentPrimaryAndSecondaryIdsAreSame (JournalEntryCommentOrchestration.fs:~33). If the update failed for any other reason (comment not found, connection error, a wrong validation firing), modified-at would also be unchanged and the test would pass. So the test cannot tell 'a rejected update preserves modified-at' apart from 'the update never ran'.

**Action:** Replace the isError assertion with a typed match: `| Error (AsError (JournalEntryCommentPrimaryAndSecondaryIdsAreSame _)) -> ()`, with the wrong-error and Ok arms failing.

**Why:** Tests/README.md: 'Never Result.isError.' Specimen 4 shows bare isError arms hiding raw DAL errors.

---

## EFF-SYS-1.1-1 — test-gap
- **Location:** REQ-SYS-1.1; Tests/Tests.Integrated/CrossDomainOrchestration/PaymentAgreementDataStates.fs:126-131, 416-427
- **Summary:** The trimmed-duplicate-name test claims 'rejected with a typed error' but asserts `Result.isError` through refusedAndStored, and no typed duplicate-name error exists.
- **Resolution:** fix-test

refusedAndStored returns `(attempt |> Result.isError), stored` (line 130), and the test asserts `Assert.True(refused)` (line 425). No CashFlowError case exists for a duplicate Payment Agreement name. The rejection comes from the DB unique constraint payment_agreement_payment_agreement_name_key (CreateCashFlowTables:50), which REQ-SYS-2.1.2 permits. So the name's 'typed error' claim is false, and the assertion would pass for any failure in the second create (an unrelated leg validation, an account lookup), not only the post-trim collision that shows REQ-SYS-1.1 trimming. This is Specimen 4.

**Action:** Match the actual error the duplicate produces (the DAL constraint-violation case naming the constraint), or rename the test so it does not claim a typed error. Confirm that the second create differs from a known-good create only by the padded name.

**Why:** A test name is a claim (bullshit-test-specimens, Hollow names). This one claims a typed error the code cannot produce, and its assertion cannot tell the trim collision apart from any other failure.

---

## EFF-SYS-1.1-2 — test-gap
- **Location:** REQ-SYS-1.1; Src/Business.FinancialServices.CashFlow/CashFlowComponent.fs:135-280; Src/Business.FinancialServices.DataIngestion/StageEntryComponent.fs:71-80; Tests/Tests.Integrated/CrossDomainOrchestration/PaymentDataStates.fs:498-507
- **Summary:** Each string type has its own copy of the trim logic, but InvoiceMemo, BlockerNote, ExternalInvoiceId, AgreementMemo and SourceFile have no trim test, and PaymentMemo's only test uses Assert.Contains, which passes when nothing is trimmed.
- **Resolution:** fix-test

Trimming is not centralized: each smart constructor calls `raw.Trim()` itself (CashFlowComponent.fs:139 BlockerNote, :216 ExternalInvoiceId, :230 AgreementMemo, :258 InvoiceMemo, :272 PaymentMemo; StageEntryComponent.fs:75 SourceFile). REQ-SYS-1.1 citations cover CommentText, Description, Source, LineMemo, AccountCode, AccountName, AccountType, AccountSubtype, AccountExternalReference, JournalRefFinancialInstitution, JournalExternalReferenceText, the Payment Agreement name and memo, and the agreement name and counterparty. REQ-CR-1.4, REQ-STG-1.17 and REQ-STG-2.26 cover rule name, staging text and source name. No test sends padded input to InvoiceMemo, BlockerNote, ExternalInvoiceId, AgreementMemo or SourceFile and asserts the trimmed value. For PaymentMemo, REQ-CF-6.7 sends "  a memo  " but asserts `Assert.Contains("a memo", stored ...)` (PaymentDataStates.fs:506), which passes for the untrimmed "  a memo  ". Smell test: delete `.Trim()` from any of these six constructors and the suite stays green.

**Action:** Add REQ-SYS-1.1 tests (isolated, at the constructor) for InvoiceMemo, BlockerNote, ExternalInvoiceId, AgreementMemo, PaymentMemo and SourceFile, each asserting exact equality with the trimmed value. Another auditor owns the CF-6.7 assertion itself; it should become Assert.Equal.

**Why:** With the logic copied per type, verifying one type says nothing about the others. Coverage has to be per type, or the trim has to move to one shared function with one test.

---

## EFF-SYS-3.4-1 — test-gap
- **Location:** REQ-SYS-3.4; Tests/Tests.Integrated/CrossDomainOrchestration/OperationInstantAndAtomicity.fs:230-239
- **Summary:** The auditable-action test checks that runCommandRouteAndAutoRollback returns the action it was given, which says nothing about whether each interface operation carries the action that identifies it.
- **Resolution:** fix-test

`let actionOf action = runCommandRouteAndAutoRollback action (fun context -> Ok(context.loggingContext.envelope |> AuditEnvelope.action))`, followed by `Assert.Equal(box JournalEntryPostNew, box posting)`. The test supplies the action and reads it back from the same runner (CommandRoute.fs:35-36: `Context.create NewTransaction auditAction`). It tests that Context.create stores its argument. Each route chooses its action by hand (e.g. JournalEntryRoutes.fs:23 `runCommandRouteAndAutoCompleteTransaction JournalEntryPostNew`). A route passing the wrong action, say Void running under JournalEntryPostNew or UpdateComment's route using `Context.create NoTransaction JournalEntryUpdateComment` for the wrong verb, would not fail this test. The name also says 'the interface's command runner', but it calls runCommandRouteAndAutoRollback, not runCommandRouteAndAutoCompleteTransaction, which the routes use. This is Specimen 6/8: input and expected value come from the same place.

**Action:** Observe the action from outside the runner on the real route path, e.g. a persisted artifact that records the action, or a test hook that captures the envelope from routeUiCommandForTesting for each verb. Assert a domain/verb to action table covering every command route.

**Why:** REQ-SYS-3.4's first clause, 'carries an auditable action identifying what the operation is', is about the route-to-action mapping. The test cannot fail for any mapping error.

---

## EFF-SYS-3.4-2 — enforcement-gap
- **Location:** REQ-SYS-3.4; Src/Ui.InterfaceBridge/Routes/ReportRoutes.fs:45; Src/Ui.InterfaceBridge/ReportVisualizationAssets/ReportFooter.fs:7; Src/Ui.InterfaceBridge/Routes/IngestionRoutes.fs:89
- **Summary:** Some operations derive a current date or write a timestamp from a fresh clock read rather than the operation's initiation instant, and the REQ-SYS-3.4 tests cover only JE post and batch post timestamps, not derived current dates.
- **Resolution:** dan-decides

REQ-SYS-3.4: 'Every timestamp the operation writes, and every "current date" it derives ... uses that instant.' ReportRoutes.fs:45 passes `Calendar.today()` (a fresh Clock.now(), Calendar.fs:11) as the PrePostingReview report date, although the route has a context. ReportFooter.fs:7 reads `Clock.now()` again for the footer, so one report run makes at least two independent clock reads and can straddle midnight. IngestionRoutes.fs:89 stamps the processed-file name with a fresh `Clock.now()` instead of the ingest operation's instant. The 3.4 tests (OperationInstantAndAtomicity.fs:179, 204) check only DB row timestamps for JE post and batch post. Nothing tests the 'current date it derives' clause (account activity reference date, projection horizon start, report date) that the REQ names.

**Action:** Dan decides whether report dates and processed-file names fall under REQ-SYS-3.4. If they do, derive them from Context.getInitiationInstant and add a REQ-SYS-3.4 test for at least one derived current date (e.g. the PrePostingReview report date or projection start equals dateFromInstant of the context instant).

**Why:** The REQ explicitly covers derived current dates to keep date logic from straddling midnight. These code paths bypass the instant, and no test would notice.

---

## EFF-SYS-5.1-1 — test-gap
- **Location:** REQ-SYS-5.1; Tests/Tests.Integrated/CrossDomainOrchestration/AgreementCreation.fs:66-97
- **Summary:** The REQ-SYS-5.1 citations for master agreements assert four fields, and no test anywhere round-trips the created/modified instants of any CashFlow entity.
- **Resolution:** fix-test

REQ-SYS-5.1 requires all entity properties to be perfectly reconstituted. The citing agreement tests assert only activeBegin, the next-instance date, activeEnd and memo (lines 73-76, 90-93). Name, counterparty, direction, cadence kind and createdAt/modifiedAt are not compared. The other REQ-SYS-5.1 citations (Account.fs:102, JournalEntryComment.fs:224, JournalEntryExternalReference.fs:202) compare every field including timestamps, which is the form this REQ asks for. Grepping Tests/ for MasterAgreement/PaymentAgreement/Instance/Invoice/Payment createdAt or modifiedAt round-trips finds none (only before/after equality in RevisedRequirementsCashFlow.fs:328 and PaymentsToPosted.fs:246). A column mapping bug in a CashFlow reader for created_at/modified_at (e.g. swapped columns) would pass the suite.

**Action:** In the REQ-SYS-5.1 agreement tests, compare the full created MasterAgreement and PaymentAgreement records to the read-back (Assert.Equal on the record), and add a timestamp round-trip for Instance, Invoice and Payment.

**Why:** The citation implies full-fidelity coverage. Checking a subset of fields leaves the rest of the reconstitution unverified.

---

## EFF-SYS-6.1.1-1 — test-gap
- **Location:** REQ-SYS-6.1.1; Specs/Behavioral/SystemWide.md:48, 64-83
- **Summary:** REQ-SYS-6.1.1 is active but has no citing test, waiver or Unenforceable entry, which is the single check-traceability failure.
- **Resolution:** dan-decides

REQ-SYS-6.1.1 ('Any exception to REQ-SYS-6.1 ... must be stated explicitly in the relevant entity spec') constrains how specs are written, not runtime behavior. The only documented exception is REQ-FP-2.7 ensure idempotency, which FP tests already cover. The Unenforceable table in SystemWide.md is empty (line 83 is a blank row). Checks/check-traceability fails Invariant 2 on this ID.

**Action:** Dan decides: list REQ-SYS-6.1.1 under Unenforceable (it binds spec authors) or waive it with a dated approval.

**Why:** Every active REQ must be tested, waived or unenforceable. This one is none of the three, which breaks the mechanical gate.

---

