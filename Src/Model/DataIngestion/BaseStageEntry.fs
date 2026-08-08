namespace Model.DataIngestion

open Model
open Model.Ledger.Accounts.AccountComponent
open Model.Ledger.Journaling.JournalEntryComponent
open NodaTime
open Utilities.AppError
open System
open Utilities.ResultHelper

type BaseStageEntryGroupId = private BaseStageEntryGroupId of string

module BaseStageEntryGroupId =
    let maxLength = 36 // accommodates a uuid if needed
    let value (BaseStageEntryGroupId gid) = gid 
    let create (raw: string) : Result<BaseStageEntryGroupId, AppError> =
        let trimmed = raw.Trim()
        if String.IsNullOrWhiteSpace trimmed then
            Error(IngestionBaseStageEntryGroupIdIsEmpty raw)
        elif trimmed.Length > maxLength then
            Error(IngestionBaseStageEntryGroupIdTooLong(raw, maxLength))
        else
            Ok(BaseStageEntryGroupId trimmed)
                
type BaseStageEntryLine = private {
        amount : Money
        lineType : JournalEntryLineType
        accountCode: AccountCode option
        memo: JournalEntryLineMemo option
    }

module BaseStageEntryLine =
    
    let confirmAmountIsPositive (m: Money) : Result<unit, AppError> =
        if m |> Money.amount <= 0M
        then Error(IngestionBaseStageEntryNonPositiveAmount(m |> Money.amount))
        else Ok()
    
    let create
        (amount : Money)
        (entryType : JournalEntryLineType)
        (accountCode: AccountCode option)
        (memo: JournalEntryLineMemo option)
        : Result<BaseStageEntryLine, AppError> =
        match amount |> confirmAmountIsPositive with
        | Error e -> Error e
        | Ok _ -> Ok {
                    amount = amount
                    lineType = entryType
                    accountCode = accountCode
                    memo = memo }
                
type BaseStageEntryGroup =
    private {
        baseStageEntryGroupId : BaseStageEntryGroupId
        entryDate : LocalDate
        description: JournalEntryDescription
        fiSource: JournalRefFinancialInstitution
        fiReference: JournalExternalReferenceText
        entries: BaseStageEntryLine list
    }

module BaseStageEntryGroup =
    
    let baseStageEntryGroupId g = g.baseStageEntryGroupId
    let entryDate g = g.entryDate
    let description g = g.description
    let fiSource g = g.fiSource
    let fiReference g = g.fiReference
    let entries g = g.entries

    let sumEntriesByType
        (debitOrCredit: JournalEntryLineType)
        (entries: BaseStageEntryLine list)
        : Result<Money, AppError> =
        entries
        |> List.filter(fun x -> x.lineType = debitOrCredit)
        |> List.map(_.amount) |> Money.sumList
        
    let private confirmAmountEquality (entries: BaseStageEntryLine list) : Result<unit, AppError> =
        result {
            let! totalDebits = entries |> sumEntriesByType Debit
            let! totalCredits = entries |> sumEntriesByType Credit
            return!
                if totalCredits = totalDebits then
                    Ok()
                else
                    Error(IngestionBaseStageGroupDebitCreditMismatch(totalDebits |> Money.amount, totalCredits |> Money.amount))
        }

    let private confirmEntryCount (entries: BaseStageEntryLine list) : Result<unit, AppError> =
        if entries |> List.length < 2 then
            Error(IngestionBaseStageGroupInsufficientEntries(entries |> List.length))
        else
            Ok()
    
    let private confirmEntries (entries: BaseStageEntryLine list) : Result<unit, AppError> =
        result {
            do! entries |> confirmEntryCount
            do! entries |> confirmAmountEquality }
    
    let create
        (baseStageEntryGroupId : BaseStageEntryGroupId)
        (entryDate : LocalDate)
        (description: JournalEntryDescription)
        (fiSource: JournalRefFinancialInstitution)
        (fiReference: JournalExternalReferenceText)
        (entries: BaseStageEntryLine list)
        : Result<BaseStageEntryGroup, AppError> =
        match confirmEntries entries with
        | Error e -> Error e
        | Ok _ -> Ok {
            baseStageEntryGroupId = baseStageEntryGroupId
            entryDate = entryDate
            description = description
            fiSource = fiSource
            fiReference = fiReference
            entries = entries }
            
module BaseStageRaw = 

    type BaseStageRawRow = {
        baseStageEntryGroupId : BaseStageEntryGroupId
        entryDate : LocalDate
        description: JournalEntryDescription
        fiSource: JournalRefFinancialInstitution
        fiReference: JournalExternalReferenceText
        amount : Money
        entryType : JournalEntryLineType
        accountCode: AccountCode option
        memo: JournalEntryLineMemo option
    }
    
    let createGroupsFromRaw
        (rawRows: BaseStageRawRow list)
        : Result<BaseStageEntryGroup list, AppError> =
        rawRows
        |> List.groupBy(_.baseStageEntryGroupId)
        |> List.map(fun (baseStageEntryGroupId, rawRowsAtGroupId) ->
            let distinctHeadersList =
                rawRowsAtGroupId
                |> List.groupBy(fun x -> x.entryDate, x.description, x.fiSource, x.fiReference)
            if distinctHeadersList |> List.length > 1
            then Error (IngestionBaseStageGroupIdDistinctDataViolation (baseStageEntryGroupId |> BaseStageEntryGroupId.value))
            else
                let theOnly = distinctHeadersList |> List.head
                let entryDate, description, fiSource, fiReference = theOnly |> fst
                let rawRowsAtTheOnly = theOnly |> snd
                result {
                    let! entries =
                        rawRowsAtTheOnly
                        |> List.map (fun row -> 
                            BaseStageEntryLine.create
                                row.amount row.entryType row.accountCode row.memo
                            )
                        |> convertListOfResultsToResultsList
                    return! BaseStageEntryGroup.create baseStageEntryGroupId entryDate description
                        fiSource fiReference entries
                    }
            )
        |> convertListOfResultsToResultsList
