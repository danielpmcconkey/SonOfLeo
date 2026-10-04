module Tests.Isolated.Model.Money

open App.Session
open Business.General
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.CrossDomainOrchestration
open System
open Business.FinancialServices.Money
open Tests.Helpers.Railroad
open App.Utility.IAppError
open Tests.Helpers.TestError
open Tests.Helpers.SadPath
open App.Utility.Result
open Xunit
open Business.FinancialServices.BizFinServError

// =============================================================================
// fromDecimal
// =============================================================================

let private dec (text: string) = Decimal.Parse(text, Globalization.CultureInfo.InvariantCulture)

let private decs (text: string) = text.Split('|') |> Array.map dec |> List.ofArray

/// Splits the amount n ways and returns the share amounts, failing the test on an error.
let private shareAmounts (amountText: string) (n: int) =
    fromDecimal (dec amountText)
    |> Result.bind (fun source -> splitByN source n)
    |> Result.map (List.map amount)
    |> Result.defaultWith (fun (e: IAppError) -> failwith (e.ToMessage()))

[<Theory>]
[<InlineData("9999999999.99")>]
[<InlineData("-9999999999.99")>]
let ``REQ-MON-1.2 REQ-MON-1.3 fromDecimal accepts the literal limits 9,999,999,999.99 and -9,999,999,999.99`` (limit: string) =
    match fromDecimal (dec limit) with
    | Ok m -> Assert.Equal(dec limit, amount m)
    | Error e -> Assert.Fail(e.ToMessage())

[<Fact>]
let ``REQ-MON-1.2 fromDecimal rejects 10,000,000,000.00 as exceeding the maximum`` () =
    match fromDecimal 10000000000.00M with
    | Error (AsError (MoneyFailedToConvertExceededMax _)) -> ()
    | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
    | Ok _ -> Assert.Fail "Expected failure; got success"

[<Fact>]
let ``REQ-MON-1.3 fromDecimal rejects -10,000,000,000.00 as below the minimum`` () =
    match fromDecimal -10000000000.00M with
    | Error (AsError (MoneyFailedToConvertBelowMin _)) -> ()
    | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
    | Ok _ -> Assert.Fail "Expected failure; got success"

[<Fact>]
let ``REQ-MON-2.2 fromDecimal accepts valid 2dp amount`` () =
    let amount_d = 3.99M
    let m = fromDecimal amount_d |> Result.defaultWith(fun (e: IAppError) -> failwith(e.ToMessage()))
    Assert.Equal(amount_d, amount m)

[<Fact>]
let ``REQ-MON-2.2 fromDecimal accepts negative amounts`` () =
    let amount_d = -3.99M
    let m = fromDecimal amount_d |> Result.defaultWith(fun (e: IAppError) -> failwith(e.ToMessage()))
    Assert.Equal(amount_d, amount m)

[<Fact>]
let ``REQ-MON-2.2 fromDecimal accepts zero`` () =
    let amount_d = 0M
    let m = fromDecimal amount_d |> Result.defaultWith(fun (e: IAppError) -> failwith(e.ToMessage()))
    Assert.Equal(amount_d, amount m)

[<Fact>]
let ``REQ-MON-2.2.1 REQ-MON-1.4 fromDecimal rejects amount with more than 2dp precision`` () =
    let amount_d = 3.998M
    let result = fromDecimal amount_d
    match result with
    | Error (AsError (MoneyFailedToConvertImproperPrecision _)) -> ()
    | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
    | Ok _ -> Assert.Fail "Expected failure; got success"

[<Fact>]
let ``REQ-MON-2.2.1 REQ-MON-1.2 fromDecimal rejects amount exceeding maxMoney`` () =
    let amount_d = maxMoney + 0.01M
    let result = fromDecimal amount_d
    match result with
    | Error (AsError (MoneyFailedToConvertExceededMax _)) -> ()
    | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
    | Ok _ -> Assert.Fail "Expected failure; got success"

[<Fact>]
let ``REQ-MON-2.2.1 REQ-MON-1.3 fromDecimal rejects amount below minMoney`` () =
    let amount_d = minMoney - 0.01M
    let result = fromDecimal amount_d
    match result with
    | Error (AsError (MoneyFailedToConvertBelowMin _)) -> ()
    | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
    | Ok _ -> Assert.Fail "Expected failure; got success"

[<Fact>]
let ``REQ-MON-2.3 fromDecimalList converts every element and returns as many Money values as it was given`` () =
    (* Assert.True(result.IsOk) is satisfied by Ok [] and by Ok [ garbage ] alike — the function
       is never asked to prove it converted anything. *)
    let list_d = [ -3.99M; 12.24M; 27194338M ]
    result {
        let! list_m = fromDecimalList list_d
        Assert.Equal(list_d |> List.length, list_m |> List.length)
        Assert.Equal<decimal list>(list_d, list_m |> List.map amount)
        return ()
    }
    |> railroadWrapper

[<Fact>]
let ``REQ-MON-2.3.1 fromDecimal list must check rounding precision`` () =
    let list_d = [ -3.99M; 12.243M; 27194338M ]
    let result = fromDecimalList list_d
    match result with
    | Error (AsError (MoneyFailedToConvertImproperPrecision _)) -> ()
    | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
    | Ok _ -> Assert.Fail "Expected failure; got success"

[<Fact>]
let ``REQ-MON-2.3.1 fromDecimal list must check max value`` () =
    let list_d = [ -3.99M; 12.24M; maxMoney + 0.01M ]
    let result = fromDecimalList list_d
    match result with
    | Error (AsError (MoneyFailedToConvertExceededMax _)) -> ()
    | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
    | Ok _ -> Assert.Fail "Expected failure; got success"

[<Fact>]
let ``REQ-MON-2.3.1 fromDecimal list must check min value`` () =
    let list_d = [ minMoney - 0.01M; 12.24M; 2719433M ]
    let result = fromDecimalList list_d
    match result with
    | Error (AsError (MoneyFailedToConvertBelowMin _)) -> ()
    | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
    | Ok _ -> Assert.Fail "Expected failure; got success"

[<Fact>]
let ``REQ-MON-2.3.2 fromDecimalList returns unsorted input, duplicates included, in its original positions`` () =
    let list_d = [ 12.24M; -3.99M; 27194338M; -3.99M; 0.01M ]
    match fromDecimalList list_d with
    | Error e -> Assert.Fail(e.ToMessage())
    | Ok list_m -> Assert.Equal<decimal list>(list_d, list_m |> List.map amount)

// =============================================================================
// splitByN
// =============================================================================

[<Fact>]
let ``REQ-MON-2.4 splitByN splits 111.17 three ways into 37.05, 37.06 and 37.06`` () =
    Assert.Equal<decimal list>([ 37.05M; 37.06M; 37.06M ], shareAmounts "111.17" 3)

[<Theory>]
[<InlineData("111.17", 3)>] // rounded share above the quotient: remainder subtracted
[<InlineData("100.00", 3)>] // rounded share below the quotient: remainder added
[<InlineData("-100.00", 3)>]
[<InlineData("-1.05", 2)>]
[<InlineData("0.01", 3)>]
[<InlineData("419.97", 30)>]
[<InlineData("9999999999.99", 2)>]
[<InlineData("-9999999999.99", 2)>]
let ``REQ-MON-2.4.1 splitByN returns n shares that sum exactly to the original amount`` (amountText: string, n: int) =
    let shares = shareAmounts amountText n
    Assert.Equal(n, shares |> List.length)
    Assert.Equal(dec amountText, shares |> List.sum)

[<Fact>]
let ``REQ-MON-2.4.2 splitByN rejects zero-ways split requests`` () =
    let expected = 111.17M
    result {
        let! source = fromDecimal expected
        let result = splitByN source 0
        return!
            match result with
            | Error (AsError (MoneyImproperSplit _)) -> Ok()
            | Error e -> Error(TestingError $"Wrong error. {e.ToMessage()}")
            | Ok _ -> Error(TestingError "Expected failure; got success")
    }
    |> railroadWrapper

[<Fact>]
let ``REQ-MON-2.4.3 splitByN rejects one-ways split requests`` () =
    let expected = 111.17M
    result {
        let! source = fromDecimal expected
        let result = splitByN source 1
        return!
            match result with
            | Error (AsError (MoneyImproperSplit _)) -> Ok()
            | Error e -> Error(TestingError $"Wrong error. {e.ToMessage()}")
            | Ok _ -> Error(TestingError "Expected failure; got success")
    }
    |> railroadWrapper

[<Fact>]
let ``REQ-MON-2.4.6 splitByN rejects negative-ways split requests`` () =
    let expected = 111.17M
    result {
        let! source = fromDecimal expected
        let result = splitByN source -1
        return!
            match result with
            | Error (AsError (MoneyImproperSplit _)) -> Ok()
            | Error e -> Error(TestingError $"Wrong error. {e.ToMessage()}")
            | Ok _ -> Error(TestingError "Expected failure; got success")
    }
    |> railroadWrapper

[<Theory>]
[<InlineData("1.05", 2, "0.52|0.53")>] // banker's rounding would give 0.52 for the second share
[<InlineData("-1.05", 2, "-0.52|-0.53")>] // rounding toward positive infinity would give -0.52
let ``REQ-MON-2.4.4 splitByN rounds a midway share away from zero`` (amountText: string, n: int, expectedText: string) =
    Assert.Equal<decimal list>(decs expectedText, shareAmounts amountText n)

[<Fact>]
let ``REQ-MON-2.4.5 splitByN subtracts the whole remainder from the first share when the rounded share is above the quotient`` () =
    // 419.97 / 30 = 13.999, rounded to 14.00; thirty of those overshoot by 0.03, taken from the first share
    Assert.Equal<decimal list>(13.97M :: List.replicate 29 14.00M, shareAmounts "419.97" 30)

[<Fact>]
let ``REQ-MON-2.4.5 splitByN adds the whole remainder to the first share when the rounded share is below the quotient`` () =
    // 100.00 / 3 = 33.333.., rounded to 33.33; three of those fall short by 0.01, added to the first share
    Assert.Equal<decimal list>([ 33.34M; 33.33M; 33.33M ], shareAmounts "100.00" 3)

// =============================================================================
// add / subtract
// =============================================================================

[<Fact>]
let ``REQ-MON-2.5 add function happy path`` () =
    let d1 = 145877.43M
    let d2 = -874.12M
    let expected = d1 + d2
    let m1 = fromDecimal d1 |> Result.defaultWith(fun (e: IAppError) -> failwith(e.ToMessage()))
    let m2 = fromDecimal d2 |> Result.defaultWith(fun (e: IAppError) -> failwith(e.ToMessage()))
    let result = add m1 m2
    match result with
    | Error e -> Assert.Fail(e.ToMessage())
    | Ok sumTotal -> Assert.Equal(expected, amount sumTotal)

[<Fact>]
let ``REQ-MON-2.5.1 add returns Error when sum exceeds max`` () =
    let d1 = maxMoney
    let d2 = 0.01M
    let m1 = fromDecimal d1 |> Result.defaultWith(fun (e: IAppError) -> failwith(e.ToMessage()))
    let m2 = fromDecimal d2 |> Result.defaultWith(fun (e: IAppError) -> failwith(e.ToMessage()))
    let result = add m1 m2
    match result with
    | Error (AsError (MoneyFailedToConvertExceededMax _)) -> ()
    | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
    | Ok _ -> Assert.Fail "Expected failure; got success"

[<Fact>]
let ``REQ-MON-2.5.1 add returns Error when sum falls below min`` () =
    let d1 = minMoney
    let d2 = -0.01M
    let m1 = fromDecimal d1 |> Result.defaultWith(fun (e: IAppError) -> failwith(e.ToMessage()))
    let m2 = fromDecimal d2 |> Result.defaultWith(fun (e: IAppError) -> failwith(e.ToMessage()))
    let result = add m1 m2
    match result with
    | Error (AsError (MoneyFailedToConvertBelowMin _)) -> ()
    | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
    | Ok _ -> Assert.Fail "Expected failure; got success"

[<Fact>]
let ``REQ-MON-2.6 subtract happy path`` () =
    let decimal1 = -874.12M
    let decimal2 = 145877.43M
    let expected = decimal2 - decimal1
    let val1 = fromDecimal decimal1 |> Result.defaultWith(fun (e: IAppError) -> failwith(e.ToMessage()))
    let val2 = fromDecimal decimal2 |> Result.defaultWith(fun (e: IAppError) -> failwith(e.ToMessage()))
    let result = subtractVal1FromVal2 val1 val2
    match result with
    | Error e -> Assert.Fail(e.ToMessage())
    | Ok sumTotal -> Assert.Equal(expected, amount sumTotal)

[<Fact>]
let ``REQ-MON-2.6.1 subtract returns Error when difference exceeds max`` () =
    // subtractVal1FromVal2 computes val2 - val1, so a negative val1 drives the result upward
    let decimal2 = maxMoney
    let decimal1 = -0.01M
    let val2 = fromDecimal decimal2 |> Result.defaultWith(fun (e: IAppError) -> failwith(e.ToMessage()))
    let val1 = fromDecimal decimal1 |> Result.defaultWith(fun (e: IAppError) -> failwith(e.ToMessage()))
    let result = subtractVal1FromVal2 val1 val2
    match result with
    | Error (AsError (MoneyFailedToConvertExceededMax _)) -> ()
    | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
    | Ok _ -> Assert.Fail "Expected failure; got success"

[<Fact>]
let ``REQ-MON-2.6.1 subtract returns Error when difference falls below min`` () =
    let decimal2 = minMoney
    let decimal1 = 0.01M
    let val2 = fromDecimal decimal2 |> Result.defaultWith(fun (e: IAppError) -> failwith(e.ToMessage()))
    let val1 = fromDecimal decimal1 |> Result.defaultWith(fun (e: IAppError) -> failwith(e.ToMessage()))
    let result = subtractVal1FromVal2 val1 val2
    match result with
    | Error (AsError (MoneyFailedToConvertBelowMin _)) -> ()
    | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
    | Ok _ -> Assert.Fail "Expected failure; got success"

[<Fact>]
let ``REQ-MON-2.8 provide a function for converting a Money type to a .NET decimal type`` () =
    let d1 = 1234.56M
    result {
        let! m1 = fromDecimal d1
        let d2 = amount m1
        Assert.Equal(d1, d2)
        return ()
    }
    |> railroadWrapper

[<Fact>]
let ``REQ-MON-2.9 sum list happy path`` () =
    let d1 = 12.34M
    let d2 = 0.01M
    let d3 = 56.78M
    let expected = [ d1; d2; d3 ] |> List.sum
    result {
        let! m1 = fromDecimal d1
        let! m2 = fromDecimal d2
        let! m3 = fromDecimal d3
        let! sumTotal = sumList [ m1; m2; m3 ]
        Assert.Equal(expected, amount sumTotal)
        return ()
    }
    |> railroadWrapper

[<Fact>]
let ``REQ-MON-2.9.1 sum list rejects results greater than maxMoney`` () =
    let d1 = maxMoney
    let d2 = 0.01M
    let m1 = fromDecimal d1 |> Result.defaultWith(fun (e: IAppError) -> failwith(e.ToMessage()))
    let m2 = fromDecimal d2 |> Result.defaultWith(fun (e: IAppError) -> failwith(e.ToMessage()))
    let result = sumList [ m1; m2 ]
    match result with
    | Error (AsError (MoneyFailedToConvertExceededMax _)) -> ()
    | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
    | Ok _ -> Assert.Fail "Expected failure; got success"

[<Fact>]
let ``REQ-MON-2.9.1 sum list rejects results lesser than minMoney`` () =
    let d1 = minMoney
    let d2 = -0.01M
    let m1 = fromDecimal d1 |> Result.defaultWith(fun (e: IAppError) -> failwith(e.ToMessage()))
    let m2 = fromDecimal d2 |> Result.defaultWith(fun (e: IAppError) -> failwith(e.ToMessage()))
    let result = sumList [ m1; m2 ]
    match result with
    | Error (AsError (MoneyFailedToConvertBelowMin _)) -> ()
    | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
    | Ok _ -> Assert.Fail "Expected failure; got success"

// =============================================================================
// compare and sign
// =============================================================================

let private moneyOf (s: string) =
    match s with
    | "max" -> maxMoney
    | "min" -> minMoney
    | other -> Decimal.Parse(other, Globalization.CultureInfo.InvariantCulture)
    |> fromDecimal
    |> Result.defaultWith (fun (e: IAppError) -> failwith (e.ToMessage()))

[<Theory>]
[<InlineData("5.00", "5.00", "equal")>]
[<InlineData("5.00", "5.01", "less")>]
[<InlineData("5.01", "5.00", "greater")>]
[<InlineData("0.00", "0.01", "less")>]
[<InlineData("0.00", "-0.01", "greater")>]
[<InlineData("max", "min", "greater")>]
[<InlineData("min", "max", "less")>]
let ``REQ-MON-2.10 for each pair of Money values (equal, one cent apart in either order, zero against plus and minus one cent, the maximum against the minimum), each of the equal, less-than, greater-than, less-or-equal and greater-or-equal comparisons gives its expected verdict, and a is less than b exactly when b is greater than a`` (a: string, b: string, relation: string) =
    let ma, mb = moneyOf a, moneyOf b
    // (equal, less, greater, less-or-equal, greater-or-equal)
    let expected =
        match relation with
        | "equal" -> (true, false, false, true, true)
        | "less" -> (false, true, false, true, false)
        | "greater" -> (false, false, true, false, true)
        | other -> failwith $"Unknown relation {other}"
    Assert.Equal(expected, (isEqual ma mb, isLessThan ma mb, isGreaterThan ma mb, isLessThanOrEqual ma mb, isGreaterThanOrEqual ma mb))
    Assert.Equal(isLessThan ma mb, isGreaterThan mb ma)

[<Theory>]
[<InlineData("max", "positive")>]
[<InlineData("0.01", "positive")>]
[<InlineData("0.00", "zero")>]
[<InlineData("-0.01", "negative")>]
[<InlineData("min", "negative")>]
let ``REQ-MON-2.11 for each of the maximum, one cent, zero, minus one cent and the minimum, exactly one of positive, zero and negative holds, and it is the expected one`` (value: string, sign: string) =
    let m = moneyOf value
    // (positive, zero, negative)
    let expected =
        match sign with
        | "positive" -> (true, false, false)
        | "zero" -> (false, true, false)
        | "negative" -> (false, false, true)
        | other -> failwith $"Unknown sign {other}"
    Assert.Equal(expected, (isPositive m, isZero m, isNegative m))
