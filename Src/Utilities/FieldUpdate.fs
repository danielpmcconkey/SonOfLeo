module Utilities.FieldUpdate

open Utilities.AppError
open Utilities.ResultHelper


/// FieldUpdate is a simple DU to use for functions that can update one
/// or many columns. This allows us to easily distinguish between "don't
/// update" and "update it to null"
type FieldUpdate<'a> =
    | NoChange
    | SetTo of 'a

module FieldUpdate =

    /// Convert from a field update of type-A to a field update of type-B,
    /// using the standard A -> B converter. It unpacks the FieldUpdate before
    /// putting it back together.
    let map (typeConverter: 'A -> 'B) (original: FieldUpdate<'A>) : FieldUpdate<'B> =
        original
        |> function
            | NoChange -> NoChange
            | SetTo x -> SetTo(x |> typeConverter)

    let mapNoChangeToOptionWithConversion conversion original =
        match original with
        | NoChange -> None
        | SetTo n -> n |> conversion |> Some


    /// Convert from a field update of type-A to a field update of type-B,
    /// using the standard FALLIBLE A -> B converter. It unpacks the
    /// FieldUpdate before putting it back together and finally wraps the whole
    /// thing in the fallible converter's result.
    let convertFieldUpdateToNewTypeFallible
        (typeConverter: 'A -> Result<'B, AppError>)
        (original: FieldUpdate<'A>)
        : Result<FieldUpdate<'B>, AppError> =
        original
        |> function
            | NoChange -> Ok NoChange
            | SetTo x -> x |> typeConverter |> Result.map SetTo

    /// Convert from a field update of type-A option to a field update of
    /// type-B option, using the standard A -> B converter. It unpacks the
    /// FieldUpdate and the option before putting it back together.
    let convertFieldUpdateOptionToNewTypeOption
        (nonOptionConverter: 'A -> 'B)
        (original: FieldUpdate<'A option>)
        : FieldUpdate<'B option> =
        original
        |> function
            | NoChange -> NoChange
            | SetTo x -> SetTo(x |> Option.map nonOptionConverter)

    /// Convert from a field update of type-A option to a field update of
    /// type-B option, using the standard FALLIBLE A -> B converter. It unpacks
    /// the FieldUpdate and the option before putting it back together and
    /// finally wraps the whole thing in the fallible converter's result.
    let convertFieldUpdateOptionToNewTypeOptionFallible
        (nonOptionConverter: 'A -> Result<'B, AppError>)
        (original: FieldUpdate<'A option>)
        : Result<FieldUpdate<'B option>, AppError> =
        original
        |> function
            | NoChange -> Ok NoChange
            | SetTo x -> x |> convertOptionToDesiredTypeWithFallibleConverter nonOptionConverter |> Result.map SetTo
