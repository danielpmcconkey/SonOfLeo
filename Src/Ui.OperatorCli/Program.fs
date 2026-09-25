open System
open App.Utility.IAppError
open Ui.InterfaceBridge.Routes.AccountRoutes
open Ui.InterfaceBridge.Routes.FiscalPeriodRoutes
open Ui.InterfaceBridge.Routes.JournalEntryRoutes
open Ui.InterfaceBridge.Routes.IngestionRoutes
open Ui.InterfaceBridge.Routes.CashFlowRoutes
open Ui.InterfaceBridge.Routes.ClassificationRoutes
open Ui.InterfaceBridge.CommandRoute
open Ui.OperatorCli.OperatorCliError


let commandRoutes =
    accountDomainCommandRoutes @ fiscalPeriodDomainCommandRoutes @ journalEntryDomainCommandRoutes @ ingestionDomainCommandRoutes @ classificationDomainCommandRoutes @ cashFlowDomainCommandRoutes

let route domain verb rest payload : Result<string, IAppError> =
    match commandRoutes |> List.tryFind(fun r -> r.domain = domain && r.verb = verb) with
    | Some command -> command.handler payload rest
    | None -> Error(CliUnknownCommand(domain, verb))

[<EntryPoint>]
let main args =
    let argList = args |> Array.toList
    let domain, verb, payload, rest =
        match argList with
        | domain :: verb :: "--file" :: filePath :: rest -> domain, verb , System.IO.File.ReadAllText(filePath), rest
        | domain :: verb :: rest  -> domain, verb, Console.In.ReadToEnd(), rest
        | _ ->
            eprintfn "Usage: SonOfLeoCli <domain> <verb> [--file <path>] [args...]"
            exit 1; failwith ""
    match route domain verb rest payload with 
    | Ok n -> n |> printfn "%s"; 0
    | Error e -> e.ToMessage() |> eprintfn "%s"; 1
