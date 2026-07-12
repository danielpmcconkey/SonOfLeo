module InterfaceBridge.BoundaryConverters.MoneyFieldConverters

open Model
open InterfaceBridge.BoundaryConverters.GenericFieldHelpers

let convertDecimalOptionToMoneyOption
    (decimalOption: decimal option)
    : Result<Money option, string> =
    let fallibleConverter = (fun string -> string |> Money.fromDecimal)
    decimalOption
    |> convertOptionToDesiredTypeWithFallibleConverter fallibleConverter

