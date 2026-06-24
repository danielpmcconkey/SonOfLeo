module Tests.Isolated.Model.Ledger.FiscalPeriod

open Model.Audit
open Model.Ledger.FiscalPeriods
open Xunit

let genericKey = "2026-06"
let genericEnvelope = AuditEnvelope.create FiscalPeriodCreate

[<Fact>]
let ``REQ-FP-1.2 PeriodKey.fromString happy path`` () =
    match PeriodKey.fromString genericKey with
    | Error e -> Assert.Fail e
    | _ -> ()

[<Fact>]
let ``REQ-FP-1.2 PeriodKey.fromString fails when given an incorrect format`` () =
    let badString = "202006"
    let expected = $"Passed string \"{badString}\" is invalid as a Period Key."
    match PeriodKey.fromString badString with
    | Error e -> Assert.Equal(expected, e)
    | _ -> Assert.Fail "Expected failure and got success"

[<Fact>]
let ``REQ-FP-1.2 PeriodKey.fromString fails when given a month of 00`` () =
    let badString = "2026-00"
    let expected = $"Passed string \"{badString}\" is invalid as a Period Key."
    match PeriodKey.fromString badString with
    | Error e -> Assert.Equal(expected, e)
    | _ -> Assert.Fail "Expected failure and got success"

[<Fact>]
let ``REQ-FP-1.2 PeriodKey.fromString fails when given a month greater than 12`` () =
    let badString = "2026-13"
    let expected = $"Passed string \"{badString}\" is invalid as a Period Key."
    match PeriodKey.fromString badString with
    | Error e -> Assert.Equal(expected, e)
    | _ -> Assert.Fail "Expected failure and got success"

[<Fact>]
let ``REQ-FP-1.4 REQ-FP-2.3 Fiscal period start date is derived from the key`` () =    
    let expectedMonth = 6
    let expectedDay = 1
    let fp = FiscalPeriod.constructNewFromKeyString genericKey genericEnvelope |> Result.defaultWith failwith
    let startDate = FiscalPeriod.startDate fp
    Assert.Equal(expectedMonth, startDate.Month)
    Assert.Equal(expectedDay, startDate.Day)

[<Fact>]
let ``REQ-FP-1.5 REQ-FP-2.3 Fiscal period end date is derived from the key`` () =    
    let expectedMonth = 6
    let expectedDay = 30
    let fp = FiscalPeriod.constructNewFromKeyString genericKey genericEnvelope |> Result.defaultWith failwith
    let endDate = FiscalPeriod.endDate fp
    Assert.Equal(expectedMonth, endDate.Month)
    Assert.Equal(expectedDay, endDate.Day)
    
