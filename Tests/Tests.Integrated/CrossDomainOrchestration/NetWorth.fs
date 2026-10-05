module Tests.Integrated.CrossDomainOrchestration.NetWorth

open NodaTime
open App.DataAccessLayer.DbTransaction
open App.Operation.CoreAuditableAction
open App.Utility
open App.Utility.IAppError
open App.Utility.Result
open App.Session
open Business.FinancialServices
open Business.FinancialServices.Ledger.JournalEntryComponent
open Business.FinancialServices.Positions.PositionsComponent
open Business.FinancialServices.Positions.PositionsError
open Business.FinancialServices.Positions.PositionsAuditableAction
open Business.CrossDomainOrchestration
open Business.CrossDomainOrchestration.JournalEntryOrchestration
open Business.CrossDomainOrchestration.NetWorth
open Ui.InterfaceBridge.CommandRoute
open Tests.Helpers
open Tests.Helpers.EntityFunctions
open Tests.Helpers.Railroad
open Tests.Helpers.PositionsValues
open Xunit

module PF = PositionsFixture

(* Every expected figure below is derived by hand from the fixture (see PositionsFixture.fs), as of the end of month -3:

   Ledger, entries dated on or before that date and not voided:
     F-1275 Fixture Positions Cash   5,000.00 Debit; the 1,000.00 mortgage payment is dated the 1st of month -2
     F-1290 Closed Bank              71.38 Debit then 71.38 Credit, in the closed period: 0.00
     F-2210 Mortgage Payable         one 25.00 Debit in the closed period: -25.00 on a credit-normal account
     F-1260 (linked to Alex Brokerage) 1,400.00 and F-1510 (linked to 12 Example Street) 400,000.00: not counted
     F-2310 300,000.00 and F-2320 180,000.00: the mortgages of 12 Example Street and 34 Example Avenue
     every other Asset and Liability account: 0.00
   Investments, each account's latest snapshot on or before the date (Old Brokerage ended at the end of month -4):
     Alex Brokerage d2 1,620.00; Alex Roth IRA d1 1,500.00; Joint Brokerage d2 5,500.00; Sam 401k d1 4,000.00;
     Sam HSA d1 300.00; total 12,920.00
   Properties owned on the date (7 Former Example Road was disposed of in month -5):
     12 Example Street 420,000.00 (the Valuation of the 15th of month -4); 34 Example Avenue 250,000.00 (purchase basis)
   Net worth = 5,000.00 + 12,920.00 + 670,000.00 - (-25.00) - (300,000.00 + 180,000.00) = 207,945.00
   12 Example Street's equity = 420,000.00 - 300,000.00 = 120,000.00; investable wealth = 207,945.00 - 120,000.00 = 87,945.00 *)

let private netWorthAsOf date = computeNetWorth (Context.create NoTransaction FetchOnly) date

let private amount (m: Money.Money) = m |> Money.amount

let private rows (accounts: LedgerAccountBalance list) = accounts |> List.map (fun r -> r.code, r.name, r.balance |> amount)

let private codes (accounts: LedgerAccountBalance list) = accounts |> List.map (fun r -> r.code)

let private property name (result: NetWorth) = result.properties |> List.find (fun p -> p.propertyName = name)

[<Collection("SharedTestData")>]
type NetWorthTests(fixture: TestDataFixture) =

    let p = fixture.Data.positions

    [<Fact>]
    member _.``REQ-RPT-8.1 net worth as of a date outside every fiscal period fails with a typed error naming the date`` () =
        let outside = Calendar.today().PlusYears(3)
        netWorthAsOf outside
        |> expectError
            (function AsError (PositionsNetWorthDateOutsideFiscalPeriods d) -> Some d | _ -> None)
            (fun d -> Assert.Equal(outside, d))

    [<Fact>]
    member _.``REQ-RPT-8.2 net worth is the fixture's unlinked Asset account balances plus its holdings' market values plus its owned Properties' values less its Liability account balances, each derived by hand from fixture data with voided entries and entries after the date excluded`` () =
        runCommandRouteAndAutoRollback FetchOnly (fun context ->
            result {
                // a 777.77 entry on F-1275 dated the day before, then voided: it must not count
                let! _, entryId =
                    createTestJournalEntryFromPrimitives
                        context "Net worth voided entry" None (p.monthEnd3.PlusDays(-1))
                        [ (p.positionsCash1275Id, 777.77M, "Debit", None); (p.positionsEquity3040Id, 777.77M, "Credit", None) ] [] []
                let! reason = CommentText.create "Entered in error"
                let! _ = JournalEntryVoiding.voidJournalEntry context None reason entryId
                let! result = computeNetWorth context p.monthEnd3
                Assert.Equal(207945.00M, result.netWorth |> amount)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-8.2 an Asset account linked to an Investment Account is absent from the counted ledger assets, its cost balance is not in net worth, and the linked account's market value is`` () =
        netWorthAsOf p.monthEnd3
        |> Result.map (fun result ->
            Assert.DoesNotContain("F-1260", result.assetAccounts |> codes)
            Assert.Equal(5000.00M, result.totalLedgerAssets |> amount)
            let brokerage = result.investmentAccounts |> List.find (fun a -> a.investmentAccountName = PF.alexBrokerage)
            Assert.Equal(1620.00M, brokerage.marketValue |> amount)
            Assert.Equal(12920.00M, result.totalInvestments |> amount)
            Assert.Equal(207945.00M, result.netWorth |> amount))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-8.2 an Asset account linked to a Property is absent from the counted ledger assets, its cost balance is not in net worth, and the Property's value is`` () =
        netWorthAsOf p.monthEnd3
        |> Result.map (fun result ->
            Assert.DoesNotContain("F-1510", result.assetAccounts |> codes)
            Assert.Equal(5000.00M, result.totalLedgerAssets |> amount)
            Assert.Equal(420000.00M, (result |> property PF.residence).value |> amount)
            Assert.Equal(670000.00M, result.totalPropertyValues |> amount))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-8.2 a parent Asset account is counted at its own balance, without its children's balances rolled in`` () =
        netWorthAsOf p.monthEnd3
        |> Result.map (fun result ->
            // F-1000 has no entries of its own; its child F-1275 holds 5,000.00
            Assert.Contains(("F-1000", "Assets", 0.00M), result.assetAccounts |> rows)
            Assert.Contains(("F-1275", "Fixture Positions Cash", 5000.00M), result.assetAccounts |> rows))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-8.2 a Property not owned on the date contributes nothing to net worth`` () =
        netWorthAsOf p.monthEnd3
        |> Result.map (fun result ->
            Assert.Equal<string list>([ PF.residence; PF.rental ], result.properties |> List.map (fun p -> p.propertyName))
            Assert.Equal(670000.00M, result.totalPropertyValues |> amount))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-8.3 a Property's equity is its value on the date less the as-of balance of each of its mortgage accounts`` () =
        netWorthAsOf p.monthEnd3
        |> Result.map (fun result ->
            let residence = result |> property PF.residence
            Assert.Equal<(string * string * decimal) list>([ "F-2310", "Fixture Residence Mortgage", 300000.00M ], residence.mortgageAccounts |> rows)
            Assert.Equal(120000.00M, residence.equity |> amount)
            Assert.Equal(70000.00M, (result |> property PF.rental).equity |> amount))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-8.2 REQ-RPT-8.3 a mortgage account's balance is subtracted from net worth exactly once, and is listed under its Property rather than among the Liability accounts`` () =
        netWorthAsOf p.monthEnd3
        |> Result.map (fun result ->
            Assert.DoesNotContain("F-2310", result.liabilityAccounts |> codes)
            Assert.DoesNotContain("F-2320", result.liabilityAccounts |> codes)
            Assert.Equal(-25.00M, result.totalLiabilities |> amount)
            // subtracted twice it would be 207,945.00 - 480,000.00; not at all, 687,945.00
            Assert.Equal(207945.00M, result.netWorth |> amount))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-8.4 investable wealth is net worth less the primary residence's equity, its value less its mortgage balance, and not less its value`` () =
        netWorthAsOf p.monthEnd3
        |> Result.map (fun result ->
            // less the value it would be 207,945.00 - 420,000.00 = -212,055.00
            Assert.Equal(87945.00M, result.investableWealth |> amount))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-8.4 as of a date on which no primary residence is owned, investable wealth equals net worth`` () =
        // the day before 12 Example Street's acquisition, after 7 Former Example Road's disposal
        netWorthAsOf (p.residenceAcquired.PlusDays(-1))
        |> Result.map (fun result ->
            Assert.Equal<string list>([ PF.rental ], result.properties |> List.map (fun p -> p.propertyName))
            Assert.Equal(result.netWorth |> amount, result.investableWealth |> amount))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-8.5 the result carries the as-of date, and each counted Asset account and each Liability account not a mortgage of a Property owned on the date, with its code, name and balance`` () =
        (* Expected rows from the fixture's own account and entry lists: every Asset account but the two linked to Positions,
           every Liability account but the mortgages of the two Properties owned on the date, each at its own balance
           from the fixture's unvoided lines dated on or before the date, in its normal-balance direction. *)
        let linkedAssets = set [ p.brokerageAtCost1260Id; p.residenceAtCost1510Id ]
        let ownedMortgages = set [ p.residenceMortgage2310Id; p.rentalMortgage2320Id ]
        let linesToDate =
            fixture.Data.journalEntries
            |> List.filter (fun je ->
                let h = je |> JournalEntryOrchestration.header
                h |> Ledger.JournalEntryHeader.voidedAt |> Option.isNone
                && h |> Ledger.JournalEntryHeader.entryDate |> EntryDate.entryDate <= p.monthEnd3)
            |> List.collect JournalEntryOrchestration.jeLines
        let sumOf accountId lineType =
            linesToDate
            |> List.filter (fun l -> Ledger.JournalEntryLine.accountId l = accountId && Ledger.JournalEntryLine.lineType l = lineType)
            |> List.sumBy (Ledger.JournalEntryLine.amount >> Money.amount)
        let expectedRows accountType excluded (balanceOf: Ledger.AccountComponent.AccountId -> decimal) =
            fixture.Data.accounts
            |> List.filter (fun a -> Ledger.Account.accountType a = accountType)
            |> List.filter (fun a -> not (excluded |> Set.contains (Ledger.Account.accountId a)))
            |> List.map (fun a ->
                a |> Ledger.Account.code |> Ledger.AccountComponent.AccountCode.value,
                a |> Ledger.Account.accountName |> Ledger.AccountComponent.AccountName.value,
                a |> Ledger.Account.accountId |> balanceOf)
            |> List.sortBy (fun (code, _, _) -> code)
        let expectedAssets =
            expectedRows Ledger.AccountComponent.AccountType.Asset linkedAssets (fun id -> sumOf id Debit - sumOf id Credit)
        let expectedLiabilities =
            expectedRows Ledger.AccountComponent.AccountType.Liability ownedMortgages (fun id -> sumOf id Credit - sumOf id Debit)
        netWorthAsOf p.monthEnd3
        |> Result.map (fun result ->
            Assert.Equal(p.monthEnd3, result.asOf)
            // the fixture's Positions cash and the closed period's mortgage payment, so the derivation is not all zeros
            Assert.Contains(("F-1275", "Fixture Positions Cash", 5000.00M), expectedAssets)
            Assert.Contains(("F-2210", "Mortgage Payable", -25.00M), expectedLiabilities)
            Assert.Equal<(string * string * decimal) list>(expectedAssets, result.assetAccounts |> rows)
            Assert.Equal<(string * string * decimal) list>(expectedLiabilities, result.liabilityAccounts |> rows))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-8.5 each included Investment Account carries its name, owners' names, account group, tax treatment, snapshot date, provenance, total market value and contribution basis`` () =
        netWorthAsOf p.monthEnd3
        |> Result.map (fun result ->
            Assert.Equal<(string * string list * string * TaxTreatment * LocalDate * Provenance * decimal * decimal option) list>(
                [ PF.alexBrokerage, [ PF.alex ], "Brokerage", TaxTreatment.Taxable, p.d2, Reported, 1620.00M, None
                  PF.alexRoth, [ PF.alex ], "Retirement", TaxTreatment.Roth, p.d1, Reported, 1500.00M, Some 1200.00M
                  PF.jointBrokerage, [ PF.alex; PF.sam ], "Brokerage", TaxTreatment.Taxable, p.d2, Reported, 5500.00M, None
                  PF.sam401k, [ PF.sam ], "Retirement", TaxTreatment.TaxDeferred, p.d1, Imported, 4000.00M, None
                  PF.samHsa, [ PF.sam ], "Health", TaxTreatment.Hsa, p.d1, Reported, 300.00M, None ],
                result.investmentAccounts
                |> List.map (fun a ->
                    a.investmentAccountName, a.ownerNames, a.accountGroup, a.taxTreatment, a.snapshotDate, a.provenance,
                    a.marketValue |> amount, a.contributionBasis |> Option.map amount)))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-8.5 each owned Property carries its name, use, owners' names, value, the date of the Valuation its value came from or the indication that it is the purchase basis, each mortgage account's code, name and balance, and its equity`` () =
        netWorthAsOf p.monthEnd3
        |> Result.map (fun result ->
            Assert.Equal<(string * PropertyUse * string list * decimal * PropertyValueSource * (string * string * decimal) list * decimal) list>(
                [ PF.residence, PrimaryResidence, [ PF.alex; PF.sam ], 420000.00M, ValuationDated p.valuation1,
                  [ "F-2310", "Fixture Residence Mortgage", 300000.00M ], 120000.00M
                  PF.rental, Rental, [ PF.sam ], 250000.00M, PurchaseBasisValue,
                  [ "F-2320", "Fixture Rental Mortgage", 180000.00M ], 70000.00M ],
                result.properties
                |> List.map (fun p ->
                    p.propertyName, p.propertyUse, p.ownerNames, p.value |> amount, p.valueSource, p.mortgageAccounts |> rows,
                    p.equity |> amount)))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-8.5 the totals of counted ledger assets, investments, property values and liabilities each equal the hand-summed rows they total`` () =
        netWorthAsOf p.monthEnd3
        |> Result.map (fun result ->
            Assert.Equal(
                (5000.00M, 12920.00M, 670000.00M, -25.00M),
                (result.totalLedgerAssets |> amount, result.totalInvestments |> amount,
                 result.totalPropertyValues |> amount, result.totalLiabilities |> amount)))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-8.5 investment market value totalled by tax treatment and by account group gives, for each value present among the included accounts, the hand-summed market value of those accounts`` () =
        netWorthAsOf p.monthEnd3
        |> Result.map (fun result ->
            // Taxable: Alex Brokerage 1,620.00 + Joint Brokerage 5,500.00; Retirement: Alex Roth IRA 1,500.00 + Sam 401k 4,000.00
            Assert.Equal<(TaxTreatment * decimal) list>(
                [ TaxTreatment.Taxable, 7120.00M; TaxTreatment.TaxDeferred, 4000.00M; TaxTreatment.Roth, 1500.00M; TaxTreatment.Hsa, 300.00M ],
                result.investmentsByTaxTreatment |> List.map (fun (t, m) -> t, m |> amount))
            Assert.Equal<(string * decimal) list>(
                [ "Brokerage", 7120.00M; "Health", 300.00M; "Retirement", 5500.00M ],
                result.investmentsByAccountGroup |> List.map (fun (g, m) -> g, m |> amount)))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-8.2 a Property linked to two asset accounts that both carry ledger balances: on a date the Property is owned neither account is among the counted ledger assets, and net worth counts the Property's value once and neither balance`` () =
        (* In a rolled-back transaction: two FixedAsset accounts carrying 60,000.00 and 90,000.00, both linked to a
           Rental bought a year ago for 150,000.00 with no Valuations. Net worth as of the end of month -3 is the
           fixture's 207,945.00 plus that 150,000.00; counting either balance as well would add 60,000.00 or 90,000.00. *)
        runCommandRouteAndAutoRollback PositionsCreateProperty (fun context ->
            result {
                let fixedAsset code name =
                    createTestAccountFromPrimitives
                        context code name "Asset" p.accountsActiveBegin None (Some "FixedAsset") None None
                    |> Result.map snd
                let! landId = fixedAsset "RPT-8.2A" "Two-asset property land at cost"
                let! buildingId = fixedAsset "RPT-8.2B" "Two-asset property building at cost"
                let! _ =
                    createTestJournalEntryFromPrimitives
                        context "Two-asset property purchase" None p.ledgerEntryDate
                        [ (landId, 60000.00M, "Debit", None)
                          (buildingId, 90000.00M, "Debit", None)
                          (p.positionsEquity3040Id, 150000.00M, "Credit", None) ] [] []
                let! sam = PositionsLookups.personIdOf context PF.sam
                let! _ =
                    RealEstateOrchestration.constructNewAndPersist
                        context (toPropertyName "56 Example Lane") Rental [ sam ]
                        (toOwnedPeriod p.rentalAcquired None) (toPurchaseBasis 150000.00M) [ landId; buildingId ] []
                let! result = computeNetWorth context p.monthEnd3
                Assert.DoesNotContain("RPT-8.2A", result.assetAccounts |> codes)
                Assert.DoesNotContain("RPT-8.2B", result.assetAccounts |> codes)
                Assert.Equal(5000.00M, result.totalLedgerAssets |> amount)
                Assert.Equal(150000.00M, (result |> property "56 Example Lane").value |> amount)
                Assert.Equal(820000.00M, result.totalPropertyValues |> amount)
                Assert.Equal(357945.00M, result.netWorth |> amount)
            })
        |> railroadWrapper

    (* In a rolled-back transaction: a Liability account carrying 50,000.00, the mortgage of a Rental disposed of on the
       10th of month -3, before the end of month -3. *)
    member private _.WithDisposedMortgagedProperty(test: Context.Context -> Result<unit, IAppError>) =
        runCommandRouteAndAutoRollback PositionsCreateProperty (fun context ->
            result {
                let! _, mortgageId =
                    createTestAccountFromPrimitives
                        context "RPT-8.5M" "Disposed property mortgage" "Liability" p.accountsActiveBegin None
                        (Some "LongTermLiability") None None
                let! _ =
                    createTestJournalEntryFromPrimitives
                        context "Disposed property mortgage" None p.ledgerEntryDate
                        [ (p.positionsEquity3040Id, 50000.00M, "Debit", None); (mortgageId, 50000.00M, "Credit", None) ] [] []
                let! sam = PositionsLookups.personIdOf context PF.sam
                let! _ =
                    RealEstateOrchestration.constructNewAndPersist
                        context (toPropertyName "78 Sold Example Court") Rental [ sam ]
                        (toOwnedPeriod p.rentalAcquired (Some p.d2)) (toPurchaseBasis 120000.00M) [] [ mortgageId ]
                return! test context
            })
        |> railroadWrapper

    [<Fact>]
    member this.``REQ-RPT-8.5 the owned-property mortgages total sums the as-of balances of the mortgage accounts of Properties owned on the date and excludes the mortgage of a Property disposed before the date`` () =
        this.WithDisposedMortgagedProperty(fun context ->
            result {
                let! result = computeNetWorth context p.monthEnd3
                // F-2310 300,000.00 (12 Example Street) + F-2320 180,000.00 (34 Example Avenue); not RPT-8.5M's 50,000.00
                Assert.Equal(480000.00M, result.totalOwnedPropertyMortgages |> amount)
            })

    [<Fact>]
    member this.``REQ-RPT-8.5 REQ-RPT-8.2 the mortgage of a Property disposed of before the date is listed among the Liability accounts at its as-of balance and subtracted from net worth once, and the Property is not among the owned Properties`` () =
        this.WithDisposedMortgagedProperty(fun context ->
            result {
                let! result = computeNetWorth context p.monthEnd3
                Assert.Equal<(string * string * decimal) list>(
                    [ "RPT-8.5M", "Disposed property mortgage", 50000.00M ],
                    result.liabilityAccounts |> rows |> List.filter (fun (code, _, _) -> code = "RPT-8.5M"))
                Assert.Equal(49975.00M, result.totalLiabilities |> amount)
                // subtracted twice it would be 107,945.00; not at all, 207,945.00
                Assert.Equal(157945.00M, result.netWorth |> amount)
                Assert.Equal<string list>([ PF.residence; PF.rental ], result.properties |> List.map (fun x -> x.propertyName))
            })

    [<Fact>]
    member _.``REQ-RPT-8.5 counted ledger assets plus investments plus property values, less liabilities and less owned-property mortgages, equals the net worth the computation returns, with a nonzero owned-property mortgages total`` () =
        netWorthAsOf p.monthEnd3
        |> Result.map (fun result ->
            Assert.Equal(480000.00M, result.totalOwnedPropertyMortgages |> amount)
            // 5,000.00 + 12,920.00 + 670,000.00 - (-25.00) - 480,000.00
            Assert.Equal(
                207945.00M,
                (result.totalLedgerAssets |> amount) + (result.totalInvestments |> amount)
                + (result.totalPropertyValues |> amount) - (result.totalLiabilities |> amount)
                - (result.totalOwnedPropertyMortgages |> amount))
            Assert.Equal(207945.00M, result.netWorth |> amount))
        |> railroadWrapper
