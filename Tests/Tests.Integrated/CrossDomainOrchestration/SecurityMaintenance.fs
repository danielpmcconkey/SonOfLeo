module Tests.Integrated.CrossDomainOrchestration.SecurityMaintenance

open App.DataAccessLayer.DbTransaction
open App.Operation.CoreAuditableAction
open App.Utility.FieldUpdate
open App.Utility.IAppError
open App.Utility.Result
open App.Session
open Business.FinancialServices.Positions
open Business.FinancialServices.Positions.PositionsAuditableAction
open Business.FinancialServices.Positions.PositionsComponent
open Business.FinancialServices.Positions.PositionsError
open Business.CrossDomainOrchestration
open Business.CrossDomainOrchestration.DimensionValueOrchestration
open Business.CrossDomainOrchestration.SecurityOrchestration
open Ui.InterfaceBridge.CommandRoute
open Tests.Helpers
open Tests.Helpers.PositionsValues
open Tests.Helpers.Railroad
open Xunit

/// A Security as the list shows it: name, ticker and each of the seven dimensions' value name, in Dimension.all order.
type private SecuritySummary = string * string option * (Dimension * string option) list

let private summary (view: SecurityView) : SecuritySummary =
    view.security |> Security.securityName |> SecurityName.value,
    view.security |> Security.ticker |> Option.map Ticker.value,
    Dimension.all |> List.map (fun d -> d, view.dimensionValueNames |> Map.tryFind d)

let private withValues (values: (Dimension * string) list) =
    Dimension.all |> List.map (fun d -> d, values |> List.tryFind (fun (vd, _) -> vd = d) |> Option.map snd)

let private listed context name =
    listSecurities context |> Result.map (List.map summary >> List.filter (fun (n, _, _) -> n = name))

let private valueNamesIn context dimension =
    listDimensionValues context dimension |> Result.map (List.map (DimensionValue.dimensionValueName >> DimensionValueName.value))

let private dimensionValueExists = function AsError (PositionsDimensionValueAlreadyExists (d, n)) -> Some(d, n) | _ -> None
let private dimensionValueNotFound = function AsError (PositionsDimensionValueNameDoesntMatch (d, n)) -> Some(d, n) | _ -> None
let private securityNameExists = function AsError (PositionsSecurityNameAlreadyExists n) -> Some n | _ -> None
let private securityNotFound = function AsError (PositionsSecurityNameDoesntMatch n) -> Some n | _ -> None

// the route path: Dimension Values and Securities are addressed by name, resolved to IDs as the routes' converters
// resolve them
let private createDimensionValue context dimension name =
    DimensionValueOrchestration.constructNewAndPersist context dimension name

let private renameDimensionValue context dimension (currentName: DimensionValueName) newName =
    PositionsLookups.dimensionValueIdOf context dimension (currentName |> DimensionValueName.value)
    |> Result.bind (fun dimensionValueId ->
        DimensionValueOrchestration.renameDimensionValue
            context
            { dimensionValueIdToUpdate = dimensionValueId; dimensionValueNameUpdate = SetTo newName })

let private dimensionValueIdsOf context (values: (Dimension * string) list) =
    values
    |> List.map (fun (d, n) -> PositionsLookups.dimensionValueIdOf context d n |> Result.map (fun id -> d, id))
    |> convertListOfResultsToResultsList

let private createSecurity context name ticker (values: (Dimension * string) list) =
    result {
        let! valueIds = dimensionValueIdsOf context values
        return!
            SecurityOrchestration.constructNewAndPersist
                context (toSecurityName name) (ticker |> Option.map toTicker) valueIds
    }

let private updateSecurity
    context
    (currentName: SecurityName)
    nameUpdate
    tickerUpdate
    (dimensionUpdates: (Dimension * DimensionValueName option) list)
    =
    result {
        let! securityId = PositionsLookups.securityIdOf context (currentName |> SecurityName.value)
        let! updates =
            dimensionUpdates
            |> List.map (fun (d, name) ->
                name
                |> convertOptionToDesiredTypeWithFallibleConverter (
                    DimensionValueName.value >> PositionsLookups.dimensionValueIdOf context d
                )
                |> Result.map (fun id -> d, id))
            |> convertListOfResultsToResultsList
        return! SecurityOrchestration.updateSecurity context securityId nameUpdate tickerUpdate updates
    }

let private allSevenOfTotalMarket =
    [ InvestmentType, "Equity Fund"; MarketCap, "Large Cap"; IndexType, "Total Market"; Sector, "Diversified"
      Region, "Domestic"; Objective, "Growth"; Benchmark, "Example Total Market Index" ]

[<Collection("SharedTestData")>]
type SecurityMaintenanceTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-POS-11.1 creating a Dimension Value stores it in its dimension, and listing that dimension returns it`` () =
        runCommandRouteAndAutoRollback PositionsCreateDimensionValue (fun context ->
            result {
                let! created = createDimensionValue context Sector (toDimensionValueName "Technology")
                Assert.Equal(Sector, created |> DimensionValue.dimension)
                let! sectors = valueNamesIn context Sector
                Assert.Contains("Technology", sectors)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-2.3 creating a Dimension Value whose name exactly matches another in the same dimension is rejected with a typed error naming the dimension and the name`` () =
        runCommandRouteAndAutoRollback PositionsCreateDimensionValue (fun context ->
            createDimensionValue context Region (toDimensionValueName "Domestic")
            |> expectError dimensionValueExists (fun found -> Assert.Equal(("Region", "Domestic"), found))
            |> Ok)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-2.3 a Dimension Value may take a name already used in a different dimension`` () =
        runCommandRouteAndAutoRollback PositionsCreateDimensionValue (fun context ->
            result {
                let! _ = createDimensionValue context Objective (toDimensionValueName "International")
                let! objectives = valueNamesIn context Objective
                let! regions = valueNamesIn context Region
                Assert.Contains("International", objectives)
                Assert.Contains("International", regions)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-2.3 a Dimension Value whose name differs from another in its dimension only by letter case is created alongside it`` () =
        runCommandRouteAndAutoRollback PositionsCreateDimensionValue (fun context ->
            result {
                let! _ = createDimensionValue context Region (toDimensionValueName "domestic")
                let! regions = valueNamesIn context Region
                Assert.Contains("domestic", regions)
                Assert.Contains("Domestic", regions)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-11.1 renaming a Dimension Value addressed by dimension and current name changes its name, and a Security referencing it lists the new name in that dimension`` () =
        runCommandRouteAndAutoRollback PositionsRenameDimensionValue (fun context ->
            result {
                let! renamed =
                    renameDimensionValue context MarketCap (toDimensionValueName "Large Cap") (toDimensionValueName "Mega Cap")
                Assert.Equal("Mega Cap", renamed |> DimensionValue.dimensionValueName |> DimensionValueName.value)
                let! securities = listed context PositionsFixture.totalMarket
                let _, _, values = securities |> List.exactlyOne
                Assert.Equal(Some "Mega Cap", values |> List.find (fun (d, _) -> d = MarketCap) |> snd)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-11.1 REQ-POS-2.3 renaming a Dimension Value to a name already used in its dimension is rejected with a typed error naming the dimension and the name`` () =
        runCommandRouteAndAutoRollback PositionsRenameDimensionValue (fun context ->
            renameDimensionValue context Region (toDimensionValueName "Domestic") (toDimensionValueName "International")
            |> expectError dimensionValueExists (fun found -> Assert.Equal(("Region", "International"), found))
            |> Ok)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-11.1 listing one dimension's values returns every fixture value of that dimension ordered by name, and no value of any other dimension`` () =
        let context = Context.create NoTransaction FetchOnly
        result {
            let! investmentTypes = valueNamesIn context InvestmentType
            let! regions = valueNamesIn context Region
            Assert.Equal<string list>([ "Bond Fund"; "Equity Fund" ], investmentTypes)
            Assert.Equal<string list>([ "Domestic"; "International" ], regions)
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-11.9 renaming a Dimension Value by a name that exists only in another dimension fails with a typed not-found error naming Dimension Value and the name`` () =
        runCommandRouteAndAutoRollback PositionsRenameDimensionValue (fun context ->
            renameDimensionValue context Sector (toDimensionValueName "Domestic") (toDimensionValueName "Anything")
            |> expectError dimensionValueNotFound (fun found -> Assert.Equal(("Sector", "Domestic"), found))
            |> Ok)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-11.2 creating a Security with a name, a ticker and a value in each of the seven dimensions stores all of them, and the list returns each one`` () =
        runCommandRouteAndAutoRollback PositionsCreateSecurity (fun context ->
            result {
                let! _ = createSecurity context "Example Growth Fund" (Some "EXGRX") allSevenOfTotalMarket
                let! found = listed context "Example Growth Fund"
                Assert.Equal<SecuritySummary list>(
                    [ "Example Growth Fund", Some "EXGRX", withValues allSevenOfTotalMarket ], found)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-3.3 REQ-POS-3.4 a Security with no ticker and no Dimension Values is created, and the list shows it with no ticker and nothing in each of the seven dimensions`` () =
        runCommandRouteAndAutoRollback PositionsCreateSecurity (fun context ->
            result {
                let! _ = createSecurity context "Example Private Fund" None []
                let! found = listed context "Example Private Fund"
                Assert.Equal<SecuritySummary list>([ "Example Private Fund", None, withValues [] ], found)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-3.2 creating a Security whose name exactly matches an existing Security's is rejected with a typed error naming the name`` () =
        runCommandRouteAndAutoRollback PositionsCreateSecurity (fun context ->
            createSecurity context PositionsFixture.bondFund None []
            |> expectError securityNameExists (fun name -> Assert.Equal(PositionsFixture.bondFund, name))
            |> Ok)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-3.2 a Security whose name differs from an existing one's only by letter case is created alongside it`` () =
        runCommandRouteAndAutoRollback PositionsCreateSecurity (fun context ->
            result {
                let! _ = createSecurity context "EXAMPLE BOND FUND" None []
                let! all = listSecurities context
                let names = all |> List.map (fun v -> v.security |> Security.securityName |> SecurityName.value)
                Assert.Contains("EXAMPLE BOND FUND", names)
                Assert.Contains(PositionsFixture.bondFund, names)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-3.3 creating a Security whose ticker exactly matches another Security's is rejected with a typed error naming the ticker`` () =
        runCommandRouteAndAutoRollback PositionsCreateSecurity (fun context ->
            createSecurity context "Example Copycat Fund" (Some "EXTMX") []
            |> expectError (function AsError (PositionsTickerAlreadyExists t) -> Some t | _ -> None) (fun t -> Assert.Equal("EXTMX", t))
            |> Ok)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-3.3 a Security whose ticker differs from another's only by letter case is created alongside it`` () =
        runCommandRouteAndAutoRollback PositionsCreateSecurity (fun context ->
            result {
                let! _ = createSecurity context "Example Lowercase Fund" (Some "extmx") []
                let! found = listed context "Example Lowercase Fund"
                let _, ticker, _ = found |> List.exactlyOne
                Assert.Equal(Some "extmx", ticker)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-3.4 a Security given two values of the same dimension is rejected with a typed error naming the dimension`` () =
        runCommandRouteAndAutoRollback PositionsCreateSecurity (fun context ->
            createSecurity context "Example Two Region Fund" None [ Region, "Domestic"; Region, "International" ]
            |> expectError (function AsError (PositionsSecurityDimensionGivenTwice d) -> Some d | _ -> None) (fun d -> Assert.Equal("Region", d))
            |> Ok)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-11.9 REQ-POS-11.2 a Security given a Dimension Value name that exists in another dimension but not in the dimension given fails with a typed not-found error naming Dimension Value and the name`` () =
        runCommandRouteAndAutoRollback PositionsCreateSecurity (fun context ->
            createSecurity context "Example Misfiled Fund" None [ Sector, "International" ]
            |> expectError dimensionValueNotFound (fun found -> Assert.Equal(("Sector", "International"), found))
            |> Ok)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-11.2 updating a Security addressed by its current name changes its name and ticker and sets one dimension's value, leaving its values in the other six dimensions unchanged`` () =
        runCommandRouteAndAutoRollback PositionsUpdateSecurity (fun context ->
            result {
                let! _ =
                    updateSecurity
                        context (toSecurityName PositionsFixture.totalMarket)
                        (SetTo(toSecurityName "Example Broad Market Fund")) (SetTo(Some(toTicker "EXBMX")))
                        [ Region, Some(toDimensionValueName "International") ]
                let! found = listed context "Example Broad Market Fund"
                let expectedValues =
                    allSevenOfTotalMarket |> List.map (fun (d, v) -> if d = Region then d, "International" else d, v)
                Assert.Equal<SecuritySummary list>(
                    [ "Example Broad Market Fund", Some "EXBMX", withValues expectedValues ], found)
                let! old = listed context PositionsFixture.totalMarket
                Assert.Empty(old)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-11.2 an update that clears a Security's ticker and one dimension's value leaves it with no ticker and nothing in that dimension, and its other dimensions unchanged`` () =
        runCommandRouteAndAutoRollback PositionsUpdateSecurity (fun context ->
            result {
                let! _ =
                    updateSecurity
                        context (toSecurityName PositionsFixture.international) NoChange (SetTo None) [ Region, None ]
                let! found = listed context PositionsFixture.international
                Assert.Equal<SecuritySummary list>(
                    [ PositionsFixture.international, None, withValues [ InvestmentType, "Equity Fund" ] ], found)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-11.2 REQ-POS-3.2 renaming a Security to another Security's name is rejected with a typed error naming the name, and neither Security changes`` () =
        runCommandRouteAndAutoRollback PositionsUpdateSecurity (fun context ->
            result {
                updateSecurity
                    context (toSecurityName PositionsFixture.bondFund) (SetTo(toSecurityName PositionsFixture.stableValue))
                    NoChange []
                |> expectError securityNameExists (fun name -> Assert.Equal(PositionsFixture.stableValue, name))
                let! bond = listed context PositionsFixture.bondFund
                let! stable = listed context PositionsFixture.stableValue
                Assert.Equal<SecuritySummary list>(
                    [ PositionsFixture.bondFund, None, withValues [ InvestmentType, "Bond Fund" ] ], bond)
                Assert.Equal<SecuritySummary list>([ PositionsFixture.stableValue, None, withValues [] ], stable)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-11.9 updating a Security by a name that matches no Security fails with a typed not-found error naming Security and the name`` () =
        runCommandRouteAndAutoRollback PositionsUpdateSecurity (fun context ->
            updateSecurity context (toSecurityName "Example Missing Fund") NoChange (SetTo None) []
            |> expectError securityNotFound (fun name -> Assert.Equal("Example Missing Fund", name))
            |> Ok)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-11.2 listing Securities returns every fixture Security ordered by name, each with its ticker and the name of its value in each of the seven dimensions or nothing, and no other Security`` () =
        listSecurities (Context.create NoTransaction FetchOnly)
        |> Result.map (fun all ->
            Assert.Equal<SecuritySummary list>(
                [ PositionsFixture.bondFund, None, withValues [ InvestmentType, "Bond Fund" ]
                  PositionsFixture.international, Some "EXINX", withValues [ InvestmentType, "Equity Fund"; Region, "International" ]
                  PositionsFixture.stableValue, None, withValues []
                  PositionsFixture.totalMarket, Some "EXTMX", withValues allSevenOfTotalMarket ],
                all |> List.map summary))
        |> railroadWrapper

    // Placeholders committed before the Src was read (audit 2026-10-04a remediation)

    [<Fact>]
    member _.``REQ-POS-11.1 REQ-SYS-6.1 renaming a Dimension Value to the name it already has succeeds, the value is stored in its dimension under that same name, and its modified-at advances`` () =
        Assert.Fail "Not yet implemented"
