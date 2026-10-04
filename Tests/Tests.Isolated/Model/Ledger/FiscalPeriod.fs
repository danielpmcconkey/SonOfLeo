module Tests.Isolated.Business.FinancialServices.Ledger.FiscalPeriod

open App.Session
open Business.General
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.CrossDomainOrchestration
open Business.FinancialServices.Ledger.FiscalPeriodComponent
open App.Utility.IAppError
open Tests.Helpers.TestError
open Tests.Helpers.SadPath
open Xunit
open Business.FinancialServices.Ledger.LedgerError
let genericKey = "2026-06"

[<Fact>]
let ``REQ-FP-1.2 PeriodKey.fromString accepts a four-digit year and two-digit month and keeps that value`` () =
    match FiscalPeriodKey.fromString genericKey with
    | Error e -> Assert.Fail(e.ToMessage())
    | Ok key -> Assert.Equal("2026-06", key |> FiscalPeriodKey.value)

[<Theory>]
[<InlineData("202006")>] // missing hyphen
[<InlineData("2026-00")>] // month less than 1
[<InlineData("2026-13")>] // month greater than 12
[<InlineData("Sep-2025")>] // total horseshit
[<InlineData("226-01")>] // three-digit year
[<InlineData("20266-01")>] // five-digit year
[<InlineData("2026-1")>] // one-digit month
[<InlineData("2026-011")>] // three-digit month
let ``REQ-FP-1.2 PeriodKey.fromString fails when given an incorrect format`` badString =
    match FiscalPeriodKey.fromString badString with
    | Error (AsError (FiscalPeriodInvalidKeyString raw)) -> Assert.Equal(badString, raw)
    | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
    | _ -> Assert.Fail "Expected failure and got success"
