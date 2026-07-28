module Tests.Integrated.Railroad

open Utilities.AppError
open Xunit


let railroadWrapper (railroad: Result<'T, AppError>) : unit =
    match railroad with
    | Ok _ -> ()
    | Error e -> Assert.Fail(AppError.toMessage e)
