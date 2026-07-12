module InterfaceBridge.BoundaryConverters.OrchestrationConverters

open InterfaceBridge.BoundaryConverters.AccountFieldConverters
open InterfaceBridge.BoundaryConverters.GenericFieldHelpers
open InterfaceBridge.BoundaryConverters.JournalEntryFieldConverters
open InterfaceBridge.BoundaryConverters.MoneyFieldConverters
open InterfaceBridge.InterfaceContracts.AccountContracts
open Model
open Model.Ledger.FiscalPeriods
open Model.Ledger.Journaling.JournalEntryComponent
open ModelOrchestrator.AccountActivity
open Utilities
open Utilities.ResultCE
open Model.Ledger.Accounts.AccountComponent


let convertAccountActivityTemporalFilterInputToAccountActivityTemporalFilter
        (input:AccountActivityTemporalFilterInput)
        : Result<AccountActivityTemporalFilter, string>  =
        
    match input with
    | AccountActivityTemporalFilterInput.DateRange dateRange ->
        Ok ( AccountActivityTemporalFilter.DateRange {
            beginDate = dateRange.beginDate
            endInclusive = dateRange.endInclusive })
    |  AccountActivityTemporalFilterInput.PeriodKey periodKey ->
        result {
            let! uuid = periodKey |> LookupCache.fiscalPeriodKeyToId.fetch
            return
                uuid
                |> FiscalPeriodId.fromGuid
                |> AccountActivityTemporalFilter.FiscalPeriodIdentifier }
let convertAccountActivityTemporalFilterInputOptionToAccountActivityTemporalFilterOption
        (input:AccountActivityTemporalFilterInput option)
        : Result<AccountActivityTemporalFilter option, string>  =
    let fallibleConverter = (fun x -> x |> convertAccountActivityTemporalFilterInputToAccountActivityTemporalFilter)
    input
    |> convertOptionToDesiredTypeWithFallibleConverter fallibleConverter
    
let convertAccountActivityFilterInputToAccountActivityFilter
        (input:AccountActivityFilterInput)
        : Result<AccountActivityFilter, string>  =
    result {
        let! accountId = // REQ-NGUI-1.5
            input.accountCode |> convertAccountCodeStringOptionToAccountIdOption
        let! accountParentId = // REQ-NGUI-1.5
            input.accountParentCode |> convertAccountCodeStringOptionToAccountIdOption
        let! accountType =
            input.accountType |> convertAccountTypeStringOptionToAccountTypeOption
        let! accountSubtype =
            input.accountSubtype |> convertAccountSubtypeStringOptionToAccountSubtypeOption
        let! amount =
            input.amount |> convertDecimalOptionToMoneyOption
        let! description =
            input.description |> convertJeDescriptionStringOptionToJeDescriptionOption
        let! source =
            input.source |> convertJeSourceStringOptionToJeSourceOption
        let! temporalFilter =
            input.temporalFilter
            |> convertAccountActivityTemporalFilterInputOptionToAccountActivityTemporalFilterOption
        return {
                   accountId = accountId
                   temporalFilter = temporalFilter
                   source = source
                   accountType = accountType
                   accountSubtype = accountSubtype
                   accountParentId = accountParentId
                   journalEntryId = input.journalEntryId
                   amount = amount
                   description = description
                   unVoidedOnly = input.unVoidedOnly } }

let convertAccountActivityDetailToAccountActivityDetailReturn
        (input:AccountActivityDetail)
        : AccountActivityDetailReturn = {
            lineId = input.lineId
            amount = input.amount |> Money.amount
            lineType = input.lineType |> JournalEntryLineType.toString 
            lineMemo = input.lineMemo |> Option.map(LineMemo.value)
            lineCreatedAt = input.lineCreatedAt
            lineModifiedAt = input.lineModifiedAt
            journalEntryId = input.journalEntryId
            entryDate = input.entryDate
            journalEntryDescription = input.journalEntryDescription |> JournalEntryDescription.value
            journalEntrySource = input.journalEntrySource |> Option.map(JournalEntrySource.value)
            journalEntryVoidedAt = input.journalEntryVoidedAt }

let convertAccountActivityToAccountActivityReturn
        (input:AccountActivity)
        : Result<AccountActivityReturn,string> =
    result {
        let! parentCodeOptionId = input.accountParentId |> convertAccountIdOptionToAccountCodeOption // REQ-NGUI-1.5
        let parentCodeOptionString = parentCodeOptionId |> Option.map(AccountCode.value)
        let detail = input.activityDetail |> Option.map(convertAccountActivityDetailToAccountActivityDetailReturn)
        return {  accountCode = input.accountCode |> AccountCode.value
                  accountName = input.accountName |> AccountName.value
                  accountType = input.accountType |> AccountType.toString
                  accountSubtype = input.accountSubtype |> Option.map(AccountSubtype.toString)
                  accountParentCode = parentCodeOptionString
                  accountExternalRef = input.accountExternalRef |> Option.map(AccountExternalReference.value)
                  activityDetail = detail }
    } 

let convertAccountActivityListToAccountActivityReturnList
        (input:AccountActivity list)
        : Result<AccountActivityReturn list,string> =
    input
    |> List.map (fun x -> x |> convertAccountActivityToAccountActivityReturn)
    |> ListHelper.listOfResultsToResultsList