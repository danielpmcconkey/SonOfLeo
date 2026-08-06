open System
open InterfaceBridge.CommandRoute
open InterfaceBridge.Routes.ReportRoutes
open Utilities.AppError

let route name rest payload : Result<string, AppError> =
    match reportingRoutes |> List.tryFind(fun r -> r.name = name) with
    | Some command -> command.handler payload rest
    | None -> Error(ReportingUnknownReportName name)

[<EntryPoint>]
let main args =
    let payload = Console.In.ReadToEnd()
    match args |> Array.toList with
    | name :: rest ->
        let result = (route name rest payload)
        match result with
        | Ok n ->
            n |> printfn "%s"
            0
        | Error e ->
            e |> AppError.toMessage |> eprintfn "%s"
            1
    | _ ->
        eprintfn "Usage: Reports <name>  [args...]"
        1
