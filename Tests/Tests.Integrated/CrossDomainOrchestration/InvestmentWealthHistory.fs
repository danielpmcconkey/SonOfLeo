module Tests.Integrated.CrossDomainOrchestration.InvestmentWealthHistory

open NodaTime
open App.DataAccessLayer.DbTransaction
open App.Operation.CoreAuditableAction
open App.Utility.IAppError
open App.Utility.Result
open App.Utility
open App.Session
open Business.FinancialServices
open Business.FinancialServices.Positions.PositionsAuditableAction
open Business.FinancialServices.Positions.PositionsComponent
open Business.FinancialServices.Positions.PositionsError
open Business.CrossDomainOrchestration
open Business.CrossDomainOrchestration.AccountSnapshotOrchestration
open Business.CrossDomainOrchestration.InvestmentWealthHistory
open Ui.InterfaceBridge.CommandRoute
open Ui.InterfaceBridge.InterfaceContracts.ReportsContracts
open Ui.InterfaceBridge.BoundaryConverters.ReportConverters
open Tests.Helpers
open Tests.Helpers.PositionsValues
open Tests.Helpers.Railroad
open Xunit

module PF = PositionsFixture

(* Holdings as of each fixture month-end, by hand from PositionsFixture.fs (Total Market is EXTMX, International EXINX):

   end of month -4: Alex Brokerage d1 1,500.00 (EXTMX 1,000.00, EXINX 500.00); Alex Roth IRA d1 1,500.00 (EXTMX 500.00,
                    Bond 1,000.00); Old Brokerage d1 1,000.00 (EXINX); Sam 401k d1 4,000.00 (EXTMX 2,000.00, Bond
                    2,000.00); Sam HSA d1 300.00 (Stable Value). Total 8,300.00.
   end of month -3: Alex Brokerage d2 1,620.00 (EXTMX 1,100.00, EXINX 520.00); Alex Roth IRA d1 1,500.00; Joint
                    Brokerage d2 5,500.00 (EXTMX); Sam 401k d1 4,000.00; Sam HSA d1 300.00. Old Brokerage has ended.
                    Total 12,920.00.
   end of month -2: Alex Brokerage d3 1,810.00; Alex Roth IRA d1 1,500.00; Joint Brokerage d2 5,500.00; Sam 401k d3
                    4,227.79; Sam HSA d3 0.00 (no lines). Total 13,037.79.
   end of month -1: Alex Brokerage d4 1,380.00; Alex Roth IRA d1 1,500.00; Joint Brokerage d4 5,750.00; Sam 401k d3
                    4,227.79; Sam HSA d3 0.00. Total 12,857.79. *)

let private history context beginDate endDate grouping =
    computeInvestmentWealthHistory context beginDate endDate grouping

let private readOnly = Context.create NoTransaction FetchOnly

let private orFail (r: Result<'T, IAppError>) : 'T =
    match r with
    | Ok v -> v
    | Error e -> failwith (e.ToMessage())

/// A point's totals as the boundary shows them: each group by its name, in the order shown.
let private resolved context (point: WealthPoint) : (WealthGroupContract * decimal) list =
    point
    |> ``convert [WealthPoint] to [WealthPointReturnRow]`` context
    |> orFail
    |> _.totals
    |> List.map (fun t -> t.group, t.marketValue)

let private totalsOf context (point: WealthPoint) = point |> resolved context |> Map.ofList

let private pointAt context date grouping =
    history context date date grouping |> Result.map List.exactlyOne

[<Collection("SharedTestData")>]
type InvestmentWealthHistoryTests(fixture: TestDataFixture) =

    let p = fixture.Data.positions

    [<Fact>]
    member _.``REQ-RPT-9.1 an end date earlier than the begin date fails with a typed error naming both dates`` () =
        history readOnly p.monthEnd2 p.monthEnd3 ByAccount
        |> expectError
            (function AsError (PositionsWealthHistoryEndBeforeBegin (b, e)) -> Some(b, e) | _ -> None)
            (fun found -> Assert.Equal((p.monthEnd2, p.monthEnd3), found))

    [<Fact>]
    member _.``REQ-RPT-9.2 a range beginning mid-month and ending on a month-end gives exactly one point per month-end from the end of the begin month to the end date, in date order`` () =
        history readOnly p.d1 p.monthEnd2 ByAccount
        |> Result.map (fun points ->
            Assert.Equal<LocalDate list>([ p.monthEnd4; p.monthEnd3; p.monthEnd2 ], points |> List.map (fun x -> x.monthEnd)))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-9.2 a range beginning and ending inside one month, before its last day, gives no points`` () =
        history readOnly p.d1 (p.d1.PlusDays(5)) ByAccount
        |> Result.map (fun points -> Assert.Empty(points))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-9.1 REQ-RPT-9.2 a range whose begin and end are the same month-end gives exactly that one point`` () =
        history readOnly p.monthEnd3 p.monthEnd3 ByAccount
        |> Result.map (fun points ->
            Assert.Equal<(LocalDate * decimal) list>([ p.monthEnd3, 12920.00M ], points |> List.map (fun x -> x.monthEnd, x.total |> Money.amount)))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-9.2 a range spanning February gives a point on its last day, the 29th in a leap year and the 28th otherwise`` () =
        result {
            // sentinel years well past the fixture: 2044 is a leap year, 2043 is not
            let! leap = history readOnly (LocalDate(2044, 2, 10)) (LocalDate(2044, 3, 5)) ByAccount
            let! common = history readOnly (LocalDate(2043, 2, 10)) (LocalDate(2043, 3, 5)) ByAccount
            Assert.Equal<LocalDate list>([ LocalDate(2044, 2, 29) ], leap |> List.map (fun x -> x.monthEnd))
            Assert.Equal<LocalDate list>([ LocalDate(2043, 2, 28) ], common |> List.map (fun x -> x.monthEnd))
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-9.2 grouped by account, each point's per-account totals and grand total equal the hand-summed market values of the holdings as of that month-end`` () =
        history readOnly p.monthEnd4 p.monthEnd1 ByAccount
        |> Result.map (fun points ->
            (* Every account with holdings at any of the four points appears at all four (REQ-RPT-9.2 as amended),
               at 0.00 where it holds nothing: Joint Brokerage before its first snapshot at d2, Old Brokerage after its
               active end, and Sam HSA from d3 on, whose latest snapshot has no lines. *)
            let expected =
                [ p.monthEnd4,
                  [ PF.alexBrokerage, 1500.00M; PF.alexRoth, 1500.00M; PF.jointBrokerage, 0.00M; PF.oldBrokerage, 1000.00M
                    PF.sam401k, 4000.00M; PF.samHsa, 300.00M ],
                  8300.00M
                  p.monthEnd3,
                  [ PF.alexBrokerage, 1620.00M; PF.alexRoth, 1500.00M; PF.jointBrokerage, 5500.00M; PF.oldBrokerage, 0.00M
                    PF.sam401k, 4000.00M; PF.samHsa, 300.00M ],
                  12920.00M
                  p.monthEnd2,
                  [ PF.alexBrokerage, 1810.00M; PF.alexRoth, 1500.00M; PF.jointBrokerage, 5500.00M; PF.oldBrokerage, 0.00M
                    PF.sam401k, 4227.79M; PF.samHsa, 0.00M ],
                  13037.79M
                  p.monthEnd1,
                  [ PF.alexBrokerage, 1380.00M; PF.alexRoth, 1500.00M; PF.jointBrokerage, 5750.00M; PF.oldBrokerage, 0.00M
                    PF.sam401k, 4227.79M; PF.samHsa, 0.00M ],
                  12857.79M ]
                |> List.map (fun (date, totals, total) ->
                    date, totals |> List.map (fun (n, v) -> Named n, v) |> Map.ofList, total)
            Assert.Equal<(LocalDate * Map<WealthGroupContract, decimal> * decimal) list>(
                expected, points |> List.map (fun x -> x.monthEnd, x |> totalsOf readOnly, x.total |> Money.amount)))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-9.2 grouped by account group, accounts whose labels match exactly share one total and accounts whose labels differ only by letter case are totalled separately`` () =
        runCommandRouteAndAutoRollback PositionsCreateInvestmentAccount (fun context ->
            result {
                let period = toActivityPeriod p.accountsActiveBegin None
                let! sam = PositionsLookups.personIdOf context PF.sam
                let! account =
                    InvestmentAccountOrchestration.constructNewAndPersist
                        context
                        (toAccountName "Lowercase Brokerage")
                        (toInstitution "Example Brokerage")
                        (toAccountGroup "brokerage")
                        TaxTreatment.Taxable
                        [ sam ]
                        period
                        None
                let accountId = account |> Business.FinancialServices.Positions.InvestmentAccount.investmentAccountId
                let! totalMarketId = PositionsLookups.securityIdOf context PF.totalMarket
                let! _ = HoldingOrchestration.constructNewAndPersist context accountId totalMarketId (Some AverageCost)
                let! _ =
                    recordSnapshots context
                        [ accountId, p.d2, Reported, None, [ totalMarketId, toQuantity 1M, toPrice 110.00M, toMoney 110.00M, None, [] ] ]
                let! point = pointAt context p.monthEnd3 ByAccountGroup
                // Brokerage: Alex Brokerage 1,620.00 + Joint Brokerage 5,500.00; Retirement: 1,500.00 + 4,000.00
                Assert.Equal<Map<WealthGroupContract, decimal>>(
                    Map.ofList
                        [ Named "Brokerage", 7120.00M; Named "brokerage", 110.00M
                          Named "Retirement", 5500.00M; Named "Health", 300.00M ],
                    point |> totalsOf context)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-9.2 grouped by tax treatment, each point gives one total per tax treatment present, equal to the hand-summed market values of that treatment's accounts`` () =
        history readOnly p.monthEnd4 p.monthEnd3 ByTaxTreatment
        |> Result.map (fun points ->
            let named totals = totals |> List.map (fun (n, v) -> Named n, v) |> Map.ofList
            Assert.Equal<Map<WealthGroupContract, decimal> list>(
                [ // Taxable: Alex Brokerage 1,500.00 + Old Brokerage 1,000.00
                  named [ "Taxable", 2500.00M; "Roth", 1500.00M; "TaxDeferred", 4000.00M; "Hsa", 300.00M ]
                  // Taxable: Alex Brokerage 1,620.00 + Joint Brokerage 5,500.00
                  named [ "Taxable", 7120.00M; "Roth", 1500.00M; "TaxDeferred", 4000.00M; "Hsa", 300.00M ] ],
                points |> List.map (totalsOf readOnly)))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-9.3 grouped by owners, a jointly owned account is totalled under its complete owner set and not under either owner's single-owner group`` () =
        pointAt readOnly p.monthEnd3 ByOwners
        |> Result.map (fun point ->
            // Alex alone: Alex Brokerage 1,620.00 + Alex Roth IRA 1,500.00; Sam alone: Sam 401k 4,000.00 + Sam HSA 300.00
            Assert.Equal<Map<WealthGroupContract, decimal>>(
                Map.ofList
                    [ Owners [ PF.alex ], 3120.00M
                      Owners [ PF.alex; PF.sam ], 5500.00M
                      Owners [ PF.sam ], 4300.00M ],
                point |> totalsOf readOnly))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-9.3 for each of the seven dimensions, grouped by it, each line is totalled under its Security's value in that dimension and lines whose Security has none are totalled under unassigned`` () =
        (* lines as of the end of month -3: EXTMX 1,100.00 + 500.00 + 5,500.00 + 2,000.00 = 9,100.00 (all seven dimensions);
           EXINX 520.00 (Equity Fund, International); Bond Fund 1,000.00 + 2,000.00 = 3,000.00 (Bond Fund only);
           Stable Value 300.00 (none) *)
        let onlyTotalMarket valueName = [ Named valueName, 9100.00M; WealthGroupContract.Unassigned, 3820.00M ]
        let expected =
            [ InvestmentType, [ Named "Equity Fund", 9620.00M; Named "Bond Fund", 3000.00M; WealthGroupContract.Unassigned, 300.00M ]
              MarketCap, onlyTotalMarket "Large Cap"
              IndexType, onlyTotalMarket "Total Market"
              Sector, onlyTotalMarket "Diversified"
              Region, [ Named "Domestic", 9100.00M; Named "International", 520.00M; WealthGroupContract.Unassigned, 3300.00M ]
              Objective, onlyTotalMarket "Growth"
              Benchmark, onlyTotalMarket "Example Total Market Index" ]
        expected
        |> List.map (fun (dimension, totals) ->
            pointAt readOnly p.monthEnd3 (ByDimension dimension)
            |> Result.map (fun point ->
                Assert.Equal<Map<WealthGroupContract, decimal>>(Map.ofList totals, point |> totalsOf readOnly)
                Assert.Equal(12920.00M, point.total |> Money.amount)))
        |> convertListOfResultsToResultsList
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-9.4 a range before the earliest snapshot and before the ledger's first fiscal period succeeds with a zero-total point for each month-end`` () =
        // twenty years back, long before the fixture's fiscal periods and its first snapshot
        let today = Calendar.today ()
        let first = LocalDate(today.Year - 20, today.Month, 1)
        let monthEnd k = first.PlusMonths(k + 1).PlusDays(-1)
        history readOnly (first.PlusDays(14)) (monthEnd 3) ByTaxTreatment
        |> Result.map (fun points ->
            Assert.Equal<(LocalDate * decimal * int) list>(
                [ monthEnd 0, 0.00M, 0
                  monthEnd 1, 0.00M, 0
                  monthEnd 2, 0.00M, 0
                  monthEnd 3, 0.00M, 0 ],
                points |> List.map (fun x -> x.monthEnd, x.total |> Money.amount, x.totals.Length)))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-9.2 grouped by account, over a range in which one account has holdings at the first month-end and none at the last, that account appears at every point, with 0.00 at the last`` () =
        // Sam HSA holds 300.00 until its d3 snapshot, which has no lines
        history readOnly p.monthEnd3 p.monthEnd2 ByAccount
        |> Result.map (fun points ->
            Assert.Equal<(LocalDate * decimal option) list>(
                [ p.monthEnd3, Some 300.00M; p.monthEnd2, Some 0.00M ],
                points |> List.map (fun x -> x.monthEnd, x |> totalsOf readOnly |> Map.tryFind (Named PF.samHsa))))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-9.2 grouped by account, an account whose first holdings fall after the first month-end of the range appears at every earlier point with 0.00`` () =
        // Joint Brokerage's first snapshot is d2, in month -3
        history readOnly p.monthEnd4 p.monthEnd3 ByAccount
        |> Result.map (fun points ->
            Assert.Equal<(LocalDate * decimal option) list>(
                [ p.monthEnd4, Some 0.00M; p.monthEnd3, Some 5500.00M ],
                points |> List.map (fun x -> x.monthEnd, x |> totalsOf readOnly |> Map.tryFind (Named PF.jointBrokerage))))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-9.2 for each grouping (account, account group, tax treatment, a dimension), the grouping values within every point are ordered by name, from a fixture whose stored order differs, with unassigned last even after a value whose name sorts after it`` () =
        (* In a rolled-back transaction: a Region value "Western", whose name sorts after "Unassigned", and a Security in
           it held by a Taxable Brokerage account named after every fixture account, 200.00 on d2. Accounts are read by
           name and their lines by Security name, so values first appear: account groups Brokerage, Retirement, Health;
           tax treatments Taxable, Roth, TaxDeferred, Hsa; regions International, Domestic, then unassigned before
           Western. At the end of month -4 Joint Brokerage and the new account have no holdings, so they are added after
           the others with 0.00; at the end of month -3 Old Brokerage is. *)
        runCommandRouteAndAutoRollback PositionsCreateInvestmentAccount (fun context ->
            result {
                let! western = DimensionValueOrchestration.constructNewAndPersist context Region (toDimensionValueName "Western")
                let westernId = western |> Business.FinancialServices.Positions.DimensionValue.dimensionValueId
                let! security =
                    SecurityOrchestration.constructNewAndPersist
                        context (toSecurityName "Example Western Fund") None [ Region, westernId ]
                let securityId = security |> Business.FinancialServices.Positions.Security.securityId
                let! sam = PositionsLookups.personIdOf context PF.sam
                let! account =
                    InvestmentAccountOrchestration.constructNewAndPersist
                        context (toAccountName "Western Example Brokerage") (toInstitution "Example Brokerage")
                        (toAccountGroup "Brokerage") TaxTreatment.Taxable [ sam ] (toActivityPeriod p.accountsActiveBegin None) None
                let accountId = account |> Business.FinancialServices.Positions.InvestmentAccount.investmentAccountId
                let! _ = HoldingOrchestration.constructNewAndPersist context accountId securityId (Some AverageCost)
                let! _ =
                    recordSnapshots context
                        [ accountId, p.d2, Reported, None, [ securityId, toQuantity 2M, toPrice 100.00M, toMoney 200.00M, None, [] ] ]
                let named values = values |> List.map (fun (n, v) -> Named n, v)
                let expected =
                    [ ByAccount,
                      [ named
                            [ PF.alexBrokerage, 1500.00M; PF.alexRoth, 1500.00M; PF.jointBrokerage, 0.00M
                              PF.oldBrokerage, 1000.00M; PF.sam401k, 4000.00M; PF.samHsa, 300.00M
                              "Western Example Brokerage", 0.00M ]
                        named
                            [ PF.alexBrokerage, 1620.00M; PF.alexRoth, 1500.00M; PF.jointBrokerage, 5500.00M
                              PF.oldBrokerage, 0.00M; PF.sam401k, 4000.00M; PF.samHsa, 300.00M
                              "Western Example Brokerage", 200.00M ] ]
                      ByAccountGroup,
                      [ named [ "Brokerage", 2500.00M; "Health", 300.00M; "Retirement", 5500.00M ]
                        named [ "Brokerage", 7320.00M; "Health", 300.00M; "Retirement", 5500.00M ] ]
                      ByTaxTreatment,
                      [ named [ "Hsa", 300.00M; "Roth", 1500.00M; "TaxDeferred", 4000.00M; "Taxable", 2500.00M ]
                        named [ "Hsa", 300.00M; "Roth", 1500.00M; "TaxDeferred", 4000.00M; "Taxable", 7320.00M ] ]
                      // end of month -4: EXTMX 1,000.00 + 500.00 + 2,000.00; EXINX 500.00 + 1,000.00; Bond 3,000.00, Stable Value 300.00
                      // end of month -3: EXTMX 1,100.00 + 500.00 + 5,500.00 + 2,000.00; EXINX 520.00; Bond and Stable Value as before
                      ByDimension Region,
                      [ named [ "Domestic", 3500.00M; "International", 1500.00M; "Western", 0.00M ] @ [ WealthGroupContract.Unassigned, 3300.00M ]
                        named [ "Domestic", 9100.00M; "International", 520.00M; "Western", 200.00M ] @ [ WealthGroupContract.Unassigned, 3300.00M ] ] ]
                let! _ =
                    expected
                    |> List.map (fun (grouping, points) ->
                        history context p.monthEnd4 p.monthEnd3 grouping
                        |> Result.map (fun found ->
                            Assert.Equal<(WealthGroupContract * decimal) list list>(
                                points, found |> List.map (resolved context))))
                    |> convertListOfResultsToResultsList
                return ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-9.2 REQ-RPT-9.3 grouped by owners, the grouping values within every point, including a jointly owned group beside a sole owner's group, are ordered by their owners' names`` () =
        (* In a rolled-back transaction: an account of Jordan's named between Alex Roth IRA and Joint Brokerage, 200.00 of
           Total Market on d2. Accounts are read by name, so owner sets first appear Alex, Jordan, then Alex and Sam, then
           Sam. *)
        runCommandRouteAndAutoRollback PositionsCreateInvestmentAccount (fun context ->
            result {
                let! jordan = PositionsLookups.personIdOf context PF.jordan
                let! account =
                    InvestmentAccountOrchestration.constructNewAndPersist
                        context (toAccountName "Custodial Example Account") (toInstitution "Example Brokerage")
                        (toAccountGroup "Custodial") TaxTreatment.Taxable [ jordan ] (toActivityPeriod p.accountsActiveBegin None) None
                let accountId = account |> Business.FinancialServices.Positions.InvestmentAccount.investmentAccountId
                let! totalMarketId = PositionsLookups.securityIdOf context PF.totalMarket
                let! _ = HoldingOrchestration.constructNewAndPersist context accountId totalMarketId (Some AverageCost)
                let! _ =
                    recordSnapshots context
                        [ accountId, p.d2, Reported, None, [ totalMarketId, toQuantity 2M, toPrice 100.00M, toMoney 200.00M, None, [] ] ]
                let! points = history context p.monthEnd3 p.monthEnd2 ByOwners
                // end of month -2: Alex alone 1,810.00 + 1,500.00; Sam alone 4,227.79 + 0.00
                Assert.Equal<(WealthGroupContract * decimal) list list>(
                    [ [ Owners [ PF.alex ], 3120.00M; Owners [ PF.alex; PF.sam ], 5500.00M
                        Owners [ PF.jordan ], 200.00M; Owners [ PF.sam ], 4300.00M ]
                      [ Owners [ PF.alex ], 3310.00M; Owners [ PF.alex; PF.sam ], 5500.00M
                        Owners [ PF.jordan ], 200.00M; Owners [ PF.sam ], 4227.79M ] ],
                    points |> List.map (resolved context))
            })
        |> railroadWrapper
