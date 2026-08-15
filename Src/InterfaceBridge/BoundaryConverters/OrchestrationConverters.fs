module InterfaceBridge.BoundaryConverters.OrchestrationConverters

open InterfaceBridge.BoundaryConverters.AccountFieldConverters
open InterfaceBridge.BoundaryConverters.JournalEntryFieldConverters
open InterfaceBridge.BoundaryConverters.MoneyFieldConverters
open InterfaceBridge.InterfaceContracts.AccountContracts
open InterfaceBridge.InterfaceContracts.SharedContracts
open Model
open Model.Ledger.FiscalPeriods
open Model.Ledger.Journaling.JournalEntryComponent
open ModelOrchestrator.AccountActivity
open ModelOrchestrator.FetchFilters
open Utilities.AppError
open Model.Ledger.Accounts.AccountComponent
open Utilities.ResultHelper


let ``convert TemporalFilterInput to TemporalFilter``
    (context: Context.Context)
    (input: TemporalFilterInput)
    : Result<TemporalFilter, AppError> =

    match input with
    | TemporalFilterInput.DateRange dateRange ->
        Ok(TemporalFilter.DateRange { beginDate = dateRange.beginDate; endInclusive = dateRange.endInclusive })
    | TemporalFilterInput.PeriodKey periodKey ->
        result {
            // make sure it's a valid string for even being a period key
            let! _ = periodKey |> FiscalPeriodKey.fromString
            let! uuid =
                periodKey
                |> LookupCache.fiscalPeriodKeyToId.fetch context
                |> Result.mapError(fun _ -> FiscalPeriodNoPeriodMatchingKey periodKey)
            return uuid |> FiscalPeriodId.fromGuid |> TemporalFilter.FiscalPeriodIdentifier
        }
let ``convert TemporalFilterInput Option To TemporalFilter Option``
    (context: Context.Context)
    (input: TemporalFilterInput option)
    : Result<TemporalFilter option, AppError> =
    let fallibleConverter = (fun x -> x |> ``convert TemporalFilterInput to TemporalFilter`` context)
    input |> convertOptionToDesiredTypeWithFallibleConverter fallibleConverter

let ``convert AccountActivityFilterInput to AccountActivityFilter``
    (context: Context.Context)
    (input: AccountActivityFilterInput)
    : Result<AccountActivityFilter, AppError> =
    result {
        let! accountId =
            input.accountCode |> ``convert AccountCodeString Option to AccountId Option`` context
        let! accountParentId =
            match input.accountParentCode |> ``convert AccountCodeString Option to AccountId Option`` context with
            | Ok x -> Ok x
            | Error(AccountCodeIsEmpty codeString) -> Error(AccountParentCodeIsEmpty codeString)
            | Error(AccountCodeTooLong (codeString, max)) -> Error(AccountParentCodeTooLong (codeString, max))
            | Error(AccountCodeDoesntMatchAccountId codeString) -> Error(AccountParentCodeInvalid codeString)
            | Error e -> Error e
        let! accountType = input.accountType |> ``convert AccountTypeString Option to AccountType Option``
        let! accountSubtype = input.accountSubtype |> ``convert AccountSubtypeString Option to AccountSubtype Option``
        let! amount = input.amount |> ``convert Decimal Option to Money Option``
        let! description = input.description |> ``convert JeDescriptionString Option to JeDescription Option``
        let! source = input.source |> ``convert JeSourceString Option to JeSource Option``
        let! temporalFilter =
            input.temporalFilter |> ``convert TemporalFilterInput Option To TemporalFilter Option`` context
        return
            { accountId = accountId
              temporalFilter = temporalFilter
              source = source
              accountType = accountType
              accountSubtype = accountSubtype
              accountParentId = accountParentId
              journalEntryId = input.journalEntryId |> Option.map(JournalEntryHeaderId.fromGuid)
              amount = amount
              description = description
              unVoidedOnly = input.unVoidedOnly }
    }

let ``convert AccountActivityDetail to AccountActivityDetailReturn``
    (input: AccountActivityDetail)
    : AccountActivityDetailReturn =
    { lineId = input.lineId |> JournalEntryLineId.value
      amount = input.amount |> Money.amount
      lineType = input.lineType |> JournalEntryLineType.toString
      lineMemo = input.lineMemo |> Option.map(JournalEntryLineMemo.value)
      lineCreatedAt = input.lineCreatedAt
      lineModifiedAt = input.lineModifiedAt
      journalEntryId = input.journalEntryHeaderId |> JournalEntryHeaderId.value
      entryDate = input.entryDate
      journalEntryDescription = input.journalEntryDescription |> JournalEntryDescription.value
      journalEntrySource = input.journalEntrySource |> Option.map(JournalEntrySource.value)
      journalEntryVoidedAt = input.journalEntryVoidedAt }

let ``convert AccountActivity to AccountActivityReturn``
    (context: Context.Context)
    (input: AccountActivity)
    : Result<AccountActivityReturn, AppError> =
    result {
        let! parentCodeOptionId = input.accountParentId |> ``convert AccountId Option to AccountCode Option`` context
        let parentCodeOptionString = parentCodeOptionId |> Option.map(AccountCode.value)
        let detail =
            input.activityDetail |> Option.map(``convert AccountActivityDetail to AccountActivityDetailReturn``)
        return
            { accountCode = input.accountCode |> AccountCode.value
              accountName = input.accountName |> AccountName.value
              accountType = input.accountType |> AccountType.toString
              accountSubtype = input.accountSubtype |> Option.map(AccountSubtype.toString)
              accountParentCode = parentCodeOptionString
              accountExternalRef = input.accountExternalRef |> Option.map(AccountExternalReference.value)
              activityDetail = detail }
    }

let ``convert AccountActivity List to AccountActivityReturn List``
    (context: Context.Context)
    (input: AccountActivity list)
    : Result<AccountActivityReturn list, AppError> =
    input
    |> List.map(fun x -> x |> ``convert AccountActivity to AccountActivityReturn`` context)
    |> convertListOfResultsToResultsList
