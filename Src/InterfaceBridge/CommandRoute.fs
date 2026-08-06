module InterfaceBridge.CommandRoute

open Utilities.AppError
open DataAccessLayer.DbTransaction
open Context.Context


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
let runCommandRouteAndAutoCompleteTransaction auditAction (func: Context -> Result<'T, AppError>) : Result<'T, AppError> =
    let context = create NewTransaction auditAction
    runWithAutoCompleteTransaction (context |> getDatabaseTransaction) (fun () -> func context)
