module Tests.Integrated.CrossDomainOrchestration.PropertyMaintenance

open NodaTime
open App.DataAccessLayer.DbTransaction
open App.Operation.CoreAuditableAction
open App.Utility
open App.Utility.FieldUpdate
open App.Utility.IAppError
open App.Utility.Result
open App.Session
open Business.General.BizGeneralError
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Positions
open Business.FinancialServices.Positions.PositionsAuditableAction
open Business.FinancialServices.Positions.PositionsComponent
open Business.FinancialServices.Positions.PositionsError
open Business.CrossDomainOrchestration.RealEstateOrchestration
open Ui.InterfaceBridge.CommandRoute
open Tests.Helpers
open Tests.Helpers.EntityFunctions
open Tests.Helpers.PositionsValues
open Tests.Helpers.Railroad
open Xunit

module PF = PositionsFixture

/// A Property as the list shows it: name, use, owners' names, acquisition date, disposal date, purchase basis, asset
/// account and mortgage accounts.
type private PropertySummary =
    string * PropertyUse * string list * LocalDate * LocalDate option * decimal * (string * string) list * (string * string) list

let private summary (view: PropertyView) : PropertySummary =
    let p = view.property
    p |> Property.propertyName |> PropertyName.value,
    p |> Property.propertyUse,
    view.ownerNames,
    p |> Property.ownedPeriod |> OwnedPeriod.acquisitionDate,
    p |> Property.ownedPeriod |> OwnedPeriod.disposalDate,
    p |> Property.purchaseBasis |> PurchaseBasis.value |> Money.amount,
    view.assetAccountCodesAndNames,
    view.mortgageAccountCodesAndNames

let private listed context name =
    listProperties context |> Result.map (List.map summary >> List.filter (fun (n, _, _, _, _, _, _, _) -> n = name))

let private listedOne context name = listed context name |> Result.map List.exactlyOne

let private newProperty name propertyUse (owners: string list) acquired disposed basis asset mortgages =
    { name = toPropertyName name
      propertyUse = propertyUse
      owners = owners |> List.map toPersonName
      ownedPeriod = toOwnedPeriod acquired disposed
      purchaseBasis = toPurchaseBasis basis
      assetAccountIds = asset |> Option.toList
      mortgageAccountIds = mortgages }

let private noChange name =
    { currentName = toPropertyName name
      nameUpdate = NoChange
      propertyUseUpdate = NoChange
      ownersUpdate = NoChange
      acquisitionDateUpdate = NoChange
      disposalDateUpdate = NoChange
      purchaseBasisUpdate = NoChange
      assetAccountIdsUpdate = NoChange
      mortgageAccountIdsUpdate = NoChange }

/// A FixedAsset account of the test's own, unlinked, inside the test's transaction.
let private fixedAssetAccount context (fixture: TestDataFixture) code name =
    createTestAccountFromPrimitives
        context code name "Asset" fixture.Data.positions.accountsActiveBegin None (Some "FixedAsset") (Some fixture.Data.assets1000Id) None
    |> Result.map snd

let private valuationsOf context name =
    listValuations context (toPropertyName name)
    |> Result.map (
        List.map (fun v ->
            v |> Valuation.valuationDate,
            v |> Valuation.valuationValue |> ValuationValue.value |> Money.amount,
            v |> Valuation.valuationBasis |> ValuationBasis.value)
    )

let private record context name date value basis =
    recordValuation context (toPropertyName name) date (toValuationValue value) (toValuationBasis basis)

let private today (context: Context.Context) = context |> Context.getInitiationInstant |> Calendar.dateFromInstant

let private residencesOverlap = function AsError (PositionsPrimaryResidencesOverlap (a, b)) -> Some(a, b) | _ -> None
let private outsideOwnership = function AsError (PositionsValuationDateOutsideOwnership (n, d)) -> Some(n, d) | _ -> None

[<Collection("SharedTestData")>]
type PropertyMaintenanceTests(fixture: TestDataFixture) =

    let p = fixture.Data.positions
    let accountIdOf code =
        fixture.Data.accounts |> List.find (fun a -> a |> Account.code |> AccountCode.value = code) |> Account.accountId

    [<Fact>]
    member _.``REQ-POS-11.6 creating a Property stores its name, use, owners, acquisition date, disposal date, purchase basis, asset account and mortgage accounts, and the list returns each of them with every linked account's code and name`` () =
        runCommandRouteAndAutoRollback PositionsCreateProperty (fun context ->
            result {
                let! cabinAccountId = fixedAssetAccount context fixture "T-1590" "Test Cabin at Cost"
                let acquired = p.rentalAcquired.PlusDays(3)
                let disposed = acquired.PlusYears(2)
                let! _ =
                    createProperty
                        context
                        (newProperty "56 Example Lane" Rental [ PF.sam; PF.alex ] acquired (Some disposed) 175000.00M
                            (Some cabinAccountId) [ accountIdOf "F-2230" ])
                let! found = listedOne context "56 Example Lane"
                Assert.Equal<PropertySummary>(
                    ("56 Example Lane", Rental, [ PF.alex; PF.sam ], acquired, Some disposed, 175000.00M,
                     [ "T-1590", "Test Cabin at Cost" ], [ "F-2230", "Fixture Loan Payable" ]),
                    found)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-9.1 creating a Property whose name exactly matches an existing Property's is rejected with a typed error naming the name`` () =
        runCommandRouteAndAutoRollback PositionsCreateProperty (fun context ->
            createProperty context (newProperty PF.rental Rental [ PF.sam ] p.rentalAcquired None 1000.00M None [])
            |> expectError
                (function AsError (PositionsPropertyNameAlreadyExists n) -> Some n | _ -> None)
                (fun n -> Assert.Equal(PF.rental, n))
            |> Ok)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-9.1 a Property whose name differs from an existing one's only by letter case is created alongside it`` () =
        runCommandRouteAndAutoRollback PositionsCreateProperty (fun context ->
            result {
                let! _ = createProperty context (newProperty "34 EXAMPLE AVENUE" Rental [ PF.sam ] p.rentalAcquired None 1000.00M None [])
                let! upper = listed context "34 EXAMPLE AVENUE"
                let! original = listed context PF.rental
                Assert.Equal((1, 1), (upper.Length, original.Length))
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-9.3 a Property with three owners is created, and the list shows all three`` () =
        runCommandRouteAndAutoRollback PositionsCreateProperty (fun context ->
            result {
                let! _ =
                    createProperty context (newProperty "Family Cabin" Rental [ PF.sam; PF.jordan; PF.alex ] p.rentalAcquired None 90000.00M None [])
                let! (_, _, owners, _, _, _, _, _) = listedOne context "Family Cabin"
                Assert.Equal<string list>([ PF.alex; PF.jordan; PF.sam ], owners)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-9.3 a Property given no owners is rejected with a typed error naming the Property`` () =
        runCommandRouteAndAutoRollback PositionsCreateProperty (fun context ->
            createProperty context (newProperty "Nobody's Cabin" Rental [] p.rentalAcquired None 90000.00M None [])
            |> expectError
                (function AsError (PositionsPropertyHasNoOwners n) -> Some n | _ -> None)
                (fun n -> Assert.Equal("Nobody's Cabin", n))
            |> Ok)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-9.3 a Property given the same Person twice among its owners is rejected with a typed error naming the Person`` () =
        runCommandRouteAndAutoRollback PositionsCreateProperty (fun context ->
            createProperty context (newProperty "Twice Cabin" Rental [ PF.sam; PF.sam ] p.rentalAcquired None 90000.00M None [])
            |> expectError
                (function AsError (PositionsPropertyOwnerRepeated (n, person)) -> Some(n, person) | _ -> None)
                (fun found -> Assert.Equal(("Twice Cabin", PF.sam), found))
            |> Ok)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-9.3 REQ-PER-2.4 a Property with an owner name that matches no Person fails with a typed error naming that name`` () =
        runCommandRouteAndAutoRollback PositionsCreateProperty (fun context ->
            createProperty context (newProperty "Ghost Cabin" Rental [ PF.sam; "Ghost Example" ] p.rentalAcquired None 90000.00M None [])
            |> expectError
                (function AsError (PersonNameDoesntMatchId n) -> Some n | _ -> None)
                (fun n -> Assert.Equal("Ghost Example", n))
            |> Ok)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-9.6 a primary residence acquired on the date another primary residence is disposed of is created`` () =
        runCommandRouteAndAutoRollback PositionsCreateProperty (fun context ->
            result {
                // between 7 Former Example Road's disposal and 12 Example Street's acquisition, touching both
                let! _ =
                    createProperty
                        context
                        (newProperty "Interim Flat" PrimaryResidence [ PF.alex ] p.formerResidenceDisposed (Some p.residenceAcquired) 150000.00M None [])
                let! found = listed context "Interim Flat"
                Assert.Equal(1, found.Length)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-9.6 a primary residence acquired the day before another primary residence's disposal date is rejected with a typed error naming both Properties`` () =
        runCommandRouteAndAutoRollback PositionsCreateProperty (fun context ->
            createProperty
                context
                (newProperty "Early Flat" PrimaryResidence [ PF.alex ] (p.formerResidenceDisposed.PlusDays(-1)) (Some(p.formerResidenceDisposed.PlusDays(10))) 150000.00M None [])
            |> expectError residencesOverlap (fun found -> Assert.Equal(("Early Flat", PF.formerResidence), found))
            |> Ok)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-9.6 a primary residence acquired while another with no disposal date is owned is rejected with a typed error naming both Properties`` () =
        runCommandRouteAndAutoRollback PositionsCreateProperty (fun context ->
            createProperty context (newProperty "Second Home" PrimaryResidence [ PF.sam ] p.d2 None 150000.00M None [])
            |> expectError residencesOverlap (fun found -> Assert.Equal(("Second Home", PF.residence), found))
            |> Ok)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-9.6 a Rental owned on the same dates as a primary residence is created`` () =
        runCommandRouteAndAutoRollback PositionsCreateProperty (fun context ->
            result {
                let! _ = createProperty context (newProperty "Same Dates Rental" Rental [ PF.sam ] p.residenceAcquired None 150000.00M None [])
                let! (_, use', _, acquired, disposed, _, _, _) = listedOne context "Same Dates Rental"
                Assert.Equal((Rental, p.residenceAcquired, None), (use', acquired, disposed))
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-9.6 an update moving a primary residence's disposal date past another primary residence's acquisition date is rejected with a typed error naming both Properties`` () =
        runCommandRouteAndAutoRollback PositionsUpdateProperty (fun context ->
            updateProperty context { noChange PF.formerResidence with disposalDateUpdate = SetTo(Some(p.residenceAcquired.PlusDays(1))) }
            |> expectError residencesOverlap (fun found -> Assert.Equal((PF.formerResidence, PF.residence), found))
            |> Ok)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-9.6 an update changing a Rental's use to PrimaryResidence while another primary residence is owned on an overlapping date is rejected with a typed error naming both Properties`` () =
        runCommandRouteAndAutoRollback PositionsUpdateProperty (fun context ->
            updateProperty context { noChange PF.rental with propertyUseUpdate = SetTo PrimaryResidence }
            |> expectError residencesOverlap (fun found -> Assert.Equal((PF.rental, PF.residence), found))
            |> Ok)
        |> railroadWrapper

    [<Theory>]
    [<InlineData("F-1270", "Asset", "Cash")>]
    [<InlineData("F-2220", "Liability", "CurrentLiability")>]
    member _.``REQ-POS-9.7 for each of an Asset account of a subtype other than FixedAsset and an account of a type other than Asset, linking it as a Property's asset account fails with a typed error naming the code and what is wrong`` (code: string, accountType: string, subtype: string) =
        runCommandRouteAndAutoRollback PositionsCreateProperty (fun context ->
            createProperty context (newProperty "Wrongly Linked Cabin" Rental [ PF.sam ] p.rentalAcquired None 1000.00M (Some(accountIdOf code)) [])
            |> expectError
                (function AsError (PositionsPropertyLedgerAccountNotAssetFixedAsset (c, t, s)) -> Some(c, t, s) | _ -> None)
                (fun found -> Assert.Equal((code, accountType, Some subtype), found))
            |> Ok)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-9.7 REQ-POS-4.9 linking a Property's asset account to a ledger account already another Property's asset account is rejected with a typed error naming the code and that other Property`` () =
        runCommandRouteAndAutoRollback PositionsUpdateProperty (fun context ->
            updateProperty context { noChange PF.rental with assetAccountIdsUpdate = SetTo [ p.residenceAtCost1510Id ] }
            |> expectError
                (function AsError (PositionsAssetAccountAlreadyLinked (c, n)) -> Some(c, n) | _ -> None)
                (fun found -> Assert.Equal(("F-1510", PF.residence), found))
            |> Ok)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-9.8 a mortgage account of a type other than Liability fails with a typed error naming the code`` () =
        runCommandRouteAndAutoRollback PositionsCreateProperty (fun context ->
            createProperty context (newProperty "Odd Mortgage Cabin" Rental [ PF.sam ] p.rentalAcquired None 1000.00M None [ accountIdOf "F-1270" ])
            |> expectError
                (function AsError (PositionsMortgageAccountNotLiability (c, t)) -> Some(c, t) | _ -> None)
                (fun found -> Assert.Equal(("F-1270", "Asset"), found))
            |> Ok)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-9.8 a ledger account already a mortgage account of one Property, given as a mortgage account of another, is rejected with a typed error naming the code and the Property already linked`` () =
        runCommandRouteAndAutoRollback PositionsUpdateProperty (fun context ->
            updateProperty context { noChange PF.rental with mortgageAccountIdsUpdate = SetTo [ p.residenceMortgage2310Id ] }
            |> expectError
                (function AsError (PositionsMortgageAccountAlreadyLinked (c, n)) -> Some(c, n) | _ -> None)
                (fun found -> Assert.Equal(("F-2310", PF.residence), found))
            |> Ok)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-9.8 a Property with two mortgage accounts is created, and the list shows both with their codes and names`` () =
        runCommandRouteAndAutoRollback PositionsCreateProperty (fun context ->
            result {
                let! _ =
                    createProperty
                        context
                        (newProperty "Twice Mortgaged" Rental [ PF.sam ] p.rentalAcquired None 1000.00M None [ accountIdOf "F-2230"; accountIdOf "F-2210" ])
                let! (_, _, _, _, _, _, _, mortgages) = listedOne context "Twice Mortgaged"
                Assert.Equal<(string * string) list>([ "F-2210", "Mortgage Payable"; "F-2230", "Fixture Loan Payable" ], mortgages)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-11.6 updating a Property addressed by its current name changes its name, use, acquisition date, disposal date, purchase basis and asset account, and the list shows every new value`` () =
        runCommandRouteAndAutoRollback PositionsUpdateProperty (fun context ->
            result {
                let! assetId = fixedAssetAccount context fixture "T-1591" "Test Townhouse at Cost"
                // a primary residence between the former residence's disposal and the residence's acquisition
                let acquired = p.formerResidenceDisposed.PlusDays(1)
                let! _ =
                    updateProperty
                        context
                        { noChange PF.rental with
                            nameUpdate = SetTo(toPropertyName "56 Example Townhouse")
                            propertyUseUpdate = SetTo PrimaryResidence
                            acquisitionDateUpdate = SetTo acquired
                            disposalDateUpdate = SetTo(Some p.residenceAcquired)
                            purchaseBasisUpdate = SetTo(toPurchaseBasis 260000.00M)
                            assetAccountIdsUpdate = SetTo [ assetId ] }
                let! found = listedOne context "56 Example Townhouse"
                Assert.Equal<PropertySummary>(
                    ("56 Example Townhouse", PrimaryResidence, [ PF.sam ], acquired, Some p.residenceAcquired, 260000.00M,
                     [ "T-1591", "Test Townhouse at Cost" ], [ "F-2320", "Fixture Rental Mortgage" ]),
                    found)
                let! old = listed context PF.rental
                Assert.Empty(old)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-11.6 an update giving owners and mortgage accounts replaces each set whole, so each set afterwards is exactly the one given: a member left out is gone and a new member is present`` () =
        runCommandRouteAndAutoRollback PositionsUpdateProperty (fun context ->
            result {
                let! _ =
                    updateProperty
                        context
                        { noChange PF.residence with
                            ownersUpdate = SetTo [ toPersonName PF.sam; toPersonName PF.jordan ]
                            mortgageAccountIdsUpdate = SetTo [ accountIdOf "F-2210" ] }
                let! (_, _, owners, _, _, _, _, mortgages) = listedOne context PF.residence
                Assert.Equal<string list>([ PF.jordan; PF.sam ], owners)
                Assert.Equal<(string * string) list>([ "F-2210", "Mortgage Payable" ], mortgages)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-11.6 an update that clears a Property's disposal date and asset account leaves it with neither`` () =
        runCommandRouteAndAutoRollback PositionsUpdateProperty (fun context ->
            result {
                let! assetId = fixedAssetAccount context fixture "T-1592" "Test Shed at Cost"
                let! _ =
                    createProperty
                        context
                        (newProperty "Sold Shed" Rental [ PF.sam ] p.rentalAcquired (Some(p.rentalAcquired.PlusMonths(2))) 5000.00M (Some assetId) [])
                let! _ = updateProperty context { noChange "Sold Shed" with disposalDateUpdate = SetTo None; assetAccountIdsUpdate = SetTo [] }
                let! (_, _, _, _, disposed, _, asset, _) = listedOne context "Sold Shed"
                Assert.Equal(None, disposed)
                Assert.Empty(asset)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-11.7 moving a Property's acquisition date after its first Valuation and its disposal date before its last is rejected with a typed error naming the earliest and latest offending valuation dates`` () =
        runCommandRouteAndAutoRollback PositionsUpdateProperty (fun context ->
            updateProperty
                context
                { noChange PF.residence with
                    acquisitionDateUpdate = SetTo(p.valuation1.PlusDays(1))
                    disposalDateUpdate = SetTo(Some(p.valuation2.PlusDays(-1))) }
            |> expectError
                (function AsError (PositionsOwnershipExcludesValuations (n, earliest, latest)) -> Some(n, earliest, latest) | _ -> None)
                (fun found -> Assert.Equal((PF.residence, p.valuation1, p.valuation2), found))
            |> Ok)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-11.7 moving a Property's acquisition date to exactly its first Valuation's date and its disposal date to exactly its last Valuation's date succeeds`` () =
        runCommandRouteAndAutoRollback PositionsUpdateProperty (fun context ->
            result {
                let! _ =
                    updateProperty
                        context { noChange PF.residence with acquisitionDateUpdate = SetTo p.valuation1; disposalDateUpdate = SetTo(Some p.valuation2) }
                let! (_, _, _, acquired, disposed, _, _, _) = listedOne context PF.residence
                Assert.Equal((p.valuation1, Some p.valuation2), (acquired, disposed))
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-11.9 updating a Property by a name that matches no Property fails with a typed not-found error naming Property and the name`` () =
        runCommandRouteAndAutoRollback PositionsUpdateProperty (fun context ->
            updateProperty context { noChange "99 Missing Street" with purchaseBasisUpdate = SetTo(toPurchaseBasis 1.00M) }
            |> expectError
                (function AsError (PositionsPropertyNameDoesntMatch n) -> Some n | _ -> None)
                (fun n -> Assert.Equal("99 Missing Street", n))
            |> Ok)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-11.6 listing Properties returns every fixture Property ordered by name, with its owners' names and its asset and mortgage accounts' codes and names, and no other Property`` () =
        listProperties (Context.create NoTransaction FetchOnly)
        |> Result.map (fun all ->
            Assert.Equal<PropertySummary list>(
                [ PF.residence, PrimaryResidence, [ PF.alex; PF.sam ], p.residenceAcquired, None, 400000.00M,
                  [ "F-1510", "Fixture Residence at Cost" ], [ "F-2310", "Fixture Residence Mortgage" ]
                  PF.rental, Rental, [ PF.sam ], p.rentalAcquired, None, 250000.00M, [], [ "F-2320", "Fixture Rental Mortgage" ]
                  PF.formerResidence, PrimaryResidence, [ PF.alex ], p.formerResidenceAcquired, Some p.formerResidenceDisposed,
                  200000.00M, [], [] ],
                all |> List.map summary))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-11.8 REQ-POS-10.1 recording a Valuation stores its date, value and basis, and listing the Property's Valuations returns it`` () =
        runCommandRouteAndAutoRollback PositionsRecordValuation (fun context ->
            result {
                let! recorded = record context PF.rental p.d2 260000.00M "Broker opinion"
                Assert.Equal(p.d2, recorded |> Valuation.valuationDate)
                let! valuations = valuationsOf context PF.rental
                Assert.Equal<(LocalDate * decimal * string) list>([ p.d2, 260000.00M, "Broker opinion" ], valuations)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-11.8 recording a Valuation for a Property and date that already has one replaces its value and basis, leaving exactly one Valuation on that date`` () =
        runCommandRouteAndAutoRollback PositionsRecordValuation (fun context ->
            result {
                let! _ = record context PF.residence p.valuation1 425000.00M "Revised appraisal"
                let! valuations = valuationsOf context PF.residence
                Assert.Equal<(LocalDate * decimal * string) list>(
                    [ p.valuation1, 425000.00M, "Revised appraisal"; p.valuation2, 430000.00M, "Comparable sales" ], valuations)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-10.3 a Valuation dated the day before the Property's acquisition date is rejected with a typed error naming the Property and the date, and one dated on the acquisition date is accepted`` () =
        runCommandRouteAndAutoRollback PositionsRecordValuation (fun context ->
            result {
                record context PF.residence (p.residenceAcquired.PlusDays(-1)) 400000.00M "Purchase"
                |> expectError outsideOwnership (fun found -> Assert.Equal((PF.residence, p.residenceAcquired.PlusDays(-1)), found))
                let! accepted = record context PF.residence p.residenceAcquired 400000.00M "Purchase"
                Assert.Equal(p.residenceAcquired, accepted |> Valuation.valuationDate)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-10.3 a Valuation dated on the Property's disposal date is accepted, and one dated the day after is rejected with a typed error naming the Property and the date`` () =
        runCommandRouteAndAutoRollback PositionsRecordValuation (fun context ->
            result {
                let! accepted = record context PF.formerResidence p.formerResidenceDisposed 240000.00M "Sale price"
                Assert.Equal(p.formerResidenceDisposed, accepted |> Valuation.valuationDate)
                record context PF.formerResidence (p.formerResidenceDisposed.PlusDays(1)) 240000.00M "Sale price"
                |> expectError outsideOwnership (fun found -> Assert.Equal((PF.formerResidence, p.formerResidenceDisposed.PlusDays(1)), found))
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-10.3 REQ-SYS-3.4 a Valuation dated on the calendar date of the operation's initiation instant is accepted, and one dated the day after is rejected with a typed error naming the Property and the date`` () =
        runCommandRouteAndAutoRollback PositionsRecordValuation (fun context ->
            result {
                let currentDate = today context
                let! accepted = record context PF.residence currentDate 435000.00M "Online estimate"
                Assert.Equal(currentDate, accepted |> Valuation.valuationDate)
                record context PF.residence (currentDate.PlusDays(1)) 435000.00M "Online estimate"
                |> expectError
                    (function AsError (PositionsValuationDateLaterThanCurrentDate (n, d)) -> Some(n, d) | _ -> None)
                    (fun found -> Assert.Equal((PF.residence, currentDate.PlusDays(1)), found))
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-11.8 deleting a Property's Valuation for a date removes it and leaves the Property's other Valuations`` () =
        runCommandRouteAndAutoRollback PositionsDeleteValuation (fun context ->
            result {
                let! _ = deleteValuation context (toPropertyName PF.residence) p.valuation1
                let! valuations = valuationsOf context PF.residence
                Assert.Equal<(LocalDate * decimal * string) list>([ p.valuation2, 430000.00M, "Comparable sales" ], valuations)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-11.8 REQ-SYS-6.1 deleting a Valuation for a date on which the Property has none fails with a typed error naming the Property and the date`` () =
        runCommandRouteAndAutoRollback PositionsDeleteValuation (fun context ->
            deleteValuation context (toPropertyName PF.residence) (p.valuation1.PlusDays(1))
            |> expectError
                (function AsError (PositionsValuationDoesntExist (n, d)) -> Some(n, d) | _ -> None)
                (fun found -> Assert.Equal((PF.residence, p.valuation1.PlusDays(1)), found))
            |> Ok)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-11.8 listing a Property's Valuations returns every fixture Valuation of that Property in date order and none of another Property`` () =
        let context = Context.create NoTransaction FetchOnly
        result {
            let! residence = valuationsOf context PF.residence
            let! rental = valuationsOf context PF.rental
            Assert.Equal<(LocalDate * decimal * string) list>(
                [ p.valuation1, 420000.00M, "Appraisal"; p.valuation2, 430000.00M, "Comparable sales" ], residence)
            Assert.Empty(rental)
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-11.9 recording a Valuation for a Property name that matches no Property fails with a typed not-found error naming Property and the name`` () =
        runCommandRouteAndAutoRollback PositionsRecordValuation (fun context ->
            record context "99 Missing Street" p.d2 1000.00M "Guess"
            |> expectError
                (function AsError (PositionsPropertyNameDoesntMatch n) -> Some n | _ -> None)
                (fun n -> Assert.Equal("99 Missing Street", n))
            |> Ok)
        |> railroadWrapper

    // Placeholders committed before the Src was read (audit 2026-10-04a remediation)

    [<Fact>]
    member _.``REQ-POS-11.11 deleting a Property with no Valuation removes it with its owners and ledger links, after which its former asset and mortgage accounts can each be linked to another Property`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.11 deleting a Property that has a Valuation is rejected with a typed error naming the Property, and the Property, its owners and its ledger links remain`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.11 REQ-SYS-6.2 deleting a Property by a name that matches no Property fails with a typed not-found error naming Property and the name`` () =
        Assert.Fail "Not yet implemented"
