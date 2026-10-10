module Tests.Integrated.CrossDomainOrchestration.PreLedgerBalanceGuards

open NodaTime
open App.Utility.FieldUpdate
open App.Utility.IAppError
open App.Utility.Result
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Ledger.FiscalPeriodComponent
open Business.FinancialServices.Ledger.LedgerAuditableAction
open Business.FinancialServices.Positions
open Business.FinancialServices.Positions.PositionsAuditableAction
open Business.FinancialServices.Positions.PositionsComponent
open Business.FinancialServices.Positions.PositionsError
open Business.CrossDomainOrchestration
open Business.CrossDomainOrchestration.PreLedgerBalanceOrchestration
open Ui.InterfaceBridge.CommandRoute
open Tests.Helpers
open Tests.Helpers.EntityFunctions
open Tests.Helpers.PositionsValues
open Tests.Helpers.Railroad
open Xunit

module PF = PositionsFixture

(* The fixture's pre-ledger balances (see PositionsFixture.fs) are dated monthEnd9 (F-2320 only), monthEnd8 and
   monthEnd7, the latest. The earliest fiscal period is month -5; month -6 has none, and starts the day after monthEnd7.
   Ledger accounts these tests link are their own, created inside the test's transaction and addressed by ID: the codes'
   route-lifetime cache would outlive the rollback. *)

let private keyOf (date: LocalDate) = FiscalPeriodKey.ofDate date
let private keyText (date: LocalDate) = keyOf date |> FiscalPeriodKey.value

let private periodKeys context =
    FiscalPeriod.fetchAll context false |> Result.map (List.map (FiscalPeriod.periodKey >> FiscalPeriodKey.value))

let private blockedFiscalPeriod =
    function
    | AsError(PositionsPreLedgerBalancesBlockFiscalPeriod(period, earliest, latest)) -> Some(period, earliest, latest)
    | _ -> None

let private blockedLink =
    function
    | AsError(PositionsPreLedgerBalancesBlockLink(code, earliest, latest)) -> Some(code, earliest, latest)
    | _ -> None

let private recordBalances context (entries: (AccountId * LocalDate * decimal) list) =
    entries |> List.map (fun (id, date, amount) -> id, date, toMoney amount) |> recordPreLedgerBalances context

/// Every pre-ledger balance: account code, balance date and balance.
let private allBalances context (p: PositionsFixtureData) =
    listPreLedgerBalances context None (p.monthEnd9.PlusYears(-10)) p.ledgerStart
    |> Result.map (
        List.map (fun v ->
            v.accountCode,
            v.preLedgerBalance |> PreLedgerBalance.balanceDate,
            v.preLedgerBalance |> PreLedgerBalance.balance |> Money.amount)
    )

let private ledgerAccount context (fixture: TestDataFixture) code name accountType subtype parent =
    createTestAccountFromPrimitives
        context code name accountType fixture.Data.positions.accountsActiveBegin None (Some subtype) (Some parent) None
    |> Result.map snd

let private investmentLedgerAccount context (fixture: TestDataFixture) code =
    ledgerAccount context fixture code "Test Brokerage at Cost" "Asset" "Investment" fixture.Data.assets1000Id

let private fixedAssetLedgerAccount context (fixture: TestDataFixture) code =
    ledgerAccount context fixture code "Test Cabin at Cost" "Asset" "FixedAsset" fixture.Data.assets1000Id

let private mortgageLedgerAccount context (fixture: TestDataFixture) code =
    ledgerAccount context fixture code "Test Cabin Mortgage" "Liability" "LongTermLiability" fixture.Data.liabilities2000Id

let private guardAccountName = "Guard Test Brokerage"
let private guardPropertyName = "Guard Test Cabin"

let private createInvestmentAccount context (p: PositionsFixtureData) (ledgerAccountId: AccountId option) =
    result {
        let! owners = PositionsLookups.personIdsOf context [ PF.alex ]
        return!
            InvestmentAccountOrchestration.constructNewAndPersist
                context
                (toAccountName guardAccountName)
                (toInstitution "Example Brokerage")
                (toAccountGroup "Brokerage")
                TaxTreatment.Taxable
                owners
                (toActivityPeriod p.accountsActiveBegin None)
                ledgerAccountId
    }

let private linkInvestmentAccount context (account: InvestmentAccount.InvestmentAccount) (ledgerAccountId: AccountId) =
    InvestmentAccountOrchestration.updateInvestmentAccount
        context
        { investmentAccountIdToUpdate = account |> InvestmentAccount.investmentAccountId
          investmentAccountNameUpdate = NoChange
          institutionUpdate = NoChange
          accountGroupUpdate = NoChange
          taxTreatmentUpdate = NoChange
          ownersUpdate = NoChange
          activityPeriodUpdate = NoChange
          ledgerAccountIdUpdate = SetTo(Some ledgerAccountId) }

let private linkOf context (account: InvestmentAccount.InvestmentAccount) =
    account
    |> InvestmentAccount.investmentAccountId
    |> InvestmentAccount.fetchById context
    |> Result.map InvestmentAccount.ledgerAccountId

let private createProperty context (p: PositionsFixtureData) (assets: AccountId list) (mortgages: AccountId list) =
    result {
        let! owners = PositionsLookups.personIdsOf context [ PF.sam ]
        return!
            RealEstateOrchestration.constructNewAndPersist
                context
                (toPropertyName guardPropertyName)
                PropertyUse.Rental
                owners
                (toOwnedPeriod p.accountsActiveBegin None)
                (toPurchaseBasis 100000.00M)
                assets
                mortgages
    }

let private updatePropertyAccounts
    context
    (property: Property.Property)
    (assetUpdate: FieldUpdate<Set<AccountId>>)
    (mortgageUpdate: FieldUpdate<Set<AccountId>>)
    =
    RealEstateOrchestration.updateProperty
        context
        { propertyIdToUpdate = property |> Property.propertyId
          propertyNameUpdate = NoChange
          propertyUseUpdate = NoChange
          ownedPeriodUpdate = NoChange
          purchaseBasisUpdate = NoChange
          assetAccountIdsUpdate = assetUpdate
          ownersUpdate = NoChange
          mortgageAccountIdsUpdate = mortgageUpdate }

let private storedProperty context (property: Property.Property) =
    property |> Property.propertyId |> Property.fetchById context

[<Collection("SharedTestData")>]
type PreLedgerBalanceGuardsTests(fixture: TestDataFixture) =

    let p = fixture.Data.positions

    [<Fact>]
    member _.``REQ-POS-15.7 creating a fiscal period whose start date is the date of a pre-ledger balance is rejected with a typed error naming the period and that date as both the earliest and latest balance date, and no fiscal period is created`` () =
        runCommandRouteAndAutoRollback FiscalPeriodCreate (fun context ->
            result {
                let balanceDate = p.monthEnd7.PlusDays(1) // the start of month -6
                let! _ = recordBalances context [ p.operatingCash1280Id, balanceDate, 10.00M ]
                FiscalPeriodCreation.constructNewAndPersist context (keyOf p.monthEnd6)
                |> expectError blockedFiscalPeriod (fun found ->
                    Assert.Equal((keyText p.monthEnd6, balanceDate, balanceDate), found))
                let! keys = periodKeys context
                Assert.DoesNotContain(keyText p.monthEnd6, keys)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-15.7 creating a fiscal period whose start date is the day after the latest pre-ledger balance date succeeds and the fiscal period exists`` () =
        runCommandRouteAndAutoRollback FiscalPeriodCreate (fun context ->
            result {
                let! created = FiscalPeriodCreation.constructNewAndPersist context (keyOf p.monthEnd6)
                Assert.Equal(p.monthEnd7.PlusDays(1), created |> FiscalPeriod.startDate)
                let! keys = periodKeys context
                Assert.Contains(keyText p.monthEnd6, keys)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-15.7 creating a fiscal period that starts after some pre-ledger balance dates and on or before others is rejected with a typed error naming the period and the earliest and latest of the balance dates on or after its start, none of those before it, and no fiscal period is created`` () =
        // month -8 starts the day after monthEnd9: the balances on monthEnd8 and monthEnd7 are on or after it
        runCommandRouteAndAutoRollback FiscalPeriodCreate (fun context ->
            result {
                FiscalPeriodCreation.constructNewAndPersist context (keyOf p.monthEnd8)
                |> expectError blockedFiscalPeriod (fun found ->
                    Assert.Equal((keyText p.monthEnd8, p.monthEnd8, p.monthEnd7), found))
                let! keys = periodKeys context
                Assert.DoesNotContain(keyText p.monthEnd8, keys)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-15.7 creating a fiscal period that starts before every pre-ledger balance date, with the earliest and latest balances on different ledger accounts, is rejected with a typed error naming the period and the earliest and latest of all balance dates, no fiscal period is created and every pre-ledger balance is unchanged`` () =
        // the earliest balance is F-2320's on monthEnd9; the latest becomes F-1280's on the first of month -6
        runCommandRouteAndAutoRollback FiscalPeriodCreate (fun context ->
            result {
                let latest = p.monthEnd7.PlusDays(1)
                let! _ = recordBalances context [ p.operatingCash1280Id, latest, 10.00M ]
                let! before = allBalances context p
                FiscalPeriodCreation.constructNewAndPersist context (keyOf p.monthEnd9)
                |> expectError blockedFiscalPeriod (fun found ->
                    Assert.Equal((keyText p.monthEnd9, p.monthEnd9, latest), found))
                let! keys = periodKeys context
                Assert.DoesNotContain(keyText p.monthEnd9, keys)
                let! after = allBalances context p
                Assert.Equal<(string * LocalDate * decimal) list>(before, after)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-15.7 ensuring fiscal periods exist for a range of months whose first missing month starts on or before a pre-ledger balance date is rejected with a typed error naming that first missing month's period and the earliest and latest such balance dates, and no fiscal period in the range is created`` () =
        // months -8, -7 and -6 are missing; -8 is the first, and the balances on monthEnd8 and monthEnd7 are on or after it
        runCommandRouteAndAutoRollback FiscalPeriodEnsure (fun context ->
            result {
                FiscalPeriodCreation.ensureFiscalPeriods context (keyOf p.monthEnd8) (keyOf p.monthEnd5)
                |> expectError blockedFiscalPeriod (fun found ->
                    Assert.Equal((keyText p.monthEnd8, p.monthEnd8, p.monthEnd7), found))
                let! keys = periodKeys context
                Assert.Empty(keys |> List.filter (fun k -> [ p.monthEnd8; p.monthEnd7; p.monthEnd6 ] |> List.map keyText |> List.contains k))
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-15.8 creating an Investment Account linked to a ledger account that carries pre-ledger balances is rejected with a typed error naming the account code and its earliest and latest balance dates, and no Investment Account is created`` () =
        runCommandRouteAndAutoRollback PositionsCreateInvestmentAccount (fun context ->
            result {
                let! ledgerId = investmentLedgerAccount context fixture "T-1291"
                let! _ = recordBalances context [ ledgerId, p.monthEnd8, 10.00M; ledgerId, p.monthEnd7, 20.00M ]
                createInvestmentAccount context p (Some ledgerId)
                |> expectError blockedLink (fun found -> Assert.Equal(("T-1291", p.monthEnd8, p.monthEnd7), found))
                let! found = InvestmentAccount.fetchByName context (toAccountName guardAccountName)
                Assert.True(found.IsNone)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-15.8 updating an Investment Account to link a ledger account that carries pre-ledger balances on two dates is rejected with a typed error naming the account code and its earliest and latest balance dates, and the Investment Account's link is unchanged`` () =
        runCommandRouteAndAutoRollback PositionsUpdateInvestmentAccount (fun context ->
            result {
                let! account = createInvestmentAccount context p None
                let! ledgerId = investmentLedgerAccount context fixture "T-1292"
                let! _ = recordBalances context [ ledgerId, p.monthEnd8, 10.00M; ledgerId, p.monthEnd7, 20.00M ]
                linkInvestmentAccount context account ledgerId
                |> expectError blockedLink (fun found -> Assert.Equal(("T-1292", p.monthEnd8, p.monthEnd7), found))
                let! link = linkOf context account
                Assert.Equal(None, link)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-15.8 creating a Property whose asset accounts include a ledger account that carries pre-ledger balances is rejected with a typed error naming the account code and its earliest and latest balance dates, and no Property is created`` () =
        runCommandRouteAndAutoRollback PositionsCreateProperty (fun context ->
            result {
                let! ledgerId = fixedAssetLedgerAccount context fixture "T-1592"
                let! _ = recordBalances context [ ledgerId, p.monthEnd8, 10.00M; ledgerId, p.monthEnd7, 20.00M ]
                createProperty context p [ ledgerId ] []
                |> expectError blockedLink (fun found -> Assert.Equal(("T-1592", p.monthEnd8, p.monthEnd7), found))
                let! found = Property.fetchByName context (toPropertyName guardPropertyName)
                Assert.True(found.IsNone)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-15.8 updating a Property's asset accounts to a set that adds a ledger account carrying pre-ledger balances on two dates is rejected with a typed error naming the account code and its earliest and latest balance dates, and the Property's asset accounts are unchanged`` () =
        runCommandRouteAndAutoRollback PositionsUpdateProperty (fun context ->
            result {
                let! keptId = fixedAssetLedgerAccount context fixture "T-1593"
                let! property = createProperty context p [ keptId ] []
                let! ledgerId = fixedAssetLedgerAccount context fixture "T-1594"
                let! _ = recordBalances context [ ledgerId, p.monthEnd8, 10.00M; ledgerId, p.monthEnd7, 20.00M ]
                updatePropertyAccounts context property (SetTo(Set.ofList [ keptId; ledgerId ])) NoChange
                |> expectError blockedLink (fun found -> Assert.Equal(("T-1594", p.monthEnd8, p.monthEnd7), found))
                let! stored = storedProperty context property
                Assert.Equal<Set<AccountId>>(Set.ofList [ keptId ], stored |> Property.assetAccountIds)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-15.8 creating a Property whose mortgage accounts include a ledger account carrying pre-ledger balances succeeds and the Property is stored with that mortgage account`` () =
        runCommandRouteAndAutoRollback PositionsCreateProperty (fun context ->
            result {
                let! mortgageId = mortgageLedgerAccount context fixture "T-2391"
                let! _ = recordBalances context [ mortgageId, p.monthEnd8, 10.00M; mortgageId, p.monthEnd7, 20.00M ]
                let! property = createProperty context p [] [ mortgageId ]
                let! stored = storedProperty context property
                Assert.Equal<Set<AccountId>>(Set.ofList [ mortgageId ], stored |> Property.mortgageAccountIds)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-15.8 updating a Property's mortgage accounts to add a ledger account carrying pre-ledger balances succeeds and the Property's mortgage accounts include it`` () =
        runCommandRouteAndAutoRollback PositionsUpdateProperty (fun context ->
            result {
                let! property = createProperty context p [] []
                let! mortgageId = mortgageLedgerAccount context fixture "T-2392"
                let! _ = recordBalances context [ mortgageId, p.monthEnd8, 10.00M; mortgageId, p.monthEnd7, 20.00M ]
                let! _ = updatePropertyAccounts context property NoChange (SetTo(Set.ofList [ mortgageId ]))
                let! stored = storedProperty context property
                Assert.Equal<Set<AccountId>>(Set.ofList [ mortgageId ], stored |> Property.mortgageAccountIds)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-15.8 once a ledger account's only pre-ledger balance is deleted, linking it to an Investment Account succeeds while other ledger accounts still carry pre-ledger balances, and the Investment Account is linked to it`` () =
        runCommandRouteAndAutoRollback PositionsUpdateInvestmentAccount (fun context ->
            result {
                let! account = createInvestmentAccount context p None
                let! ledgerId = investmentLedgerAccount context fixture "T-1295"
                let! _ = recordBalances context [ ledgerId, p.monthEnd7, 10.00M ]
                let! _ = deletePreLedgerBalance context ledgerId p.monthEnd7
                let! _ = linkInvestmentAccount context account ledgerId
                let! others = allBalances context p
                Assert.NotEmpty(others)
                let! link = linkOf context account
                Assert.Equal(Some ledgerId, link)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-15.8 once a ledger account's only pre-ledger balance is deleted, adding it to a Property's asset accounts succeeds while other ledger accounts still carry pre-ledger balances, and the Property's asset accounts include it`` () =
        runCommandRouteAndAutoRollback PositionsUpdateProperty (fun context ->
            result {
                let! property = createProperty context p [] []
                let! ledgerId = fixedAssetLedgerAccount context fixture "T-1595"
                let! _ = recordBalances context [ ledgerId, p.monthEnd7, 10.00M ]
                let! _ = deletePreLedgerBalance context ledgerId p.monthEnd7
                let! _ = updatePropertyAccounts context property (SetTo(Set.ofList [ ledgerId ])) NoChange
                let! others = allBalances context p
                Assert.NotEmpty(others)
                let! stored = storedProperty context property
                Assert.Equal<Set<AccountId>>(Set.ofList [ ledgerId ], stored |> Property.assetAccountIds)
            })
        |> railroadWrapper
