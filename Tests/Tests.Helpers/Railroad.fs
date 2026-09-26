module Tests.Helpers.Railroad

open App.Utility.IAppError
open Xunit


let railroadWrapper (railroad: Result<'T, IAppError>) : unit =
    match railroad with
    | Ok _ -> ()
    | Error e -> Assert.Fail(e.ToMessage())
