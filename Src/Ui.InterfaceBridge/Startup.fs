module Ui.InterfaceBridge.Startup

open App.Utility.IAppError
open App.Utility.Result
open Ui.InterfaceBridge.BridgeError

/// confirmConfiguration reads every setting a command depends on before any command runs: the configuration file, the
/// time zone, and the connection string. It does not connect, so a failure here means no data was read or written.
let confirmConfiguration () : Result<unit, IAppError> =
    result {
        do! App.Utility.Config.confirmReadable ()
        let! _ = App.Utility.Clock.configuredTimeZone.Value
        do! App.DataAccessLayer.DbConnection.confirmConfigured ()
    }

/// run is the interfaces' outermost frame. Configuration is confirmed first. A typed error is reported as its message;
/// anything the command throws is wrapped in a typed error carrying the exception, so its message and stack trace are
/// reported the same way. Either exits non-zero instead of terminating on an unhandled exception.
let run (programName: string) (command: unit -> Result<string, IAppError>) : int =
    try
        match confirmConfiguration () |> Result.bind command with
        | Ok output ->
            output |> printfn "%s"
            0
        | Error e ->
            e.ToMessage() |> eprintfn "%s"
            1
    with ex ->
        InterfaceCommandThrew(programName, ex) |> BridgeError.toMessage |> eprintfn "%s"
        1
