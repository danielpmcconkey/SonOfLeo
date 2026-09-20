module InterfaceBridge.BoundaryConverters.MoneyFieldConverters

open Model
open App.Utility.AppError
open App.Utility.Result

let ``convert Decimal Option to Money Option`` (decimalOption: decimal option) : Result<Money option, AppError> =
    let fallibleConverter = (fun string -> string |> Money.fromDecimal)
    decimalOption |> convertOptionToDesiredTypeWithFallibleConverter fallibleConverter
