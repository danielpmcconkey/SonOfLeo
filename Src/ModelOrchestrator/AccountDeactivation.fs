module ModelOrchestrator.AccountDeactivation

open System
open Model.Audit
open Model.Ledger.Accounts
open Model.Ledger.Accounts.Account
open Model.Ledger.Journaling
open Model.Ledger.Journaling.JournalEntryComponent
open Model.Money
open NodaTime
open Utilities
open Utilities.DAL
open Utilities.ResultCE

let private updateDb
        (accountId: Guid)
        (activeEndUpdate: LocalDate)
        (auditEnvelope: AuditEnvelope)
        (transaction: DbTransaction option)
        : Result<Account, string> =
            
    let parameters = [
        { name = "@modified"; value = DbInstant (AuditEnvelope.instant auditEnvelope) } // REQ-SYS-3.3 
        { name = "@unique_id"; value = UniqueId accountId };
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
        return! accountId |> fetchById transaction
    }

let private validateProposedDeactivationDate
        (account: Account)
        (proposedDate: LocalDate)
        : Result<unit, string> =
    let ab = activeBegin account
    if proposedDate < ab then
        Error $"Deactivating account {uniqueId account} failed because the active end ({proposedDate}) would be before the active begin ({ab})" else
        Ok () // REQ-AC-4.2

let private validateNoActiveChildrenBeforeDeactivation
    (transaction: DbTransaction option)
    (account: Account)
    (auditEnvelope: AuditEnvelope)
    : Result<unit, string> =
    let accountId = uniqueId account
    result {
        let! children = accountId |> fetchByParentId transaction
        do!
            let referenceDate = (AuditEnvelope.instant auditEnvelope) |> Calendar.dateFromInstant
            if children |> List.exists (isActive referenceDate) // REQ-AC-4.3
            then Error $"Account {accountId} deactivation failed because one or more child account records is active"
            else Ok ()
    }

let private validateZeroBalance
        (transaction: DbTransaction option)
        (accountId: Guid)
        : Result<unit, string> =
    result {
        let! nonVoidedLines = accountId |> JournalEntryLine.fetchByAccountId transaction true // REQ-JE-4.7
        let! debits = nonVoidedLines |> JournalEntryLine.sumLinesByType Debit 
        let! credits = nonVoidedLines |> JournalEntryLine.sumLinesByType Credit
        let! diff = Money.subtract debits credits
        return!
            if diff |> Money.amount <> 0M
            then Error "The Account has a non-zero balance."
            else Ok()
    }

let private validateNoJournalEntriesAfterDeactivationDate
        (deactivationDate: LocalDate)
        (transaction: DbTransaction option)
        (accountId: Guid)
        : Result<unit, string> =
    let query = """
        SELECT count(je.entry_date)
        FROM ledger.journal_entry_line jel
        left join ledger.journal_entry je on jel.journal_entry_id = je.unique_id
        where jel.account_id = @account_id
        and je.entry_date > @deactivation_date
        ;"""
    let parameters = [
        { name = "@account_id"; value = UniqueId accountId };
        { name = "@deactivation_date"; value = DbLocalDate deactivationDate };
    ]
    match executeScalar query parameters transaction with
        | Error e -> Error e
        | Ok x when (x :?> int64) = 0L -> Ok()
        | Ok x when (x :?> int64) > 0L -> Error "Account is associated to Journal Entries dated after the deactivation date"
        | _ -> Error "Failed to validate the Account's Journal Entries prior to deactivation"

let private validateJournalEntries
        (accountId: Guid)
        (deactivationDate: LocalDate)
        (transaction: DbTransaction option)
        : Result<unit, string> =
    result {
        do! accountId |> validateZeroBalance transaction // REQ-AC-4.4
        do! accountId |> validateNoJournalEntriesAfterDeactivationDate deactivationDate transaction // REQ-AC-4.6
        return ()
    }

/// deactivateAccountById updates the active_end date in the database and returns a
/// fully reconstituted (and inactive) account. If the caller provides the
/// explicitEnd, the system will update the active_end to that explicit time.
/// Otherwise, the active_end will be the system clock time 
let deactivateAccountById
        (explicitEnd: LocalDate option)
        (auditEnvelope: AuditEnvelope)
        (transaction: DbTransaction option)
        (accountId: Guid)
        : Result<Account, string> = // REQ-AC-4.1
    let deactivationDate =
        match explicitEnd with
        | Some m -> m
        | None -> Calendar.dateFromInstant (AuditEnvelope.instant auditEnvelope)
    result {
        let! accountCurrent = accountId |> fetchById transaction
        do! // REQ-AC-4.5
            match activeEnd accountCurrent with
            | None -> Ok ()
            | Some x -> Error $"Account {accountId} deactivation failed because active end is already set to {x}"
        let! () = validateProposedDeactivationDate accountCurrent deactivationDate // REQ-AC-4.2
        let! () = validateNoActiveChildrenBeforeDeactivation transaction accountCurrent auditEnvelope // REQ-AC-4.3
        let! () = validateJournalEntries accountId deactivationDate transaction
        let! newAccount = updateDb accountId deactivationDate auditEnvelope transaction
        return newAccount                
    }
    