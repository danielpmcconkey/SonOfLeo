module Tests.Integrated.CrossDomainOrchestration.PropertyMortgageBalanceAccounts

open App.Utility.FieldUpdate
open App.Utility.IAppError
open App.Utility.Result
open Business.FinancialServices.Positions
open Business.FinancialServices.Positions.PositionsAuditableAction
open Business.FinancialServices.Positions.PositionsComponent
open Business.FinancialServices.Positions.PositionsError
open Business.CrossDomainOrchestration
open Ui.InterfaceBridge.InterfaceContracts.PositionsContracts
open Ui.InterfaceBridge.BoundaryConverters.PositionsFieldConverters
open Ui.InterfaceBridge.CommandRoute
open Tests.Helpers
open Tests.Helpers.PositionsValues
open Tests.Helpers.Railroad
open Xunit

module PF = PositionsFixture

(* 34 Example Avenue (Rental, Sam) has Fixture Rental Mortgage as its one mortgage Balance Account; Fixture Checking is
   a Cash Balance Account (see PositionsFixture.fs). *)

/// A Debt Balance Account of the test's own, owned by Sam, with no Balance.
let private debtAccount context name =
    BalanceAccountOrchestration.constructNewAndPersist
        context (BalanceAccountName.create name |> mustBe) BalanceAccountKind.Debt (toInstitution "Example Mortgage Lender")
        (PositionsLookups.personIdsOf context [ PF.sam ] |> mustBe)

// the route path: mortgage Balance Accounts are addressed by name and resolved as the route's converter resolves them
let private createPropertyAcquired acquired context name (mortgageNames: string list) =
    result {
        let! sam = PositionsLookups.personIdOf context PF.sam
        let! mortgageIds = mortgageNames |> ``convert [BalanceAccountNameString list] to [BalanceAccountId list]`` context
        return!
            RealEstateOrchestration.constructNewAndPersist
                context (toPropertyName name) Rental [ sam ] (toOwnedPeriod acquired None)
                (toPurchaseBasis 100000.00M) [] [] mortgageIds
    }

let private setMortgages context propertyName (mortgageNames: string list) =
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
          mortgageBalanceAccountNamesUpdate = SetTo mortgageNames }
    input
    |> ``convert [PropertyUpdateInput] to [PropertyFieldUpdates]`` context
    |> Result.bind (RealEstateOrchestration.updateProperty context)

/// The Property's mortgage Balance Accounts as the Property list returns them, or None when no Property has the name.
let private listedMortgagesOf context name =
    RealEstateOrchestration.listProperties context
    |> Result.bind (fun properties ->
        match properties |> List.tryFind (fun p -> p |> Property.propertyName |> PropertyName.value = name) with
        | Some property ->
            property |> ``convert [Property] to [PropertyReturn]`` context |> Result.map (fun r -> Some r.mortgageBalanceAccounts)
        | None -> Ok None)

[<Collection("SharedTestData")>]
type PropertyMortgageBalanceAccountsTests(fixture: TestDataFixture) =

    let createProperty context name mortgageNames =
        createPropertyAcquired fixture.Data.positions.rentalAcquired context name mortgageNames

    [<Fact>]
    member _.``REQ-POS-9.9 REQ-POS-11.6 creating a Property with two mortgage Balance Accounts stores both, and the list returns their names in name order`` () =
        runCommandRouteAndAutoRollback PositionsCreateProperty (fun context ->
            result {
                let! _ = debtAccount context "Cabin Second Mortgage"
                let! _ = debtAccount context "Cabin First Mortgage"
                let! _ = createProperty context "Example Cabin" [ "Cabin Second Mortgage"; "Cabin First Mortgage" ]
                let! mortgages = listedMortgagesOf context "Example Cabin"
                Assert.Equal<string list option>(Some [ "Cabin First Mortgage"; "Cabin Second Mortgage" ], mortgages)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-9.9 a Property given a Cash Balance Account as a mortgage is rejected with a typed error naming the Balance Account and that it is not a Debt, and no Property is created`` () =
        runCommandRouteAndAutoRollback PositionsCreateProperty (fun context ->
            result {
                createProperty context "Example Cabin" [ PF.checking ]
                |> expectError
                    (function AsError (PositionsMortgageBalanceAccountNotDebt (n, kind)) -> Some(n, kind) | _ -> None)
                    (fun found -> Assert.Equal((PF.checking, "Cash"), found))
                let! mortgages = listedMortgagesOf context "Example Cabin"
                Assert.Equal(None, mortgages)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-9.9 REQ-POS-11.9 a Property given a mortgage Balance Account name that matches no Balance Account fails with a typed not-found error naming Balance Account and the name`` () =
        runCommandRouteAndAutoRollback PositionsCreateProperty (fun context ->
            createProperty context "Example Cabin" [ "Missing Example Mortgage" ]
            |> expectError
                (function AsError (PositionsBalanceAccountNameDoesntMatch n) -> Some n | _ -> None)
                (fun n -> Assert.Equal("Missing Example Mortgage", n))
            |> Ok)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-9.9 a Property given the same mortgage Balance Account twice is rejected with a typed error naming it, and no Property is created`` () =
        runCommandRouteAndAutoRollback PositionsCreateProperty (fun context ->
            result {
                let! _ = debtAccount context "Cabin Mortgage"
                createProperty context "Example Cabin" [ "Cabin Mortgage"; "Cabin Mortgage" ]
                |> expectError
                    (function AsError (PositionsPropertyMortgageBalanceAccountRepeated (property, n)) -> Some(property, n) | _ -> None)
                    (fun found -> Assert.Equal(("Example Cabin", "Cabin Mortgage"), found))
                let! mortgages = listedMortgagesOf context "Example Cabin"
                Assert.Equal(None, mortgages)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-9.9 a Balance Account already a mortgage of one Property, given as a mortgage of another, is rejected with a typed error naming the Balance Account and the Property already linked`` () =
        runCommandRouteAndAutoRollback PositionsCreateProperty (fun context ->
            createProperty context "Example Cabin" [ PF.rentalMortgageBalanceAccount ]
            |> expectError
                (function AsError (PositionsMortgageBalanceAccountAlreadyLinked (n, property)) -> Some(n, property) | _ -> None)
                (fun found -> Assert.Equal((PF.rentalMortgageBalanceAccount, PF.rental), found))
            |> Ok)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-9.9 REQ-POS-11.6 an update giving a Property's mortgage Balance Accounts as the one it already has plus a new one leaves it linked to both`` () =
        runCommandRouteAndAutoRollback PositionsUpdateProperty (fun context ->
            result {
                let! _ = debtAccount context "Avenue Second Mortgage"
                let! _ = setMortgages context PF.rental [ PF.rentalMortgageBalanceAccount; "Avenue Second Mortgage" ]
                let! mortgages = listedMortgagesOf context PF.rental
                Assert.Equal<string list option>(Some [ "Avenue Second Mortgage"; PF.rentalMortgageBalanceAccount ], mortgages)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-11.6 an update giving a Property's mortgage Balance Accounts replaces the set whole: one left out is no longer its mortgage and a new one is`` () =
        runCommandRouteAndAutoRollback PositionsUpdateProperty (fun context ->
            result {
                let! _ = debtAccount context "Avenue Refinanced Mortgage"
                let! _ = setMortgages context PF.rental [ "Avenue Refinanced Mortgage" ]
                let! mortgages = listedMortgagesOf context PF.rental
                let! formerId = PositionsLookups.balanceAccountIdOf context PF.rentalMortgageBalanceAccount
                let! former = formerId |> Property.fetchByMortgageBalanceAccountId context
                Assert.Equal<string list option>(Some [ "Avenue Refinanced Mortgage" ], mortgages)
                Assert.Equal(None, former |> Option.map (Property.propertyName >> PropertyName.value))
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-11.11 deleting a Property with mortgage Balance Accounts and no Valuation removes its links with it, after which each former mortgage Balance Account can be named as another Property's mortgage`` () =
        runCommandRouteAndAutoRollback PositionsDeleteProperty (fun context ->
            result {
                let! _ = debtAccount context "Cabin First Mortgage"
                let! _ = debtAccount context "Cabin Second Mortgage"
                let! cabin = createProperty context "Example Cabin" [ "Cabin First Mortgage"; "Cabin Second Mortgage" ]
                let! _ = cabin |> Property.propertyId |> RealEstateOrchestration.deleteProperty context
                let! _ = createProperty context "Example Chalet" [ "Cabin First Mortgage"; "Cabin Second Mortgage" ]
                let! mortgages = listedMortgagesOf context "Example Chalet"
                Assert.Equal<string list option>(Some [ "Cabin First Mortgage"; "Cabin Second Mortgage" ], mortgages)
            })
        |> railroadWrapper
