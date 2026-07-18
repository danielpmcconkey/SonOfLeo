module InterfaceBridge.BoundaryConverters.MoneyFieldConverters

open Model
open Utilities.AppError
open Utilities.ResultHelper

let ``convert Decimal Option to Money Option``
    (decimalOption: decimal option)
    : Result<Money option, AppError> =
    let fallibleConverter = (fun string -> string |> Money.fromDecimal)
    decimalOption
    |> convertOptionToDesiredTypeWithFallibleConverter fallibleConverter

