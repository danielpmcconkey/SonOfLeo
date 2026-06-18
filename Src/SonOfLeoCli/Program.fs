open System
open Model.UI.InterfaceContractTypes
open SonOfLeoCli.AccountRoutes


let commandRoutes = accountDomainCommandRoutes // in future append other domain routes
    

let route (domain) (verb) (rest) (payload) : Result<string, string> =
  match commandRoutes |> List.tryFind (fun r -> r.domain = domain && r.verb = verb) with
  | Some command -> command.handler payload rest
  | None -> Error $"Unknown command: {domain} {verb}"

    
[<EntryPoint>]
let main args =
    let payload = Console.In.ReadToEnd()
    match args |> Array.toList with
    | domain :: verb :: rest ->
        let result = (route domain verb rest payload)
        match result with
        | Ok n -> n |> printfn "%s"; 0
        | Error e -> e |> eprintfn "%s"; 1
    | _ -> eprintfn "Usage: sonofleo <domain> <verb> [args...]"; 1
