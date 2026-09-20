module Ui.InterfaceBridge.BoundaryConverters.MoneyFieldConverters

open App.Utility.AppError
open App.Utility.Result
open Business.FinancialServices

let ``convert Decimal Option to Money Option``
    (decimalOption: decimal option)
    : Result<Money.Money option, AppError> =
    let fallibleConverter = (fun string -> string |> Money.fromDecimal)
    decimalOption |> convertOptionToDesiredTypeWithFallibleConverter fallibleConverter
