module Model.UI.BoundaryConverters.OrchestrationConverters

open Model.UI.BoundaryConverters.AccountFieldConverters
open Model.UI.BoundaryConverters.JournalEntryFieldConverters
open Model.UI.BoundaryConverters.MoneyFieldConverters
open Model.UI.InterfaceContractTypes
open ModelOrchestrator
open Utilities.ResultCE
open Model.AccountActivity



let convertAccountActivityFetchInputToAccountActivityFetch
        (input:AccountActivityFetchInput)
        : Result<AccountActivity.AccountActivity.AccountActivityFilter, string>  =
    result {
        let! accountId = // REQ-NGUI-1.5
            input.filter.accountCode |> convertAccountCodeStringOptionToAccountIdOption
        let! accountParentId = // REQ-NGUI-1.5
            input.filter.accountParentCode |> convertAccountCodeStringOptionToAccountIdOption
        let! accountType =
            input.filter.accountType |> convertAccountTypeStringOptionToAccountTypeOption
        let! accountSubtype =
            input.filter.accountSubtype |> convertAccountSubtypeStringOptionToAccountSubtypeOption
        let! amount =
            input.filter.amount |> convertDecimalOptionToMoneyOption
        let! description =
            input.filter.description |> convertJeDescriptionStringOptionToJeDescriptionOption
        let! source =
            input.filter.source |> convertJeSourceStringOptionToJeSourceOption
        let! (temporalFilter : AccountActivity.AccountActivityTemporalFilter option) =
            match input.filter.temporalFilter with
            | None -> Ok None
            | Some (DateRange dateRange) ->
                Ok ( Some ( AccountActivityTemporalFilter.DateRange {
                    beginDate = dateRange.beginDate
                    endInclusive = dateRange.endInclusive }))
            | Some (PeriodKey periodKey) ->
                periodKey
                |> FiscalPeriodKey.value
                |> LookupCache.fiscalPeriodKeyToId.fetch
                |> Result.map ( fun pkUuid -> Some (
                    pkUuid |> FiscalPeriodId.fromGuid |> AccountActivityTemporalFilter.FiscalPeriodIdentifier ) )
        let sort = input.sort |> Option.map (fun x -> 
            match x with
            | AccountCode -> AccountActivitySort.AccountCode
            | EntryDate -> AccountActivitySort.EntryDate )
        {
                   accountId = accountId
                   temporalFilter = temporalFilter
                   source = source
                   accountType = accountType
                   accountSubtype = accountSubtype
                   accountParentId = accountParentId
                   journalEntryId = input.filter.journalEntryId
                   amount = amount
                   description = description
                   unVoidedOnly = input.filter.unVoidedOnly } }
