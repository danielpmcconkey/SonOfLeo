module InterfaceBridge.BoundaryConverters.MoneyFieldConverters

open Model
open InterfaceBridge.BoundaryConverters.GenericFieldHelpers

let ``convert Decimal Option to Money Option``
    (decimalOption: decimal option)
    : Result<Money option, string> =
    let fallibleConverter = (fun string -> string |> Money.fromDecimal)
    decimalOption
    |> ``convert Option to Desired Type with Fallible Converter`` fallibleConverter

