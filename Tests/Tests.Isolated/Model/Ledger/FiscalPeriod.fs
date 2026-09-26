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
let ``REQ-FP-1.2 PeriodKey.fromString happy path`` () =
    match FiscalPeriodKey.fromString genericKey with
    | Error e -> Assert.Fail(e.ToMessage())
    | _ -> ()

[<Theory>]
[<InlineData("202006")>] // missing hyphen
[<InlineData("2026-00")>] // month less than 1
[<InlineData("2026-13")>] // month greater than 12
[<InlineData("Sep-2025")>] // total horseshit
let ``REQ-FP-1.2 PeriodKey.fromString fails when given an incorrect format`` badString =
    match FiscalPeriodKey.fromString badString with
    | Error (AsError (FiscalPeriodInvalidKeyString _)) -> Assert.True(true)
    | Error _ -> Assert.Fail "Incorrect error type"
    | _ -> Assert.Fail "Expected failure and got success"
