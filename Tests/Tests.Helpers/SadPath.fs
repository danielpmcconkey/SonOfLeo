module Tests.Helpers.SadPath

open System
open Microsoft.FSharp.Reflection
open Utilities.AppError

(*
Functions that help with validating sad path functionality

Example usages

//  AccountCodeDoesntMatchAccountId of string
let burp = isCorrectError (Ok "burp") AccountCodeDoesntMatchAccountId None

// AccountDeactivationProposedDateIsInvalid of Guid * LocalDate * LocalDate
let fart = isCorrectError (Ok "fart") AccountDeactivationProposedDateIsInvalid None

// AccountNameTooLong of string * int
let sneeze = isCorrectErrorString (Ok "sneeze") "AccountNameTooLong" (Some "You probably need to clean up test data.")

// AccountBalanceFetchInvalidArguments (no arguments)
let cough = isCorrectErrorEmpty (Ok "cough") AccountBalanceFetchInvalidArguments None

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

let private makeDefault (t: Type) : obj =
    if t = typeof<string> then "" :> obj
    elif t.IsValueType then Activator.CreateInstance(t)
    else null

let isCorrectError
    (result: Result<'T, AppError>)
    (expectedCaseConstructor: 'A -> AppError)
    (additionalWarningOnSuccess: string option)
    : Result<unit, AppError> =
    let argType = typeof<'A>
    let defaultArg =
        if FSharpType.IsTuple argType then
            let elements = FSharpType.GetTupleElements argType |> Array.map makeDefault
            FSharpValue.MakeTuple(elements, argType) :?> 'A
        else
            makeDefault argType :?> 'A
    let sample = expectedCaseConstructor defaultArg
    let caseName = FSharpValue.GetUnionFields(sample, typeof<AppError>) |> fst |> _.Name
    isCorrectErrorString result caseName additionalWarningOnSuccess
    
let isCorrectErrorEmpty
    (result: Result<'T, AppError>)
    expectedCaseConstructor
    (additionalWarningOnSuccess: string option)
    : Result<unit, AppError> =
    // build a "default" version of that error so we can get the string type for the comparison
    let sample = expectedCaseConstructor
    let caseName = FSharpValue.GetUnionFields(sample, typeof<AppError>) |> fst |> _.Name
    isCorrectErrorString result caseName additionalWarningOnSuccess
    
