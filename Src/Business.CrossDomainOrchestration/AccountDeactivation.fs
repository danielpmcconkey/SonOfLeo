module Business.FinancialServices.AccountDeactivation

open Business.General
open Business.FinancialServices.Ledger.Account
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.JournalEntryComponent
open NodaTime
open App.Utility
open App.Utility.AppError
open App.DataAccessLayer.QueryParameter
open App.DataAccessLayer.ExecuteReader
open App.DataAccessLayer.ExecuteNonQuery
open App.DataAccessLayer.ExecuteScalar
open App.Utility.Result
open App.Session

let private updateActiveEnd (context: Context.Context) (activeEndUpdate: LocalDate) (account: Account) : Result<Account, AppError> =
    let accountId = account |> Account.accountId
    let uuid = accountId |> AccountId.value
    let parameters =
        [ { name = "@modified"; value = DbInstant(context |> Context.getInitiationInstant) }
          { name = "@unique_id"; value = UniqueId uuid }
          { name = "@active_end"; value = NullableDbLocalDate(Some activeEndUpdate) } ]

    let queryStatement =
        $"""
        UPDATE ledger.account
        set
            modified_at = @modified
            , active_end = @active_end
        WHERE unique_id = @unique_id;
    """
    result {
        let! () = executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters ExactlyOne
        return! accountId |> Account.fetchById context
    }

let private confirmProposedDeactivationDateIsValid
    (proposedDate: LocalDate)
    (account: Account)
    : Result<unit, AppError> =
    let ab = account |> Account.activityPeriod |> ActivityPeriod.activeBegin
    if proposedDate < ab then
        Error(
            AccountDeactivationProposedDateIsInvalid(account |> Account.accountId |> AccountId.value, proposedDate, ab)
        )
    else
        Ok()

let private confirmNoActiveChildrenBeforeDeactivation (context: Context.Context) (account: Account) : Result<unit, AppError> =
    let accountId = account |> Account.accountId
    result {
        let! children = accountId |> Account.fetchByParentId context
        do!
            let referenceDate = (context |> Context.getInitiationInstant) |> Calendar.dateFromInstant
            if
                children
                |> List.exists(fun x -> x |> Account.activityPeriod |> ActivityPeriod.isActive referenceDate)
            then
                Error(AccountActiveChildrenBeforeDeactivation(account |> Account.accountId |> AccountId.value))
            else
                Ok()
    }

let private confirmZeroBalanceBeforeDeactivation (context: Context.Context) (account: Account) : Result<unit, AppError> =
    let accountId = account |> Account.accountId
    result {
        let! nonVoidedLines = accountId |> JournalEntryLine.fetchByAccountId context true
        let! debits = nonVoidedLines |> JournalEntryLine.sumLinesByType Debit
        let! credits = nonVoidedLines |> JournalEntryLine.sumLinesByType Credit
        let! diff = Money.subtractVal1FromVal2 debits credits
        return!
            if diff |> Money.amount <> 0M then
                Error(
                    AccountNonZeroBalanceBeforeDeactivation(
                        account |> Account.accountId |> AccountId.value,
                        debits |> Money.amount,
                        credits |> Money.amount
                    )
                )
            else
                Ok()
    }

let private confirmNoJournalEntriesAfterDeactivationDate
    (context: Context.Context)
    (deactivationDate: LocalDate)
    (account: Account)
    : Result<unit, AppError> =
    let accountId = account |> Account.accountId
    let queryStatement =
        """
        SELECT count(je.entry_date)
        FROM ledger.journal_entry_line jel
        left join ledger.journal_entry je on jel.journal_entry_id = je.unique_id
        where jel.account_id = @account_id
        and je.entry_date > @deactivation_date
        and je.voided_at is null
        ;"""
    let uuid = accountId |> AccountId.value
    let parameters =
        [ { name = "@account_id"; value = UniqueId uuid }
          { name = "@deactivation_date"; value = DbLocalDate deactivationDate } ]
    match executeScalar (context |> Context.getDatabaseTransaction) queryStatement parameters longUnboxing with
    | Error e -> Error e
    | Ok x when x = 0L -> Ok()
    | Ok x when x > 0L -> Error(AccountDeactivationWithJournalEntriesDatedAfterDeactivationDate uuid)
    | _ -> Error(AccountDeactivationFailedJournalEntryValidation)

let private confirmJournalEntriesAreInProperState
    (context: Context.Context)
    (deactivationDate: LocalDate)
    (account: Account)
    : Result<unit, AppError> =
    result {
        do! account |> confirmZeroBalanceBeforeDeactivation context
        do! account |> confirmNoJournalEntriesAfterDeactivationDate context deactivationDate
        return ()
    }
    
/// If the caller provides the explicitEnd, the system will update the active_end to that explicit time. Otherwise, it
/// will use the context's initiation instant to determine the end date
let deactivateAccount
    (context: Context.Context)
    (explicitEnd: LocalDate option)
    (account: Account)
    : Result<Account, AppError> =
    let accountId = account |> Account.accountId
    let deactivationDate =
        match explicitEnd with
        | Some m -> m
        | None -> Calendar.dateFromInstant(context |> Context.getInitiationInstant)
    result {
        let activeEnd = account |> Account.activityPeriod |> ActivityPeriod.activeEnd
        do!
            match activeEnd with
            | None -> Ok()
            | Some x -> Error(AccountAlreadyInactive(accountId |> AccountId.value, x))
        let! () = account |> confirmProposedDeactivationDateIsValid deactivationDate
        let! () = account |> confirmNoActiveChildrenBeforeDeactivation context
        let! () = account |> confirmJournalEntriesAreInProperState context deactivationDate
        let! newAccount = account |> updateActiveEnd context deactivationDate
        return newAccount
    }
