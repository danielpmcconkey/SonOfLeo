module Tests.Integrated.CrossDomainOrchestration.HoldingMaintenance

open App.DataAccessLayer.DbTransaction
open App.Operation.CoreAuditableAction
open App.Utility.IAppError
open App.Utility.Result
open App.Session
open Business.FinancialServices.Positions
open Business.FinancialServices.Positions.PositionsAuditableAction
open Business.FinancialServices.Positions.PositionsComponent
open Business.FinancialServices.Positions.PositionsError
open Business.CrossDomainOrchestration.HoldingOrchestration
open Ui.InterfaceBridge.CommandRoute
open Tests.Helpers
open Tests.Helpers.PositionsValues
open Tests.Helpers.Railroad
open Xunit

module PF = PositionsFixture

/// A Holding as listed: account name, Security name and basis method.
type private HoldingSummary = string * string * BasisMethod option

let private summary (view: HoldingView) : HoldingSummary =
    view.investmentAccountName, view.securityName, view.holding |> Holding.basisMethod

// the route path: accounts and Securities are addressed by name, resolved to IDs as the routes' converters resolve them
let private holdingsOf context account =
    PositionsLookups.investmentAccountIdOf context account
    |> Result.bind (Some >> listHoldings context)
    |> Result.map (List.map summary)

let private withIds context account security =
    result {
        let! accountId = PositionsLookups.investmentAccountIdOf context account
        let! securityId = PositionsLookups.securityIdOf context security
        return accountId, securityId
    }

let private create context account security basisMethod =
    withIds context account security
    |> Result.bind (fun (accountId, securityId) -> constructNewAndPersist context accountId securityId basisMethod)

let private changeBasisMethod context account security basisMethod =
    withIds context account security
    |> Result.bind (fun (accountId, securityId) -> changeHoldingBasisMethod context accountId securityId basisMethod)

let private basisNotAllowed = function AsError (PositionsBasisMethodNotAllowed (a, s, t)) -> Some(a, s, t) | _ -> None

let private fixtureHoldings : HoldingSummary list =
    [ PF.alexBrokerage, PF.international, Some SpecificLot
      PF.alexBrokerage, PF.totalMarket, Some AverageCost
      PF.alexRoth, PF.bondFund, None
      PF.alexRoth, PF.totalMarket, None
      PF.jointBrokerage, PF.totalMarket, Some AverageCost
      PF.oldBrokerage, PF.international, Some AverageCost
      PF.sam401k, PF.bondFund, None
      PF.sam401k, PF.totalMarket, None
      PF.samHsa, PF.stableValue, None ]

[<Collection("SharedTestData")>]
type HoldingMaintenanceTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-POS-11.5 creating a Holding in a Taxable account with AverageCost stores it, and listing that account's Holdings shows the Security with AverageCost`` () =
        runCommandRouteAndAutoRollback PositionsCreateHolding (fun context ->
            result {
                let! _ = create context PF.jordanCustodial PF.totalMarket (Some AverageCost)
                let! holdings = holdingsOf context PF.jordanCustodial
                Assert.Equal<HoldingSummary list>([ PF.jordanCustodial, PF.totalMarket, Some AverageCost ], holdings)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-11.5 creating a Holding in a Roth account with no basis method stores it, and the list shows it with no basis method`` () =
        runCommandRouteAndAutoRollback PositionsCreateHolding (fun context ->
            result {
                let! _ = create context PF.alexRoth PF.stableValue None
                let! holdings = holdingsOf context PF.alexRoth
                Assert.Contains((PF.alexRoth, PF.stableValue, None), holdings)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-5.1 creating a second Holding of the same Security in the same account is rejected with a typed error naming the account and the Security`` () =
        runCommandRouteAndAutoRollback PositionsCreateHolding (fun context ->
            create context PF.alexBrokerage PF.totalMarket (Some SpecificLot)
            |> expectError
                (function AsError (PositionsHoldingAlreadyExists (a, s)) -> Some(a, s) | _ -> None)
                (fun found -> Assert.Equal((PF.alexBrokerage, PF.totalMarket), found))
            |> Ok)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-5.1 a Security already held in one account can be held in a second account`` () =
        runCommandRouteAndAutoRollback PositionsCreateHolding (fun context ->
            result {
                let! _ = create context PF.jordanCustodial PF.international (Some AverageCost)
                let! all = listHoldings context None
                let holdersOfInternational =
                    all |> List.map summary |> List.filter (fun (_, s, _) -> s = PF.international) |> List.map (fun (a, _, _) -> a)
                Assert.Equal<string list>([ PF.alexBrokerage; PF.jordanCustodial; PF.oldBrokerage ], holdersOfInternational)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-5.2 creating a Holding with no basis method in a Taxable account is rejected with a typed error naming the account, the Security and Taxable`` () =
        runCommandRouteAndAutoRollback PositionsCreateHolding (fun context ->
            create context PF.jordanCustodial PF.bondFund None
            |> expectError basisNotAllowed (fun found -> Assert.Equal((PF.jordanCustodial, PF.bondFund, "Taxable"), found))
            |> Ok)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-5.2 creating a Holding with a basis method in a Roth account is rejected with a typed error naming the account, the Security and Roth`` () =
        runCommandRouteAndAutoRollback PositionsCreateHolding (fun context ->
            create context PF.alexRoth PF.stableValue (Some AverageCost)
            |> expectError basisNotAllowed (fun found -> Assert.Equal((PF.alexRoth, PF.stableValue, "Roth"), found))
            |> Ok)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-11.5 REQ-POS-5.2 changing a Taxable account's Holding from AverageCost to SpecificLot stores SpecificLot`` () =
        runCommandRouteAndAutoRollback PositionsUpdateHoldingBasisMethod (fun context ->
            result {
                let! _ =
                    changeBasisMethod context PF.alexBrokerage PF.totalMarket (Some SpecificLot)
                let! holdings = holdingsOf context PF.alexBrokerage
                Assert.Contains((PF.alexBrokerage, PF.totalMarket, Some SpecificLot), holdings)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-11.5 REQ-POS-5.2 removing the basis method of a Taxable account's Holding is rejected with a typed error naming the account, the Security and Taxable, and the basis method is unchanged`` () =
        runCommandRouteAndAutoRollback PositionsUpdateHoldingBasisMethod (fun context ->
            result {
                changeBasisMethod context PF.alexBrokerage PF.totalMarket None
                |> expectError basisNotAllowed (fun found -> Assert.Equal((PF.alexBrokerage, PF.totalMarket, "Taxable"), found))
                let! holdings = holdingsOf context PF.alexBrokerage
                Assert.Contains((PF.alexBrokerage, PF.totalMarket, Some AverageCost), holdings)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-11.9 creating a Holding naming a Security that matches no Security fails with a typed not-found error naming Security and the name`` () =
        runCommandRouteAndAutoRollback PositionsCreateHolding (fun context ->
            create context PF.jordanCustodial "Example Missing Fund" (Some AverageCost)
            |> expectError
                (function AsError (PositionsSecurityNameDoesntMatch n) -> Some n | _ -> None)
                (fun n -> Assert.Equal("Example Missing Fund", n))
            |> Ok)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-11.9 creating a Holding naming an Investment Account that matches no account fails with a typed not-found error naming Investment Account and the name`` () =
        runCommandRouteAndAutoRollback PositionsCreateHolding (fun context ->
            create context "Missing Brokerage" PF.totalMarket (Some AverageCost)
            |> expectError
                (function AsError (PositionsInvestmentAccountNameDoesntMatch n) -> Some n | _ -> None)
                (fun n -> Assert.Equal("Missing Brokerage", n))
            |> Ok)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-11.5 listing Holdings with no account filter returns every fixture Holding ordered by account name then Security name, each with its basis method, and no other Holding`` () =
        listHoldings (Context.create NoTransaction FetchOnly) None
        |> Result.map (fun all -> Assert.Equal<HoldingSummary list>(fixtureHoldings, all |> List.map summary))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-11.5 listing Holdings limited to one account returns every Holding of that account and none of any other account`` () =
        holdingsOf (Context.create NoTransaction FetchOnly) PF.sam401k
        |> Result.map (fun holdings ->
            Assert.Equal<HoldingSummary list>(
                [ PF.sam401k, PF.bondFund, None; PF.sam401k, PF.totalMarket, None ], holdings))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-11.10 deleting a Holding that no snapshot line references removes it, and listing the account's Holdings no longer shows that Security while the account's other Holdings remain`` () =
        runCommandRouteAndAutoRollback PositionsDeleteHolding (fun context ->
            result {
                let! _ = create context PF.alexRoth PF.stableValue None
                let! before = holdingsOf context PF.alexRoth
                Assert.Contains((PF.alexRoth, PF.stableValue, None), before)
                let! accountId, securityId = withIds context PF.alexRoth PF.stableValue
                let! _ = deleteHolding context accountId securityId
                let! after = holdingsOf context PF.alexRoth
                Assert.Equal<HoldingSummary list>(
                    [ PF.alexRoth, PF.bondFund, None; PF.alexRoth, PF.totalMarket, None ], after)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-11.10 deleting a Holding that a snapshot line references is rejected with a typed error naming the account and the Security, and the Holding remains`` () =
        runCommandRouteAndAutoRollback PositionsDeleteHolding (fun context ->
            result {
                let! accountId, securityId = withIds context PF.alexBrokerage PF.international
                deleteHolding context accountId securityId
                |> expectError
                    (function AsError (PositionsHoldingReferencedBySnapshots (a, s)) -> Some(a, s) | _ -> None)
                    (fun found -> Assert.Equal((PF.alexBrokerage, PF.international), found))
                let! holdings = holdingsOf context PF.alexBrokerage
                Assert.Equal<HoldingSummary list>(
                    [ PF.alexBrokerage, PF.international, Some SpecificLot
                      PF.alexBrokerage, PF.totalMarket, Some AverageCost ],
                    holdings)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-11.10 REQ-SYS-6.2 deleting a Holding of a Security the account does not hold fails with a typed not-found error naming Holding, the account and the Security`` () =
        runCommandRouteAndAutoRollback PositionsDeleteHolding (fun context ->
            result {
                let! accountId, securityId = withIds context PF.jordanCustodial PF.totalMarket
                deleteHolding context accountId securityId
                |> expectError
                    (function AsError (PositionsHoldingDoesntExist (a, s)) -> Some(a, s) | _ -> None)
                    (fun found -> Assert.Equal((PF.jordanCustodial, PF.totalMarket), found))
            })
        |> railroadWrapper
