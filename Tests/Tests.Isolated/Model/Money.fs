module Tests.Isolated.Model.Money

open System
open Model.Money
open Tests.Helpers.Railroad
open Utilities.AppError
open Utilities.ResultHelper
open Xunit

// =============================================================================
// fromDecimal
// =============================================================================

[<Fact>]
let ``REQ-MON-2.2 fromDecimal accepts valid 2dp amount`` () =
    let amount_d = 3.99M
    let m = fromDecimal amount_d |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
    Assert.Equal(amount_d, amount m)

[<Fact>]
let ``REQ-MON-2.2 fromDecimal accepts negative amounts`` () =
    let amount_d = -3.99M
    let m = fromDecimal amount_d |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
    Assert.Equal(amount_d, amount m)

[<Fact>]
let ``REQ-MON-2.2 fromDecimal accepts zero`` () =
    let amount_d = 0M
    let m = fromDecimal amount_d |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
    Assert.Equal(amount_d, amount m)

[<Fact>]
let ``REQ-MON-2.2.1 REQ-MON-1.4 fromDecimal rejects amount with more than 2dp precision`` () =
    let amount_d = 3.998M
    let result = fromDecimal amount_d
    match result with
    | Error(MoneyFailedToConvertImproperPrecision _) -> ()
    | Error e -> Assert.Fail $"Wrong error. {AppError.toMessage e}"
    | Ok _ -> Assert.Fail "Expected failure; got success"

[<Fact>]
let ``REQ-MON-2.2.1 REQ-MON-1.2 fromDecimal rejects amount exceeding maxMoney`` () =
    let amount_d = maxMoney + 0.01M
    let result = fromDecimal amount_d
    match result with
    | Error(MoneyFailedToConvertExceededMax _) -> ()
    | Error e -> Assert.Fail $"Wrong error. {AppError.toMessage e}"
    | Ok _ -> Assert.Fail "Expected failure; got success"

[<Fact>]
let ``REQ-MON-2.2.1 REQ-MON-1.3 fromDecimal rejects amount below minMoney`` () =
    let amount_d = minMoney - 0.01M
    let result = fromDecimal amount_d
    match result with
    | Error(MoneyFailedToConvertBelowMin _) -> ()
    | Error e -> Assert.Fail $"Wrong error. {AppError.toMessage e}"
    | Ok _ -> Assert.Fail "Expected failure; got success"

[<Fact>]
let ``REQ-MON-2.3 fromDecimal list happy path`` () =
    let list_d = [ -3.99M; 12.24M; 27194338M ]
    let result = fromDecimalList list_d
    Assert.True(result.IsOk)

[<Fact>]
let ``REQ-MON-2.3.1 fromDecimal list must check rounding precision`` () =
    let list_d = [ -3.99M; 12.243M; 27194338M ]
    let result = fromDecimalList list_d
    match result with
    | Error(MoneyFailedToConvertImproperPrecision _) -> ()
    | Error e -> Assert.Fail $"Wrong error. {AppError.toMessage e}"
    | Ok _ -> Assert.Fail "Expected failure; got success"

[<Fact>]
let ``REQ-MON-2.3.1 fromDecimal list must check max value`` () =
    let list_d = [ -3.99M; 12.24M; maxMoney + 0.01M ]
    let result = fromDecimalList list_d
    match result with
    | Error(MoneyFailedToConvertExceededMax _) -> ()
    | Error e -> Assert.Fail $"Wrong error. {AppError.toMessage e}"
    | Ok _ -> Assert.Fail "Expected failure; got success"

[<Fact>]
let ``REQ-MON-2.3.1 fromDecimal list must check min value`` () =
    let list_d = [ minMoney - 0.01M; 12.24M; 2719433M ]
    let result = fromDecimalList list_d
    match result with
    | Error(MoneyFailedToConvertBelowMin _) -> ()
    | Error e -> Assert.Fail $"Wrong error. {AppError.toMessage e}"
    | Ok _ -> Assert.Fail "Expected failure; got success"

[<Fact>]
let ``REQ-MON-2.3.2 fromDecimal list preserves sort order`` () =
    let list_d = [ -3.99M; 12.24M; 27194338M ]
    let result = fromDecimalList list_d
    match result with
    | Error e -> Assert.Fail(AppError.toMessage e)
    | Ok list_m -> List.zip list_d list_m |> List.iter(fun (d, m) -> Assert.Equal(d, amount m))


// =============================================================================
// splitByN
// =============================================================================

[<Fact>]
let ``REQ-MON-2.4 splitByN happy path`` () =
    result {
        let! source = fromDecimal 111.17M
        let result = splitByN source 3
        Assert.True(result.IsOk)
        return ()
    }
    |> railroadWrapper

[<Fact>]
let ``REQ-MON-2.4.1 splitByN produces parts that sum exactly to original`` () =
    let expected = 111.17M
    result {
        let! source = fromDecimal expected
        let! shares = splitByN source 3
        let! sumTotal = shares |> sumList
        Assert.Equal(expected, amount sumTotal)
        return ()
    }
    |> railroadWrapper

[<Fact>]
let ``REQ-MON-2.4.2 splitByN rejects zero-ways split requests`` () =
    let expected = 111.17M
    result {
        let! source = fromDecimal expected
        let result = splitByN source 0
        return!
            match result with
            | Error(MoneyImproperSplit _) -> Ok()
            | Error e -> Error(TestingError $"Wrong error. {AppError.toMessage e}")
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
            | Error(MoneyImproperSplit _) -> Ok()
            | Error e -> Error(TestingError $"Wrong error. {AppError.toMessage e}")
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
            | Error(MoneyImproperSplit _) -> Ok()
            | Error e -> Error(TestingError $"Wrong error. {AppError.toMessage e}")
            | Ok _ -> Error(TestingError "Expected failure; got success")
    }
    |> railroadWrapper

[<Fact>]
let ``REQ-MON-2.4.4 splitByN rounds using midway rounding up`` () =
    let n = 1.05M
    let m = 2
    let expected = 0.53M // banker's would round at 0.52
    result {
        let! source = fromDecimal n
        let! shares = splitByN source m
        let secondShare = amount shares[1] // get the second, because the first would carry the uneven remainder
        Assert.Equal(expected, secondShare)
        return ()
    }
    |> railroadWrapper

[<Fact>]
let ``REQ-MON-2.4.5 splitByN applies uneven remainder entirely to the first share`` () =
    let n = 419.97M
    let m = 30
    let sharesBeforeRounding = n / decimal m //13.999M
    let rounded = Math.Round(sharesBeforeRounding, 2, MidpointRounding.AwayFromZero) // 14M
    let roundedAtM = rounded * decimal m // 420M
    let expectedRemainder = roundedAtM - n // 0.03M
    let expectedFirst = rounded - expectedRemainder // 13.97M
    result {
        let! source = fromDecimal n
        let! shares = splitByN source m
        let firstShare = amount shares[0]
        let secondShare = amount shares[1] // check the second to see if the non-remainder-adjusted amounts are right while we're here
        Assert.Equal(expectedFirst, firstShare)
        Assert.Equal(rounded, secondShare)
        return ()
    }
    |> railroadWrapper

// =============================================================================
// add / subtract
// =============================================================================

[<Fact>]
let ``REQ-MON-2.5 add function happy path`` () =
    let d1 = 145877.43M
    let d2 = -874.12M
    let expected = d1 + d2
    let m1 = fromDecimal d1 |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
    let m2 = fromDecimal d2 |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
    let result = add m1 m2
    match result with
    | Error e -> Assert.Fail(AppError.toMessage e)
    | Ok sumTotal -> Assert.Equal(expected, amount sumTotal)

[<Fact>]
let ``REQ-MON-2.5.1 add returns Error when sum exceeds max`` () =
    let d1 = maxMoney
    let d2 = 0.01M
    let m1 = fromDecimal d1 |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
    let m2 = fromDecimal d2 |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
    let result = add m1 m2
    match result with
    | Error(MoneyFailedToConvertExceededMax _) -> ()
    | Error e -> Assert.Fail $"Wrong error. {AppError.toMessage e}"
    | Ok _ -> Assert.Fail "Expected failure; got success"

[<Fact>]
let ``REQ-MON-2.6 subtract happy path`` () =
    let decimal1 = -874.12M
    let decimal2 = 145877.43M
    let expected = decimal2 - decimal1
    let val1 = fromDecimal decimal1 |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
    let val2 = fromDecimal decimal2 |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
    let result = subtractVal1FromVal2 val1 val2
    match result with
    | Error e -> Assert.Fail(AppError.toMessage e)
    | Ok sumTotal -> Assert.Equal(expected, amount sumTotal)

[<Fact>]
let ``REQ-MON-2.6.1 subtract returns Error when difference falls below min`` () =
    let decimal2 = minMoney
    let decimal1 = 0.01M
    let val2 = fromDecimal decimal2 |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
    let val1 = fromDecimal decimal1 |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
    let result = subtractVal1FromVal2 val1 val2
    match result with
    | Error(MoneyFailedToConvertBelowMin _) -> ()
    | Error e -> Assert.Fail $"Wrong error. {AppError.toMessage e}"
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
    let m1 = fromDecimal d1 |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
    let m2 = fromDecimal d2 |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
    let result = sumList [ m1; m2 ]
    match result with
    | Error(MoneyFailedToConvertExceededMax _) -> ()
    | Error e -> Assert.Fail $"Wrong error. {AppError.toMessage e}"
    | Ok _ -> Assert.Fail "Expected failure; got success"

[<Fact>]
let ``REQ-MON-2.9.1 sum list rejects results lesser than minMoney`` () =
    let d1 = minMoney
    let d2 = -0.01M
    let m1 = fromDecimal d1 |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
    let m2 = fromDecimal d2 |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
    let result = sumList [ m1; m2 ]
    match result with
    | Error(MoneyFailedToConvertBelowMin _) -> ()
    | Error e -> Assert.Fail $"Wrong error. {AppError.toMessage e}"
    | Ok _ -> Assert.Fail "Expected failure; got success"
