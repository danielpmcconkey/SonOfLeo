module InterfaceBridge.CommandRoute

open Utilities.AppError
open DataAccessLayer.DbTransaction



type CommandRoute =
    {
      domain: string
      verb: string
      description: string
      inputContract: string
      outputContract: string
      handler: string -> string list -> Result<string, AppError> }


type ReportRoute =
    {
      name: string
      description: string
      inputContract: string
      outputContract: string
      handler: string -> string list -> Result<string, AppError> }

// runRouteAndAutoCompleteTransaction is used for routes only. It creates a net
// new transaction and context, then has the DAL automatically commit or
// rollback, depending on success or failure of the function.
let runCommandRouteAndAutoCompleteTransaction auditAction
        (func: Context.Context -> Result<'T, AppError>) : Result<'T, AppError> =
    let context = Context.create NewTransaction auditAction
    runWithAutoCompleteTransaction (context |> Context.getDatabaseTransaction) (fun () -> func context)

/// runFuncAndAutoRollback is used mostly for testing, though we also use it for shadow posting. It creates a context
/// and automatically rolls back any database changes at the end (whether the func succeeds, fails, or raises).
let runCommandRouteAndAutoRollback auditAction (func: Context.Context -> Result<'T, AppError>) : Result<'T, AppError> =
    let context = Context.create NewTransaction auditAction
    let tran = context |> Context.getDatabaseTransaction
    let funcResult =
        try
            context |> func
        with _ ->
            tran |> rollback |> ignore
            reraise()
    match funcResult with
    | Error funcError ->
        match tran |> rollback with
        | Ok _ -> Error funcError
        | Error rollbackError -> Error rollbackError
    | Ok funcResult ->
        match tran |> rollback with
        | Ok _ -> Ok funcResult
        | Error rollbackError -> Error rollbackError
