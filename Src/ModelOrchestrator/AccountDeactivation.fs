module ModelOrchestrator.AccountDeactivation

open Model.Audit
open Model.Ledger.Accounts
open Model.Ledger.Accounts.AccountComponent
open Model.Ledger.Journaling
open Model.Ledger.Journaling.JournalEntryComponent
open Model
open NodaTime
open Utilities
open Utilities.AppError
open Utilities.DAL
open Utilities.ResultCE

let private updateDb
        (account: Account)
        (activeEndUpdate: LocalDate)
        (auditEnvelope: AuditEnvelope)
        (transaction: DbTransaction option)
        : Result<Account, AppError> =
    let accountId = account |> Account.accountId
    let uuid = accountId |> AccountId.value
    let parameters = [
        { name = "@modified"; value = DbInstant (AuditEnvelope.instant auditEnvelope) } // REQ-SYS-3.3 
        { name = "@unique_id"; value = UniqueId uuid };
        { name = "@active_end"; value = NullableDbLocalDate (Some activeEndUpdate) };
    ]

    let query = $"""
        UPDATE ledger.account
        set
            modified_at = @modified -- REQ-SYS-3.3
            , active_end = @active_end
        WHERE unique_id = @unique_id;
    """
    result {
        let! () = executeNonQuery query parameters ExactlyOne transaction
        return! accountId |> Account.fetchById transaction
    }

let private confirmProposedDeactivationDateIsValid
        (account: Account)
        (proposedDate: LocalDate)
        : Result<unit, AppError> =
    let ab = account |> Account.activityPeriod |> AccountActivityPeriod.activeBegin
    if proposedDate < ab
    then Error (AccountDeactivationProposedDateIsInvalid (
            account |> Account.accountId |> AccountId.value, proposedDate, ab)) 
    else Ok () // REQ-AC-4.2

let private confirmNoActiveChildrenBeforeDeactivation
    (transaction: DbTransaction option)
    (account: Account)
    (auditEnvelope: AuditEnvelope)
    : Result<unit, AppError> =
    let accountId = account |> Account.accountId
    result {
        let! children = accountId |> Account.fetchByParentId transaction
        do!
            let referenceDate = (AuditEnvelope.instant auditEnvelope) |> Calendar.dateFromInstant
            if children |> List.exists (fun x ->
                x |> Account.activityPeriod |> AccountActivityPeriod.isActive referenceDate) // REQ-AC-4.3
            then Error (AccountActiveChildrenBeforeDeactivation (account |> Account.accountId |> AccountId.value))
            else Ok () }

let private confirmZeroBalanceBeforeDeactivation
        (transaction: DbTransaction option)
        (account: Account)
        : Result<unit, AppError> =
    let accountId = account |> Account.accountId
    result {
        let! nonVoidedLines = accountId |> JournalEntryLine.fetchByAccountId transaction true // REQ-JE-4.7
        let! debits = nonVoidedLines |> JournalEntryLine.sumLinesByType Debit 
        let! credits = nonVoidedLines |> JournalEntryLine.sumLinesByType Credit
        let! diff = Money.subtractVal1FromVal2 debits credits
        return!
            if diff |> Money.amount <> 0M
            then Error (AccountNonZeroBalanceBeforeDeactivation (
                account |> Account.accountId |> AccountId.value, debits |> Money.amount, credits |> Money.amount ) )
            else Ok() }

let private confirmNoJournalEntriesAfterDeactivationDate
        (deactivationDate: LocalDate)
        (transaction: DbTransaction option)
        (account: Account)
        : Result<unit, AppError> =
    let accountId = account |> Account.accountId
    let query = """
        SELECT count(je.entry_date)
        FROM ledger.journal_entry_line jel
        left join ledger.journal_entry je on jel.journal_entry_id = je.unique_id
        where jel.account_id = @account_id
        and je.entry_date > @deactivation_date
        and je.voided_at is null
        ;"""
    let uuid = accountId |> AccountId.value
    let parameters = [
        { name = "@account_id"; value = UniqueId uuid };
        { name = "@deactivation_date"; value = DbLocalDate deactivationDate };
    ]
    match executeScalar query parameters longUnboxing transaction with
        | Error e -> Error e
        | Ok x when x = 0L -> Ok()
        | Ok x when x > 0L -> Error (AccountDeactivationWithJournalEntriesDatedAfterDeactivationDate uuid)
        | _ -> Error (AccountDeactivationFailedJournalEntryValidation ())

let private confirmJournalEntriesAreInProperState
        (account: Account)
        (deactivationDate: LocalDate)
        (transaction: DbTransaction option)
        : Result<unit, AppError> =
    result {
        do! account |> confirmZeroBalanceBeforeDeactivation transaction // REQ-AC-4.4
        do! account |> confirmNoJournalEntriesAfterDeactivationDate deactivationDate transaction // REQ-AC-4.6
        return () }

/// deactivateAccountById updates the active_end date in the database and returns a
/// fully reconstituted (and inactive) account. If the caller provides the
/// explicitEnd, the system will update the active_end to that explicit time.
/// Otherwise, the active_end will be the system clock time 
let deactivateAccount
        (explicitEnd: LocalDate option)
        (auditEnvelope: AuditEnvelope)
        (transaction: DbTransaction option)
        (account: Account)
        : Result<Account, AppError> = // REQ-AC-4.1
    let accountId = account |> Account.accountId
    let deactivationDate =
        match explicitEnd with
        | Some m -> m
        | None -> Calendar.dateFromInstant (AuditEnvelope.instant auditEnvelope)
    result {
        let activeEnd = account |> Account.activityPeriod |> AccountActivityPeriod.activeEnd
        do! // REQ-AC-4.5
            match activeEnd with
            | None -> Ok ()
            | Some x -> Error (AccountAlreadyInactive(accountId |> AccountId.value, x))
        let! () = confirmProposedDeactivationDateIsValid account deactivationDate // REQ-AC-4.2
        let! () = confirmNoActiveChildrenBeforeDeactivation transaction account auditEnvelope // REQ-AC-4.3
        let! () = confirmJournalEntriesAreInProperState account deactivationDate transaction
        let! newAccount = updateDb account deactivationDate auditEnvelope transaction
        return newAccount }
    