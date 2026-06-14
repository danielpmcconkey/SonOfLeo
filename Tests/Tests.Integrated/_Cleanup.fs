module Tests.Integrated._Cleanup

open System
open Utilities.DAL
open Utilities.ResultCE

let CleanUpAccountId (id:Guid) : Result<unit, string> =
    let parameters = [
        { name = "@id"; value = UniqueId id };
    ]
    let query = $"""
            delete from ledger.account
            WHERE id = @id;
        """
    result {
        return! executeNonQuery query parameters ExactlyOne
    }