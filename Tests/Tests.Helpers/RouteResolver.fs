module Tests.Helpers.RouteResolver


open DataAccessLayer.DbTransaction
open InterfaceBridge.Routes.AccountRoutes
open InterfaceBridge.Routes.FiscalPeriodRoutes
open InterfaceBridge.Routes.IngestionRoutes
open InterfaceBridge.Routes.JournalEntryRoutes
open InterfaceBridge.Routes.ReportRoutes
open Utilities.AppError


let commandRoutes =
    accountDomainCommandRoutes @ fiscalPeriodDomainCommandRoutes @ journalEntryDomainCommandRoutes @ ingestionDomainCommandRoutes

let routeUiCommandForTesting
    (domain: string)
    (verb: string)
    (rest: string list)
    (payload: string)
    : Result<string, AppError> =
    match commandRoutes |> List.tryFind(fun r -> r.domain = domain && r.verb = verb) with
    | Some command -> command.handler payload rest
    | None -> Error(CliUnknownCommand(domain, verb))

let routeReportingCommandForTesting
    (name: string)
    (rest: string list)
    (payload: string)
    : Result<string, AppError> =
    match reportingRoutes |> List.tryFind(fun r -> r.name = name) with
    | Some command -> command.handler payload rest
    | None -> Error(ReportingUnknownReportName name)
