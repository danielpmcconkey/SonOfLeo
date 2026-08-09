module Model.DataIngestion.StageEntryLine

open Model
open Model.Ledger.Accounts.AccountComponent
open Model.Ledger.Journaling.JournalEntryComponent
open Utilities.AppError
open System
                
type StageEntryLine = private {
        amount : Money
        lineType : JournalEntryLineType
        accountCode: AccountCode option
        memo: JournalEntryLineMemo option
    }

    
let amount l = l.amount 
let lineType l = l.lineType 
let accountCode l = l.accountCode 
let memo l = l.memo 

let confirmAmountIsPositive (m: Money) : Result<unit, AppError> =
    if m |> Money.amount <= 0M
    then Error(IngestionStageLineNonPositiveAmount(m |> Money.amount))
    else Ok()

let create
    (amount : Money)
    (entryType : JournalEntryLineType)
    (accountCode: AccountCode option)
    (memo: JournalEntryLineMemo option)
    : Result<StageEntryLine, AppError> =
    match amount |> confirmAmountIsPositive with
    | Error e -> Error e
    | Ok _ -> Ok {
                amount = amount
                lineType = entryType
                accountCode = accountCode
                memo = memo }
