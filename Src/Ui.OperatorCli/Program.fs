open System
open App.Utility.IAppError
open Ui.InterfaceBridge.CommandRoute
open Ui.InterfaceBridge.Routes.PersonRoutes
open Ui.InterfaceBridge.Routes.AccountRoutes
open Ui.InterfaceBridge.Routes.FiscalPeriodRoutes
open Ui.InterfaceBridge.Routes.JournalEntryRoutes
open Ui.InterfaceBridge.Routes.IngestionRoutes
open Ui.InterfaceBridge.Routes.CashFlowRoutes
open Ui.InterfaceBridge.Routes.PositionsRoutes
open Ui.InterfaceBridge.Routes.ClassificationRoutes
open Ui.OperatorCli.OperatorCliError


let commandRoutes =
    accountDomainCommandRoutes @ fiscalPeriodDomainCommandRoutes @ journalEntryDomainCommandRoutes @ ingestionDomainCommandRoutes @ classificationDomainCommandRoutes @ cashFlowDomainCommandRoutes
    @ personDomainCommandRoutes @ positionsDomainCommandRoutes

let route domain verb rest payload : Result<string, IAppError> =
    match commandRoutes |> List.tryFind(fun r -> r.domain = domain && r.verb = verb) with
    | Some command -> command.handler payload rest
    | None -> Error(CliUnknownCommand(domain, verb))

[<EntryPoint>]
let main args =
    match args |> Array.toList with
    | domain :: verb :: "--file" :: filePath :: rest ->
        Ui.InterfaceBridge.Startup.run (fun () ->
            route domain verb rest (System.IO.File.ReadAllText(filePath)))
    | domain :: verb :: rest ->
        Ui.InterfaceBridge.Startup.run (fun () -> route domain verb rest (Console.In.ReadToEnd()))
    | _ ->
        eprintfn "Usage: SonOfLeoCli <domain> <verb> [--file <path>] [args...]"
        1
