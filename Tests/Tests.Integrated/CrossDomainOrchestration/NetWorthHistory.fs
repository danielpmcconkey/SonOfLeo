module Tests.Integrated.CrossDomainOrchestration.NetWorthHistory

open NodaTime
open App.DataAccessLayer.DbTransaction
open App.Operation.CoreAuditableAction
open App.Utility.IAppError
open App.Utility.Result
open App.Session
open Business.FinancialServices
open Business.FinancialServices.Ledger.FiscalPeriodComponent
open Business.FinancialServices.Ledger.LedgerAuditableAction
open Business.FinancialServices.Positions.PositionsError
open Business.FinancialServices.Positions.PositionsComponent
open Business.CrossDomainOrchestration
open Business.CrossDomainOrchestration.NetWorth
open Business.CrossDomainOrchestration.NetWorthHistory
open Ui.InterfaceBridge.CommandRoute
open Tests.Helpers
open Tests.Helpers.Railroad
open Tests.Helpers.PositionsValues
open Xunit

(* Expected figures are derived by hand from the fixture (see PositionsFixture.fs and NetWorthPreLedger.fs).

   monthEnd6, the last month-end before the earliest fiscal period (the same records as monthEnd7):
     no ledger account has a balance, so counted ledger assets and liabilities are absent, each 0.00
     investments 250.00 (Sam HSA's pre-ledger snapshot); property values 450,000.00 (the rental and the former
     residence); cash 2,600.00 (Fixture Savings 100.00, Fixture Checking 2,500.00); debts 15.00 (Fixture Card 15.00,
     Fixture Loan 0.00); owned-property mortgages 184,000.00 (Fixture Rental Mortgage, the rental's mortgage Balance
     Account)
     net worth 250.00 + 450,000.00 + 2,600.00 - 15.00 - 184,000.00 = 268,835.00; investable wealth 68,835.00 (less the
     former residence's equity, 200,000.00)
   monthEnd5, the end of the earliest (closed) fiscal period:
     ledger assets 0.00, every unlinked Asset account listed at its ledger balance of 0.00
     investments 250.00 (Sam HSA's pre-ledger snapshot is still its latest; its d1 snapshot is in month -4)
     property values 250,000.00 (the rental; the former residence was disposed of on the first of month -5 and the
       residence is acquired in month -4)
     liabilities -25.00 (F-2210's 25.00 Debit in the closed period); owned-property mortgages 0.00 (F-2320's 180,000.00
       Credit is dated in month -4, and Fixture Rental Mortgage closed at 0.00 on the first of month -5)
     cash 0.00 and debts 0.00: every Balance Account closed at 0.00 on the first of month -5, which is still a balance
     net worth 0.00 + 250.00 + 250,000.00 - (-25.00) - 0.00 = 250,275.00; no primary residence owned, so investable
     wealth is also 250,275.00; nothing absent *)

let private historyOf beginDate endDate =
    computeNetWorthHistory (Context.create NoTransaction FetchOnly) beginDate endDate

let private monthEnds (points: NetWorthPoint list) = points |> List.map (fun pt -> pt.monthEnd)

/// A point: month-end, the nine totals and the absent components.
let private pointSummary (pt: NetWorthPoint) =
    pt.monthEnd,
    [ pt.totalLedgerAssets; pt.totalInvestments; pt.totalPropertyValues; pt.totalCash; pt.totalLiabilities; pt.totalDebts
      pt.totalOwnedPropertyMortgages; pt.netWorth; pt.investableWealth ]
    |> List.map Money.amount,
    pt.absentComponents

let private fiscalPeriodKeyOf (date: LocalDate) =
    $"{date.Year}-{date.Month:D2}" |> FiscalPeriodKey.fromString |> mustBe

let private outsideFiscalPeriods = function AsError (PositionsNetWorthDateOutsideFiscalPeriods d) -> Some d | _ -> None

[<Collection("SharedTestData")>]
type NetWorthHistoryTests(fixture: TestDataFixture) =

    let p = fixture.Data.positions
    // the last day of month +k, from the first of month -5
    let monthPlusEnd k = p.ledgerStart.PlusMonths(6 + k).PlusDays(-1)

    [<Fact>]
    member _.``REQ-RPT-10.1 net worth history with the end date the day before the begin date fails with a typed error naming both dates`` () =
        historyOf p.monthEnd7 (p.monthEnd7.PlusDays(-1))
        |> expectError
            (function AsError (PositionsNetWorthHistoryEndBeforeBegin (b, e)) -> Some(b, e) | _ -> None)
            (fun found -> Assert.Equal((p.monthEnd7, p.monthEnd7.PlusDays(-1)), found))

    [<Fact>]
    member _.``REQ-RPT-10.2 a range beginning mid-month and ending on a month-end gives exactly one point per month-end from the end of the begin month to the end date, in date order`` () =
        historyOf (p.monthEnd8.PlusDays(-10)) p.monthEnd6
        |> Result.map (fun points ->
            Assert.Equal<LocalDate list>([ p.monthEnd8; p.monthEnd7; p.monthEnd6 ], points |> monthEnds))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-10.2 a range beginning on a month-end includes a point on that begin date, and one ending mid-month includes no point for that last month`` () =
        historyOf p.monthEnd8 (p.monthEnd6.PlusDays(10))
        |> Result.map (fun points ->
            Assert.Equal<LocalDate list>([ p.monthEnd8; p.monthEnd7; p.monthEnd6 ], points |> monthEnds))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-10.2 a range beginning and ending inside one month, before its last day, gives no points`` () =
        historyOf (p.monthEnd7.PlusDays(1)) (p.monthEnd6.PlusDays(-1))
        |> Result.map (fun points -> Assert.Empty(points))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-10.2 a range from the last month-end before the earliest fiscal period to the first fiscal-period month-end gives two points, the first without ledger figures and the second with them, each with its nine hand-derived totals and its absent components`` () =
        historyOf p.monthEnd6 p.monthEnd5
        |> Result.map (fun points ->
            Assert.Equal<(LocalDate * decimal list * NetWorthComponent list) list>(
                [ p.monthEnd6,
                  [ 0.00M; 250.00M; 450000.00M; 2600.00M; 0.00M; 15.00M; 184000.00M; 268835.00M; 68835.00M ],
                  [ CountedLedgerAssets; Liabilities ]
                  p.monthEnd5,
                  [ 0.00M; 250.00M; 250000.00M; 0.00M; -25.00M; 0.00M; 0.00M; 250275.00M; 250275.00M ],
                  [] ],
                points |> List.map pointSummary))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-10.3 a range whose month-ends run past the last fiscal period fails with a typed error naming the earliest month-end that is neither within a fiscal period nor pre-ledger`` () =
        // the fixture's fiscal periods end with month +4
        historyOf (monthPlusEnd 3) (monthPlusEnd 6)
        |> expectError outsideFiscalPeriods (fun d -> Assert.Equal(monthPlusEnd 5, d))

    [<Fact>]
    member _.``REQ-RPT-10.3 a range whose month-ends include one in a gap between two fiscal periods fails with a typed error naming that month-end`` () =
        // a period for month +6 leaves month +5 a gap between it and the fixture's last, month +4
        runCommandRouteAndAutoRollback FiscalPeriodCreate (fun context ->
            result {
                let! _ = monthPlusEnd 6 |> fiscalPeriodKeyOf |> FiscalPeriodCreation.constructNewAndPersist context
                computeNetWorthHistory context (monthPlusEnd 4) (monthPlusEnd 6)
                |> expectError outsideFiscalPeriods (fun d -> Assert.Equal(monthPlusEnd 5, d))
                let! withoutGap = computeNetWorthHistory context (monthPlusEnd 6) (monthPlusEnd 6)
                Assert.Equal<LocalDate list>([ monthPlusEnd 6 ], withoutGap |> monthEnds)
            })
        |> railroadWrapper
