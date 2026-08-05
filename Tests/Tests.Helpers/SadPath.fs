module Tests.Helpers.SadPath

open Microsoft.FSharp.Reflection
open Utilities.AppError

(*
Functions that help with validating sad path functionality

Example usages

//  AccountCodeDoesntMatchAccountId of string
let burp =
    isCorrectError (Ok "burp") AccountCodeDoesntMatchAccountId None

// AccountDeactivationProposedDateIsInvalid of Guid * LocalDate * LocalDate
let fart =
    isCorrectError (Ok "fart") AccountDeactivationProposedDateIsInvalid None

// AccountNameTooLong of string * int
let sneeze = isCorrectErrorString (Ok "sneeze") "AccountNameTooLong" (Some "You probably need to clean up test data.")

*)

let isCorrectErrorString
    (result: Result<'T, AppError>)
    (expected: string)
    (additionalWarningOnSuccess: string option)
    : Result<unit, AppError> =
    match result with
    | Ok _ ->
        let warn = match additionalWarningOnSuccess with | Some x -> $" {x}" | None -> ""
        Error(TestingError $"Expected failure; returned success.{warn}")
    | Error e ->
        let caseName = FSharpValue.GetUnionFields(e, typeof<AppError>) |> fst |> _.Name
        if caseName = expected then Ok()
        else Error(TestingError $"Wrong error type. Expected {expected}. Got {caseName}: {AppError.toMessage e}")

let isCorrectError
    (result: Result<'T, AppError>)
    (expectedCaseConstructor: 'A -> AppError)
    (additionalWarningOnSuccess: string option)
    : Result<unit, AppError> =
    // build a "default" version of that error so we can get the string type for the comparison
    let sample = expectedCaseConstructor (Unchecked.defaultof<'A>)
    let caseName = FSharpValue.GetUnionFields(sample, typeof<AppError>) |> fst |> _.Name
    isCorrectErrorString result caseName additionalWarningOnSuccess
    
