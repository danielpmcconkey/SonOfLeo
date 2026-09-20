open System
open App.Utility.AppError
open Ui.InterfaceBridge.CommandRoute
open Ui.InterfaceBridge.Routes.ReportRoutes

let route name rest payload : Result<string, AppError> =
    match reportingRoutes |> List.tryFind(fun r -> r.name = name) with
    | Some command -> command.handler payload rest
    | None -> Error(ReportingUnknownReportName name)

[<EntryPoint>]
let main args =
    let argList = args |> Array.toList
    let name, payload, rest =
        match argList with
        | name :: "--file" :: filePath :: rest -> name, System.IO.File.ReadAllText(filePath), rest
        | name :: rest -> name, Console.In.ReadToEnd(), rest
        | _ ->
            eprintfn "Usage: Reports <name> [--file <path>] [args...]"
            exit 1; failwith ""
    match route name rest payload with
    | Ok n -> n |> printfn "%s"; 0
    | Error e -> e |> AppError.toMessage |> eprintfn "%s"; 1

