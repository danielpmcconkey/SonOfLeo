open System
open InterfaceBridge.Routes.AccountRoutes
open InterfaceBridge.Routes.FiscalPeriodRoutes
open InterfaceBridge.Routes.JournalEntryRoutes
open InterfaceBridge.CommandRoute
open Utilities.AppError


let commandRoutes = 
    accountDomainCommandRoutes
    @ fiscalPeriodDomainCommandRoutes
    @ journalEntryDomainCommandRoutes

let route domain verb rest payload : Result<string, AppError> =
  match commandRoutes |> List.tryFind (fun r -> r.domain = domain && r.verb = verb) with // REQ-NGUI-1.1, REQ-NGUI-3.8
  | Some command -> command.handler payload rest
  | None -> Error (CliUnknownCommand (domain, verb)) // REQ-NGUI-3.9

[<EntryPoint>]
let main args =
    let payload = Console.In.ReadToEnd() // REQ-NGUI-3.3
    match args |> Array.toList with
    | domain :: verb :: rest -> // REQ-NGUI-3.1, REQ-NGUI-3.2, REQ-NGUI-3.4
        let result = (route domain verb rest payload) // REQ-NGUI-1.1
        match result with
        | Ok n -> n |> printfn "%s"; 0 // REQ-NGUI-3.6 REQ-NGUI-1.3
        | Error e -> e |> AppError.toMessage |> eprintfn "%s"; 1 // REQ-NGUI-3.7, REQ-NGUI-1.3.1
    | _ -> eprintfn "Usage: sonofleo <domain> <verb> [args...]"; 1
