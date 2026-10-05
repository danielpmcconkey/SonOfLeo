module Tests.Helpers.RouteResolver


open App.DataAccessLayer.DbTransaction
open Ui.InterfaceBridge.Routes.ReportRoutes
open Ui.OperatorCli.OperatorCliError
open Ui.ReportCli.ReportCliError
open App.Utility.IAppError
open Tests.Helpers.TestError
open Tests.Helpers.SadPath


// The shipped operator CLI's own route table, so a domain the executable can't reach can't be reached here either.
let commandRoutes = Program.commandRoutes

let routeUiCommandForTesting
    (domain: string)
    (verb: string)
    (rest: string list)
    (payload: string)
    : Result<string, IAppError> =
    match commandRoutes |> List.tryFind(fun r -> r.domain = domain && r.verb = verb) with
    | Some command -> command.handler payload rest
    | None -> Error(CliUnknownCommand(domain, verb))

let routeReportingCommandForTesting
    (name: string)
    (rest: string list)
    (payload: string)
    : Result<string, IAppError> =
    match reportingRoutes |> List.tryFind(fun r -> r.name = name) with
    | Some command -> command.handler payload rest
    | None -> Error(ReportingUnknownReportName name)
