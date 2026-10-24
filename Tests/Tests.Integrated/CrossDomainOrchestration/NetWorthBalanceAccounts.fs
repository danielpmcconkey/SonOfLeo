module Tests.Integrated.CrossDomainOrchestration.NetWorthBalanceAccounts

open NodaTime
open App.DataAccessLayer.DbTransaction
open App.Operation.CoreAuditableAction
open App.Utility.FieldUpdate
open App.Utility.IAppError
open App.Utility.Result
open App.Session
open Business.FinancialServices
open Business.FinancialServices.Ledger.FiscalPeriodComponent
open Business.FinancialServices.Ledger.LedgerAuditableAction
open Business.FinancialServices.Positions
open Business.FinancialServices.Positions.PositionsAuditableAction
open Business.FinancialServices.Positions.PositionsError
open Business.FinancialServices.Positions.PositionsComponent
open Business.CrossDomainOrchestration
open Business.CrossDomainOrchestration.NetWorth
open Ui.InterfaceBridge.InterfaceContracts.PositionsContracts
open Ui.InterfaceBridge.BoundaryConverters.PositionsFieldConverters
open Ui.InterfaceBridge.CommandRoute
open Tests.Helpers
open Tests.Helpers.Railroad
open Tests.Helpers.PositionsValues
open Xunit

module PF = PositionsFixture

(* Every expected figure below is derived by hand from the fixture (see PositionsFixture.fs). The earliest fiscal period
   starts on ledgerStart, the first of month -5; monthEnd k is the last day of month -k, so monthEnd6 to monthEnd9 are
   earlier than it and no ledger account has a balance on them.

   Properties owned before ledgerStart: 34 Example Avenue (Rental, acquired a year ago, purchase basis 250,000.00, its
   mortgage Balance Account Fixture Rental Mortgage) and 7 Former Example Road (PrimaryResidence, acquired six years ago,
   disposed of on ledgerStart, 200,000.00, no mortgage). 12 Example Street is acquired in month -4. Property values
   450,000.00. Sam HSA's one snapshot before ledgerStart (the 15th of month -7) is 250.00.

   Balance Accounts, each closing at 0.00 on ledgerStart:
     Fixture Savings (Cash, Alex and Sam) 100.00 on monthEnd8
     Fixture Checking (Cash, Alex and Sam) 2,000.00 on monthEnd8, 2,500.00 on monthEnd7
     Fixture Card (Debt, Alex) 15.00 on monthEnd8
     Fixture Loan (Debt, Sam) 1,500.00 on monthEnd8, 0.00 on monthEnd7
     Fixture Rental Mortgage (Debt, Sam) 186,000.00 on monthEnd9, 185,000.00 on monthEnd8, 184,000.00 on monthEnd7

   monthEnd10: no Balance yet. Property values 450,000.00; net worth 450,000.00.
   monthEnd9: owned-property mortgages 186,000.00, nothing else but property values.
     Net worth 450,000.00 - 186,000.00 = 264,000.00; investable wealth 264,000.00 - 200,000.00 = 64,000.00.
   monthEnd8: cash 100.00 + 2,000.00 = 2,100.00; debts 15.00 + 1,500.00 = 1,515.00; owned-property mortgages 185,000.00;
     no investments. Net worth 2,100.00 + 450,000.00 - 1,515.00 - 185,000.00 = 265,585.00; investable wealth
     265,585.00 - 200,000.00 = 65,585.00.
   monthEnd3, within a fiscal period: every Balance Account at 0.00; the ledger and positions figures of NetWorth.fs:
     counted ledger assets 5,000.00, investments 12,920.00, property values 670,000.00, liabilities -25.00, owned-property
     mortgages 480,000.00 (F-2310 300,000.00, F-2320 180,000.00); net worth 207,945.00. *)

let private readOnly = Context.create NoTransaction FetchOnly

let private netWorthAsOf date = computeNetWorth readOnly date

let private amount (m: Money.Money) = m |> Money.amount

/// The nine totals: counted ledger assets, investments, property values, cash, liabilities, debts, owned-property
/// mortgages, net worth and investable wealth.
let private totals (r: NetWorth) =
    [ r.totalLedgerAssets; r.totalInvestments; r.totalPropertyValues; r.totalCash; r.totalLiabilities; r.totalDebts
      r.totalOwnedPropertyMortgages; r.netWorth; r.investableWealth ]
    |> List.map amount

let private nameOf context (b: NetWorthBalanceAccount) =
    b.balanceAccountId |> BalanceAccount.fetchById context |> mustBe |> BalanceAccount.balanceAccountName |> BalanceAccountName.value

/// A listed Balance Account: name, kind, owners' names, balance and the date of its Balance.
let private listedIn context (r: NetWorth) =
    r.balanceAccounts
    |> List.map (fun b ->
        nameOf context b, b.kind |> BalanceAccountKind.toString, b.ownerIds |> PositionsLookups.personNamesOf context,
        b.balance |> amount, b.balanceDate)

let private namesListed context (r: NetWorth) = r.balanceAccounts |> List.map (nameOf context)

let private propertyIn context name (r: NetWorth) =
    r.properties |> List.find (fun p -> p.propertyId |> PositionsLookups.propertyNameOf context = name)

/// A Property's listed mortgage Balance Accounts as (name, balance).
let private mortgageBalanceAccountsOf context name (r: NetWorth) =
    (r |> propertyIn context name).mortgageBalanceAccounts |> List.map (fun b -> nameOf context b, b.balance |> amount)

/// A Balance Account of the test's own, owned by Sam, with the Balances given.
let private balanceAccountWith context name kind (balances: (LocalDate * decimal) list) =
    result {
        let! sam = PositionsLookups.personIdOf context PF.sam
        let! created =
            BalanceAccountOrchestration.constructNewAndPersist
                context (BalanceAccountName.create name |> mustBe) kind (toInstitution "Example Bank") [ sam ]
        let balanceAccountId = created |> BalanceAccount.balanceAccountId
        let! _ =
            balances
            |> List.map (fun (date, value) -> balanceAccountId, date, toMoney value)
            |> BalanceAccountOrchestration.recordBalances context
        return balanceAccountId
    }

// the route path: the update goes through the route's converter as an input contract, then to the orchestrator
let private setMortgageBalanceAccounts context propertyName (names: string list) =
    let input: PropertyUpdateInput =
        { propertyName = propertyName
          propertyNameUpdate = NoChange
          propertyUseUpdate = NoChange
          ownersUpdate = NoChange
          acquisitionDateUpdate = NoChange
          disposalDateUpdate = NoChange
          purchaseBasisUpdate = NoChange
          assetAccountCodesUpdate = NoChange
          mortgageAccountCodesUpdate = NoChange
          mortgageBalanceAccountNamesUpdate = SetTo names }
    input
    |> ``convert [PropertyUpdateInput] to [PropertyFieldUpdates]`` context
    |> Result.bind (RealEstateOrchestration.updateProperty context)

let private ledgerCodesAndBalances context (rows: LedgerAccountBalance list) =
    rows |> List.map (fun r -> r.accountId |> PositionsLookups.ledgerCodeAndNameOf context |> fst, r.balance |> amount)

let private fiscalPeriodKeyOf (date: LocalDate) =
    $"{date.Year}-{date.Month:D2}" |> FiscalPeriodKey.fromString |> mustBe

let private outsideFiscalPeriods = function AsError (PositionsNetWorthDateOutsideFiscalPeriods d) -> Some d | _ -> None

[<Collection("SharedTestData")>]
type NetWorthBalanceAccountsTests(fixture: TestDataFixture) =

    let p = fixture.Data.positions
    let monthEnd10 = LocalDate(p.monthEnd9.Year, p.monthEnd9.Month, 1).PlusDays(-1)

    [<Fact>]
    member _.``REQ-RPT-8.1 REQ-RPT-8.7 net worth as of the day before the earliest fiscal period's start lists no Asset or Liability account, and as of that start date lists them at their ledger balances`` () =
        result {
            let! before = netWorthAsOf (p.ledgerStart.PlusDays(-1))
            let! onStart = netWorthAsOf p.ledgerStart
            Assert.Empty(before.assetAccounts)
            Assert.Empty(before.liabilityAccounts)
            Assert.Contains(("F-1275", 0.00M), onStart.assetAccounts |> ledgerCodesAndBalances readOnly)
            Assert.Contains(("F-2230", 0.00M), onStart.liabilityAccounts |> ledgerCodesAndBalances readOnly)
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
                let inLaterPeriod = monthPlus5Start.PlusMonths(1).PlusDays(14)
                let! computed = computeNetWorth context inLaterPeriod
                Assert.Equal(inLaterPeriod, computed.asOf)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-8.2 REQ-RPT-8.7 net worth on a date before the earliest fiscal period is the hand-derived holdings' market values plus owned Properties' values plus Cash Balance Account balances, less Debt Balance Account balances, with no ledger account contributing`` () =
        netWorthAsOf p.monthEnd8
        |> Result.map (fun r ->
            Assert.Equal<decimal list>(
                [ 0.00M; 0.00M; 450000.00M; 2100.00M; 0.00M; 1515.00M; 185000.00M; 265585.00M; 65585.00M ], totals r)
            Assert.Empty(r.assetAccounts)
            Assert.Empty(r.liabilityAccounts))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-8.2 REQ-POS-16.6 a Cash Balance Account adds its latest Balance on or before the date to net worth and a Debt Balance Account subtracts its own, each ignoring a Balance dated after the date`` () =
        runCommandRouteAndAutoRollback PositionsRecordBalanceAccountBalances (fun context ->
            result {
                let! _ = balanceAccountWith context "Example Wallet" BalanceAccountKind.Cash [ p.d1, 300.00M; p.d3, 500.00M ]
                let! _ = balanceAccountWith context "Example Store Card" BalanceAccountKind.Debt [ p.d1, 40.00M; p.d3, 90.00M ]
                let! r = computeNetWorth context p.monthEnd3
                Assert.Equal<decimal list>([ 300.00M; 40.00M; 208205.00M ], [ r.totalCash; r.totalDebts; r.netWorth ] |> List.map amount)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-8.3 REQ-RPT-8.4 on a date before the earliest fiscal period a primary residence's equity is its value less its mortgage Balance Account's balance, and investable wealth is net worth less that equity, not less the value`` () =
        runCommandRouteAndAutoRollback PositionsUpdateProperty (fun context ->
            result {
                let! _ = balanceAccountWith context "Former Residence Mortgage" BalanceAccountKind.Debt [ p.monthEnd9, 50000.00M ]
                let! _ = setMortgageBalanceAccounts context PF.formerResidence [ "Former Residence Mortgage" ]
                let! r = computeNetWorth context p.monthEnd8
                let residence = r |> propertyIn context PF.formerResidence
                // equity 200,000.00 - 50,000.00; net worth 265,585.00 - 50,000.00; less the equity, not the value
                Assert.Equal<decimal list>(
                    [ 150000.00M; 215585.00M; 65585.00M ], [ residence.equity; r.netWorth; r.investableWealth ] |> List.map amount)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-8.3 REQ-RPT-8.5 a Debt Balance Account that is a mortgage of a Property owned on the date is listed under that Property and counted in the owned-property mortgages total, not among the debts`` () =
        netWorthAsOf p.monthEnd8
        |> Result.map (fun r ->
            Assert.Equal<(string * decimal) list>(
                [ PF.rentalMortgageBalanceAccount, 185000.00M ], r |> mortgageBalanceAccountsOf readOnly PF.rental)
            Assert.Equal(65000.00M, (r |> propertyIn readOnly PF.rental).equity |> amount)
            Assert.Equal(185000.00M, r.totalOwnedPropertyMortgages |> amount)
            Assert.Equal(1515.00M, r.totalDebts |> amount)
            Assert.DoesNotContain(PF.rentalMortgageBalanceAccount, r |> namesListed readOnly))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-8.5 a Debt Balance Account that is a mortgage of a Property not owned on the date is listed among the Balance Accounts and counted in the debts total`` () =
        runCommandRouteAndAutoRollback PositionsUpdateProperty (fun context ->
            result {
                let! _ = balanceAccountWith context "Former Residence Mortgage" BalanceAccountKind.Debt [ p.monthEnd9, 40000.00M ]
                let! _ = setMortgageBalanceAccounts context PF.formerResidence [ "Former Residence Mortgage" ]
                // 7 Former Example Road was disposed of on ledgerStart
                let! r = computeNetWorth context p.monthEnd3
                Assert.Equal<(string * string * string list * decimal * LocalDate) list>(
                    [ "Former Residence Mortgage", "Debt", [ PF.sam ], 40000.00M, p.monthEnd9 ], r |> listedIn context)
                Assert.Equal<decimal list>([ 40000.00M; 167945.00M ], [ r.totalDebts; r.netWorth ] |> List.map amount)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-8.5 each listed Balance Account carries its name, kind, owners' names, balance and the date of the Balance it came from, and Balance Accounts are listed by name from a fixture set whose stored order differs from it`` () =
        netWorthAsOf p.monthEnd8
        |> Result.map (fun r ->
            Assert.Equal<(string * string * string list * decimal * LocalDate) list>(
                [ PF.card, "Debt", [ PF.alex ], 15.00M, p.monthEnd8
                  PF.checking, "Cash", [ PF.alex; PF.sam ], 2000.00M, p.monthEnd8
                  PF.loan, "Debt", [ PF.sam ], 1500.00M, p.monthEnd8
                  PF.savings, "Cash", [ PF.alex; PF.sam ], 100.00M, p.monthEnd8 ],
                r |> listedIn readOnly))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-8.5 REQ-RPT-8.8 on a date on which every Balance Account's balance is 0.00, none is listed, cash and debts each total 0.00, and neither is named absent`` () =
        netWorthAsOf p.monthEnd3
        |> Result.map (fun r ->
            Assert.Empty(r.balanceAccounts)
            Assert.Equal<decimal list>([ 0.00M; 0.00M ], [ r.totalCash; r.totalDebts ] |> List.map amount)
            Assert.DoesNotContain(Cash, r.absentComponents)
            Assert.DoesNotContain(Debts, r.absentComponents))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-8.5 a Property's mortgage Balance Account whose balance is 0.00 is not listed under the Property and adds nothing to its mortgages, and one with a nonzero balance is listed`` () =
        result {
            let! closed = netWorthAsOf p.monthEnd3
            let! open' = netWorthAsOf p.monthEnd7
            // at monthEnd3 34 Example Avenue's equity is 250,000.00 less F-2320's 180,000.00 only
            Assert.Empty(closed |> mortgageBalanceAccountsOf readOnly PF.rental)
            Assert.Equal(70000.00M, (closed |> propertyIn readOnly PF.rental).equity |> amount)
            Assert.Equal<(string * decimal) list>(
                [ PF.rentalMortgageBalanceAccount, 184000.00M ], open' |> mortgageBalanceAccountsOf readOnly PF.rental)
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-8.8 on a date before any Balance is recorded, no Balance Account is counted or listed, and cash and debts are both named absent, each totalling 0.00`` () =
        netWorthAsOf monthEnd10
        |> Result.map (fun r ->
            Assert.Empty(r.balanceAccounts)
            Assert.Empty(r |> mortgageBalanceAccountsOf readOnly PF.rental)
            Assert.Contains(Cash, r.absentComponents)
            Assert.Contains(Debts, r.absentComponents)
            Assert.Equal<decimal list>(
                [ 0.00M; 0.00M; 450000.00M; 0.00M; 0.00M; 0.00M; 0.00M; 450000.00M; 250000.00M ], totals r))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-8.8 on a date on which the only Debt balance is an owned Property's mortgage Balance Account, debts is named absent and totals 0.00, and mortgages of owned Properties is present`` () =
        netWorthAsOf p.monthEnd9
        |> Result.map (fun r ->
            Assert.Equal<NetWorthComponent list>(
                [ CountedLedgerAssets; Investments; Cash; Liabilities; Debts ], r.absentComponents)
            Assert.Equal<decimal list>(
                [ 0.00M; 0.00M; 450000.00M; 0.00M; 0.00M; 0.00M; 186000.00M; 264000.00M; 64000.00M ], totals r))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-8.8 on a date on which property values are the only contributor, the other six components are named absent, each totalling 0.00`` () =
        // three years ago only 7 Former Example Road is owned, with no mortgage
        netWorthAsOf (p.formerResidenceAcquired.PlusYears(3))
        |> Result.map (fun r ->
            Assert.Equal<NetWorthComponent list>(
                [ CountedLedgerAssets; Investments; Cash; Liabilities; Debts; OwnedPropertyMortgages ], r.absentComponents)
            Assert.Equal<decimal list>(
                [ 0.00M; 0.00M; 200000.00M; 0.00M; 0.00M; 0.00M; 0.00M; 200000.00M; 0.00M ], totals r))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-8.8 on a date before any record, all seven components are named absent in the order counted ledger assets, investments, property values, cash, liabilities, debts, mortgages of owned Properties, each totalling 0.00, and net worth is 0.00`` () =
        netWorthAsOf (p.formerResidenceAcquired.PlusDays(-1))
        |> Result.map (fun r ->
            Assert.Equal<NetWorthComponent list>(
                [ CountedLedgerAssets; Investments; PropertyValues; Cash; Liabilities; Debts; OwnedPropertyMortgages ],
                r.absentComponents)
            Assert.Equal<decimal list>(List.replicate 9 0.00M, totals r))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-8.8 on a date within a fiscal period, Asset and Liability accounts whose ledger balance is 0.00 are listed at 0.00, Balance Accounts at 0.00 keep cash and debts present, and no component is named absent`` () =
        // at the end of month -3 F-1280 and F-2230 have no ledger entry, and every Balance Account closed at 0.00
        netWorthAsOf p.monthEnd3
        |> Result.map (fun r ->
            Assert.Contains(("F-1280", 0.00M), r.assetAccounts |> ledgerCodesAndBalances readOnly)
            Assert.Contains(("F-2230", 0.00M), r.liabilityAccounts |> ledgerCodesAndBalances readOnly)
            Assert.Empty(r.absentComponents))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-8.5 with all seven totals nonzero, counted ledger assets plus investments plus property values plus cash, less liabilities, debts and owned-property mortgages, equals the net worth the computation returns`` () =
        runCommandRouteAndAutoRollback PositionsRecordBalanceAccountBalances (fun context ->
            result {
                let! _ = balanceAccountWith context "Example Wallet" BalanceAccountKind.Cash [ p.d2, 700.00M ]
                let! _ = balanceAccountWith context "Example Store Card" BalanceAccountKind.Debt [ p.d2, 300.00M ]
                let! r = computeNetWorth context p.monthEnd3
                let seven =
                    [ r.totalLedgerAssets; r.totalInvestments; r.totalPropertyValues; r.totalCash; r.totalLiabilities
                      r.totalDebts; r.totalOwnedPropertyMortgages ]
                    |> List.map amount
                Assert.Equal<decimal list>([ 5000.00M; 12920.00M; 670000.00M; 700.00M; -25.00M; 300.00M; 480000.00M ], seven)
                let assets = seven.[0] + seven.[1] + seven.[2] + seven.[3]
                Assert.Equal(assets - seven.[4] - seven.[5] - seven.[6], r.netWorth |> amount)
                Assert.Equal(208345.00M, r.netWorth |> amount)
            })
        |> railroadWrapper
