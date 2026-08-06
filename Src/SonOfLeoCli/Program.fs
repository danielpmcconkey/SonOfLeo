open System
open InterfaceBridge.Routes.AccountRoutes
open InterfaceBridge.Routes.FiscalPeriodRoutes
open InterfaceBridge.Routes.JournalEntryRoutes
open InterfaceBridge.CommandRoute
open Utilities.AppError


let commandRoutes =
    accountDomainCommandRoutes @ fiscalPeriodDomainCommandRoutes @ journalEntryDomainCommandRoutes

let route domain verb rest payload : Result<string, AppError> =
    match commandRoutes |> List.tryFind(fun r -> r.domain = domain && r.verb = verb) with
    | Some command -> command.handler payload rest
    | None -> Error(CliUnknownCommand(domain, verb))

[<EntryPoint>]
let main args =
    let payload = Console.In.ReadToEnd()
    match args |> Array.toList with
    | domain :: verb :: rest ->
        let result = (route domain verb rest payload)
        match result with
        | Ok n ->
            n |> printfn "%s"
            0
        | Error e ->
            e |> AppError.toMessage |> eprintfn "%s"
            1
    | _ ->
        eprintfn "Usage: SonOfLeoCli <domain> <verb> [args...]"
        1
