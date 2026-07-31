module ModelOrchestrator.AccountDeactivation

open Model.Ledger.Accounts
open Model.Ledger.Accounts.AccountComponent
open Model.Ledger.Journaling
open Model.Ledger.Journaling.JournalEntryComponent
open Model
open NodaTime
open Utilities
open Utilities.AppError
open DataAccessLayer.QueryParameters
open DataAccessLayer.ExecuteReader
open DataAccessLayer.ExecuteNonQuery
open DataAccessLayer.ExecuteScalar
open Utilities.ResultHelper
open Context.Context

let private updateDb (context: Context) (activeEndUpdate: LocalDate) (account: Account) : Result<Account, AppError> =
    let accountId = account |> Account.accountId
    let uuid = accountId |> AccountId.value
    let parameters =
        [ { name = "@modified"; value = DbInstant(context |> getInitiationInstant) }
          { name = "@unique_id"; value = UniqueId uuid }
          { name = "@active_end"; value = NullableDbLocalDate(Some activeEndUpdate) } ]

    let query =
        $"""
        UPDATE ledger.account
        set
            modified_at = @modified
            , active_end = @active_end
        WHERE unique_id = @unique_id;
    """
    result {
        let! () = executeNonQuery (context |> getDatabaseTransaction) query parameters ExactlyOne
        return! accountId |> Account.fetchById context
    }

let private confirmProposedDeactivationDateIsValid
    (proposedDate: LocalDate)
    (account: Account)
    : Result<unit, AppError> =
    let ab = account |> Account.activityPeriod |> AccountActivityPeriod.activeBegin
    if proposedDate < ab then
        Error(
            AccountDeactivationProposedDateIsInvalid(account |> Account.accountId |> AccountId.value, proposedDate, ab)
        )
    else
        Ok()

let private confirmNoActiveChildrenBeforeDeactivation (context: Context) (account: Account) : Result<unit, AppError> =
    let accountId = account |> Account.accountId
    result {
        let! children = accountId |> Account.fetchByParentId context
        do!
            let referenceDate = (context |> getInitiationInstant) |> Calendar.dateFromInstant
            if
                children
                |> List.exists(fun x -> x |> Account.activityPeriod |> AccountActivityPeriod.isActive referenceDate)
            then
                Error(AccountActiveChildrenBeforeDeactivation(account |> Account.accountId |> AccountId.value))
            else
                Ok()
    }

let private confirmZeroBalanceBeforeDeactivation (context: Context) (account: Account) : Result<unit, AppError> =
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
    (context: Context)
    (deactivationDate: LocalDate)
    (account: Account)
    : Result<unit, AppError> =
    let accountId = account |> Account.accountId
    let query =
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
    match executeScalar (context |> getDatabaseTransaction) query parameters longUnboxing with
    | Error e -> Error e
    | Ok x when x = 0L -> Ok()
    | Ok x when x > 0L -> Error(AccountDeactivationWithJournalEntriesDatedAfterDeactivationDate uuid)
    | _ -> Error(AccountDeactivationFailedJournalEntryValidation)

let private confirmJournalEntriesAreInProperState
    (context: Context)
    (deactivationDate: LocalDate)
    (account: Account)
    : Result<unit, AppError> =
    result {
        do! account |> confirmZeroBalanceBeforeDeactivation context
        do! account |> confirmNoJournalEntriesAfterDeactivationDate context deactivationDate
        return ()
    }

/// deactivateAccountById updates the active_end date in the database and returns a
/// fully reconstituted (and inactive) account. If the caller provides the
/// explicitEnd, the system will update the active_end to that explicit time.
/// Otherwise, the active_end will be the system clock time
let deactivateAccount
    (context: Context)
    (explicitEnd: LocalDate option)
    (account: Account)
    : Result<Account, AppError> =
    let accountId = account |> Account.accountId
    let deactivationDate =
        match explicitEnd with
        | Some m -> m
        | None -> Calendar.dateFromInstant(context |> getInitiationInstant)
    result {
        let activeEnd = account |> Account.activityPeriod |> AccountActivityPeriod.activeEnd
        do!
            match activeEnd with
            | None -> Ok()
            | Some x -> Error(AccountAlreadyInactive(accountId |> AccountId.value, x))
        let! () = account |> confirmProposedDeactivationDateIsValid deactivationDate
        let! () = account |> confirmNoActiveChildrenBeforeDeactivation context
        let! () = account |> confirmJournalEntriesAreInProperState context deactivationDate
        let! newAccount = account |> updateDb context deactivationDate
        return newAccount
    }
