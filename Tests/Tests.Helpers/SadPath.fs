module Tests.Helpers.SadPath

open System
open Microsoft.FSharp.Reflection
open App.Utility.IAppError
open Tests.Helpers.TestError

(*
Functions that help with validating sad path functionality

Errors cross every Src boundary as IAppError; each domain's error DU implements it. Two ways
to assert on the case that came back:

1. Typed match with the AsError active pattern. The domain type is inferred from the case,
   so the match stays exhaustive-checked and can bind the payload:

    match result with
    | Error (AsError (AccountNameTooLong (returned, limit))) -> Assert.Equal(raw, returned)
    | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
    | Ok _ -> Assert.Fail "Expected failure; got success"

2. The isCorrectError family, when only the case matters:

//  AccountCodeDoesntMatchAccountId of string
let burp = isCorrectError (Ok "burp") AccountCodeDoesntMatchAccountId None

// AccountDeactivationProposedDateIsInvalid of Guid * LocalDate * LocalDate
let fart = isCorrectError (Ok "fart") AccountDeactivationProposedDateIsInvalid None

// AccountNameTooLong of string * int
let sneeze = isCorrectErrorString (Ok "sneeze") "AccountNameTooLong" (Some "You probably need to clean up test data.")

// AccountBalanceFetchInvalidArguments (no arguments)
let cough = isCorrectErrorEmpty (Ok "cough") AccountBalanceFetchInvalidArguments None

*)

/// AsError matches an IAppError that is the domain error type 'E, yielding it typed.
let (|AsError|_|) (e: IAppError) : 'E option =
    match box e with
    | :? 'E as typed -> Some typed
    | _ -> None

let private checkErrorCase
    (result: Result<'T, IAppError>)
    (isExpected: IAppError -> bool)
    (expectedDescription: string)
    (additionalWarningOnSuccess: string option)
    : Result<unit, IAppError> =
    match result with
    | Ok _ ->
        let warn = match additionalWarningOnSuccess with | Some x -> $" {x}" | None -> ""
        error (TestingError $"Expected failure; returned success.{warn}")
    | Error e ->
        if isExpected e then Ok()
        else
            error (TestingError
                $"Wrong error type. Expected {expectedDescription}. Got {e.DomainName}.{e.CaseName}: {e.ToMessage()}")

/// isCorrectErrorString matches on case name alone. Case names are unique across every domain's error DU, so the name
/// still identifies exactly one case.
let isCorrectErrorString
    (result: Result<'T, IAppError>)
    (expectedCase: string)
    (additionalWarningOnSuccess: string option)
    : Result<unit, IAppError> =
    checkErrorCase result (fun e -> e.CaseName = expectedCase) expectedCase additionalWarningOnSuccess

let private isCorrectErrorSample
    (result: Result<'T, IAppError>)
    (sample: IAppError)
    (additionalWarningOnSuccess: string option)
    : Result<unit, IAppError> =
    checkErrorCase
        result
        (fun e -> e.DomainName = sample.DomainName && e.CaseName = sample.CaseName)
        $"{sample.DomainName}.{sample.CaseName}"
        additionalWarningOnSuccess

let private makeDefault (t: Type) : obj =
    if t = typeof<string> then "" :> obj
    elif t.IsValueType then Activator.CreateInstance(t)
    else null

let isCorrectError
    (result: Result<'T, IAppError>)
    (expectedCaseConstructor: 'A -> #IAppError)
    (additionalWarningOnSuccess: string option)
    : Result<unit, IAppError> =
    let argType = typeof<'A>
    let defaultArg =
        if FSharpType.IsTuple argType then
            let elements = FSharpType.GetTupleElements argType |> Array.map makeDefault
            FSharpValue.MakeTuple(elements, argType) :?> 'A
        else
            makeDefault argType :?> 'A
    let sample = expectedCaseConstructor defaultArg :> IAppError
    isCorrectErrorSample result sample additionalWarningOnSuccess

let isCorrectErrorEmpty
    (result: Result<'T, IAppError>)
    (expectedCase: #IAppError)
    (additionalWarningOnSuccess: string option)
    : Result<unit, IAppError> =
    isCorrectErrorSample result (expectedCase :> IAppError) additionalWarningOnSuccess
