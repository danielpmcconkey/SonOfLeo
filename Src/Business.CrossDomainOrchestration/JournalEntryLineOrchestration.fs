module Business.FinancialServices.JournalEntryLineOrchestration

open App.Utility.AppError
open App.Utility.Result
open App.Session
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Ledger.JournalEntryLine
open Business.FinancialServices.Ledger.JournalEntryComponent

let private confirmAmountIsPositive (m: Money.Money) : Result<unit, AppError> =
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

let constructNewAndPersist
    (context: Context.Context)
    (journalEntryId: JournalEntryHeaderId)
    (accountId: AccountId)
    (amount: Money.Money)
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
        let! () = line |> persist context
        return line
    }

