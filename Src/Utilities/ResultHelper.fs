module Utilities.ResultHelper

open Utilities.AppError



/// ``convert Option to Desired Type with Fallible Converter`` is a converter for dealing with
/// arguments:
///     sourceOption is an option in the original type
///     fallibleConverter is a conversion function that takes a non-option of the original type and returns a Result of the desired type
/// returns:
///     a Result of desired type 
let ``convert Option to Desired Type with Fallible Converter``
        (fallibleConverter: 'a -> Result<'b, AppError>)
        (sourceOption: 'a option)
        : Result<'b option, AppError> =
    match sourceOption with
    | None -> Ok None
    | Some x -> fallibleConverter x |> Result.map Some
