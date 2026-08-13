open System
open InterfaceBridge.Routes.AccountRoutes
open InterfaceBridge.Routes.FiscalPeriodRoutes
open InterfaceBridge.Routes.IngestionRoutes
open InterfaceBridge.Routes.JournalEntryRoutes
open InterfaceBridge.CommandRoute
open Utilities.AppError


let commandRoutes =
    accountDomainCommandRoutes @ fiscalPeriodDomainCommandRoutes @ journalEntryDomainCommandRoutes @ ingestionDomainCommandRoutes

let route domain verb rest payload : Result<string, AppError> =
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
    | Error e -> e |> AppError.toMessage |> eprintfn "%s"; 1
