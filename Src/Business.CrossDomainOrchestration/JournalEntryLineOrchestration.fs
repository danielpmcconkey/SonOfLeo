module Business.CrossDomainOrchestration.JournalEntryLineOrchestration

open App.Utility.IAppError
open App.Utility.Result
open App.DataAccessLayer.DalError
open App.Session
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.LedgerError
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Ledger.JournalEntryLine
open Business.FinancialServices.Ledger.JournalEntryComponent

let private confirmAmountIsPositive (m: Money.Money) : Result<unit, IAppError> =
    if
        m |> Money.amount <= 0M
    then
        Error(JournalEntryLineNonPositiveAmount(m |> Money.amount))
    else
        Ok()

let private confirmAccountExists (context: Context.Context) (accountId: AccountId) : Result<unit, IAppError> =
    match accountId |> Account.fetchById context with
    | Ok _ -> Ok()
    | Error e ->
        if e.DomainName = nameof DalError && e.CaseName = nameof DalError.DalResultantRowsDidntMatchExpectation
        then Error (JournalEntryLineAccountDoesntExist(accountId |> AccountId.value))
        else Error e

let constructNewAndPersist
    (context: Context.Context)
    (journalEntryId: JournalEntryHeaderId)
    (accountId: AccountId)
    (amount: Money.Money)
    (lineType: JournalEntryLineType)
    (memo: JournalEntryLineMemo option)
    : Result<JournalEntryLine, IAppError> =
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

