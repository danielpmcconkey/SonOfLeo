namespace Tests.Helpers

open App.Session
open App.Utility
open App.Utility.IAppError
open App.Utility.Result
open Business.General
open Business.General.PersonComponent
open Business.General.Person
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.Account
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Positions.PositionsComponent
open Business.CrossDomainOrchestration
open Business.CrossDomainOrchestration.JournalEntryOrchestration
open Ui.InterfaceBridge.BoundaryConverters.PersonFieldConverters
open Ui.InterfaceBridge.BoundaryConverters.AccountFieldConverters
open Ui.InterfaceBridge.BoundaryConverters.PositionsFieldConverters
open NodaTime
open Tests.Helpers.EntityFunctions

(*
The Positions archetypes. Every name here is fictional. The dates hang off four anchor months: the four whole months
before this one, called month -4 to month -1. Each of those is an open fiscal period. The rest of the fixture's ledger
has no entry dated in months -4 to -2, so as of the end of month -3 the only ledger balances are the ones the closed
period left behind (F-2210 Mortgage Payable carries one 25.00 Debit) and the ones staged here.

Snapshots, each line's quantity times price equal to its market value:

  Alex Brokerage (Taxable; Alex; Brokerage; linked to F-1260)
    d1 Reported  EXTMX 10 @ 100.00 = 1000.00 (cost 900.00)   EXINX 20 @ 25.00 = 500.00 (cost 480.00)   total 1500.00
    d2 Reported  EXTMX 10 @ 110.00 = 1100.00 (cost 900.00)   EXINX 20 @ 26.00 = 520.00 (cost 480.00)   total 1620.00
    d3 Reported  EXTMX 12 @ 105.00 = 1260.00 (cost 1110.00)  EXINX 20 @ 27.50 = 550.00 (cost 480.00)   total 1810.00
    d4 Reported  EXTMX 12 @ 115.00 = 1380.00 (cost 1110.00)                                             total 1380.00
  Alex Roth IRA (Roth; Alex; Retirement)
    d1 Reported, contribution basis 1200.00
                 EXTMX 5 @ 100.00 = 500.00 (no cost)          Example Bond Fund 100 @ 10.00 = 1000.00   total 1500.00
  Joint Brokerage (Taxable; Alex and Sam; Brokerage)
    d2 Reported  EXTMX 50 @ 110.00 = 5500.00 (cost 5000.00)                                             total 5500.00
                 lots, in the order supplied (A is acquired after B and C; B and C are identical):
                   A acquired lastYear + 60 days, 19.999998, cost 2000.00
                   B acquired lastYear + 30 days, 15.000001, cost 1500.00
                   C acquired lastYear + 30 days, 15.000001, cost 1500.00                          lots sum to 50
    d4 Reported  EXTMX 50 @ 115.00 = 5750.00 (cost 5000.00)                                             total 5750.00
  Sam 401k (TaxDeferred; Sam; Retirement)
    d1 Imported  EXTMX 20 @ 100.00 = 2000.00                  Example Bond Fund 200 @ 10.00 = 2000.00   total 4000.00
    d3 Reported  EXTMX 20.5 @ 105.00 = 2152.50
                 Example Bond Fund 210.123456 @ 9.876543 = 2075.29 (exact product 2075.293348492608)    total 4227.79
  Sam HSA (Hsa; Sam; Health)
    samHsaPreLedgerDate (the 15th of month -7, a pre-ledger date)
       Imported  Example Stable Value Fund 250 @ 1.00 = 250.00                                         total 250.00
    d1 Reported  Example Stable Value Fund 300 @ 1.00 = 300.00                                         total 300.00
    d3 Reported  no lines                                                                               total 0.00
  Old Brokerage (Taxable; Sam; Brokerage; active end monthEnd4)
    d1 Reported  EXINX 40 @ 25.00 = 1000.00 (cost 950.00)                                             total 1000.00
  Jordan Custodial (Taxable; Jordan; Custodial) has no Holding and no snapshot.

Properties:

  12 Example Street (PrimaryResidence; Alex and Sam), acquired residenceAcquired, purchase basis 400,000.00, asset
    account F-1510, mortgage F-2310. Valuations: 420,000.00 on valuation1, 430,000.00 on valuation2.
  34 Example Avenue (Rental; Sam), acquired a year ago, purchase basis 250,000.00, mortgage F-2320, no Valuations.
  7 Former Example Road (PrimaryResidence; Alex), acquired six years ago, disposed of on the first of month -5,
    purchase basis 200,000.00, no links.

Activity, recorded as one range per account:

  Sam 401k, range d1 - 1 to d3 + 1, between its d1 and d3 snapshots. Every kind appears at least once.
    d1 - 1   Purchase EXTMX 7 @ 100.00, 700.00                      before the roll-forward window
    d1       Purchase EXTMX 4 @ 100.00, 400.00                      on the first snapshot date: already in it
    d1 + 1   Contribution EXTMX 2 @ 100.00, 200.00, source "Employee deferral"       EXTMX in 2
    d1 + 2   Contribution, no Security, 150.00, source "Employer match"              no units
    d1 + 3   RolloverIn Bond 10, 100.00                                              Bond in 10
    d1 + 4   TransferIn EXTMX 1, 100.00                                              EXTMX in 1
    d1 + 5   Purchase Bond 5 @ 10.00, 50.00                                          Bond in 5
    d1 + 6   Reinvestment Bond 0.123456 @ 9.876543, 1.22                             Bond in 0.123456
    d1 + 7   AdjustmentIn EXTMX 3, 0.00                                              EXTMX in 3
    d1 + 8   Withdrawal EXTMX 1, 100.00                                              EXTMX out 1
    d1 + 9   RolloverOut Bond 2, 20.00                                               Bond out 2
    d1 + 10  TransferOut EXTMX 1.5, 150.00                                           EXTMX out 1.5
    d1 + 11  Sale Bond 3 @ 10.00, 30.00                                              Bond out 3
    d1 + 13  AdjustmentOut EXTMX 3, 0.00                                             EXTMX out 3
    d1 + 14  Dividend EXTMX, 12.34                                                   no units
    d1 + 15  Interest, no Security, 0.56                                             no units
    d1 + 16  CapitalGainDistribution Bond, 7.89                                      no units
    d3 - 1   Reinvestment EXTMX 0.125 @ 105.00, 13.13, twice, identical              EXTMX in 0.25
    d3       Fee EXTMX 0.25, 25.00                                                   EXTMX out 0.25
    d3 + 1   Sale EXTMX 6 @ 105.00, 630.00                          after the window
  In the window EXTMX moves in 6.25 and out 5.75 (20 to 20.5), Bond in 15.123456 and out 5 (200 to 210.123456):
  the roll-forward from d1 to d3 balances.

  Alex Brokerage, range d3 + 1 to d4, between its d3 and d4 snapshots:
    d3 + 2   Sale EXTMX 15 @ 105.00, 1575.00
    d3 + 3   Dividend EXINX, 5.50
  The roll-forward from d3 to d4 does not balance: EXTMX expected 12 - 15 = -3 against 12, a difference of 15;
  EXINX expected 20 against 0 (it is not on d4), a difference of -20.

Pre-ledger balances (the earliest fiscal period starts on ledgerStart, the first of month -5; monthEnd k is the last
day of month -k):

  F-1000 Assets                   100.00 on monthEnd8
  F-1275 Fixture Positions Cash   2,000.00 on monthEnd8; 2,500.00 on monthEnd7
  F-1280 Fixture Operating Cash   -15.00 on monthEnd8
  F-2230 Fixture Loan Payable     1,500.00 on monthEnd8; 0.00 on monthEnd7
  F-2320 Fixture Rental Mortgage  186,000.00 on monthEnd9; 185,000.00 on monthEnd8; 184,000.00 on monthEnd7

Ledger entries, all dated ledgerEntryDate (the 5th of month -4) except the last:

  F-1260 Fixture Brokerage at Cost   Debit 1,400.00     F-3040 Credit 1,400.00
  F-1275 Fixture Positions Cash      Debit 5,000.00     F-3040 Credit 5,000.00
  F-1510 Fixture Residence at Cost   Debit 400,000.00   F-2310 Credit 300,000.00   F-3040 Credit 100,000.00
  F-3040                             Debit 180,000.00   F-2320 Credit 180,000.00
  dated mortgagePaymentDate (the 1st of month -2):
  F-2310                             Debit 1,000.00     F-1275 Credit 1,000.00
*)

type PositionsFixtureData =
    { /// Snapshot dates: the 10th of months -4, -3, -2 and -1.
      d1: LocalDate
      d2: LocalDate
      d3: LocalDate
      d4: LocalDate
      /// The last day of months -4, -3, -2 and -1.
      monthEnd4: LocalDate
      monthEnd3: LocalDate
      monthEnd2: LocalDate
      monthEnd1: LocalDate
      /// The last day of months -5 to -9. Month -5 is the earliest fiscal period; months -6 to -9 are pre-ledger.
      monthEnd5: LocalDate
      monthEnd6: LocalDate
      monthEnd7: LocalDate
      monthEnd8: LocalDate
      monthEnd9: LocalDate
      /// The start of the earliest fiscal period, the first of month -5.
      ledgerStart: LocalDate
      /// The date of Sam HSA's one pre-ledger snapshot, the 15th of month -7.
      samHsaPreLedgerDate: LocalDate
      /// Every account except Old Brokerage is active from this date with no end.
      accountsActiveBegin: LocalDate
      ledgerEntryDate: LocalDate
      mortgagePaymentDate: LocalDate
      residenceAcquired: LocalDate
      rentalAcquired: LocalDate
      formerResidenceAcquired: LocalDate
      formerResidenceDisposed: LocalDate
      valuation1: LocalDate
      valuation2: LocalDate
      brokerageAtCost1260Id: AccountId
      positionsCash1275Id: AccountId
      residenceAtCost1510Id: AccountId
      residenceMortgage2310Id: AccountId
      rentalMortgage2320Id: AccountId
      positionsEquity3040Id: AccountId
      operatingCash1280Id: AccountId
      loanPayable2230Id: AccountId }

/// Name-to-ID lookups for tests that address Persons and Positions entities by name, as the routes' converters do.
module PositionsLookups =
    let personIdOf context name = name |> ``convert [PersonNameString] to [PersonId]`` context
    let personIdsOf context names = names |> ``convert [PersonNameString list] to [PersonId list]`` context
    let dimensionValueIdOf context dimension name =
        name |> ``convert [DimensionValueNameString] to [DimensionValueId]`` context dimension
    let securityIdOf context name = name |> ``convert [SecurityNameString] to [SecurityId]`` context
    let investmentAccountIdOf context name = name |> ``convert [InvestmentAccountNameString] to [InvestmentAccountId]`` context
    let propertyIdOf context name = name |> ``convert [PropertyNameString] to [PropertyId]`` context

module PositionsFixture =
    let alex = "Alex Example"
    let sam = "Sam Example"
    let jordan = "Jordan Example"
    let alexBirthdate = LocalDate(1980, 4, 12)
    let samBirthdate = LocalDate(1982, 9, 30)
    let jordanBirthdate = LocalDate(2010, 1, 5)

    let totalMarket = "Example Total Market Index Fund"
    let international = "Example International Index Fund"
    let bondFund = "Example Bond Fund"
    let stableValue = "Example Stable Value Fund"

    let alexBrokerage = "Alex Brokerage"
    let alexRoth = "Alex Roth IRA"
    let jointBrokerage = "Joint Brokerage"
    let sam401k = "Sam 401k"
    let samHsa = "Sam HSA"
    let oldBrokerage = "Old Brokerage"
    let jordanCustodial = "Jordan Custodial"

    let residence = "12 Example Street"
    let rental = "34 Example Avenue"
    let formerResidence = "7 Former Example Road"

    /// Each dimension's values, as the fixture creates them.
    let dimensionValues =
        [ InvestmentType, "Equity Fund"
          InvestmentType, "Bond Fund"
          MarketCap, "Large Cap"
          IndexType, "Total Market"
          Sector, "Diversified"
          Region, "Domestic"
          Region, "International"
          Objective, "Growth"
          Benchmark, "Example Total Market Index" ]

    let private mustBe (r: Result<'a, IAppError>) = r |> Result.defaultWith (fun e -> failwith (e.ToMessage()))
    let private personName raw = PersonName.create raw |> mustBe
    let private securityName raw = SecurityName.create raw |> mustBe
    let private money d = Money.fromDecimal d |> mustBe

    /// Stages the Positions archetypes. Returns their data, the ledger accounts it created and the journal entries it
    /// posted, so the caller can count them with the rest of its fixture.
    let stage (context: Context.Context) (today: LocalDate) (assets1000Id: AccountId) (liabilities2000Id: AccountId) (equity3000Id: AccountId) =
        result {
            let firstOfThisMonth = LocalDate(today.Year, today.Month, 1)
            let monthStart k = firstOfThisMonth.PlusMonths(-k)
            let d1, d2, d3, d4 = (monthStart 4).PlusDays(9), (monthStart 3).PlusDays(9), (monthStart 2).PlusDays(9), (monthStart 1).PlusDays(9)
            let monthEnd k = (monthStart (k - 1)).PlusDays(-1)
            let lastYear = today.PlusYears(-1)
            let ledgerEntryDate = (monthStart 4).PlusDays(4)
            let mortgagePaymentDate = monthStart 2
            let residenceAcquired = (monthStart 4).PlusDays(4)
            let formerResidenceDisposed = monthStart 5
            let valuation1 = (monthStart 4).PlusDays(14)
            let valuation2 = (monthStart 2).PlusDays(14)

            // Ledger accounts and entries
            let account code name accountType subtype parent =
                createTestAccountFromPrimitives context code name accountType lastYear None subtype parent None
            let! brokerageAtCost, brokerageAtCostId =
                account "F-1260" "Fixture Brokerage at Cost" "Asset" (Some "Investment") (Some assets1000Id)
            let! positionsCash, positionsCashId =
                account "F-1275" "Fixture Positions Cash" "Asset" (Some "Cash") (Some assets1000Id)
            let! residenceAtCost, residenceAtCostId =
                account "F-1510" "Fixture Residence at Cost" "Asset" (Some "FixedAsset") (Some assets1000Id)
            let! residenceMortgage, residenceMortgageId =
                account "F-2310" "Fixture Residence Mortgage" "Liability" (Some "LongTermLiability") (Some liabilities2000Id)
            let! rentalMortgage, rentalMortgageId =
                account "F-2320" "Fixture Rental Mortgage" "Liability" (Some "LongTermLiability") (Some liabilities2000Id)
            let! positionsEquity, positionsEquityId =
                account "F-3040" "Fixture Positions Equity" "Equity" None (Some equity3000Id)

            let entry description date lines =
                createTestJournalEntryFromPrimitives context description None date lines [] [] |> Result.map fst
            let! brokerageEntry =
                entry "Fixture brokerage cost" ledgerEntryDate
                    [ (brokerageAtCostId, 1400.00M, "Debit", None); (positionsEquityId, 1400.00M, "Credit", None) ]
            let! cashEntry =
                entry "Fixture positions cash" ledgerEntryDate
                    [ (positionsCashId, 5000.00M, "Debit", None); (positionsEquityId, 5000.00M, "Credit", None) ]
            let! residenceEntry =
                entry "Fixture residence purchase" ledgerEntryDate
                    [ (residenceAtCostId, 400000.00M, "Debit", None)
                      (residenceMortgageId, 300000.00M, "Credit", None)
                      (positionsEquityId, 100000.00M, "Credit", None) ]
            let! rentalEntry =
                entry "Fixture rental mortgage" ledgerEntryDate
                    [ (positionsEquityId, 180000.00M, "Debit", None); (rentalMortgageId, 180000.00M, "Credit", None) ]
            let! mortgagePaymentEntry =
                entry "Fixture residence mortgage payment" mortgagePaymentDate
                    [ (residenceMortgageId, 1000.00M, "Debit", None); (positionsCashId, 1000.00M, "Credit", None) ]

            // Persons
            let! _ = PersonOrchestration.constructNewAndPersist context (personName alex) alexBirthdate
            let! _ = PersonOrchestration.constructNewAndPersist context (personName sam) samBirthdate
            let! _ = PersonOrchestration.constructNewAndPersist context (personName jordan) jordanBirthdate

            // Dimension values and Securities
            let! _ =
                dimensionValues
                |> List.map (fun (dimension, name) ->
                    DimensionValueName.create name
                    |> Result.bind (DimensionValueOrchestration.constructNewAndPersist context dimension))
                |> convertListOfResultsToResultsList
            let security name ticker values =
                result {
                    let! ticker = ticker |> convertOptionToDesiredTypeWithFallibleConverter Ticker.create
                    let! values =
                        values
                        |> List.map (fun (d, n) -> PositionsLookups.dimensionValueIdOf context d n |> Result.map (fun v -> d, v))
                        |> convertListOfResultsToResultsList
                    return! SecurityOrchestration.constructNewAndPersist context (securityName name) ticker values
                }
            let! _ =
                security totalMarket (Some "EXTMX")
                    [ InvestmentType, "Equity Fund"; MarketCap, "Large Cap"; IndexType, "Total Market"
                      Sector, "Diversified"; Region, "Domestic"; Objective, "Growth"
                      Benchmark, "Example Total Market Index" ]
            let! _ = security international (Some "EXINX") [ InvestmentType, "Equity Fund"; Region, "International" ]
            let! _ = security bondFund None [ InvestmentType, "Bond Fund" ]
            let! _ = security stableValue None []

            // Investment Accounts and Holdings
            let investmentAccount name institution group treatment owners activeEnd ledgerAccountId =
                result {
                    let! institution = Institution.create institution
                    let! group = AccountGroup.create group
                    let! period =
                        ActivityPeriod.create lastYear activeEnd ActivityPeriod.NotConsideredAvailableBeforeBeginDate
                    let! name = InvestmentAccountName.create name
                    let! owners = PositionsLookups.personIdsOf context owners
                    return!
                        InvestmentAccountOrchestration.constructNewAndPersist
                            context name institution group treatment owners period ledgerAccountId
                }
            let! _ = investmentAccount alexBrokerage "Example Brokerage" "Brokerage" Taxable [ alex ] None (Some brokerageAtCostId)
            let! _ = investmentAccount alexRoth "Example Brokerage" "Retirement" Roth [ alex ] None None
            let! _ = investmentAccount jointBrokerage "Example Brokerage" "Brokerage" Taxable [ alex; sam ] None None
            let! _ = investmentAccount sam401k "Example Retirement Services" "Retirement" TaxDeferred [ sam ] None None
            let! _ = investmentAccount samHsa "Example Health Bank" "Health" Hsa [ sam ] None None
            let! _ = investmentAccount oldBrokerage "Example Brokerage" "Brokerage" Taxable [ sam ] (Some(monthEnd 4)) None
            let! _ = investmentAccount jordanCustodial "Example Brokerage" "Custodial" Taxable [ jordan ] None None

            let holding account security basisMethod =
                result {
                    let! accountId = PositionsLookups.investmentAccountIdOf context account
                    let! securityId = PositionsLookups.securityIdOf context security
                    return! HoldingOrchestration.constructNewAndPersist context accountId securityId basisMethod
                }
            let! _ = holding alexBrokerage totalMarket (Some AverageCost)
            let! _ = holding alexBrokerage international (Some SpecificLot)
            let! _ = holding alexRoth totalMarket None
            let! _ = holding alexRoth bondFund None
            let! _ = holding jointBrokerage totalMarket (Some AverageCost)
            let! _ = holding sam401k totalMarket None
            let! _ = holding sam401k bondFund None
            let! _ = holding samHsa stableValue None
            let! _ = holding oldBrokerage international (Some AverageCost)

            // Snapshots
            let lotsLine security (quantity: decimal) (price: decimal) (marketValue: decimal) (costBasis: decimal option) lots =
                PositionsLookups.securityIdOf context security
                |> mustBe
                |> fun securityId ->
                    securityId,
                    (Quantity.fromDecimal quantity |> mustBe),
                    (Price.fromDecimal price |> mustBe),
                    money marketValue,
                    (costBasis |> Option.map money),
                    (lots
                     |> List.map (fun (acquired: LocalDate, lotQuantity: decimal, lotCost: decimal option) ->
                         acquired, (Quantity.fromDecimal lotQuantity |> mustBe), (lotCost |> Option.map money)))
            let line security quantity price marketValue costBasis = lotsLine security quantity price marketValue costBasis []
            let samHsaPreLedgerDate = (monthStart 7).PlusDays(14)
            let snapshot account date provenance contributionBasis lines =
                (PositionsLookups.investmentAccountIdOf context account |> mustBe),
                date,
                provenance,
                (contributionBasis |> Option.map (money >> ContributionBasis.create >> mustBe)),
                lines
            let! _ =
                AccountSnapshotOrchestration.recordSnapshots context
                    [ snapshot alexBrokerage d1 Reported None
                          [ line totalMarket 10M 100.00M 1000.00M (Some 900.00M)
                            line international 20M 25.00M 500.00M (Some 480.00M) ]
                      snapshot alexBrokerage d2 Reported None
                          [ line totalMarket 10M 110.00M 1100.00M (Some 900.00M)
                            line international 20M 26.00M 520.00M (Some 480.00M) ]
                      snapshot alexBrokerage d3 Reported None
                          [ line totalMarket 12M 105.00M 1260.00M (Some 1110.00M)
                            line international 20M 27.50M 550.00M (Some 480.00M) ]
                      snapshot alexBrokerage d4 Reported None [ line totalMarket 12M 115.00M 1380.00M (Some 1110.00M) ]
                      snapshot alexRoth d1 Reported (Some 1200.00M)
                          [ line totalMarket 5M 100.00M 500.00M None; line bondFund 100M 10.00M 1000.00M None ]
                      snapshot jointBrokerage d2 Reported None
                          [ lotsLine totalMarket 50M 110.00M 5500.00M (Some 5000.00M)
                                [ lastYear.PlusDays(60), 19.999998M, Some 2000.00M
                                  lastYear.PlusDays(30), 15.000001M, Some 1500.00M
                                  lastYear.PlusDays(30), 15.000001M, Some 1500.00M ] ]
                      snapshot jointBrokerage d4 Reported None [ line totalMarket 50M 115.00M 5750.00M (Some 5000.00M) ]
                      snapshot sam401k d1 Imported None
                          [ line totalMarket 20M 100.00M 2000.00M None; line bondFund 200M 10.00M 2000.00M None ]
                      snapshot sam401k d3 Reported None
                          [ line totalMarket 20.5M 105.00M 2152.50M None
                            line bondFund 210.123456M 9.876543M 2075.29M None ]
                      snapshot samHsa samHsaPreLedgerDate Imported None [ line stableValue 250M 1.00M 250.00M None ]
                      snapshot samHsa d1 Reported None [ line stableValue 300M 1.00M 300.00M None ]
                      snapshot samHsa d3 Reported None []
                      snapshot oldBrokerage d1 Reported None [ line international 40M 25.00M 1000.00M (Some 950.00M) ] ]

            // Activity
            let activity date kind description source security (quantity: decimal option) (price: decimal option) amount =
                (date: LocalDate),
                (kind: ActivityKind),
                (ActivityDescription.create description |> mustBe),
                (source |> Option.map (ActivitySource.create >> mustBe)),
                (security |> Option.map (PositionsLookups.securityIdOf context >> mustBe)),
                (quantity |> Option.map (Quantity.fromDecimal >> mustBe)),
                (price |> Option.map (Price.fromDecimal >> mustBe)),
                money amount
            let day (anchor: LocalDate) offset = anchor.PlusDays(offset)
            let reinvestment = activity (day d3 -1) ActivityKind.Reinvestment "Dividend reinvested" None (Some totalMarket) (Some 0.125M) (Some 105.00M) 13.13M
            let! _ =
                InvestmentActivityOrchestration.recordActivity context
                    [ (PositionsLookups.investmentAccountIdOf context sam401k |> mustBe),
                      day d1 -1,
                      day d3 1,
                      [ activity (day d1 -1) ActivityKind.Purchase "Bought before the window" None (Some totalMarket) (Some 7M) (Some 100.00M) 700.00M
                        activity d1 ActivityKind.Purchase "Bought on the first snapshot date" None (Some totalMarket) (Some 4M) (Some 100.00M) 400.00M
                        activity (day d1 1) ActivityKind.Contribution "Payroll contribution" (Some "Employee deferral") (Some totalMarket) (Some 2M) (Some 100.00M) 200.00M
                        activity (day d1 2) ActivityKind.Contribution "Employer contribution to cash" (Some "Employer match") None None None 150.00M
                        activity (day d1 3) ActivityKind.RolloverIn "Rollover from a prior plan" None (Some bondFund) (Some 10M) None 100.00M
                        activity (day d1 4) ActivityKind.TransferIn "Transfer in of shares" None (Some totalMarket) (Some 1M) None 100.00M
                        activity (day d1 5) ActivityKind.Purchase "Exchange purchase" None (Some bondFund) (Some 5M) (Some 10.00M) 50.00M
                        activity (day d1 6) ActivityKind.Reinvestment "Interest reinvested" None (Some bondFund) (Some 0.123456M) (Some 9.876543M) 1.22M
                        activity (day d1 7) ActivityKind.AdjustmentIn "Share class conversion in" None (Some totalMarket) (Some 3M) None 0.00M
                        activity (day d1 8) ActivityKind.Withdrawal "Hardship withdrawal" None (Some totalMarket) (Some 1M) None 100.00M
                        activity (day d1 9) ActivityKind.RolloverOut "Rollover to another plan" None (Some bondFund) (Some 2M) None 20.00M
                        activity (day d1 10) ActivityKind.TransferOut "Transfer out of shares" None (Some totalMarket) (Some 1.5M) None 150.00M
                        activity (day d1 11) ActivityKind.Sale "Exchange sale" None (Some bondFund) (Some 3M) (Some 10.00M) 30.00M
                        activity (day d1 13) ActivityKind.AdjustmentOut "Share class conversion out" None (Some totalMarket) (Some 3M) None 0.00M
                        activity (day d1 14) ActivityKind.Dividend "Dividend paid" None (Some totalMarket) None None 12.34M
                        activity (day d1 15) ActivityKind.Interest "Interest on cash" None None None None 0.56M
                        activity (day d1 16) ActivityKind.CapitalGainDistribution "Capital gain paid" None (Some bondFund) None None 7.89M
                        reinvestment
                        reinvestment
                        activity d3 ActivityKind.Fee "Advisory fee" None (Some totalMarket) (Some 0.25M) None 25.00M
                        activity (day d3 1) ActivityKind.Sale "Sold after the window" None (Some totalMarket) (Some 6M) (Some 105.00M) 630.00M ]
                      (PositionsLookups.investmentAccountIdOf context alexBrokerage |> mustBe),
                      day d3 1,
                      d4,
                      [ activity (day d3 2) ActivityKind.Sale "Sale" None (Some totalMarket) (Some 15M) (Some 105.00M) 1575.00M
                        activity (day d3 3) ActivityKind.Dividend "Dividend paid" None (Some international) None None 5.50M ] ]

            // Pre-ledger balances
            let! operatingCashId = "F-1280" |> fallibleConverterAccountCodeToAccountId context
            let! loanPayableId = "F-2230" |> fallibleConverterAccountCodeToAccountId context
            let! _ =
                PreLedgerBalanceOrchestration.recordPreLedgerBalances context
                    [ assets1000Id, monthEnd 8, money 100.00M
                      positionsCashId, monthEnd 8, money 2000.00M
                      positionsCashId, monthEnd 7, money 2500.00M
                      operatingCashId, monthEnd 8, money -15.00M
                      loanPayableId, monthEnd 8, money 1500.00M
                      loanPayableId, monthEnd 7, money 0.00M
                      rentalMortgageId, monthEnd 9, money 186000.00M
                      rentalMortgageId, monthEnd 8, money 185000.00M
                      rentalMortgageId, monthEnd 7, money 184000.00M ]

            // Properties and Valuations
            let property name propertyUse owners acquired disposed basis assetAccountIds mortgageIds =
                result {
                    let! name = PropertyName.create name
                    let! period = OwnedPeriod.create acquired disposed
                    let! basis = Money.fromDecimal basis |> Result.bind PurchaseBasis.create
                    let! owners = PositionsLookups.personIdsOf context owners
                    return!
                        RealEstateOrchestration.constructNewAndPersist
                            context name propertyUse owners period basis assetAccountIds mortgageIds
                }
            let formerResidenceAcquired = today.PlusYears(-6)
            let! _ =
                property formerResidence PrimaryResidence [ alex ] formerResidenceAcquired (Some formerResidenceDisposed)
                    200000.00M [] []
            let! _ =
                property residence PrimaryResidence [ alex; sam ] residenceAcquired None 400000.00M
                    [ residenceAtCostId ] [ residenceMortgageId ]
            let! _ = property rental Rental [ sam ] lastYear None 250000.00M [] [ rentalMortgageId ]
            let valuation date value basis =
                result {
                    let! propertyId = PositionsLookups.propertyIdOf context residence
                    let! value = Money.fromDecimal value |> Result.bind ValuationValue.create
                    let! basis = ValuationBasis.create basis
                    return! RealEstateOrchestration.recordValuation context propertyId date value basis
                }
            let! _ = valuation valuation1 420000.00M "Appraisal"
            let! _ = valuation valuation2 430000.00M "Comparable sales"

            let data =
                { d1 = d1
                  d2 = d2
                  d3 = d3
                  d4 = d4
                  monthEnd4 = monthEnd 4
                  monthEnd3 = monthEnd 3
                  monthEnd2 = monthEnd 2
                  monthEnd1 = monthEnd 1
                  monthEnd5 = monthEnd 5
                  monthEnd6 = monthEnd 6
                  monthEnd7 = monthEnd 7
                  monthEnd8 = monthEnd 8
                  monthEnd9 = monthEnd 9
                  ledgerStart = monthStart 5
                  samHsaPreLedgerDate = samHsaPreLedgerDate
                  accountsActiveBegin = lastYear
                  ledgerEntryDate = ledgerEntryDate
                  mortgagePaymentDate = mortgagePaymentDate
                  residenceAcquired = residenceAcquired
                  rentalAcquired = lastYear
                  formerResidenceAcquired = formerResidenceAcquired
                  formerResidenceDisposed = formerResidenceDisposed
                  valuation1 = valuation1
                  valuation2 = valuation2
                  brokerageAtCost1260Id = brokerageAtCostId
                  positionsCash1275Id = positionsCashId
                  residenceAtCost1510Id = residenceAtCostId
                  residenceMortgage2310Id = residenceMortgageId
                  rentalMortgage2320Id = rentalMortgageId
                  positionsEquity3040Id = positionsEquityId
                  operatingCash1280Id = operatingCashId
                  loanPayable2230Id = loanPayableId }
            let accounts = [ brokerageAtCost; positionsCash; residenceAtCost; residenceMortgage; rentalMortgage; positionsEquity ]
            let entries = [ brokerageEntry; cashEntry; residenceEntry; rentalEntry; mortgagePaymentEntry ]
            return data, accounts, (entries: JournalEntry list)
        }

/// Constructors for the Positions slice's value types that fail the test on a construction error, for tests whose
/// subject is not the construction itself.
module PositionsValues =
    let mustBe (r: Result<'a, IAppError>) = r |> Result.defaultWith (fun e -> failwith (e.ToMessage()))
    let toPersonName raw = PersonName.create raw |> mustBe
    let toSecurityName raw = SecurityName.create raw |> mustBe
    let toAccountName raw = InvestmentAccountName.create raw |> mustBe
    let toPropertyName raw = PropertyName.create raw |> mustBe
    let toDimensionValueName raw = DimensionValueName.create raw |> mustBe
    let toTicker raw = Ticker.create raw |> mustBe
    let toInstitution raw = Institution.create raw |> mustBe
    let toAccountGroup raw = AccountGroup.create raw |> mustBe
    let toMoney d = Money.fromDecimal d |> mustBe
    let toQuantity d = Quantity.fromDecimal d |> mustBe
    let toPrice d = Price.fromDecimal d |> mustBe
    let toPurchaseBasis d = toMoney d |> PurchaseBasis.create |> mustBe
    let toValuationValue d = toMoney d |> ValuationValue.create |> mustBe
    let toValuationBasis raw = ValuationBasis.create raw |> mustBe
    let toContributionBasis d = toMoney d |> ContributionBasis.create |> mustBe
    let toActivityPeriod (activeBegin: LocalDate) (activeEnd: LocalDate option) =
        ActivityPeriod.create activeBegin activeEnd ActivityPeriod.NotConsideredAvailableBeforeBeginDate |> mustBe
    let toOwnedPeriod (acquired: LocalDate) (disposed: LocalDate option) = OwnedPeriod.create acquired disposed |> mustBe

    /// Asserts the result failed with the error pick recognises, then checks that error's payload.
    let expectError (pick: IAppError -> 'p option) (check: 'p -> unit) (result: Result<'a, IAppError>) : unit =
        match result with
        | Error e ->
            match pick e with
            | Some payload -> check payload
            | None -> Xunit.Assert.Fail $"Wrong error. {e.ToMessage()}"
        | Ok _ -> Xunit.Assert.Fail "Expected failure; got success"
