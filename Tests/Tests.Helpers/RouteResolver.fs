module Tests.Helpers.RouteResolver

open Context.Context
open DataAccessLayer.DbTransaction
open InterfaceBridge.Routes.AccountRoutes
open InterfaceBridge.Routes.FiscalPeriodRoutes
open InterfaceBridge.Routes.JournalEntryRoutes
open Utilities.AppError


let commandRoutes =
    accountDomainCommandRoutes @ fiscalPeriodDomainCommandRoutes @ journalEntryDomainCommandRoutes

let routeUiCommandForTesting
    (domain: string)
    (verb: string)
    (rest: string list)
    (payload: string)
    : Result<string, AppError> =
    match commandRoutes |> List.tryFind(fun r -> r.domain = domain && r.verb = verb) with // REQ-NGUI-1.1, REQ-NGUI-3.8
    | Some command -> command.handler payload rest
    | None -> Error(CliUnknownCommand(domain, verb)) // REQ-NGUI-3.9



/// runFuncAndAutoRollback is used for testing only. It creates a context and
/// automatically rolls back any database changes at the end (whether success
/// or failure).
let runFuncAndAutoRollback auditAction (func: Context -> Result<'T, AppError>) : Result<'T, AppError> =
    let context = create NewTransaction auditAction
    let tran = context |> getDatabaseTransaction
    match context |> func with
    | Error funcError ->
        match tran |> rollback with
        | Ok _ -> Error funcError
        | Error rollbackError -> Error rollbackError
    | Ok funcResult ->
        match tran |> rollback with
        | Ok _ -> Ok funcResult
        | Error rollbackError -> Error rollbackError
