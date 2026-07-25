module Utilities.ResultHelper

open Utilities.AppError

/// ResultBuilder is a class that provides computational expressions for
/// more elegant results binding and mapping
type ResultBuilder() =
    member _.Bind(result, f) = Result.bind f result
    member _.Return(value) = Ok value
    member _.ReturnFrom(result) = result
    member _.Zero() = Ok()

let result = ResultBuilder()

let convertListOfResultsToResultsList<'T> (listOfResults: Result<'T, AppError> list) : Result<'T list, AppError> =
    listOfResults
    |> List.foldBack(fun createResult acc ->
        match createResult, acc with
        | Ok validCr, Ok validAcc -> Ok(validCr :: validAcc)
        | Error e, _ -> Error e
        | _, Error e -> Error e)
    <| Ok []

/// convertOptionToDesiredTypeWithFallibleConverter is a converter for
/// dealing with options (typically primitives) that need to be converting
/// to a desired type, but whose converters can fail, meaning that you
/// can't use a traditional option map.
///
/// arguments:
///     sourceOption is an option in the original type
///
///     fallibleConverter is a conversion function that takes a non-option
///         of the original type and returns a Result of the desired type
///
/// returns:
///     a Result of desired type
let convertOptionToDesiredTypeWithFallibleConverter
    (fallibleConverter: 'a -> Result<'b, AppError>)
    (sourceOption: 'a option)
    : Result<'b option, AppError> =
    match sourceOption with
    | None -> Ok None
    | Some x -> fallibleConverter x |> Result.map Some
