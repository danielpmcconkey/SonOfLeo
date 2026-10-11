# architecture-auditor

## ARCH-DRIFT-1 — architecture
- **Location:** Src/Ui.InterfaceBridge/Routes/CashFlowRoutes.fs:118-137,260-272; Src/Business.CrossDomainOrchestration/InstanceOrchestration.fs (cancelInstance, cancelInvoice); Architecture/SonOfLeo.archimate 'Cash flow functions'
- **Summary:** The CashFlow CancelInstance and CancelInvoice routes offer a use case that no ApplicationFunction in the model describes.
- **Resolution:** dan-decides

Skills/ArchiMate/model_drift.py printed 'NO DRIFT' at HEAD 905c0b9. It cannot see capabilities, so I compared every route verb in Src/Ui.InterfaceBridge/Routes/*.fs and every public orchestration function against the model's 370 ApplicationFunctions. Every route maps to a capability, sometimes a coarse one (for example 'Maintain holdings' covers Holding Delete, and 'Reconcile against a simulated post of staged entries' covers ShadowReconcile). Two do not. CashFlow/CancelInstance (CashFlowRoutes.fs:261, handler at 118) and CashFlow/CancelInvoice (CashFlowRoutes.fs:268, handler at 129) call InstanceOrchestration.cancelInstance and cancelInvoice. The route description reads 'Cancel an instance and every invoice it holds, with a reason note. Refused while any of its invoices has a payment. Cancellation is permanent.' Nothing in the model mentions cancellation (a search of the .archimate for 'cancel' finds nothing). The closest function, 'Update an instance composite' ('Apply changes to an instance, its invoices and payments, and re-derive their states'), describes the CreateInvoice/UpdateInvoice path, not a permanent cancellation with a reason note and a payment guard.

**Action:** Add to the model: 'Cancel an instance' and 'Cancel an invoice' ApplicationFunctions under 'Cash flow functions', realized by InstanceOrchestration.

**Why:** The model is the written architecture. A use case the code offers with no matching capability is drift that later audits cannot judge against.

---

## ARCH-ERR-1 — architecture
- **Location:** Src/Business.FinancialServices.Positions/PositionsError.fs:7-14, 36
- **Summary:** PositionsError declares the domain type ActivityShapeProblem, and the error case PositionsActivityShapeInvalid carries it. The todo comment admits this was done for compile order.
- **Resolution:** dan-decides

Rule 'Error cases carry primitives': 'An error module declares no other type... A type declared in an error module because the error module compiles first breaks this rule.' PositionsError.fs:8-9 says: '// todo: ActivityShapeProblem belongs with the Activity kind in PositionsComponent. It is declared here only because an error case carries it and PositionsError compiles first.' Line 10 declares `type ActivityShapeProblem = | SecurityMissing | QuantityMissing | QuantityNotAllowed | QuantityWithoutSecurity`, and line 36 has `PositionsActivityShapeInvalid of string * LocalDate * string * ActivityShapeProblem`. The ruling SS-3 (todo comments are not directives) does not cover this. It is about whether a todo must cite a REQ, not about type placement.

**Action:** Move ActivityShapeProblem into PositionsComponent next to ActivityKind and give it a toString there. Change PositionsActivityShapeInvalid to carry a string.

**Why:** The rule exists so every error module compiles ahead of the domain types it reports on. The comment shows the code working around the hierarchy instead of following it.

---

## ARCH-ERR-2 — architecture
- **Location:** Src/Business.FinancialServices.CashFlow/CashFlowError.fs:7-10, 76
- **Summary:** CashFlowError declares the union OrphanedLineReason, and the error case CashflowPaymentAgreementLinksOrphaned carries it.
- **Resolution:** dan-decides

Rule 'Error cases carry primitives': 'An error module declares no other type.' Line 8 declares `type OrphanedLineReason = | NoOpenInvoiceCoversDate | CoveringInvoicesOverpaid`, and line 76 has `CashflowPaymentAgreementLinksOrphaned of (Guid * Guid * OrphanedLineReason) list`. OrphanedLineReason is a cash-flow domain value, not a primitive or NodaTime value.

**Action:** Move OrphanedLineReason into CashFlowComponent with a toString. Change the error case to carry (Guid * Guid * string) list.

**Why:** Error modules are foundations. A domain type declared in one sits at the wrong rung and blurs the error vocabulary into domain vocabulary.

---

## ARCH-ERR-3 — architecture
- **Location:** Src/Business.FinancialServices.Classification/ClassificationError.fs:6-17, 37-38
- **Summary:** ClassificationError declares the union StageLineProtection, and two error cases carry it.
- **Resolution:** dan-decides

Rule 'Error cases carry primitives': 'An error module declares no other type.' Line 8 declares `type StageLineProtection = | LinkedToPaymentAgreement | ReferencedByPayment | RecordedInClassificationRun`, with a private describeProtection at line 13. Lines 37-38 are `ClassificationStageEntryLineCannotBeRemoved of Guid * StageLineProtection` and `ClassificationStageEntryLineCannotBeChanged of Guid * StageLineProtection`.

**Action:** Move StageLineProtection, with describeProtection as its toString, into ClassificationComponent. Change both error cases to carry Guid * string.

**Why:** The same rule covers the same pattern in Positions and CashFlow. The type is a domain fact (why a line is protected), not an error primitive.

---

## ARCH-ERR-4 — architecture
- **Location:** Src/Business.FinancialServices.DataIngestion/DataIngestionError.fs:6-8, 38
- **Summary:** DataIngestionError declares the record IngestionRejectedRecord, whose field `error` is an IAppError, and IngestionFileRejected carries a list of it.
- **Resolution:** dan-decides

Rule 'Error cases carry primitives': 'An error case's fields are primitives and NodaTime values... An error module declares no other type.' Line 8 is `type IngestionRejectedRecord = { lineNumbers: int list; groupId: string option; error: IAppError }`, and line 38 is `IngestionFileRejected of string * IngestionRejectedRecord list`. The module declares a second type, and the case nests a non-primitive interface value.

**Action:** Carry each rejected record as primitives: line numbers, group ID and the inner error's rendered message as a string. Alternatively, move the record to StageEntryComponent and have the error case carry its toString.

**Why:** An error case that nests other error objects has fields that are not primitives, so it falls outside the 'primitives only' contract.

---

## ARCH-ERR-5 — architecture
- **Location:** Src/App.DataAccessLayer/DalError.fs:14-22
- **Summary:** Nine DalError cases carry `exn`, which is neither a primitive nor a NodaTime value.
- **Resolution:** dan-decides

Rule 'Error cases carry primitives': 'An error case's fields are primitives and NodaTime values (string, decimal, int, LocalDate and the like).' These cases carry an exception object: DalErrorDuringAutoCompleteTransactionRun, DalErrorDuringLongUnboxing, DalErrorDuringNonQueryExecution, DalErrorDuringReaderQueryExecution, DalErrorDuringScalarExecution, DalErrorDuringStringUnboxing, DalErrorDuringTransactionCommit, DalErrorDuringTransactionCreation and DalErrorDuringTransactionRollback, each `of exn`. exn breaks neither compile order nor the hierarchy (it is BCL), so the rule's stated purpose is not harmed. Its wording is still not met.

**Action:** Either carry the exception's message (and type name) as strings, or have Dan state in the rule's documentation that BCL exceptions count as allowed payloads.

**Why:** The rule was swept to every error case on 2026-10-11. An exception payload is the most common case it leaves undecided.

---

## ARCH-ERR-6 — architecture
- **Location:** Src/App.Utility/UtilityError.fs:7, 11
- **Summary:** The UtilityError cases ConfigReadError (string * exn) and FileIoError (exn) carry exception objects.
- **Resolution:** dan-decides

Rule 'Error cases carry primitives' limits case fields to primitives and NodaTime values. Line 7 is `ConfigReadError of string * exn` and line 11 is `FileIoError of exn`. This is the same question as DalError, in App.Utility.

**Action:** Carry the exception message as a string, or have Dan exempt exn in the rule's documentation.

**Why:** It keeps the error-payload contract uniform across tiers.

---

## ARCH-COMP-1 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/AccountActivity.fs:15-26
- **Summary:** AccountActivityDetail is a journal-entry-only record defined in orchestration.
- **Resolution:** dan-decides

Rule 'Composite types are defined in the cross domain orchestration tier': 'A composite is built from the types of more than one business domain. A record whose fields all come from one domain is that domain's component type and is defined in that domain's components module, even when its only consumer is an orchestration function or a report.' Its fields are lineId: JournalEntryLineId, amount: Money, lineType: JournalEntryLineType, lineMemo: JournalEntryLineMemo option, created/modified Instants, journalEntryHeaderId, entryDate, journalEntryDescription, journalEntrySource and journalEntryVoidedAt, all from the journal entry domain plus the Money foundation. The enclosing AccountActivity, which adds the account fields, is a true account plus journal entry composite and is fine.

**Action:** Move AccountActivityDetail to JournalEntryComponent in Business.FinancialServices.Ledger.

**Why:** A single-domain record in orchestration hides a domain's own vocabulary in the composite tier ('Any component type in the cross domain orchestration or higher is a signal...').

---

## ARCH-COMP-2 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/AccountBalance.fs:16-21
- **Summary:** The AccountBalance record (accountId, totalCredits, totalDebits, netBalance) holds only account-domain fields and Money, yet it is defined in orchestration.
- **Resolution:** dan-decides

Rule 'Composite types are defined in the cross domain orchestration tier': a record whose fields all come from one domain belongs in that domain's components module. Its fields are AccountId and three Money values. The model names AccountBalance as the pattern for report reads ('A report read may reconstitute its own rows...'), and that licence is about the read, which can stay in orchestration because it joins journal entry lines. It says nothing about where the row type lives.

**Action:** Move the AccountBalance row type to AccountComponent. Keep fetchByAccountIdList (the cross-schema read) in orchestration.

**Why:** The type-placement rule and the report-read licence are separate rules. Meeting one does not clear the other.

---

## ARCH-COMP-3 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/AccountSnapshotOrchestration.fs:26-42
- **Summary:** SnapshotLineView, SnapshotView and RecordedSnapshot hold only positions-domain types, yet they are defined in orchestration.
- **Resolution:** dan-decides

Rule 'Composite types are defined in the cross domain orchestration tier' requires that a composite span more than one domain. SnapshotLineView = {line: AccountSnapshotLine; securityName: string; lots: AccountSnapshotLot list}. SnapshotView = {header: AccountSnapshotHeader; investmentAccountName: string; lines: SnapshotLineView list}. RecordedSnapshot = {snapshot: SnapshotView; replacedExisting: bool}. All of it is Positions. They reference Positions entity types, which compile after PositionsComponent.

**Action:** Define these in Business.FinancialServices.Positions, in a module placed after the snapshot entity modules (PositionsComponent compiles before the entities, so it cannot hold them), or reshape them to carry IDs only.

**Why:** Positions views defined in orchestration make orchestration the owner of Positions vocabulary.

---

## ARCH-COMP-4 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/AgreementOrchestration.fs:19-25, 310-313
- **Summary:** Agreement and AgreementListing are cash-flow-only records defined in orchestration.
- **Resolution:** dan-decides

Rule 'Composite types are defined in the cross domain orchestration tier': a composite must span more than one domain. Agreement = {masterAgreement: MasterAgreement; paymentAgreements: PaymentAgreement list; instances: Instance list; invoices: Invoice list; payments: Payment list}, and every field is a CashFlow entity. AgreementListing = {masterAgreement; paymentAgreements} is also CashFlow only.

**Action:** Define Agreement and AgreementListing in Business.FinancialServices.CashFlow (in a module after the entity modules they hold).

**Why:** The rule now says in so many words that a composite must cross domains. These do not.

---

## ARCH-COMP-5 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/FetchFilterAndSort.fs:80-93
- **Summary:** AgreementFilter (cash flow only) and FetchStageEntrySort (data ingestion only) are single-domain types in orchestration.
- **Resolution:** dan-decides

Rule 'Composite types are defined in the cross domain orchestration tier'. AgreementFilter = {agreementIds: MasterAgreementId list option; activeAgreementsOnly: bool} draws only on cash flow. FetchStageEntrySort (line 80) lists sort keys of staged entries only. The model makes FetchFilters the base of orchestration ('Foundations Are the Base of Their Container'). That says where filters sit inside orchestration, not that single-domain filters belong there. The other filters in the file (AccountActivityFilter, ClassificationRuleFilter, StageEntryFetchFilter, JournalEntryFetchFilter via TemporalFilter/FiscalPeriodId) do cross domains.

**Action:** Move AgreementFilter to CashFlowComponent and FetchStageEntrySort to StageEntryComponent, or have Dan state that the foundation filters are exempt.

**Why:** The rule is now swept across every type, and the foundation status of FetchFilters does not address it.

---

## ARCH-COMP-6 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/HoldingOrchestration.fs:12-16
- **Summary:** HoldingView is a positions-only record defined in orchestration.
- **Resolution:** dan-decides

Rule 'Composite types are defined in the cross domain orchestration tier'. HoldingView = {holding: Holding; investmentAccountName: string; securityName: string}. Holding, investment account and security are all Positions.

**Action:** Define the view in Business.FinancialServices.Positions, or drop it in favour of the Holding plus IDs and resolve names at the bridge.

**Why:** A composite must draw on more than one domain.

---

## ARCH-COMP-7 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/HoldingsAsOf.fs:14-24
- **Summary:** HoldingsAsOfLine is a positions-only record defined in orchestration.
- **Resolution:** dan-decides

Rule 'Composite types are defined in the cross domain orchestration tier'. Its fields are securityName, ticker, dimensionValueNames, basisMethod: BasisMethod, quantity: Quantity, price: Price, marketValue/reportedCostBasis: Money and lots: AccountSnapshotLot list. These are Positions plus the core financial foundations. HoldingsAsOfAccount (line 26) does cross into the person and ledger-account domains.

**Action:** Move HoldingsAsOfLine into Business.FinancialServices.Positions (after AccountSnapshotLot).

**Why:** A composite must draw on more than one domain.

---

## ARCH-COMP-8 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/InstanceOrchestration.fs:22-30, 412-440
- **Summary:** InvoiceComposite, InstanceComposite, InvoiceCompositeUpdate and InstanceCompositeUpdate are cash-flow-only records defined in orchestration.
- **Resolution:** dan-decides

Rule 'Composite types are defined in the cross domain orchestration tier'. InvoiceComposite = {invoice: Invoice; payments: Payment list}. InstanceComposite = {instance: Instance; invoiceComposites: InvoiceComposite list}. InvoiceCompositeUpdate and InstanceCompositeUpdate use only Invoice/Payment/Instance field updates and CashFlowComponent types. All of it is cash flow. PaymentAgreementClassificationResult (line 37) does cross into classification and is fine.

**Action:** Define the four types in Business.FinancialServices.CashFlow (in a module after Instance/Invoice/Payment).

**Why:** Despite the 'Composite' in their names, these do not meet the model's definition of a composite.

---

## ARCH-COMP-9 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/InvestmentActivityOrchestration.fs:24-37
- **Summary:** ActivityView and RecordedRange are positions-only records defined in orchestration.
- **Resolution:** dan-decides

Rule 'Composite types are defined in the cross domain orchestration tier'. ActivityView = {activity: InvestmentActivity; investmentAccountName: string; securityName: string option}. RecordedRange = {investmentAccountName: string; beginDate; endDate; removed: int; recorded: int}. Both are Positions only.

**Action:** Define them in Business.FinancialServices.Positions, after InvestmentActivity.

**Why:** A composite must draw on more than one domain.

---

## ARCH-COMP-10 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/InvestmentWealthHistory.fs:13-17
- **Summary:** WealthPoint (monthEnd, totals by WealthGroup, total) is a positions-only record defined in orchestration.
- **Resolution:** dan-decides

Rule 'Composite types are defined in the cross domain orchestration tier'. Its fields are LocalDate, (WealthGroup * Money) list and Money. WealthGroup is in PositionsComponent.fs:433.

**Action:** Move WealthPoint to PositionsComponent.

**Why:** A composite must draw on more than one domain.

---

## ARCH-COMP-11 — contradiction
- **Location:** Src/Business.CrossDomainOrchestration/JournalEntryOrchestration.fs:20-25; Architecture/SonOfLeo.archimate 'Avoid creating types strictly for code convenience'
- **Summary:** The JournalEntry 'composite' holds only journal entry types, so the new composite constraint rejects it, while another principle in the model uses it as its example of a composite.
- **Resolution:** dan-decides

The documentation of 'Composite types are defined in the cross domain orchestration tier' says: 'A composite is built from the types of more than one business domain. A record whose fields all come from one domain is that domain's component type.' JournalEntry = private {header: JournalEntryHeader; jeLines: JournalEntryLine list; externalReferences: JournalEntryExternalReference list; comments: JournalEntryComment list}, and all of it is the journal entry domain. The documentation of the principle 'Avoid creating types strictly for code convenience' calls this type 'a new JounalEntry (which is a composite of JournalEntryHeader, JournalEntryLine list, JournalEntryExternalReference list, and JournalEntryComment list)'. The same holds for StageEntry (ARCH-COMP-17) and InstanceComposite (ARCH-COMP-8). The model contradicts itself, or the code breaks the newer rule.

**Action:** Either move JournalEntry into Business.FinancialServices.Ledger (after the JE entity modules), or have Dan reword the composite constraint to allow multi-entity aggregates within one domain.

**Why:** Two rules in the model disagree about the same type, and the 2026-10-11 sweep rule makes every single-domain aggregate a finding until that is settled.

---

## ARCH-COMP-12 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/NetWorth.fs:17-22
- **Summary:** LedgerAccountBalance (code, name, balance, balanceDate) is an account-only record defined in orchestration.
- **Resolution:** dan-decides

Rule 'Composite types are defined in the cross domain orchestration tier'. Its fields are a ledger account's code and name, a Money balance and an optional date. All of it is the account domain. The other NetWorth records (NetWorthProperty, NetWorth) do cross domains.

**Action:** Move it to AccountComponent (typed as AccountId plus Money; see the ID and primitive findings).

**Why:** A composite must draw on more than one domain.

---

## ARCH-COMP-13 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/PeriodActivity.fs:15-21
- **Summary:** PeriodActivityLine is a journal-entry-only record defined in orchestration.
- **Resolution:** dan-decides

Rule 'Composite types are defined in the cross domain orchestration tier'. Its fields are entryDate, journalEntryId: JournalEntryHeaderId, description: JournalEntryDescription, lineType: JournalEntryLineType, amount: Money and memo: JournalEntryLineMemo option, all journal entry. The ruling REPORT-RECORDS covers only privacy, and its 'Not covered' note (Hobson, 2026-10-11) excludes placement.

**Action:** Move PeriodActivityLine to JournalEntryComponent.

**Why:** A composite must draw on more than one domain, even when a report is its only consumer.

---

## ARCH-COMP-14 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/PrePostingReview.fs:19-32
- **Summary:** PrePostingPayment and PrePostingAgreement are cash-flow-only records defined in orchestration.
- **Resolution:** dan-decides

Rule 'Composite types are defined in the cross domain orchestration tier'. PrePostingPayment holds PaymentId, InvoiceId, Money, invoice/due/instance dates and PaymentState, all cash flow. PrePostingAgreement = {paymentAgreementName: PaymentAgreementName; masterAgreementName: AgreementName}, also cash flow. PrePostingLine and PrePostingEntry do cross into ingestion, account and classification.

**Action:** Move both to CashFlowComponent.

**Why:** A composite must draw on more than one domain.

---

## ARCH-COMP-15 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/Reconciliation.fs:13-19
- **Summary:** ReconciliationRow is an account-only record defined in orchestration.
- **Resolution:** dan-decides

Rule 'Composite types are defined in the cross domain orchestration tier'. Its fields are accountCode: AccountCode, accountName: AccountName, asOf: LocalDate and externalBalance/ledgerBalance/delta: Money. That is the account domain plus the Money foundation.

**Action:** Move ReconciliationRow to AccountComponent (keyed by AccountId; see ARCH-ID-13).

**Why:** A composite must draw on more than one domain.

---

## ARCH-COMP-16 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/SecurityOrchestration.fs:12-15
- **Summary:** SecurityView (security plus dimension value names) is a positions-only record defined in orchestration.
- **Resolution:** dan-decides

Rule 'Composite types are defined in the cross domain orchestration tier'. SecurityView = {security: Security; dimensionValueNames: Map<Dimension, string>}. Both security and dimension value are Positions.

**Action:** Define it in Business.FinancialServices.Positions after Security, or drop it and resolve names at the bridge.

**Why:** A composite must draw on more than one domain.

---

## ARCH-COMP-17 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/StageEntryOrchestration.fs:23-35
- **Summary:** StageEntry and DeduplicationResult are data-ingestion-only records defined in orchestration.
- **Resolution:** dan-decides

Rule 'Composite types are defined in the cross domain orchestration tier'. StageEntry = private {stageEntryHeader: StageEntryHeader; seLines: StageEntryLine list; statusTransitions: StageEntryStatusTransition list}, all data ingestion. DeduplicationResult = {ingested: StageEntry list; declinedForPayment: StageEntry list}, also ingestion only. AccountClassificationResult (line 37) does cross into classification.

**Action:** Define StageEntry and DeduplicationResult in Business.FinancialServices.DataIngestion (after StageEntryLine), or settle the question in ARCH-COMP-11.

**Why:** A composite must draw on more than one domain.

---

## ARCH-COMP-18 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/TrialBalance.fs:11-26
- **Summary:** TrialBalanceRowNested and TrialBalanceRowFlattened are account-only records defined in orchestration.
- **Resolution:** dan-decides

Rule 'Composite types are defined in the cross domain orchestration tier'. Their fields are accountCode, accountName, generation: int and totalCredits/totalDebits/netBalance: Money. That is the account domain plus Money. The ruling REPORT-RECORDS cites this module as the precedent for public rows, but its 'Not covered' note says it does not govern placement or field types.

**Action:** Move both row types to AccountComponent.

**Why:** The REPORT-RECORDS ruling explicitly leaves placement open, so this is not suppressed.

---

## ARCH-COMP-19 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/UnitRollForward.fs:14-29
- **Summary:** RollForwardRow and RollForward are positions-only records defined in orchestration.
- **Resolution:** dan-decides

Rule 'Composite types are defined in the cross domain orchestration tier'. RollForwardRow holds a security name and quantities, and RollForward holds an investment account name, two dates and rows. All of it is Positions.

**Action:** Move both to PositionsComponent (typed per ARCH-PRIM-10).

**Why:** A composite must draw on more than one domain.

---

## ARCH-PRIM-1 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/HoldingsAsOf.fs:15-17, 27-29, 31-32
- **Summary:** The HoldingsAsOf report types hold persisted names, ticker, institution and account group as bare strings, although domain types exist for each.
- **Resolution:** dan-decides

Rule 'Below the UserInterfaceBridge, a persisted value travels as its domain type, never as the primitive it unwraps to': '...report or view rows alike... A Business-tier type that holds a persisted value as a bare string, decimal or Guid breaks this rule.' The hits are HoldingsAsOfLine.securityName: string (SecurityName exists, PositionsComponent.fs:130), ticker: string option (Ticker, :139), dimensionValueNames: Map<Dimension, string> (DimensionValueName, :121), and HoldingsAsOfAccount.investmentAccountName: string (InvestmentAccountName, :146), institution: string (Institution, :155), accountGroup: string (AccountGroup, :164), ownerNames: string list (PersonName, PersonComponent.fs:13) and ledgerAccountCodeAndName: (string * string) option (AccountCode/AccountName).

**Action:** Type every field with its domain type, or replace the names with IDs per ARCH-ID-1.

**Why:** The 2026-10-11 rule carries the naked-primitive ban to report rows. Strings below the bridge lose the validation the domain types carry.

---

## ARCH-PRIM-2 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/NetWorth.fs:18-19, 25-27, 36, 38, 83
- **Summary:** The NetWorth rows hold ledger code and name, investment account name, owner names, account group and property name as strings.
- **Resolution:** dan-decides

Rule 'Below the UserInterfaceBridge, a persisted value travels as its domain type, never as the primitive it unwraps to'. The hits are LedgerAccountBalance.code: string and name: string (18-19; filled from AccountCode.value and AccountName.value at 143-144), NetWorthInvestmentAccount.investmentAccountName: string (25), ownerNames: string list (26), accountGroup: string (27), NetWorthProperty.propertyName: string (36; PropertyName.value at 209), ownerNames: string list (38) and NetWorth.investmentsByAccountGroup: (string * Money) list (83). Domain types exist for all of them: AccountCode, AccountName, InvestmentAccountName, PersonName, AccountGroup and PropertyName.

**Action:** Use the domain types, or IDs per ARCH-ID-2, and leave unwrapping to ReportConverters/NetWorthWriter.

**Why:** The domain is unwrapped in orchestration instead of at the boundary converters, the 'one place only' the rule allows.

---

## ARCH-PRIM-3 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/AccountSnapshotOrchestration.fs:28, 35, 164
- **Summary:** SnapshotLineView.securityName and SnapshotView.investmentAccountName are bare strings, and viewSnapshot takes the account name as a string.
- **Resolution:** dan-decides

Rule 'Below the UserInterfaceBridge, a persisted value travels as its domain type, never as the primitive it unwraps to'. Line 28 is securityName: string (SecurityName exists). Line 35 is investmentAccountName: string (InvestmentAccountName exists). Line 164 is viewSnapshot (accountName: string), which fills line 35. The string parameters used only to build error messages (88, 112) are allowed under 'Error cases carry primitives' and are not counted here.

**Action:** Type the view fields as SecurityName and InvestmentAccountName (or IDs per ARCH-ID-3).

**Why:** View rows are explicitly inside the scope of the rule.

---

## ARCH-PRIM-4 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/HoldingOrchestration.fs:14-15
- **Summary:** HoldingView.investmentAccountName and securityName are bare strings.
- **Resolution:** dan-decides

Rule 'Below the UserInterfaceBridge, a persisted value travels as its domain type, never as the primitive it unwraps to'. These are investmentAccountName: string (InvestmentAccountName exists) and securityName: string (SecurityName exists), filled by accountNameOf/securityNameOf at 18-20 through .value.

**Action:** Use InvestmentAccountName and SecurityName, or IDs per ARCH-ID-4.

**Why:** View rows are inside the rule's scope.

---

## ARCH-PRIM-5 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/InvestmentActivityOrchestration.fs:26-27, 32
- **Summary:** ActivityView and RecordedRange hold the investment account name and security name as strings.
- **Resolution:** dan-decides

Rule 'Below the UserInterfaceBridge, a persisted value travels as its domain type, never as the primitive it unwraps to'. The hits are ActivityView.investmentAccountName: string (26), securityName: string option (27) and RecordedRange.investmentAccountName: string (32). securityNamesById (41-43) builds a Map<SecurityId, string> by unwrapping SecurityName.

**Action:** Use InvestmentAccountName and SecurityName, or IDs.

**Why:** View rows are inside the rule's scope.

---

## ARCH-PRIM-6 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/InvestmentAccountOrchestration.fs:20-21
- **Summary:** InvestmentAccountView holds owner names and the linked ledger account's code and name as strings.
- **Resolution:** dan-decides

Rule 'Below the UserInterfaceBridge, a persisted value travels as its domain type, never as the primitive it unwraps to'. The fields are ownerNames: string list (PersonName exists) and ledgerAccountCodeAndName: (string * string) option (AccountCode/AccountName exist), filled at 258-263 through PersonOrchestration.personNamesOf and PositionsLedgerLinks.ledgerAccountCodeAndName.

**Action:** Carry the owner PersonIds and the ledger AccountId (already on the entity), or the domain types.

**Why:** View rows are inside the rule's scope.

---

## ARCH-PRIM-7 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/RealEstateOrchestration.fs:22-24
- **Summary:** PropertyView holds owner names and the asset and mortgage account codes and names as strings.
- **Resolution:** dan-decides

Rule 'Below the UserInterfaceBridge, a persisted value travels as its domain type, never as the primitive it unwraps to'. The fields are ownerNames: string list, assetAccountCodesAndNames: (string * string) list and mortgageAccountCodesAndNames: (string * string) list, filled at 257-261.

**Action:** Use domain types, or IDs per ARCH-ID-7.

**Why:** View rows are inside the rule's scope.

---

## ARCH-PRIM-8 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/PreLedgerBalanceOrchestration.fs:21-22, 32-33
- **Summary:** PreLedgerBalanceView holds the ledger account code and name as strings.
- **Resolution:** dan-decides

Rule 'Below the UserInterfaceBridge, a persisted value travels as its domain type, never as the primitive it unwraps to'. The fields are accountCode: string and accountName: string, filled with codeOf account and AccountName.value at 32-33.

**Action:** Drop them (the entity already carries ledgerAccountId), or type them AccountCode/AccountName.

**Why:** View rows are inside the rule's scope.

---

## ARCH-PRIM-9 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/SecurityOrchestration.fs:14, 129
- **Summary:** SecurityView.dimensionValueNames is a Map<Dimension, string> holding persisted dimension value names.
- **Resolution:** dan-decides

Rule 'Below the UserInterfaceBridge, a persisted value travels as its domain type, never as the primitive it unwraps to'. DimensionValueName exists (PositionsComponent.fs:121). Line 129 unwraps it with DimensionValueName.value to fill the map.

**Action:** Drop the map (Security already holds Map<Dimension, DimensionValueId>), or type it Map<Dimension, DimensionValueName>.

**Why:** View rows are inside the rule's scope.

---

## ARCH-PRIM-10 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/UnitRollForward.fs:15-17, 20, 25, 44
- **Summary:** RollForwardRow holds the persisted snapshot quantities as decimal and the security and account names as strings.
- **Resolution:** dan-decides

Rule 'Below the UserInterfaceBridge, a persisted value travels as its domain type, never as the primitive it unwraps to'. startQuantity: decimal (16) and endQuantity: decimal (20) are snapshot line quantities, persisted, unwrapped by Quantity.amount at line 44. unitsIn/unitsOut (17-18) are sums of persisted activity quantities. securityName: string (15) and RollForward.investmentAccountName: string (25) are also bare. The doc comment at 12-13 justifies decimal for expected and difference only ('a missing activity can make the expected quantity negative, and a difference has a sign'). Those two are derived, so I do not count them. The justification does not reach the start and end quantities.

**Action:** Type startQuantity, endQuantity, unitsIn and unitsOut as Quantity, and the names as SecurityName/InvestmentAccountName or IDs. Keep expected and difference as decimal.

**Why:** Snapshot quantities are persisted values whose domain type (6-dp Quantity) exists.

---

## ARCH-PRIM-11 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/PrePostingReview.fs:22-25
- **Summary:** PrePostingPayment holds invoice and payment values as the types they unwrap to (Money, LocalDate) instead of PaymentAmount, InvoiceDate, DueDate and InvoiceAmount.
- **Resolution:** dan-decides

Rule 'Below the UserInterfaceBridge, a persisted value travels as its domain type, never as the primitive it unwraps to'. The hits are paymentAmount: Money (PaymentAmount wraps Money, CashFlowComponent.fs:374), invoiceDate: LocalDate (InvoiceDate wraps LocalDate, :349), dueDate: LocalDate (DueDate, :354) and invoiceAmount: Money (InvoiceAmount, :369). Each persisted value is unwrapped one level below its domain type, in the Business tier.

**Action:** Type the fields PaymentAmount, InvoiceDate, DueDate and InvoiceAmount.

**Why:** The rule forbids traveling as 'the primitive it unwraps to', and these wrappers exist precisely to keep invoice dates and amounts distinct.

---

## ARCH-PRIM-12 — architecture
- **Location:** Src/Business.FinancialServices.Positions/PositionsComponent.fs:433-436
- **Summary:** The WealthGroup union carries persisted names as `GroupName of string` and `GroupOwners of string list`.
- **Resolution:** dan-decides

Rule 'Below the UserInterfaceBridge, a persisted value travels as its domain type, never as the primitive it unwraps to'. GroupName holds an investment account name, account group or dimension value name, depending on the grouping (InvestmentWealthHistory.fs:19 groupedValues). GroupOwners holds person names. Domain types exist for each.

**Action:** Make the cases carry domain types or IDs: GroupAccount of InvestmentAccountId, GroupAccountGroup of AccountGroup, GroupDimensionValue of DimensionValueId and GroupOwners of Set<PersonId>.

**Why:** This is a component type in the lowest Positions module. The rule applies with no report exemption.

---

## ARCH-PRIM-13 — architecture
- **Location:** Src/Business.FinancialServices.Ledger/Account.fs:295, 302-305; Src/Ui.InterfaceBridge/Routes/AccountRoutes.fs:71, 81
- **Summary:** Account.updateAccountNameById and updateExternalReferenceById take the new name and reference as string and string option, so the UI hands the Business tier primitives and the domain types are built below the bridge.
- **Resolution:** dan-decides

Rule 'Below the UserInterfaceBridge, a persisted value travels as its domain type, never as the primitive it unwraps to': 'Domain types are unwrapped to primitives in one place only: the UserInterfaceBridge's boundary converters.' Account.fs:295 is `(newName: string)`, and Account.fs:305 is `(newReference: string option) // todo make this as FieldUpdate`. AccountRoutes.fs:71/81 pass accountUpdate.newName and newReference straight from the contract. AccountName.create and AccountExternalReference.create then run inside the entity module.

**Action:** Convert at the bridge (AccountName.create and AccountExternalReference.create in the account converter) and change both functions to take AccountName and FieldUpdate<AccountExternalReference option>.

**Why:** The persisted value crosses the bridge as a primitive, and the todo marks a known compromise.

---

## ARCH-PRIM-14 — architecture
- **Location:** Src/Business.FinancialServices.Ledger/FiscalPeriod.fs:128, 134-135; Src/Business.FinancialServices.Ledger/JournalEntryComponent.fs:106
- **Summary:** FiscalPeriod.fetchIdByKey takes the persisted period key as a bare string although FiscalPeriodKey exists.
- **Resolution:** dan-decides

Rule 'Below the UserInterfaceBridge, a persisted value travels as its domain type, never as the primitive it unwraps to'. Line 128 is `let fetchIdByKey (context) (key: string)`, which binds CharString key against ledger.fiscal_period.period_key. Its only caller is EntryDate.create in JournalEntryComponent.fs:106. FiscalPeriodKey (FiscalPeriodComponent.fs:15) is the domain type for that column.

**Action:** Take a FiscalPeriodKey parameter and have EntryDate.create build one.

**Why:** The key is a persisted value, and the rule applies to every value traveling below the bridge.

---

## ARCH-PRIM-15 — architecture
- **Location:** Src/Business.FinancialServices.Classification/ClassificationRule.fs:28, 46
- **Summary:** The ClassificationRule entity holds the persisted rule priority as a naked int.
- **Resolution:** dan-decides

Rules 'Naked primitives are discouraged in Entity or component types' and 'Below the UserInterfaceBridge, a persisted value travels as its domain type...'. Line 28 is `priority: int // lower number wins when multiple rules match`, and create takes (priority: int) at line 46. No priority domain type exists. REQ-CR-1.6 says only that 'Classification rule priority is an integer', so a wrapper would add no validation today, but it would carry the ordering semantics ('lower wins') as a type. This and the primitive rule are the only entity fields of int type I found carrying a business value. The ordinal fields (InvestmentActivity.fs:20, AccountSnapshotLot.fs:18) are storage-order indices, and I did not count them.

**Action:** Introduce a RulePriority wrapper in ClassificationComponent, or have Dan confirm int is the domain type here.

**Why:** The rule names entity types first. A naked int priority is exactly what it discourages.

---

## ARCH-PRIM-16 — architecture
- **Location:** Src/Business.FinancialServices.Classification/ClassificationComponent.fs:131-135
- **Summary:** The PrioritizedMatch component holds the rule priority as a naked int.
- **Resolution:** dan-decides

Rule 'Naked primitives are discouraged in Entity or component types'. PrioritizedMatch = {claimant; ruleId; priority: int}, a component type carrying the persisted priority copied from the rule. This is the same value as ARCH-PRIM-15, in another file.

**Action:** Use the same RulePriority type as the entity, if one is introduced.

**Why:** Component types are named in the rule.

---

## ARCH-RR-1 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/HoldingsAsOf.fs:43-62, 64-83, 119-120, 122-145, 164-172, 201
- **Summary:** The HoldingsAsOf report read keeps eleven columns as bare strings, decimals and Guid after reconstitute, and passes the raw row itself on to buildAccount.
- **Resolution:** dan-decides

Rule 'A report read may reconstitute its own rows, but only into domain types': 'its reconstitute turns every column into its domain type ... before anything else sees it. No column is read into a field as a bare string, decimal or Guid.' Private RawRow (43-62) holds accountName, institution, accountGroup, taxTreatment, ownerNames, ledgerCode, ledgerName, provenance, securityName, ticker and basisMethod as string, and contributionBasis, quantity, price, marketValue and reportedCostBasis as decimal, plus lineId as Guid. reconstitute (119-120) returns `RawRow * (Guid * LineWithoutLots) option`, so the raw row leaves reconstitute unchanged. buildAccount (122-145) converts taxTreatment, provenance and contributionBasis afterward. accountName, institution, accountGroup, ownerNames, ledgerCode, ledgerName, securityName, ticker and dimension names are never converted. The fetch groups accounts by raw.accountName (201) and keys lots by the raw Guid. The SQL (164-172) selects names and codes in place of IDs. The ruling REPORT-SQL allows the cross-schema SELECT. It does not cover what the rows reconstitute into.

**Action:** Reconstitute every column into its domain type (InvestmentAccountId, Institution, AccountGroup, TaxTreatment, PersonId set, AccountId, SecurityId, Ticker, DimensionValueId, BasisMethod, Quantity, Price, Money, AccountSnapshotLineId) inside reconstitute, select IDs rather than names, and group by InvestmentAccountId.

**Why:** The model's own AccountBalance shows the expected shape. Raw primitives leaking past reconstitute is exactly what the 2026-10-11 rule forbids.

---

## ARCH-ID-1 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/HoldingsAsOf.fs:15, 17, 27, 31-32, 164-172, 201
- **Summary:** HoldingsAsOf rows carry security, dimension value, owner and ledger-account names and codes, and no IDs, and the query selects names to stand in for IDs.
- **Resolution:** dan-decides

Principle 'IDs are used in the Business tier while human-readable strings are used at the UI boundary': 'A report or view row carries the IDs of the things it reports on, not their codes or names... A Business-tier type that carries another entity's code or name in place of its ID breaks this principle; so does a query below the UserInterfaceBridge that selects a code or name to stand in for an ID.' HoldingsAsOfLine has securityName and dimensionValueNames but no SecurityId or DimensionValueId. HoldingsAsOfAccount has investmentAccountName, ownerNames and ledgerAccountCodeAndName but no InvestmentAccountId, PersonId or AccountId. The SELECT (165-172) reads ia.account_name, string_agg(per.person_name), a.code, a.account_name, sec.security_name and the dimension value_name columns, and accounts are identified by name at line 201.

**Action:** Carry InvestmentAccountId, SecurityId, DimensionValueIds, owner PersonIds and the ledger AccountId. Resolve names at the bridge via LookupCache, adding caches for the Positions and Person tables.

**Why:** Names in place of IDs push identity into display text, and the principle now applies to reads explicitly.

---

## ARCH-ID-2 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/NetWorth.fs:18-19, 25-26, 36, 38, 143-144, 198, 209
- **Summary:** The NetWorth rows identify ledger accounts by code and name, investment accounts and properties by name, and owners by name.
- **Resolution:** dan-decides

Principle 'IDs are used in the Business tier while human-readable strings are used at the UI boundary'. LedgerAccountBalance has code and name and no AccountId (18-19, filled 143-144). NetWorthInvestmentAccount has investmentAccountName and ownerNames and no InvestmentAccountId or PersonIds (25-26). NetWorthProperty has propertyName and ownerNames and no PropertyId or PersonIds (36, 38, filled 198 and 209). Mortgage rows sort by code (199-204) rather than carrying an ID.

**Action:** Carry AccountId, InvestmentAccountId, PropertyId and PersonIds, and let NetWorthWriter/ReportConverters resolve names through LookupCache.

**Why:** Report rows must carry IDs, and the names come from the bridge.

---

## ARCH-ID-3 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/AccountSnapshotOrchestration.fs:28, 35
- **Summary:** The snapshot views add a security name and an investment account name that the bridge should resolve from the IDs already present.
- **Resolution:** dan-decides

Principle 'IDs are used in the Business tier while human-readable strings are used at the UI boundary': a view row 'carries the IDs of the things it reports on, not their codes or names'. SnapshotLineView.securityName (28) and SnapshotView.investmentAccountName (35) are names. The header already holds investmentAccountId, and the line's holding gives securityId.

**Action:** Remove the names (and the SecurityId lookup if needed, carry the SecurityId), and resolve them in PositionsConverters.

**Why:** Names are a UI concern under this principle.

---

## ARCH-ID-4 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/HoldingOrchestration.fs:14-15
- **Summary:** HoldingView carries the investment account name and security name, though Holding already holds their IDs.
- **Resolution:** dan-decides

Principle 'IDs are used in the Business tier while human-readable strings are used at the UI boundary'. The fields are investmentAccountName: string and securityName: string, and Holding has investmentAccountId and securityId.

**Action:** Return the Holding alone and resolve names at the bridge.

**Why:** View rows carry IDs, not names.

---

## ARCH-ID-5 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/InvestmentActivityOrchestration.fs:26-27, 32, 41-43
- **Summary:** ActivityView and RecordedRange carry account and security names, and securityNamesById builds a name map in the Business tier.
- **Resolution:** dan-decides

Principle 'IDs are used in the Business tier while human-readable strings are used at the UI boundary'. The fields are ActivityView.investmentAccountName and securityName (26-27) and RecordedRange.investmentAccountName (32). securityNamesById (41-43) maps SecurityId to a name string below the bridge. InvestmentActivity already holds investmentAccountId and holdingId.

**Action:** Carry IDs and resolve names at the bridge.

**Why:** View rows carry IDs, not names.

---

## ARCH-ID-6 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/InvestmentAccountOrchestration.fs:20-21, 258-263
- **Summary:** InvestmentAccountView carries owner names and the ledger account's code and name.
- **Resolution:** dan-decides

Principle 'IDs are used in the Business tier while human-readable strings are used at the UI boundary'. The fields are ownerNames: string list and ledgerAccountCodeAndName: (string * string) option. The entity already holds owners: Set<PersonId> and ledgerAccountId: AccountId option.

**Action:** Return the entity alone, and have the bridge resolve owners and the ledger code (Account.idToCode/idToName already exist).

**Why:** View rows carry IDs, not names.

---

## ARCH-ID-7 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/RealEstateOrchestration.fs:22-24, 257-261
- **Summary:** PropertyView carries owner names and the asset and mortgage accounts' codes and names.
- **Resolution:** dan-decides

Principle 'IDs are used in the Business tier while human-readable strings are used at the UI boundary'. The fields are ownerNames, assetAccountCodesAndNames and mortgageAccountCodesAndNames. Property already holds owners, assetAccountIds and mortgageAccountIds.

**Action:** Return the Property alone and resolve names at the bridge.

**Why:** View rows carry IDs, not names.

---

## ARCH-ID-8 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/PreLedgerBalanceOrchestration.fs:21-22, 32-33
- **Summary:** PreLedgerBalanceView carries the ledger account's code and name beside the entity, which already holds ledgerAccountId.
- **Resolution:** dan-decides

Principle 'IDs are used in the Business tier while human-readable strings are used at the UI boundary'. The fields are accountCode: string and accountName: string. Account.idToCode and idToName caches exist for the bridge to use.

**Action:** Drop both fields and resolve them in the converter.

**Why:** View rows carry IDs, not names.

---

## ARCH-ID-9 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/SecurityOrchestration.fs:14, 129
- **Summary:** SecurityView maps each dimension to its value's name, though Security holds the DimensionValueIds.
- **Resolution:** dan-decides

Principle 'IDs are used in the Business tier while human-readable strings are used at the UI boundary'. The field is dimensionValueNames: Map<Dimension, string>, built at line 129 from DimensionValue names.

**Action:** Return the Security alone and resolve dimension value names at the bridge.

**Why:** View rows carry IDs, not names.

---

## ARCH-ID-10 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/UnitRollForward.fs:15, 25, 85-92
- **Summary:** Roll-forward rows identify the security by name and the account by name, with no HoldingId, SecurityId or InvestmentAccountId.
- **Resolution:** dan-decides

Principle 'IDs are used in the Business tier while human-readable strings are used at the UI boundary'. These are RollForwardRow.securityName (15) and RollForward.investmentAccountName (25). securityNameOf (85-92) resolves names in the Business tier, and rows are sorted by name.

**Action:** Carry HoldingId or SecurityId and InvestmentAccountId, and resolve and sort names at the bridge.

**Why:** Report rows carry IDs, not names.

---

## ARCH-ID-11 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/PrePostingReview.fs:31-32, 39-41, 49, 81, 99
- **Summary:** Pre-posting review rows identify accounts by code and name, rules by name, agreements by name and the ingestion source by name, with no IDs.
- **Resolution:** dan-decides

Principle 'IDs are used in the Business tier while human-readable strings are used at the UI boundary'. The hits are PrePostingAgreement.paymentAgreementName/masterAgreementName (31-32; no PaymentAgreementId or MasterAgreementId), PrePostingLine.accountCode/accountName (39-40; no AccountId), ruleName (41; no ClassificationRuleId) and PrePostingEntry.sourceName: JournalRefFinancialInstitution (49; no IngestionSourceId). They are filled at 81 and 99.

**Action:** Carry AccountId, ClassificationRuleId, PaymentAgreementId, MasterAgreementId and IngestionSourceId. The bridge already has PaymentAgreement.idToName, MasterAgreement.idToName and Account.idToCode/idToName.

**Why:** The caches that should do this work already exist.

---

## ARCH-ID-12 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/TrialBalance.fs:12-13, 21-22
- **Summary:** Trial balance rows carry account code and name and no AccountId.
- **Resolution:** dan-decides

Principle 'IDs are used in the Business tier while human-readable strings are used at the UI boundary': 'A report or view row carries the IDs of the things it reports on, not their codes or names.' TrialBalanceRowNested and TrialBalanceRowFlattened have accountCode: AccountCode and accountName: AccountName and no AccountId at all. StageEntryOrchestration.postWithTrialBalances and Reconciliation compare these rows, which can only match on code. The REPORT-RECORDS ruling cites this module for privacy only, and its 'Not covered' note excludes field types.

**Action:** Key the rows by AccountId, and resolve code and name in ReportConverters/TrialBalanceWriter via Account.idToCode/idToName.

**Why:** The ruling on public rows explicitly says nothing about which fields a row carries.

---

## ARCH-ID-13 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/Reconciliation.fs:14-15
- **Summary:** ReconciliationRow identifies the account by code and name with no AccountId.
- **Resolution:** dan-decides

Principle 'IDs are used in the Business tier while human-readable strings are used at the UI boundary'. The fields are accountCode: AccountCode and accountName: AccountName.

**Action:** Carry AccountId and resolve code and name at the bridge.

**Why:** Report rows carry IDs, not names.

---

## ARCH-ID-14 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/PeriodActivity.fs:24-25
- **Summary:** PeriodActivityAccount identifies the account by code and name with no AccountId.
- **Resolution:** dan-decides

Principle 'IDs are used in the Business tier while human-readable strings are used at the UI boundary'. The fields are accountCode: AccountCode and accountName: AccountName.

**Action:** Carry AccountId and resolve code and name at the bridge.

**Why:** Report rows carry IDs, not names.

---

## ARCH-ID-15 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/BalanceSheetIntegrity.fs:14-15
- **Summary:** DeactivatedAccountWithBalance identifies the account by code and name with no AccountId.
- **Resolution:** dan-decides

Principle 'IDs are used in the Business tier while human-readable strings are used at the UI boundary'. The fields are code: AccountCode and accountName: AccountName.

**Action:** Carry AccountId and resolve code and name at the bridge.

**Why:** Report rows carry IDs, not names.

---

## ARCH-ID-16 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/PersonOrchestration.fs:59-64
- **Summary:** personNamesOf turns PersonIds into sorted name strings in the Business tier, feeding the NetWorth, InvestmentAccount and Property views.
- **Resolution:** dan-decides

Principle 'IDs are used in the Business tier while human-readable strings are used at the UI boundary': 'The UserInterfaceBridge turns IDs into codes and names through App.DataAccessLayer.LookupCache.' personNamesOf (Set<PersonId> -> Result<string list>) is that translation done below the bridge. Its callers are NetWorth.fs:198, InvestmentAccountOrchestration.fs:258 and RealEstateOrchestration.fs:257.

**Action:** Remove it from orchestration. Add a Person idToName LookupCache in Person.fs and resolve owners in the converters.

**Why:** It is the mechanism behind several of the name-carrying views above.

---

## ARCH-ID-17 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/PositionsLedgerLinks.fs:24-28
- **Summary:** ledgerAccountCodeAndName resolves an AccountId to its code and name for display in Business-tier views.
- **Resolution:** dan-decides

Principle 'IDs are used in the Business tier while human-readable strings are used at the UI boundary'. The comment says 'A linked ledger account's code and name, for showing the link.' It is used for the InvestmentAccountView and PropertyView fields (InvestmentAccountOrchestration.fs:262, RealEstateOrchestration.fs:261). Its use for error messages (RealEstateOrchestration.fs:100) is allowed under 'Error cases carry primitives', and so is codeOf.

**Action:** Drop its use for views. The bridge has Account.idToCode/idToName.

**Why:** Display translation belongs to the bridge.

---

## ARCH-ID-18 — architecture
- **Location:** Src/Business.FinancialServices.CashFlow/Instance.fs:18, 123, 160
- **Summary:** The Instance entity carries its master agreement's name, joined in by the query 'for read convenience'.
- **Resolution:** dan-decides

Principle 'IDs are used in the Business tier while human-readable strings are used at the UI boundary': '...so does a query below the UserInterfaceBridge that selects a code or name to stand in for an ID.' Line 18 is `masterAgreementName: AgreementName // not separately tracked in the database; here for read convenience`. The query selects ma.agreement_name (160), and mapRawForDbRead reads it (123). The entity already holds masterAgreementID (17). MasterAgreement.idToName (MasterAgreement.fs:329) exists for the bridge.

**Action:** Remove masterAgreementName from Instance and resolve it via MasterAgreement.idToName at the bridge.

**Why:** An entity carrying another entity's name blurs ownership and makes every instance read depend on the master agreement table.

---

## ARCH-ID-19 — architecture
- **Location:** Src/Business.FinancialServices.CashFlow/CashFlowComponent.fs:393, 400, 410-411, 421-422
- **Summary:** The cash flow component rows carry agreement names, payment agreement names and an account code and name in place of IDs.
- **Resolution:** dan-decides

Principle 'IDs are used in the Business tier while human-readable strings are used at the UI boundary'. The hits are PaymentPostingTransition.agreementName: AgreementName (393; no MasterAgreementId), ProjectedInvoice.agreementName (400), ProjectedAccount.accountCode/accountName (410-411; beside accountId), and BillToChase.agreementName/paymentAgreementName (421-422; no MasterAgreementId or PaymentAgreementId). ProjectedAccount's code and name also bring account-domain values into a cash-flow component.

**Action:** Carry MasterAgreementId, PaymentAgreementId and AccountId only, and resolve names in CashFlowConverters via the existing caches.

**Why:** Report and view rows carry IDs, not names.

---

## ARCH-ID-20 — architecture
- **Location:** Src/Business.FinancialServices.Positions/PositionsComponent.fs:433-436
- **Summary:** WealthGroup identifies groups by investment account name, dimension value name and owner names instead of IDs.
- **Resolution:** dan-decides

Principle 'IDs are used in the Business tier while human-readable strings are used at the UI boundary'. `GroupName of string` stands for an investment account or dimension value (InvestmentWealthHistory.fs:19-35), and `GroupOwners of string list` stands for a set of persons. The totals are grouped on those strings, so two accounts with the same name would merge.

**Action:** Group by InvestmentAccountId, DimensionValueId or Set<PersonId> (AccountGroup is an attribute and may stay a value), and resolve labels at the bridge.

**Why:** Grouping by display text is the identity-by-name failure the principle guards against.

---

## ARCH-PRIV-1 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/StageEntryOrchestration.fs:32-41
- **Summary:** DeduplicationResult and AccountClassificationResult are public records returned by write use cases. They are not read-only report outputs.
- **Resolution:** dan-decides

Constraints 'Entity, component, and composite type definitions are private' and 'Access to Entity, component, or composite type fields is strictly through accessor functions'. Both records are public and read by field. They are returned by deduplicateStagedEntries and applyAccountClassification, which write. The ruling REPORT-RECORDS exempts only 'read-only report and view output records', so it does not match.

**Action:** Make both private with accessors, or have Dan extend the exemption to use-case result records.

**Why:** The exemption's scope is read-only outputs, and these come from writes.

---

## ARCH-PRIV-2 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/InstanceOrchestration.fs:37-44, 412-440
- **Summary:** PaymentAgreementClassificationResult, InvoiceCompositeUpdate and InstanceCompositeUpdate are public composite records.
- **Resolution:** dan-decides

Constraint 'Entity, component, and composite type definitions are private'. PaymentAgreementClassificationResult is the result of a classification write run. The two *CompositeUpdate records are write inputs built by the bridge (CashFlowRoutes.fs:111). InvoiceComposite and InstanceComposite in the same file are private, so the module follows the rule in part. REPORT-RECORDS does not cover write results or inputs.

**Action:** Make them private with constructors and accessors, or have Dan rule on use-case result and input records.

**Why:** The exemption's scope is read-only outputs only.

---

## ARCH-PRIV-3 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/AccountSnapshotOrchestration.fs:39-42; InvestmentActivityOrchestration.fs:31-37; PreLedgerBalanceOrchestration.fs:25-28
- **Summary:** RecordedSnapshot, RecordedRange and RecordedPreLedgerBalance are public records returned by record (write) operations.
- **Resolution:** dan-decides

Constraint 'Entity, component, and composite type definitions are private'. They are returned by recordSnapshots, recordActivity and recordPreLedgerBalances, which write, and they carry replacedExisting/removed/recorded. They are not read-only report outputs, so REPORT-RECORDS does not match. (These are listed together because they are one pattern from one slice, in three files.)

**Action:** Make them private with accessors, or have Dan extend the exemption to write results.

**Why:** The exemption's scope is read-only outputs only.

---

## ARCH-PRIV-4 — architecture
- **Location:** Src/Business.FinancialServices.Classification/ClassificationComponent.fs:59, 117, 131, 143, 148, 153, 177
- **Summary:** Seven classification component records are public and read by field.
- **Resolution:** dan-decides

Constraints 'Entity, component, and composite type definitions are private' and 'Access to ... component ... type fields is strictly through accessor functions'. The public records are MoneySearchPattern (59), MatchCandidate (117), PrioritizedMatch (131), ClassificationResult (143), ClassificationRun (148), PaymentAgreementClaimCluster (153) and PaymentAgreementDecision (177). They are inputs to and results of classification runs, which write rule_match rows, so they are not read-only report outputs.

**Action:** Make them private with accessors, or have Dan rule on classifier working types.

**Why:** Component types are named in the constraint, and no ruling exempts them.

---

## ARCH-PRIV-5 — architecture
- **Location:** Src/Business.FinancialServices.CashFlow/CashFlowComponent.fs:386-396
- **Summary:** The InvoiceDecision and PaymentPostingTransition component records are public, and both come from write operations.
- **Resolution:** dan-decides

Constraint 'Entity, component, and composite type definitions are private'. InvoiceDecision is in the payment-agreement classification decision log, and PaymentPostingTransition is returned by transitionPaymentsToPosted. The projection rows (ProjectedInvoice, ProjectedAccount, BillToChase, CashFlowProjection) are read-only outputs, so REPORT-RECORDS covers their privacy, and they are not counted here.

**Action:** Make the two private with accessors.

**Why:** The exemption covers read-only outputs only.

---

## ARCH-CMOD-1 — architecture
- **Location:** Src/Business.FinancialServices.DataIngestion/BaseStageEntry.fs:11, 25
- **Summary:** The component types BaseStageEntryGroupId and BaseStageRawRow are defined in BaseStageEntry.fs, not in the DataIngestion components module, StageEntryComponent.
- **Resolution:** dan-decides

Constraint 'Component types are defined in a components module in the lowest tier of their business domain'. BaseStageEntryGroupId (11) is a private single-case wrapper with smart construction, and BaseStageRawRow (25) is a public record of domain types. Neither is an entity. StageEntryComponent.fs compiles before BaseStageEntry.fs, and these types reference only types visible there (Money, the JE component types, AccountId).

**Action:** Move both into StageEntryComponent (and make BaseStageRawRow private if it is not a read-only output), or have Dan confirm that BaseStageEntry is a components module in its own right.

**Why:** Component vocabulary scattered outside the components module weakens the placement rule that the 2026-10-11 sweep now enforces.

---

## ARCH-CMOD-2 — architecture
- **Location:** Src/Business.FinancialServices.Classification/ClassificationRule.fs:64-77
- **Summary:** The public records StoredFieldMatch and StoredRuleGroup are defined in the ClassificationRule entity module, and their fields are bare strings and decimal.
- **Resolution:** dan-decides

Constraint 'Component types are defined in a components module in the lowest tier of their business domain'. Both are declared in the entity module. Their doc comment (64-66) explains that they are the JSON shape written to classification_rule.rule_groups: 'plain strings and numbers under fixed names, and every value goes back through its smart constructor on read'. That justifies their primitive fields as a persistence shape, much as the raw tuple in mapRawForDbRead is one. It does not explain their placement or why they are public.

**Action:** Make them private to ClassificationRule.fs (they are used only by its to/from stored conversions), or move them to ClassificationComponent.

**Why:** Public persistence-shape types with primitive fields invite the Business tier to pass rule groups around unvalidated.

---


