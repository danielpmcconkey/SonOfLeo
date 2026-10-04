module Tests.Isolated.Model.QuantityAndPrice

open System
open App.Utility.IAppError
open Business.FinancialServices
open Business.FinancialServices.BizFinServError
open Tests.Helpers.TestError
open Xunit

let private dec (text: string) = Decimal.Parse(text, Globalization.CultureInfo.InvariantCulture)

/// The decimal's text, which carries its scale: 1.50 and 1.5 are equal decimals but different text.
let private exactText (d: decimal) = d.ToString(Globalization.CultureInfo.InvariantCulture)

let private quantity (d: decimal) =
    Quantity.fromDecimal d |> Result.defaultWith (fun (e: IAppError) -> failwith (e.ToMessage()))

let private price (d: decimal) =
    Price.fromDecimal d |> Result.defaultWith (fun (e: IAppError) -> failwith (e.ToMessage()))

[<Fact>]
let ``REQ-QP-1.1 REQ-QP-3.1 Quantity.fromDecimal accepts zero and gives back zero`` () =
    match Quantity.fromDecimal 0M with
    | Ok q -> Assert.Equal(0M, q |> Quantity.amount)
    | Error e -> Assert.Fail(e.ToMessage())

[<Fact>]
let ``REQ-QP-1.1 REQ-QP-3.1 Quantity.fromDecimal rejects -0.000001 with a typed negative-value error carrying the value`` () =
    match Quantity.fromDecimal -0.000001M with
    | Error (AsError (QuantityFailedToConvertNegative raw)) -> Assert.Equal(-0.000001M, raw)
    | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
    | Ok _ -> Assert.Fail "Expected failure; got success"

[<Fact>]
let ``REQ-QP-1.2 REQ-QP-3.1 Quantity.fromDecimal accepts the maximum 9,999,999,999.999999 and gives it back unchanged`` () =
    match Quantity.fromDecimal 9999999999.999999M with
    | Ok q -> Assert.Equal("9999999999.999999", q |> Quantity.amount |> exactText)
    | Error e -> Assert.Fail(e.ToMessage())

[<Fact>]
let ``REQ-QP-1.2 REQ-QP-3.1 Quantity.fromDecimal rejects 10,000,000,000, the smallest six-place value above the maximum, with a typed exceeds-maximum error carrying the value`` () =
    match Quantity.fromDecimal 10000000000.000000M with
    | Error (AsError (QuantityFailedToConvertExceededMax (raw, _))) -> Assert.Equal(10000000000M, raw)
    | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
    | Ok _ -> Assert.Fail "Expected failure; got success"

[<Fact>]
let ``REQ-QP-1.3 REQ-QP-3.1 Quantity.fromDecimal accepts a value with exactly six decimal places and gives back every digit`` () =
    match Quantity.fromDecimal 123.456789M with
    | Ok q -> Assert.Equal("123.456789", q |> Quantity.amount |> exactText)
    | Error e -> Assert.Fail(e.ToMessage())

[<Theory>]
[<InlineData("0.0000001")>]
[<InlineData("1.2345675")>]
let ``REQ-QP-1.3 REQ-QP-3.1 for each of 0.0000001 and 1.2345675, Quantity.fromDecimal rejects a value with a seventh decimal place with a typed precision error carrying the unrounded value`` (text: string) =
    match Quantity.fromDecimal (dec text) with
    | Error (AsError (QuantityFailedToConvertImproperPrecision raw)) -> Assert.Equal(text, raw |> exactText)
    | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
    | Ok _ -> Assert.Fail "Expected failure; got success"

[<Fact>]
let ``REQ-QP-2.1 REQ-QP-3.2 Price.fromDecimal accepts zero and gives back zero`` () =
    match Price.fromDecimal 0M with
    | Ok p -> Assert.Equal(0M, p |> Price.amount)
    | Error e -> Assert.Fail(e.ToMessage())

[<Fact>]
let ``REQ-QP-2.1 REQ-QP-3.2 Price.fromDecimal rejects -0.000001 with a typed negative-value error carrying the value`` () =
    match Price.fromDecimal -0.000001M with
    | Error (AsError (PriceFailedToConvertNegative raw)) -> Assert.Equal(-0.000001M, raw)
    | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
    | Ok _ -> Assert.Fail "Expected failure; got success"

[<Fact>]
let ``REQ-QP-2.2 REQ-QP-3.2 Price.fromDecimal accepts the maximum 9,999,999,999.999999 and gives it back unchanged`` () =
    match Price.fromDecimal 9999999999.999999M with
    | Ok p -> Assert.Equal("9999999999.999999", p |> Price.amount |> exactText)
    | Error e -> Assert.Fail(e.ToMessage())

[<Fact>]
let ``REQ-QP-2.2 REQ-QP-3.2 Price.fromDecimal rejects 10,000,000,000, the smallest six-place value above the maximum, with a typed exceeds-maximum error carrying the value`` () =
    match Price.fromDecimal 10000000000.000000M with
    | Error (AsError (PriceFailedToConvertExceededMax (raw, _))) -> Assert.Equal(10000000000M, raw)
    | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
    | Ok _ -> Assert.Fail "Expected failure; got success"

[<Fact>]
let ``REQ-QP-2.3 REQ-QP-3.2 Price.fromDecimal accepts a value with exactly six decimal places and gives back every digit`` () =
    match Price.fromDecimal 98.765432M with
    | Ok p -> Assert.Equal("98.765432", p |> Price.amount |> exactText)
    | Error e -> Assert.Fail(e.ToMessage())

[<Theory>]
[<InlineData("0.0000001")>]
[<InlineData("1.2345675")>]
let ``REQ-QP-2.3 REQ-QP-3.2 for each of 0.0000001 and 1.2345675, Price.fromDecimal rejects a value with a seventh decimal place with a typed precision error carrying the unrounded value`` (text: string) =
    match Price.fromDecimal (dec text) with
    | Error (AsError (PriceFailedToConvertImproperPrecision raw)) -> Assert.Equal(text, raw |> exactText)
    | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
    | Ok _ -> Assert.Fail "Expected failure; got success"

[<Theory>]
[<InlineData("0")>]
[<InlineData("0.000000")>]
[<InlineData("9999999999.999999")>]
[<InlineData("42.500000")>]
[<InlineData("3.141593")>]
let ``REQ-QP-3.3 for each of zero, the maximum and a six-place value, converting to a Quantity and back gives the identical decimal including its scale, and the same for a Price`` (text: string) =
    Assert.Equal(text, dec text |> quantity |> Quantity.amount |> exactText)
    Assert.Equal(text, dec text |> price |> Price.amount |> exactText)

/// Every ordered pair over the values, a value paired with itself included.
let private orderedPairs (values: decimal list) =
    [ for a in values do
          for b in values do
              yield a, b ]

let private comparisonValues = [ 0M; 1.000001M; 1.000002M; 250M ]

[<Fact>]
let ``REQ-QP-3.4 for every ordered pair of two different Quantities and of one Quantity with itself, each of equal, less than, greater than, at most and at least gives the verdict decimal comparison gives`` () =
    for a, b in orderedPairs comparisonValues do
        let qa, qb = quantity a, quantity b
        Assert.True((a = b) = Quantity.isEqual qa qb, $"isEqual {a} {b}")
        Assert.True((a < b) = Quantity.isLessThan qa qb, $"isLessThan {a} {b}")
        Assert.True((a > b) = Quantity.isGreaterThan qa qb, $"isGreaterThan {a} {b}")
        Assert.True((a <= b) = Quantity.isLessThanOrEqual qa qb, $"isLessThanOrEqual {a} {b}")
        Assert.True((a >= b) = Quantity.isGreaterThanOrEqual qa qb, $"isGreaterThanOrEqual {a} {b}")

[<Fact>]
let ``REQ-QP-3.4 for every ordered pair of two different Prices and of one Price with itself, each of equal, less than, greater than, at most and at least gives the verdict decimal comparison gives`` () =
    for a, b in orderedPairs comparisonValues do
        let pa, pb = price a, price b
        Assert.True((a = b) = Price.isEqual pa pb, $"isEqual {a} {b}")
        Assert.True((a < b) = Price.isLessThan pa pb, $"isLessThan {a} {b}")
        Assert.True((a > b) = Price.isGreaterThan pa pb, $"isGreaterThan {a} {b}")
        Assert.True((a <= b) = Price.isLessThanOrEqual pa pb, $"isLessThanOrEqual {a} {b}")
        Assert.True((a >= b) = Price.isGreaterThanOrEqual pa pb, $"isGreaterThanOrEqual {a} {b}")

[<Fact>]
let ``REQ-QP-3.5 a Quantity of 1.234567 times a Price of 2.5 is exactly 3.0864175, keeping the digits beyond two decimal places that Money could not hold`` () =
    let product = Price.multiplyQuantity (quantity 1.234567M) (price 2.5M)
    Assert.Equal(3.0864175M, product)

[<Theory>]
[<InlineData("10", "0", "0")>]
[<InlineData("0.5", "0.5", "0.25")>]
[<InlineData("100.000001", "3", "300.000003")>]
[<InlineData("0.000001", "0.000001", "0.000000000001")>]
[<InlineData("9999999999.999999", "0.000001", "9999.999999999999")>]
[<InlineData("9999999999.999999", "2.123456", "21234559999.999997876544")>]
let ``REQ-QP-3.5 a Quantity times a Price is the exact decimal product for each of several pairs, including a zero Price and the maximum Quantity at a six-place Price`` (q: string, p: string, expected: string) =
    Assert.Equal(dec expected, Price.multiplyQuantity (quantity (dec q)) (price (dec p)))
