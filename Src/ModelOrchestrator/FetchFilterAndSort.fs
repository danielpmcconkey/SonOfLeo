module ModelOrchestrator.FetchFilters

open Model
open Model.DataIngestion
open Model.DataIngestion.Classification
open Model.Ledger.Accounts.AccountComponent
open Model.Ledger.FiscalPeriods
open Model.Ledger.Journaling.JournalEntryComponent
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
