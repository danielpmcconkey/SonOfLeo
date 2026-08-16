namespace Tests.Integrated.InterfaceBridge.IngestionRoutes

open InterfaceBridge.InterfaceContracts.IngestionContracts
open Model.DataIngestion
open Tests.Helpers
open Tests.Helpers.Railroad
open Tests.Helpers.RouteResolver
open Utilities
open Utilities.AppError
open Utilities.Json.Json
open Xunit


[<Collection("SharedTestData")>]
type IngestionRouteTests(fixture: TestDataFixture) =


    // =========================================================================
    // REQ-STG-3.1 — IngestRawFileToStage route happy path
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-3.1 IngestRawFileToStage route ingests valid file and returns result`` () =
        Assert.Fail "not implemented"


    // =========================================================================
    // REQ-STG-1.1 through 1.12 — Format validation at route level
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-1.5 IngestRawFileToStage rejects record with invalid entry_date`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-STG-1.6 IngestRawFileToStage rejects record with negative amount`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-STG-1.7 IngestRawFileToStage rejects record with invalid line_type`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-STG-1.8 IngestRawFileToStage rejects record with empty account_code`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-STG-1.9 IngestRawFileToStage rejects record with description over 1000 chars`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-STG-1.10 IngestRawFileToStage rejects record with fi_source over 100 chars`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-STG-1.11 IngestRawFileToStage rejects record with fi_reference over 100 chars`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-STG-1.12 IngestRawFileToStage rejects record with memo over 1000 chars`` () =
        Assert.Fail "not implemented"


    // =========================================================================
    // UpdateStageEntry route
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-6.1 REQ-STG-6.2 UpdateStageEntry route happy path`` () =
        Assert.Fail "not implemented"


    // =========================================================================
    // PostStageEntries route — shadow
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-8.1 PostStageEntries shadow route returns trial balances and wasRolledBack true`` () =
        Assert.Fail "not implemented"


    // =========================================================================
    // PostStageEntries route — real
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-9.1 PostStageEntries real route posts entries and returns wasRolledBack false`` () =
        Assert.Fail "not implemented"


    // =========================================================================
    // CreateIngestionSource route
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-2.4 CreateIngestionSource route happy path`` () =
        Assert.Fail "not implemented"
