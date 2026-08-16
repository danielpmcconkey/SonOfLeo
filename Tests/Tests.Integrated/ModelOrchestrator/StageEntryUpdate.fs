namespace Tests.Integrated.ModelOrchestrator

open InterfaceBridge.CommandRoute
open Logger.Audit
open Model.DataIngestion
open ModelOrchestrator.StageEntryOrchestration
open Tests.Helpers
open Tests.Helpers.Railroad
open Utilities
open Utilities.AppError
open Utilities.FieldUpdate
open Xunit


[<Collection("SharedTestData")>]
type StageEntryUpdateTests(fixture: TestDataFixture) =


    // =========================================================================
    // REQ-STG-6.1 — Override account_code on a staged line
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-6.1 operator can override account_code on a parser-assigned line`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-STG-6.1 operator can override account_code on a classifier-assigned line`` () =
        Assert.Fail "not implemented"


    // =========================================================================
    // REQ-STG-6.2 — Operator sets fields and status explicitly
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-6.2 updateStageEntry allows operator to set status explicitly`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-STG-6.2 updateStageEntry validates balanced entry after update`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-STG-6.2 updateStageEntry validates account codes exist after update`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-STG-6.2 updateStageEntry validates legal status transition`` () =
        Assert.Fail "not implemented"


    // =========================================================================
    // REQ-STG-6.3 — Override duplicate flag
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-6.3 operator can transition entry from Duplicate to Reviewed`` () =
        Assert.Fail "not implemented"


    // =========================================================================
    // REQ-STG-4.3 — Every status transition creates audit record
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-4.3 manual status transition creates audit record`` () =
        Assert.Fail "not implemented"
