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
    match commandRoutes |> List.tryFind(fun r -> r.domain = domain && r.verb = verb) with
    | Some command -> command.handler payload rest
    | None -> Error(CliUnknownCommand(domain, verb))



/// runFuncAndAutoRollback is used for testing only. It creates a context and
/// automatically rolls back any database changes at the end (whether the func
/// succeeds, fails, or raises).
let runFuncAndAutoRollback auditAction (func: Context -> Result<'T, AppError>) : Result<'T, AppError> =
    let context = create NewTransaction auditAction
    let tran = context |> getDatabaseTransaction
    (* A failing Assert raises rather than returning Error, so on that path the rollback
       below never runs: the transaction stays open holding its row locks, and the next
       test to touch those rows blocks until the connection dies. Roll back here and
       re-raise, so xUnit still reports the original assertion failure. The rollback
       result is discarded deliberately — the exception being re-raised is the more
       informative failure, and surfacing a rollback error would mask it. *)
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
