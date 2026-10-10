module Tests.Integrated.CrossDomainOrchestration.NetWorthPreLedger

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
open Business.CrossDomainOrchestration
open Business.CrossDomainOrchestration.NetWorth
open Ui.InterfaceBridge.CommandRoute
open Tests.Helpers
open Tests.Helpers.Railroad
open Tests.Helpers.PositionsValues
open Xunit

module PF = PositionsFixture

(* Every expected figure below is derived by hand from the fixture (see PositionsFixture.fs). The earliest fiscal period
   starts on ledgerStart, the first of month -5; monthEnd k is the last day of month -k, so monthEnd6 to monthEnd9 are
   pre-ledger dates. Ledger account balances on those dates come only from pre-ledger balances.

   Properties owned: 34 Example Avenue (Rental, acquired a year ago, purchase basis 250,000.00, mortgage F-2320) and
   7 Former Example Road (PrimaryResidence, acquired six years ago, disposed of on ledgerStart, 200,000.00, no mortgage).
   12 Example Street is acquired in month -4, so it is owned on none of these dates. Property values 450,000.00.

   monthEnd9: no asset, liability or investment record on or before it.
     mortgages F-2320 186,000.00 (monthEnd9). Net worth 450,000.00 - 186,000.00 = 264,000.00;
     investable wealth 264,000.00 - 200,000.00 (the former residence's equity) = 64,000.00.
     Absent: counted ledger assets, investments, liabilities.
   monthEnd8:
     assets F-1000 100.00, F-1275 2,000.00, F-1280 -15.00 = 2,085.00 (all dated monthEnd8)
     liabilities F-2230 1,500.00 (monthEnd8); mortgages F-2320 185,000.00 (monthEnd8); investments none
     Net worth 2,085.00 + 450,000.00 - 1,500.00 - 185,000.00 = 265,585.00. Absent: investments.
   monthEnd7:
     assets F-1000 100.00 (monthEnd8), F-1275 2,500.00 (monthEnd7), F-1280 -15.00 (monthEnd8) = 2,585.00
     liabilities F-2230 0.00 (monthEnd7); mortgages F-2320 184,000.00 (monthEnd7)
     investments Sam HSA's pre-ledger snapshot, 250.00
     Net worth 2,585.00 + 250.00 + 450,000.00 - 0.00 - 184,000.00 = 268,835.00. Nothing absent.
   Three years ago: only 7 Former Example Road, 200,000.00, with no mortgage. Net worth 200,000.00.
   The day before 7 Former Example Road was acquired: nothing at all. Net worth 0.00. *)

let private netWorthAsOf date = computeNetWorth (Context.create NoTransaction FetchOnly) date

let private amount (m: Money.Money) = m |> Money.amount

/// An account as listed: code, balance and the date the balance came from.
let private dated (accounts: LedgerAccountBalance list) = accounts |> List.map (fun r -> r.code, r.balance |> amount, r.balanceDate)

let private balances (accounts: LedgerAccountBalance list) = accounts |> List.map (fun r -> r.code, r.balance |> amount)

/// The seven totals: ledger assets, investments, property values, liabilities, owned-property mortgages, net worth and
/// investable wealth.
let private totals (r: NetWorth) =
    [ r.totalLedgerAssets; r.totalInvestments; r.totalPropertyValues; r.totalLiabilities; r.totalOwnedPropertyMortgages
      r.netWorth; r.investableWealth ]
    |> List.map amount

let private fiscalPeriodKeyOf (date: LocalDate) =
    $"{date.Year}-{date.Month:D2}" |> FiscalPeriodKey.fromString |> mustBe

let private outsideFiscalPeriods = function AsError (PositionsNetWorthDateOutsideFiscalPeriods d) -> Some d | _ -> None

[<Collection("SharedTestData")>]
type NetWorthPreLedgerTests(fixture: TestDataFixture) =

    let p = fixture.Data.positions

    [<Fact>]
    member _.``REQ-RPT-8.1 REQ-RPT-8.5 net worth as of the day before the earliest fiscal period's start date returns a result marked pre-ledger, and as of that start date returns one marked not pre-ledger`` () =
        result {
            let! before = netWorthAsOf (p.ledgerStart.PlusDays(-1))
            let! onStart = netWorthAsOf p.ledgerStart
            Assert.Equal((true, false), (before.isPreLedger, onStart.isPreLedger))
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-8.1 net worth as of a date in a gap between two fiscal periods fails with a typed error naming the date`` () =
        // the fixture's fiscal periods run to the end of month +4; a period for month +6 leaves month +5 a gap
        let monthPlus5Start = p.ledgerStart.PlusMonths(10)
        let inGap = monthPlus5Start.PlusDays(14)
        runCommandRouteAndAutoRollback FiscalPeriodCreate (fun context ->
            result {
                let! _ = monthPlus5Start.PlusMonths(1) |> fiscalPeriodKeyOf |> FiscalPeriodCreation.constructNewAndPersist context
                computeNetWorth context inGap |> expectError outsideFiscalPeriods (fun d -> Assert.Equal(inGap, d))
                let! inLaterPeriod = computeNetWorth context (monthPlus5Start.PlusMonths(1).PlusDays(14))
                Assert.False(inLaterPeriod.isPreLedger)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-8.2 REQ-RPT-8.7 net worth on a pre-ledger date is the hand-derived sum of the latest pre-ledger balances of the unlinked Asset accounts, the holdings' market values and the owned Properties' values, less the latest pre-ledger balances of the Liability accounts`` () =
        netWorthAsOf p.monthEnd7
        |> Result.map (fun r ->
            Assert.Equal<decimal list>([ 2585.00M; 250.00M; 450000.00M; 0.00M; 184000.00M; 268835.00M; 68835.00M ], totals r))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-8.7 on a pre-ledger date, an account with several pre-ledger balances takes the latest one on or before the date, ignores one dated after it, and carries that balance's date`` () =
        // F-1275 has 2,000.00 on monthEnd8 and 2,500.00 on monthEnd7
        result {
            let! onMe8 = netWorthAsOf p.monthEnd8
            let! onMe7 = netWorthAsOf p.monthEnd7
            let f1275 (r: NetWorth) = r.assetAccounts |> dated |> List.find (fun (c, _, _) -> c = "F-1275")
            Assert.Equal(("F-1275", 2000.00M, Some p.monthEnd8), f1275 onMe8)
            Assert.Equal(("F-1275", 2500.00M, Some p.monthEnd7), f1275 onMe7)
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-8.7 on a pre-ledger date, an account whose pre-ledger balance is 0.00 is listed at 0.00, and an account with no pre-ledger balance on or before the date is not listed at all`` () =
        // on monthEnd7 F-2230's latest balance is 0.00; F-1250, F-2210 and the rest have none
        netWorthAsOf p.monthEnd7
        |> Result.map (fun r ->
            Assert.Equal<(string * decimal) list>([ "F-2230", 0.00M ], r.liabilityAccounts |> balances)
            Assert.Equal<string list>([ "F-1000"; "F-1275"; "F-1280" ], r.assetAccounts |> List.map (fun a -> a.code)))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-8.7 on a pre-ledger date a parent account and its child, each with a pre-ledger balance, are each listed at their own balance, not summed`` () =
        // F-1000 is the parent of F-1275 and F-1280; rolled up it would be 2,085.00
        netWorthAsOf p.monthEnd8
        |> Result.map (fun r ->
            Assert.Equal<(string * decimal) list>(
                [ "F-1000", 100.00M; "F-1275", 2000.00M; "F-1280", -15.00M ], r.assetAccounts |> balances))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-8.7 on a date within a fiscal period, an account that carries pre-ledger balances is listed at its ledger balance and no pre-ledger balance figures in the result`` () =
        // at the end of month -3 F-1275's ledger balance is 5,000.00, F-1280's and F-2230's 0.00; net worth is slice 1's
        netWorthAsOf p.monthEnd3
        |> Result.map (fun r ->
            let listed = (r.assetAccounts |> balances) @ (r.liabilityAccounts |> balances)
            let find code = listed |> List.find (fun (c, _) -> c = code)
            Assert.Equal<(string * decimal) list>(
                [ "F-1000", 0.00M; "F-1275", 5000.00M; "F-1280", 0.00M; "F-2230", 0.00M ],
                [ find "F-1000"; find "F-1275"; find "F-1280"; find "F-2230" ])
            Assert.Equal(207945.00M, r.netWorth |> amount))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-8.3 REQ-RPT-8.7 on a pre-ledger date a Property's equity is its value less its mortgage account's latest pre-ledger balance on or before the date`` () =
        // 34 Example Avenue: 250,000.00 less F-2320's 185,000.00 of monthEnd8
        netWorthAsOf p.monthEnd8
        |> Result.map (fun r ->
            let rental = r.properties |> List.find (fun prop -> prop.propertyName = PF.rental)
            Assert.Equal<(string * decimal * LocalDate option) list>(
                [ "F-2320", 185000.00M, Some p.monthEnd8 ], rental.mortgageAccounts |> dated)
            Assert.Equal(65000.00M, rental.equity |> amount))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-8.5 on a pre-ledger date the result gives every listed asset, liability and mortgage account the date of the pre-ledger balance it came from, and on a fiscal-period date it gives no balance dates`` () =
        result {
            let! preLedger = netWorthAsOf p.monthEnd7
            let mortgages (r: NetWorth) = r.properties |> List.collect (fun prop -> prop.mortgageAccounts)
            Assert.Equal<(string * LocalDate option) list>(
                [ "F-1000", Some p.monthEnd8; "F-1275", Some p.monthEnd7; "F-1280", Some p.monthEnd8
                  "F-2230", Some p.monthEnd7; "F-2320", Some p.monthEnd7 ],
                (preLedger.assetAccounts @ preLedger.liabilityAccounts @ mortgages preLedger)
                |> List.map (fun a -> a.code, a.balanceDate))
            let! fiscal = netWorthAsOf p.monthEnd3
            let all = fiscal.assetAccounts @ fiscal.liabilityAccounts @ mortgages fiscal
            Assert.NotEmpty(all)
            Assert.All(all, fun a -> Assert.Equal(None, a.balanceDate))
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-8.8 on a pre-ledger date on which only investments are absent, investments is named as the one absent component and totals 0.00`` () =
        netWorthAsOf p.monthEnd8
        |> Result.map (fun r ->
            Assert.Equal<NetWorthComponent list>([ Investments ], r.absentComponents)
            Assert.Equal(0.00M, r.totalInvestments |> amount)
            Assert.Equal(265585.00M, r.netWorth |> amount))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-8.8 on a pre-ledger date on which the only Liability balance is an owned Property's mortgage, liabilities is named absent with counted ledger assets and investments, each totalling 0.00, and mortgages of owned Properties and property values are present`` () =
        netWorthAsOf p.monthEnd9
        |> Result.map (fun r ->
            Assert.Equal<NetWorthComponent list>([ CountedLedgerAssets; Investments; Liabilities ], r.absentComponents)
            Assert.Equal<decimal list>([ 0.00M; 0.00M; 450000.00M; 0.00M; 186000.00M; 264000.00M; 64000.00M ], totals r))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-8.8 on a pre-ledger date on which property values are the only contributor, the other four components are named absent, each totalling 0.00`` () =
        netWorthAsOf (p.formerResidenceAcquired.PlusYears(3))
        |> Result.map (fun r ->
            Assert.Equal<NetWorthComponent list>(
                [ CountedLedgerAssets; Investments; Liabilities; OwnedPropertyMortgages ], r.absentComponents)
            Assert.Equal<decimal list>([ 0.00M; 0.00M; 200000.00M; 0.00M; 0.00M; 200000.00M; 0.00M ], totals r))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-8.8 on a pre-ledger date before any record, all five components are named absent, each totalling 0.00, and net worth is 0.00`` () =
        netWorthAsOf (p.formerResidenceAcquired.PlusDays(-1))
        |> Result.map (fun r ->
            Assert.Equal<NetWorthComponent list>(
                [ CountedLedgerAssets; Investments; PropertyValues; Liabilities; OwnedPropertyMortgages ], r.absentComponents)
            Assert.Equal<decimal list>([ 0.00M; 0.00M; 0.00M; 0.00M; 0.00M; 0.00M; 0.00M ], totals r))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-8.8 on a date within a fiscal period, Asset and Liability accounts whose ledger balance is 0.00 are listed at 0.00 and no component is named absent`` () =
        // at the end of month -3 F-1280 and F-2230 have no ledger entry
        netWorthAsOf p.monthEnd3
        |> Result.map (fun r ->
            Assert.Contains(("F-1280", 0.00M), r.assetAccounts |> balances)
            Assert.Contains(("F-2230", 0.00M), r.liabilityAccounts |> balances)
            Assert.Empty(r.absentComponents))
        |> railroadWrapper
