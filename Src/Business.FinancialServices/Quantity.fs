module Business.FinancialServices.Quantity

open System
open App.Utility.IAppError
open Business.FinancialServices.BizFinServError

type Quantity = private { amount: decimal }

let maxQuantity: decimal = 9999999999.999999M

let amount (q: Quantity) = q.amount

// Rejected, never rounded: the rounded value is only a check on the raw one.
let fromDecimal (raw: decimal) : Result<Quantity, IAppError> =
    let rounded = Math.Round(raw, 6, MidpointRounding.AwayFromZero)
    match raw with
    | x when x <> rounded -> Error(QuantityFailedToConvertImproperPrecision raw)
    | x when x < 0M -> Error(QuantityFailedToConvertNegative raw)
    | x when x > maxQuantity -> Error(QuantityFailedToConvertExceededMax(raw, maxQuantity))
    | _ -> Ok { amount = raw }

let isEqual (q: Quantity) (r: Quantity) : bool = q.amount = r.amount
let isLessThan (q: Quantity) (r: Quantity) : bool = q.amount < r.amount
let isGreaterThan (q: Quantity) (r: Quantity) : bool = q.amount > r.amount
let isLessThanOrEqual (q: Quantity) (r: Quantity) : bool = q.amount <= r.amount
let isGreaterThanOrEqual (q: Quantity) (r: Quantity) : bool = q.amount >= r.amount

let isPositive (q: Quantity) : bool = q.amount > 0M
