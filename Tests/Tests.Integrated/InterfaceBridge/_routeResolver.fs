module Tests.Integrated.InterfaceBridge._routeResolver

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
