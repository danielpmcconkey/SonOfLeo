module Ui.InterfaceBridge.Startup

open App.Utility.IAppError

/// run is the interfaces' outermost frame: a command's output goes to stdout with exit code 0, and a typed error's
/// message to stderr with exit code 1.
let run (command: unit -> Result<string, IAppError>) : int =
    match command () with
    | Ok output ->
        output |> printfn "%s"
        0
    | Error e ->
        e.ToMessage() |> eprintfn "%s"
        1
