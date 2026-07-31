module Tests.Helpers.Cleanup

open System
open DataAccessLayer.DbTransaction
open DataAccessLayer.ExecuteNonQuery
open DataAccessLayer.ExecuteReader
open Logger.Audit
open Model.Ledger.Accounts.AccountComponent
open Model.Ledger.FiscalPeriods
open Model.Ledger.Journaling.JournalEntryComponent
open Utilities.AppError
open Utilities.ResultHelper
open DataAccessLayer.QueryParameters
open Context.Context

(*
 * These functions are used in tests' "finally" blocks where the test flow that
 * may or may not have failed before calling cleanup. Therefore, they take
 * options as their key parameters. Do the option resolution here so you don't
 * have to do it everywhere 
 *)

//=================================================
// Account clean up
//=================================================

let cleanUpAccountId (accountId: AccountId option) : Result<unit, AppError> =
    let context = create NoTransaction FetchOnly
    match accountId with
    | None -> Ok()
    | Some x ->
        let uniqueId = x |> AccountId.value
        let parameters = [ { name = "@unique_id"; value = UniqueId uniqueId } ]
        let query =
            $"""
                delete from ledger.account
                WHERE unique_id = @unique_id;
            """
        executeNonQuery (context |> getDatabaseTransaction) query parameters ExactlyOne

let cleanUpAccountList (l: AccountId option list) : Result<unit, AppError> =
    l
    |> List.map cleanUpAccountId
    |> List.choose (function
        | Error e -> Some e
        | Ok _ -> None)
    |> function
        | [] -> Ok()
        | errors ->
            let baseMessage =
                "One or more errors returns while deleting a list of account IDs. Individual errors follow, separated by '||'"
            let insideErrors = errors |> List.map(AppError.toMessage) |> String.concat "||"
            Error(TestingError $"{baseMessage}||{insideErrors}")

let cleanUpParentIdAndChildren (parentId: AccountId option) (children: AccountId option list) : Result<unit, AppError> =
    result {
        let! _ =
            children // clean the children before parent
            |> cleanUpAccountList
        let! _ = cleanUpAccountId parentId // note that the parent won't be cleaned up if any of the child cleanups failed
        return ()
    }

//=================================================
// Fiscal Period clean up
//=================================================
let cleanUpFiscalPeriodId (fpId: FiscalPeriodId option) : Result<unit, AppError> =
    let context = create NoTransaction FetchOnly
    match fpId with
    | None -> Ok()
    | Some x ->
        let uuid = x |> FiscalPeriodId.value
        let parameters = [ { name = "@unique_id"; value = UniqueId uuid } ]
        let query =
            $"""
                delete from ledger.fiscal_period
                WHERE unique_id = @unique_id;
            """
        executeNonQuery (context |> getDatabaseTransaction) query parameters ExactlyOne

let cleanUpFiscalPeriodKey (key: string option) : Result<unit, AppError> =
    let context = create NoTransaction FetchOnly
    match key with
    | None -> Ok()
    | Some x ->
        let parameters = [ { name = "@period_key"; value = CharString x } ]
        let query =
            $"""
                delete from ledger.fiscal_period
                WHERE period_key = @period_key;
            """
        executeNonQuery (context |> getDatabaseTransaction) query parameters ExactlyOne

let cleanUpFiscalPeriodIdsList (l: FiscalPeriodId option list) : Result<unit, AppError> =
    l
    |> List.map cleanUpFiscalPeriodId
    |> List.choose (function
        | Error e -> Some e
        | Ok _ -> None)
    |> function
        | [] -> Ok()
        | errors ->
            let baseMessage =
                "One or more errors returns while deleting a list of fiscal period IDs. Individual errors follow, separated by '||'"
            let insideErrors = errors |> List.map(AppError.toMessage) |> String.concat "||"
            Error(TestingError $"{baseMessage}||{insideErrors}")

let cleanUpFiscalPeriodKeysList (l: string option list) : Result<unit, AppError> =
    l
    |> List.map cleanUpFiscalPeriodKey
    |> List.choose (function
        | Error e -> Some e
        | Ok _ -> None)
    |> function
        | [] -> Ok()
        | errors ->
            let baseMessage =
                "One or more errors returns while deleting a list of fiscal period keys. Individual errors follow, separated by '||'"
            let insideErrors = errors |> List.map(AppError.toMessage) |> String.concat "||"
            Error(TestingError $"{baseMessage}||{insideErrors}")

//=================================================
// Journal Entry clean up
//=================================================

let cleanUpJournalEntryId (journalEntryHeaderId: JournalEntryHeaderId option) : Result<unit, AppError> =
    let context = create NoTransaction FetchOnly
    match journalEntryHeaderId with
    | None -> Ok()
    | Some x ->
        let uuid = x |> JournalEntryHeaderId.value
        let parameters = [ { name = "@unique_id"; value = UniqueId uuid } ]
        // delete children before the header, in FK order
        let commentQuery =
            $"""
                delete from ledger.journal_entry_comment
                WHERE journal_primary_entry_id = @unique_id
                   OR journal_secondary_entry_id = @unique_id;
            """
        let extReferenceQuery =
            $"""
                delete from ledger.journal_entry_ext_reference
                WHERE journal_entry_id = @unique_id;
            """
        let lineQuery =
            $"""
                delete from ledger.journal_entry_line
                WHERE journal_entry_id = @unique_id;
            """
        let headerQuery =
            $"""
                delete from ledger.journal_entry
                WHERE unique_id = @unique_id;
            """

        result {
            let! _ = executeNonQuery (context |> getDatabaseTransaction) commentQuery parameters AnyQuantityIsAcceptable
            let! _ =
                executeNonQuery (context |> getDatabaseTransaction) extReferenceQuery parameters AnyQuantityIsAcceptable
            let! _ = executeNonQuery (context |> getDatabaseTransaction) lineQuery parameters AnyQuantityIsAcceptable
            return! executeNonQuery (context |> getDatabaseTransaction) headerQuery parameters ExactlyOne
        }

let cleanUpJournalEntryExtReferenceId (uniqueId: Guid option) : Result<unit, AppError> =
    let context = create NoTransaction FetchOnly
    match uniqueId with
    | None -> Ok()
    | Some x ->
        let parameters = [ { name = "@unique_id"; value = UniqueId x } ]
        let query =
            $"""
                delete from ledger.journal_entry_ext_reference
                WHERE unique_id = @unique_id;
            """
        executeNonQuery (context |> getDatabaseTransaction) query parameters ExactlyOne

let cleanUpJournalEntryCommentId (uniqueId: Guid option) : Result<unit, AppError> =
    let context = create NoTransaction FetchOnly
    match uniqueId with
    | None -> Ok()
    | Some x ->
        let parameters = [ { name = "@unique_id"; value = UniqueId x } ]
        let query =
            $"""
                delete from ledger.journal_entry_comment
                WHERE unique_id = @unique_id;
            """
        executeNonQuery (context |> getDatabaseTransaction) query parameters ExactlyOne

let cleanUpJournalEntryList (l: JournalEntryHeaderId option list) : Result<unit, AppError> =
    l
    |> List.map cleanUpJournalEntryId
    |> List.choose (function
        | Error e -> Some e
        | Ok _ -> None)
    |> function
        | [] -> Ok()
        | errors ->
            let baseMessage =
                "One or more errors returns while deleting a list of journal entry IDs. Individual errors follow, separated by '||'"
            let insideErrors = errors |> List.map(AppError.toMessage) |> String.concat "||"
            Error(TestingError $"{baseMessage}||{insideErrors}")
