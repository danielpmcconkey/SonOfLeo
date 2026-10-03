module Business.CrossDomainOrchestration.FetchFilters

open NodaTime
open App.Utility.IAppError
open App.Utility.Result
open App.DataAccessLayer.QueryParameter
open App.Session
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Ledger.FiscalPeriodComponent
open Business.FinancialServices.Ledger.JournalEntryComponent
open Business.FinancialServices.DataIngestion.StageEntryComponent
open Business.FinancialServices.CashFlow.CashFlowComponent
open Business.FinancialServices.Classification.ClassificationComponent

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
      amount: Money.Money option
      description: JournalEntryDescription option
      unVoidedOnly: bool }

type JournalEntryFetchFilter =
    { journalEntryHeaderId: JournalEntryHeaderId option
      financialInstitution: JournalRefFinancialInstitution option
      referenceText: JournalExternalReferenceText option
      temporalFilter: TemporalFilter option }

type ClassificationRuleFilter =
    { ruleId: ClassificationRuleId option
      nameLike: ClassificationRuleName option
      accountAtMatch: AccountId option
      paymentAgreementAtMatch: PaymentAgreementId option
      claimantType: ClassificationClaimantType option
      sourceLike: JournalRefFinancialInstitution option
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
      amount: Money.Money option
      lineType: JournalEntryLineType option
      accountId: AccountId option
      memo: JournalEntryLineMemo option
      journalEntryHeaderId: JournalEntryHeaderId option
      journalEntryLineId: JournalEntryLineId option }

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
    activeAgreementsOnly: bool
}

let getDateRangeFromTemporalFilter 
    (context: Context.Context)
    (temporalFilter: TemporalFilter)
    : Result<FilterDateRange, IAppError>=
    match temporalFilter with
    | DateRange dr -> Ok dr
    | FiscalPeriodIdentifier fpId ->
        fpId
        |> FiscalPeriod.fetchById context
        |> Result.map(fun fp ->
            let beginDate = fp |> FiscalPeriod.startDate
            let endDate = fp |> FiscalPeriod.endDate
            { beginDate = beginDate; endInclusive = endDate})

let createIdPredicateAndParameters<'T>
    (valueFunc: 'T -> System.Guid)
    (parameterPrefix: string)
    (columnReferences: string list)
    (idListOption: 'T list option)
    : string option * QueryParameter list =
    let ids = idListOption |> Option.defaultValue []
    let filters =
        [ 1 .. (ids |> List.length) ]
        |> List.zip ids
        |> List.map(fun (id, iterator) ->
            let uuid = id |> valueFunc
            ($"@{parameterPrefix}_{iterator}", { name = $"@{parameterPrefix}_{iterator}"; value = UniqueId uuid }))
    let idsInString = filters |> List.map fst |> String.concat ", "
    let predicate =
        if idListOption |> Option.isNone
        then None
        else
            let innerString = 
                columnReferences
                |> List.map (fun columnReference -> $"{columnReference} in ({idsInString})")
                |> String.concat $"{System.Environment.NewLine}or "
            $"({innerString})" |> Some
    let parameters = (filters |> List.map snd)
    predicate, parameters
