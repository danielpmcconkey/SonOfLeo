module ModelOrchestrator.FetchFilters

open Model
open Model.Ledger
open Model.CashFlow.CashFlowComponent
open Model.Ledger.AccountComponent
open Model.Ledger.FiscalPeriodComponent
open Model.Ledger.JournalEntryComponent
open NodaTime
open Utilities.AppError
open Utilities.ResultHelper
open DataAccessLayer.QueryParameters
open Model.DataIngestion.Classification.ClassificationRuleComponent
open Model.DataIngestion.StageEntryComponent

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
      paymentAgreementAtMatch: PaymentAgreementId option
      claimantType: ClassificationClaimantType option
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
      paymentAgreementId: PaymentAgreementId option
      memo: JournalEntryLineMemo option
      accountClassificationRuleId: ClassificationRuleId option
      paymentClassificationRuleId: ClassificationRuleId option }

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

let getDateRangeFromTemporalFilter 
    (context: Context.Context)
    (temporalFilter: TemporalFilter)
    : Result<FilterDateRange, AppError>=
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

let createAgreementNamesPredicateAndParameters
    (agreementNamesListOption: AgreementName list option) 
    : string option * QueryParameter list =
    let parameterPrefix = "agreement_name"
    let names = agreementNamesListOption |> Option.defaultValue []
    let filters =
        [ 1 .. (names |> List.length) ]
        |> List.zip names
        |> List.map(fun (name, iterator) ->
            let likeStr = $"%%{name |> AgreementName.value}%%"
            let paramName = $"@{parameterPrefix}_{iterator}"
            ($"ma.agreement_name like {paramName}",
             { name = paramName; value = CharString likeStr }))
    let predicate =
        if agreementNamesListOption |> Option.isNone
        then None
        else
            let innerString =
                filters
                |> List.map fst
                |> String.concat $"{System.Environment.NewLine}    or "
            $"({innerString})" |> Some
    let parameters = (filters |> List.map snd)
    predicate, parameters

let createAmountPredicateAndParameters
    (parameterPrefix: string)
    (columnReference: string)
    (amountFilterOption: AmountFilter option)
    : string option * QueryParameter list =
    match amountFilterOption with
    | None -> None, []
    | Some amountFilter -> 
        let filters = 
            match amountFilter with
            | AmountRange range ->
                let min = range.inclusiveFloor |> Money.amount
                let max = range.inclusiveCeiling |> Money.amount
                [
                    ($"{columnReference} >= @{parameterPrefix}_min",
                     { name = $"@{parameterPrefix}_min"; value = Numeric min })
                    ($"{columnReference} >= @{parameterPrefix}_max",
                     { name = $"@{parameterPrefix}_max"; value = Numeric max })
                ]
            | ExactAmount amount -> 
                let amountDec = amount |> Money.amount
                [
                    ($"{columnReference} = @{parameterPrefix}",
                     { name = $"@{parameterPrefix}"; value = Numeric amountDec })
                ]
        let predicate =
            filters
            |> List.map fst
            |> String.concat $"{System.Environment.NewLine}and "
            |> Some
        let parameters = (filters |> List.map snd)
        predicate, parameters

let createTemporalPredicateAndParameters
    (context: Context.Context)
    (parameterPrefix: string)
    (columnReference: string)
    (temporalFilterOption: TemporalFilter option)
    : Result<string option * QueryParameter list, AppError> =
    if temporalFilterOption |> Option.isNone then Ok (None, []) else
    result {
        let! filterDateRange =
            temporalFilterOption
            |> Option.get
            |> getDateRangeFromTemporalFilter context
        let predicate =
            $"({columnReference} >= @{parameterPrefix}_begin and {columnReference} <= @{parameterPrefix}_end_inclusive)"
            |> Some
        let parameters =
            [
                { name = $"@{parameterPrefix}_begin"; value = DbLocalDate filterDateRange.beginDate }
                { name = $"@{parameterPrefix}_end"; value = DbLocalDate filterDateRange.endInclusive }
            ]
        return predicate, parameters
    }

let createBasicPredicateAndParameters<'T>
    (valueFunc: 'T -> QueryParameterValue)
    (parameterName: string)
    (columnReference: string)
    (basicFilterOption: 'T option)
    : string option * QueryParameter list =
    if basicFilterOption |> Option.isNone then None, [] else
    let nonPrimitiveValue = basicFilterOption |> Option.get
    let predicate = $"{columnReference} = @{parameterName}" |> Some
    let parameters =
        [
            { name = $"@{parameterName}"; value = nonPrimitiveValue |> valueFunc }
        ]
    predicate, parameters

let createStringLikePredicateAndParameters<'T>
    (valueFunc: 'T -> string)
    (parameterName: string)
    (columnReference: string)
    (stringLikeFilterOption: 'T option)
    : string option * QueryParameter list =
    if stringLikeFilterOption |> Option.isNone then None, [] else
    let nonPrimitiveValue = stringLikeFilterOption |> Option.get
    let predicate = $"{columnReference} like @{parameterName}" |> Some    
    let stringVal = $"%%{nonPrimitiveValue |> valueFunc}%%"
    let parameters =
        [
            { name = $"@{parameterName}"; value = CharString stringVal }
        ]
    predicate, parameters
    
