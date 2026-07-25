module Tests.Integrated.Rollback

open DataAccessLayer.DbTransaction
open Utilities.AppError

let withRollback (func: DbTransaction -> unit) : unit =
    match withManualCommitTransaction Ok with
    | TransactionCreateFail e -> failwith(AppError.toMessage e)
    | Failed(e, tran) ->
        tran |> rollback |> ignore
        failwith(AppError.toMessage e)
    | Success(tran, _) ->
        try
            func tran
        finally
            tran |> rollback |> ignore
