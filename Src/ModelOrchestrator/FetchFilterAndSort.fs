module ModelOrchestrator.FetchFilters

open Model
open Model.CashFlow.CashFlowComponent
open Model.DataIngestion
open Model.DataIngestion.Classification
open Model.Ledger.Accounts.AccountComponent
open Model.Ledger.FiscalPeriods
open Model.Ledger.Journaling.JournalEntryComponent
open Model.CashFlow
open NodaTime

type FetchSort =
    | AccountCodeAsc
    | AccountCodeDesc
    | EntryDateAsc
    | EntryDateDesc
    | AmountAsc
    | AmountDesc

type FilterDateRange = { beginDate: LocalDate; endInclusive: LocalDate }

type TemporalFilter =
    | FiscalPeriodIdentifier of FiscalPeriodId
    | DateRange of FilterDateRange

type AmountRange = { inclusiveFloor: Money; inclusiveCeiling: Money }
type AmountFilter =
    | ExactAmount of Money
    | AmountRange of AmountRange

type AccountActivityFilter =
    { accountId: AccountId option
      temporalFilter: TemporalFilter option
      source: JournalEntrySource option
      accountType: AccountType option
      accountSubtype: AccountSubtype option
      accountParentId: AccountId option
      journalEntryId: JournalEntryHeaderId option
      amount: Money option
      description: JournalEntryDescription option
      unVoidedOnly: bool }

type JournalEntryFetchFilter =
    { journalEntryHeaderId: JournalEntryHeaderId option
      source: JournalEntrySource option
      financialInstitution: JournalRefFinancialInstitution option
      referenceText: JournalExternalReferenceText option
      temporalFilter: TemporalFilter option
      unVoidedOnly: bool }

type ClassificationRuleFilter =
    { ruleId: ClassificationRuleId option
      nameLike: ClassificationRuleName option
      accountAtMatch: AccountId option
      sourceLike: string option
      activeOnly: bool }

type FetchSortClassificationRule =
    | AccountCodeAsc
    | AccountCodeDesc
    | PriorityAsc
    | PriorityDesc

type StageEntryFetchFilter =
    { stageEntryHeaderId : StageEntryHeaderId option
      sourceFile: SourceFile option
      temporalFilter: TemporalFilter option
      description: JournalEntryDescription option
      ingestionSource: JournalRefFinancialInstitution option
      fiReference: JournalExternalReferenceText option
      status: StagedEntryStatus option
      stageEntryLineId: StageEntryLineId option
      amount: Money option
      lineType: JournalEntryLineType option
      accountId: AccountId option
      memo: JournalEntryLineMemo option
      classificationRuleId: ClassificationRuleId option }

type FetchStageEntrySort =
    | EntryDateAsc
    | EntryDateDesc
    | FiAsc
    | FiDesc
    | StatusAsc
    | StatusDesc
    | DescriptionAsc
    | DescriptionDesc

type AgreementFilter = {
    agreementIds: MasterAgreementId list option
    agreementNames: AgreementName list option
    direction: FlowDirection option
    activeAgreementsOnly: bool // show only those whose end dates are >= today
    accountIds: AccountId list option // either payment agreement debit or payment agreement credit
    paymentAgreementExpectedAmount: AmountFilter option
    instanceTemporalFilter: TemporalFilter option
    externalInvoiceId: ExternalInvoiceId option
    invoiceDateTemporalFilter: TemporalFilter option
    invoiceDueTemporalFilter: TemporalFilter option
    invoiceAmount: AmountFilter option
    invoiceState: InvoiceState option
    invoicePaymentState: PaymentState option
    invoicePostedState: PostedState option
    invoiceBlocker: Blocker option
    journalEntryHeaderId: JournalEntryHeaderId option
    stageEntryHeaderId: StageEntryHeaderId option
    paymentAmount: AmountFilter option
    paymentPostedToLedgerTemporalFilter: TemporalFilter option
}
