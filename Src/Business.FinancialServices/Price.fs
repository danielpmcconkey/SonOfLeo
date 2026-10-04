module Business.FinancialServices.Price

open System
open App.Utility.IAppError
open Business.FinancialServices.BizFinServError
open Business.FinancialServices.Quantity

type Price = private { amount: decimal }

let maxPrice: decimal = 9999999999.999999M

let amount (p: Price) = p.amount

// Rejected, never rounded: the rounded value is only a check on the raw one.
let fromDecimal (raw: decimal) : Result<Price, IAppError> =
    let rounded = Math.Round(raw, 6, MidpointRounding.AwayFromZero)
    match raw with
    | x when x <> rounded -> Error(PriceFailedToConvertImproperPrecision raw)
    | x when x < 0M -> Error(PriceFailedToConvertNegative raw)
    | x when x > maxPrice -> Error(PriceFailedToConvertExceededMax(raw, maxPrice))
    | _ -> Ok { amount = raw }

let isEqual (p: Price) (q: Price) : bool = p.amount = q.amount
let isLessThan (p: Price) (q: Price) : bool = p.amount < q.amount
let isGreaterThan (p: Price) (q: Price) : bool = p.amount > q.amount
let isLessThanOrEqual (p: Price) (q: Price) : bool = p.amount <= q.amount
let isGreaterThanOrEqual (p: Price) (q: Price) : bool = p.amount >= q.amount

/// The exact product, not Money. A caller that needs Money converts it with Money.fromDecimal.
let multiplyQuantity (quantity: Quantity) (price: Price) : decimal =
    (quantity |> Quantity.amount) * price.amount
