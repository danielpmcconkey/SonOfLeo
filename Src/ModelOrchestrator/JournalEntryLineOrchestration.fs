module ModelOrchestrator.JournalEntryLineOrchestration

open Model
open Model.Ledger.Account
open Model.Ledger.AccountComponent
open Model.Ledger
open Model.Ledger.JournalEntryLine
open Model.Ledger.JournalEntryComponent
open Utilities.AppError
open Utilities.ResultHelper

let private confirmAmountIsPositive (m: Money) : Result<unit, AppError> =
    if
        m |> Money.amount <= 0M
    then
        Error(JournalEntryLineNonPositiveAmount(m |> Money.amount))
    else
        Ok()

let private confirmAccountExists (context: Context.Context) (accountId: AccountId) : Result<unit, AppError> =
    match accountId |> Account.fetchById context with
    | Error(DalResultantRowsDidntMatchExpectation _) ->
        Error(JournalEntryLineAccountDoesntExist(accountId |> AccountId.value))
    | Error e -> Error e
    | Ok _ -> Ok()

let constructNewAndSaveToDb
    (context: Context.Context)
    (journalEntryId: JournalEntryHeaderId)
    (accountId: AccountId)
    (amount: Money)
    (lineType: JournalEntryLineType)
    (memo: JournalEntryLineMemo option)
    : Result<JournalEntryLine, AppError> =
    let journalEntryLineId = JournalEntryLineId.create()
    let now = context |> Context.getInitiationInstant
    let createdAt = now
    let modifiedAt = now
    result {
        do! amount |> confirmAmountIsPositive
        do! accountId |> confirmAccountExists context
        let line =
            create
                journalEntryLineId
                journalEntryId
                accountId
                amount
                lineType
                memo
                createdAt
                modifiedAt
        let! () = line |> insertNewToDb context
        return line
    }

// todo find out why we have no edit functions on JE line
