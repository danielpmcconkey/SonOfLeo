open System
open App.Utility.IAppError
open Ui.InterfaceBridge.CommandRoute
open Ui.InterfaceBridge.Routes.ReportRoutes
open Ui.ReportCli.ReportCliError

let route name rest payload : Result<string, IAppError> =
    match reportingRoutes |> List.tryFind(fun r -> r.name = name) with
    | Some command -> command.handler payload rest
    | None -> Error(ReportingUnknownReportName name)

[<EntryPoint>]
let main args =
    match args |> Array.toList with
    | name :: "--file" :: filePath :: rest ->
        Ui.InterfaceBridge.Startup.run "Reports" (fun () -> route name rest (System.IO.File.ReadAllText(filePath)))
    | name :: rest -> Ui.InterfaceBridge.Startup.run "Reports" (fun () -> route name rest (Console.In.ReadToEnd()))
    | _ ->
        eprintfn "Usage: Reports <name> [--file <path>] [args...]"
        1
