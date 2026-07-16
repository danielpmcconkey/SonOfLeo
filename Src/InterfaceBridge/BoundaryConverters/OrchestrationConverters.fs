module InterfaceBridge.BoundaryConverters.OrchestrationConverters

open InterfaceBridge.BoundaryConverters.AccountFieldConverters
open InterfaceBridge.BoundaryConverters.GenericFieldHelpers
open InterfaceBridge.BoundaryConverters.JournalEntryFieldConverters
open InterfaceBridge.BoundaryConverters.MoneyFieldConverters
open InterfaceBridge.InterfaceContracts.AccountContracts
open InterfaceBridge.InterfaceContracts.JournalContracts
open Model
open Model.Ledger.FiscalPeriods
open Model.Ledger.JournalEntryPrimitives
open Model.Ledger.Journaling.JournalEntryComponent
open ModelOrchestrator.AccountActivity
open Utilities
open Utilities.ResultCE
open Model.Ledger.Accounts.AccountComponent


let ``convert AccountActivityTemporalFilterInput to AccountActivityTemporalFilter``
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
let ``convert AccountActivityTemporalFilterInput Option To AccountActivityTemporalFilter Option``
        (input:AccountActivityTemporalFilterInput option)
        : Result<AccountActivityTemporalFilter option, string>  =
    let fallibleConverter = (fun x -> x |> ``convert AccountActivityTemporalFilterInput to AccountActivityTemporalFilter``)
    input
    |> ``convert Option to Desired Type with Fallible Converter`` fallibleConverter
    
let ``convert AccountActivityFilterInput to AccountActivityFilter``
        (input:AccountActivityFilterInput)
        : Result<AccountActivityFilter, string>  =
    result {
        let! accountId = // REQ-NGUI-1.5
            input.accountCode |> ``convert AccountCodeString Option to AccountId Option``
        let! accountParentId = // REQ-NGUI-1.5
            input.accountParentCode |> ``convert AccountCodeString Option to AccountId Option``
        let! accountType =
            input.accountType |> ``convert AccountTypeString Option to AccountType Option``
        let! accountSubtype =
            input.accountSubtype |> ``convert AccountSubtypeString Option to AccountSubtype Option``
        let! amount =
            input.amount |> ``convert Decimal Option to Money Option``
        let! description =
            input.description |> ``convert JeDescriptionString Option to JeDescription Option``
        let! source =
            input.source |> ``convert JeSourceString Option to JeSource Option``
        let! temporalFilter =
            input.temporalFilter
            |> ``convert AccountActivityTemporalFilterInput Option To AccountActivityTemporalFilter Option``
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

let ``convert AccountActivityDetail to AccountActivityDetailReturn``
        (input:AccountActivityDetail)
        : AccountActivityDetailReturn = {
            lineId = input.lineId
            amount = input.amount |> Money.amount
            lineType = input.lineType |> JournalEntryLineType.toString 
            lineMemo = input.lineMemo |> Option.map(JournalEntryLineMemo.value)
            lineCreatedAt = input.lineCreatedAt
            lineModifiedAt = input.lineModifiedAt
            journalEntryId = input.journalEntryId
            entryDate = input.entryDate
            journalEntryDescription = input.journalEntryDescription |> JournalEntryDescription.value
            journalEntrySource = input.journalEntrySource |> Option.map(JournalEntrySource.value)
            journalEntryVoidedAt = input.journalEntryVoidedAt }

let ``convert AccountActivity to AccountActivityReturn``
        (input:AccountActivity)
        : Result<AccountActivityReturn, AppError> =
    result {
        let! parentCodeOptionId = input.accountParentId |> ``convert AccountId Option to AccountCode Option`` // REQ-NGUI-1.5
        let parentCodeOptionString = parentCodeOptionId |> Option.map(AccountCode.value)
        let detail = input.activityDetail |> Option.map(``convert AccountActivityDetail to AccountActivityDetailReturn``)
        return {  accountCode = input.accountCode |> AccountCode.value
                  accountName = input.accountName |> AccountName.value
                  accountType = input.accountType |> AccountType.toString
                  accountSubtype = input.accountSubtype |> Option.map(AccountSubtype.toString)
                  accountParentCode = parentCodeOptionString
                  accountExternalRef = input.accountExternalRef |> Option.map(AccountExternalReference.value)
                  activityDetail = detail }
    } 

let ``convert AccountActivity List to AccountActivityReturn List``
        (input:AccountActivity list)
        : Result<AccountActivityReturn list, AppError> =
    input
    |> List.map (fun x -> x |> ``convert AccountActivity to AccountActivityReturn``)
    |> ListHelper.listOfResultsToResultsList