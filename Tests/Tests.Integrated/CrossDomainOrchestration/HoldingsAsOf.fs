module Tests.Integrated.CrossDomainOrchestration.HoldingsAsOf

open NodaTime
open App.DataAccessLayer.DbTransaction
open App.Operation.CoreAuditableAction
open App.Utility.Result
open App.Session
open Business.FinancialServices
open Business.FinancialServices.Positions
open Business.FinancialServices.Positions.PositionsComponent
open Business.CrossDomainOrchestration.HoldingsAsOf
open Tests.Helpers
open Tests.Helpers.Railroad
open Xunit

module PF = PositionsFixture

let private holdingsAsOf date = fetchHoldingsAsOf (Context.create NoTransaction FetchOnly) date

let private names (accounts: HoldingsAsOfAccount list) = accounts |> List.map (fun a -> a.investmentAccountName)

let private find name (accounts: HoldingsAsOfAccount list) = accounts |> List.find (fun a -> a.investmentAccountName = name)

/// A line as returned: Security name, ticker, the seven dimensions' value names in Dimension.all order, basis method,
/// quantity, price, market value and cost basis.
type private LineSummary =
    string * string option * string option list * BasisMethod option * decimal * decimal * decimal * decimal option

let private lineSummary (l: HoldingsAsOfLine) : LineSummary =
    l.securityName,
    l.ticker,
    Dimension.all |> List.map (fun d -> l.dimensionValueNames |> Map.tryFind d),
    l.basisMethod,
    l.quantity |> Quantity.amount,
    l.price |> Price.amount,
    l.marketValue |> Money.amount,
    l.reportedCostBasis |> Option.map Money.amount

/// An account as returned: name, institution, group, tax treatment, owners, ledger link, snapshot date, provenance and
/// contribution basis.
type private AccountSummary =
    string * string * string * TaxTreatment * string list * (string * string) option * LocalDate * Provenance * decimal option

let private accountSummary (a: HoldingsAsOfAccount) : AccountSummary =
    a.investmentAccountName, a.institution, a.accountGroup, a.taxTreatment, a.ownerNames, a.ledgerAccountCodeAndName,
    a.snapshotDate, a.provenance, a.contributionBasis |> Option.map Money.amount

/// A lot as returned: acquired date, quantity and reported cost basis.
type private LotSummary = LocalDate * decimal * decimal option

let private lotsByLine (a: HoldingsAsOfAccount) : (string * LotSummary list) list =
    a.lines
    |> List.map (fun l ->
        l.securityName,
        l.lots
        |> List.map (fun lot ->
            lot |> AccountSnapshotLot.acquiredDate,
            lot |> AccountSnapshotLot.quantity |> Quantity.amount,
            lot |> AccountSnapshotLot.reportedCostBasis |> Option.map Money.amount))

let private allSeven = [ Some "Equity Fund"; Some "Large Cap"; Some "Total Market"; Some "Diversified"; Some "Domestic"; Some "Growth"; Some "Example Total Market Index" ]
let private internationalValues = [ Some "Equity Fund"; None; None; None; Some "International"; None; None ]
let private bondValues = [ Some "Bond Fund"; None; None; None; None; None; None ]
let private noValues = [ None; None; None; None; None; None; None ]

[<Collection("SharedTestData")>]
type HoldingsAsOfTests(fixture: TestDataFixture) =

    let p = fixture.Data.positions

    [<Fact>]
    member _.``REQ-POS-8.1 the accounts in holdings as of a date are exactly the fixture accounts active on that date with a snapshot on or before it`` () =
        // as of the end of month -4 every snapshot so far is d1's; Joint Brokerage's first is d2 and Jordan Custodial has none
        holdingsAsOf p.monthEnd4
        |> Result.map (fun accounts ->
            Assert.Equal<string list>([ PF.alexBrokerage; PF.alexRoth; PF.oldBrokerage; PF.sam401k; PF.samHsa ], names accounts))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-8.1 REQ-POS-8.2 an account whose latest snapshot on or before the date is weeks older than the date is included with that older snapshot's date and lines`` () =
        holdingsAsOf p.monthEnd2
        |> Result.map (fun accounts ->
            let roth = accounts |> find PF.alexRoth
            Assert.Equal(p.d1, roth.snapshotDate)
            Assert.Equal<LineSummary list>(
                [ PF.bondFund, None, bondValues, None, 100M, 10.00M, 1000.00M, None
                  PF.totalMarket, Some "EXTMX", allSeven, None, 5M, 100.00M, 500.00M, None ],
                roth.lines |> List.map lineSummary))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-8.1 a snapshot dated after the as-of date is ignored in favour of the account's latest one on or before it`` () =
        holdingsAsOf (p.d2.PlusDays(1))
        |> Result.map (fun accounts ->
            let brokerage = accounts |> find PF.alexBrokerage
            Assert.Equal(p.d2, brokerage.snapshotDate)
            Assert.Equal<(string * decimal) list>(
                [ PF.international, 520.00M; PF.totalMarket, 1100.00M ],
                brokerage.lines |> List.map (fun l -> l.securityName, l.marketValue |> Money.amount)))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-8.1 an account whose active end is before the date is excluded although it has snapshots on or before the date`` () =
        holdingsAsOf p.monthEnd3
        |> Result.map (fun accounts -> Assert.DoesNotContain(PF.oldBrokerage, names accounts))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-8.1 REQ-POS-4.7 an account is included as of its active end date and excluded as of the day after`` () =
        result {
            let! onEnd = holdingsAsOf p.monthEnd4
            let! dayAfter = holdingsAsOf (p.monthEnd4.PlusDays(1))
            Assert.Contains(PF.oldBrokerage, names onEnd)
            Assert.DoesNotContain(PF.oldBrokerage, names dayAfter)
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-8.1 an account active on the date with no snapshot on or before it is excluded`` () =
        result {
            let! endOfMonth4 = holdingsAsOf p.monthEnd4
            let! endOfMonth1 = holdingsAsOf p.monthEnd1
            // Joint Brokerage's first snapshot is d2; Jordan Custodial never has one
            Assert.DoesNotContain(PF.jointBrokerage, names endOfMonth4)
            Assert.DoesNotContain(PF.jordanCustodial, names endOfMonth1)
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-8.1 an account whose latest snapshot has no lines is included with no lines`` () =
        holdingsAsOf p.monthEnd2
        |> Result.map (fun accounts ->
            let hsa = accounts |> find PF.samHsa
            Assert.Equal(p.d3, hsa.snapshotDate)
            Assert.Empty(hsa.lines))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-8.2 each included account carries its name, institution, account group, tax treatment, owners' names, linked ledger account's code and name, and its snapshot's date, provenance and contribution basis, as the fixture holds them`` () =
        result {
            let! endOfMonth1 = holdingsAsOf p.monthEnd1
            Assert.Equal<AccountSummary list>(
                [ PF.alexBrokerage, "Example Brokerage", "Brokerage", TaxTreatment.Taxable, [ PF.alex ],
                  Some("F-1260", "Fixture Brokerage at Cost"), p.d4, Reported, None
                  PF.alexRoth, "Example Brokerage", "Retirement", TaxTreatment.Roth, [ PF.alex ], None, p.d1, Reported, Some 1200.00M
                  PF.jointBrokerage, "Example Brokerage", "Brokerage", TaxTreatment.Taxable, [ PF.alex; PF.sam ], None, p.d4, Reported, None
                  PF.sam401k, "Example Retirement Services", "Retirement", TaxTreatment.TaxDeferred, [ PF.sam ], None, p.d3, Reported, None
                  PF.samHsa, "Example Health Bank", "Health", TaxTreatment.Hsa, [ PF.sam ], None, p.d3, Reported, None ],
                endOfMonth1 |> List.map accountSummary)
            let! endOfMonth4 = holdingsAsOf p.monthEnd4
            let sam401k = endOfMonth4 |> find PF.sam401k
            Assert.Equal((p.d1, Imported), (sam401k.snapshotDate, sam401k.provenance))
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-8.3 each line carries its Security's name and ticker, the name of its value in each of the seven dimensions or nothing, its Holding's basis method, and its quantity, price, market value and cost basis as recorded`` () =
        holdingsAsOf p.monthEnd2
        |> Result.map (fun accounts ->
            Assert.Equal<(string * LineSummary list) list>(
                [ PF.alexBrokerage,
                  [ PF.international, Some "EXINX", internationalValues, Some SpecificLot, 20M, 27.50M, 550.00M, Some 480.00M
                    PF.totalMarket, Some "EXTMX", allSeven, Some AverageCost, 12M, 105.00M, 1260.00M, Some 1110.00M ]
                  PF.alexRoth,
                  [ PF.bondFund, None, bondValues, None, 100M, 10.00M, 1000.00M, None
                    PF.totalMarket, Some "EXTMX", allSeven, None, 5M, 100.00M, 500.00M, None ]
                  PF.jointBrokerage, [ PF.totalMarket, Some "EXTMX", allSeven, Some AverageCost, 50M, 110.00M, 5500.00M, Some 5000.00M ]
                  PF.sam401k,
                  [ PF.bondFund, None, bondValues, None, 210.123456M, 9.876543M, 2075.29M, None
                    PF.totalMarket, Some "EXTMX", allSeven, None, 20.5M, 105.00M, 2152.50M, None ]
                  PF.samHsa, [] ],
                accounts |> List.map (fun a -> a.investmentAccountName, a.lines |> List.map lineSummary)))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-8.4 accounts are ordered by name and the lines within each account by Security name`` () =
        holdingsAsOf p.monthEnd4
        |> Result.map (fun accounts ->
            Assert.Equal<(string * string list) list>(
                [ PF.alexBrokerage, [ PF.international; PF.totalMarket ]
                  PF.alexRoth, [ PF.bondFund; PF.totalMarket ]
                  PF.oldBrokerage, [ PF.international ]
                  PF.sam401k, [ PF.bondFund; PF.totalMarket ]
                  PF.samHsa, [ PF.stableValue ] ],
                accounts |> List.map (fun a -> a.investmentAccountName, a.lines |> List.map (fun l -> l.securityName))))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-8.3 holdings as of a date give each line of the account's latest snapshot its lots with acquired date, quantity and reported cost basis in the order supplied, and a line recorded without lots carries none`` () =
        // as of the end of month -3 Joint Brokerage's latest snapshot is d2, whose one line carries the fixture's three
        // lots; Alex Brokerage's is also d2, whose lines carry none
        let lastYear = p.accountsActiveBegin
        holdingsAsOf p.monthEnd3
        |> Result.map (fun accounts ->
            Assert.Equal<(string * LotSummary list) list>(
                [ PF.totalMarket,
                  [ lastYear.PlusDays(60), 19.999998M, Some 2000.00M
                    lastYear.PlusDays(30), 15.000001M, Some 1500.00M
                    lastYear.PlusDays(30), 15.000001M, Some 1500.00M ] ],
                accounts |> find PF.jointBrokerage |> lotsByLine)
            Assert.Equal<(string * LotSummary list) list>(
                [ PF.international, []; PF.totalMarket, [] ], accounts |> find PF.alexBrokerage |> lotsByLine))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-8.3 holdings as of a date after a snapshot recorded without lots give that snapshot's lines no lots, not the lots of an earlier snapshot`` () =
        // Joint Brokerage's d4 snapshot carries no lots; its d2 snapshot carries three
        holdingsAsOf p.monthEnd1
        |> Result.map (fun accounts ->
            let joint = accounts |> find PF.jointBrokerage
            Assert.Equal(p.d4, joint.snapshotDate)
            Assert.Equal<(string * LotSummary list) list>([ PF.totalMarket, [] ], joint |> lotsByLine))
        |> railroadWrapper
