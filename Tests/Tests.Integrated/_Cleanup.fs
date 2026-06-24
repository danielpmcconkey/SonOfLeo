module Tests.Integrated._Cleanup

open System
open Utilities.DAL
open Utilities.ResultCE

(*
 * These functions are used in tests' "finally" blocks where the test flow that
 * may or may not have failed before calling cleanup. Therefore, they take
 * options as their key parameters. Do the option resolution here so you don't
 * have to do it everywhere 
 *)

//=================================================
// Account clean up
//=================================================

let cleanUpAccountId (uniqueId:Guid option) : Result<unit, string> =
    match uniqueId with
    | None -> Ok ()
    | Some x -> 
        let parameters = [
            { name = "@unique_id"; value = UniqueId x };
        ]
        let query = $"""
                delete from ledger.account
                WHERE unique_id = @unique_id;
            """
        result {
            return! executeNonQuery query parameters ExactlyOne None
        }

let cleanUpAccountList (l: Guid option list) : Result<unit, string> =    
    l
    |> List.map cleanUpAccountId
    |> List.choose (function Error e -> Some e | Ok _ -> None)
    |> function 
            | [] -> Ok ()
            | errors ->
                let baseMessage = "One or more errors returns while deleting a list of account IDs. Individual errors follow, separated by '||'"
                let insideErrors = String.concat "||" errors
                Error $"{baseMessage}||{insideErrors}"

let cleanUpParentIdAndChildren (parentId: Guid option) (children: Guid option list) : Result<unit, string> =
    result {
        let! _ =
            children // clean the children before parent
            |> cleanUpAccountList
        let! _ = cleanUpAccountId parentId  // note that the parent won't be cleaned up if any of the child cleanups failed
        return ()
    }

//=================================================
// Fiscal Period clean up
//=================================================
let cleanUpFiscalPeriodId (uniqueId:Guid option) : Result<unit, string> =
    match uniqueId with
    | None -> Ok ()
    | Some x -> 
        let parameters = [
            { name = "@unique_id"; value = UniqueId x };
        ]
        let query = $"""
                delete from ledger.fiscal_period
                WHERE unique_id = @unique_id;
            """
        result {
            return! executeNonQuery query parameters ExactlyOne None
        }
let cleanUpFiscalPeriodKey (key:string option) : Result<unit, string> =
    match key with
    | None -> Ok ()
    | Some x -> 
        let parameters = [
            { name = "@period_key"; value = CharString x};
        ]
        let query = $"""
                delete from ledger.fiscal_period
                WHERE period_key = @period_key;
            """
        result {
            return! executeNonQuery query parameters ExactlyOne None
        }

let cleanUpFiscalPeriodIdsList (l: Guid option list) : Result<unit, string> =    
    l
    |> List.map cleanUpFiscalPeriodId
    |> List.choose (function Error e -> Some e | Ok _ -> None)
    |> function 
            | [] -> Ok ()
            | errors ->
                let baseMessage = "One or more errors returns while deleting a list of fiscal period IDs. Individual errors follow, separated by '||'"
                let insideErrors = String.concat "||" errors
                Error $"{baseMessage}||{insideErrors}"

let cleanUpFiscalPeriodKeysList (l: string option list) : Result<unit, string> =    
    l
    |> List.map cleanUpFiscalPeriodKey
    |> List.choose (function Error e -> Some e | Ok _ -> None)
    |> function 
            | [] -> Ok ()
            | errors ->
                let baseMessage = "One or more errors returns while deleting a list of fiscal period keys. Individual errors follow, separated by '||'"
                let insideErrors = String.concat "||" errors
                Error $"{baseMessage}||{insideErrors}"