module Tests.Integrated._Cleanup

open System
open Utilities.DAL
open Utilities.ResultCE

/// used for the finally block tests that may or may not have failed before calling
/// this function. therefore, it takes a guid option. Do the option resolution
/// once here so you don't have to do it everywhere 
let cleanUpAccountId (id:Guid option) : Result<unit, string> =
    match id with
    | None -> Ok ()
    | Some x -> 
        let parameters = [
            { name = "@id"; value = UniqueId x };
        ]
        let query = $"""
                delete from ledger.account
                WHERE id = @id;
            """
        result {
            return! executeNonQuery query parameters ExactlyOne
        }

/// used for the finally block tests that may or may not have failed before calling
/// this function. therefore, it takes an option list. Do the option resolution
/// once here so you don't have to do it everywhere 
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

/// used for the finally block tests that may or may not have failed before calling
/// this function. therefore, everything takes an option. Do the option resolution once
/// here so you don't have to do it everywhere 
let cleanUpParentIdAndChildren (parentId: Guid option) (children: Guid option list) : Result<unit, string> =
    result {
        let! _ =
            children // clean the children before parent
            |> cleanUpAccountList
        let! _ = cleanUpAccountId parentId  // note that the parent won't be cleaned up if any of the child cleanups failed
        return ()
    }