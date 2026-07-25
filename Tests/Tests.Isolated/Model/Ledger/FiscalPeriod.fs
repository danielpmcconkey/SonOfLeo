module Tests.Isolated.Model.Ledger.FiscalPeriod

open Model.Audit
open Model.Ledger.FiscalPeriods
open Utilities.AppError
open Xunit
let genericKey = "2026-06"
let genericEnvelope = AuditEnvelope.create FiscalPeriodCreate


[<Fact>]
let ``REQ-FP-1.2 PeriodKey.fromString happy path`` () =
    match FiscalPeriodKey.fromString genericKey with
    | Error e -> Assert.Fail(AppError.toMessage e)
    | _ -> ()

[<Theory>]
[<InlineData("202006")>] // missing hyphen
[<InlineData("2026-00")>] // month less than 1
[<InlineData("2026-13")>] // month greater than 12
[<InlineData("Sep-2025")>] // total horseshit
let ``REQ-FP-1.2 PeriodKey.fromString fails when given an incorrect format`` badString =
    match FiscalPeriodKey.fromString badString with
    | Error(FiscalPeriodInvalidKeyString e) -> Assert.True(true)
    | Error _ -> Assert.Fail "Incorrect error type"
    | _ -> Assert.Fail "Expected failure and got success"
