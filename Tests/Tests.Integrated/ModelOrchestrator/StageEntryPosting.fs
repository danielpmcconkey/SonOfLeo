namespace Tests.Integrated.ModelOrchestrator

open InterfaceBridge.CommandRoute
open InterfaceBridge.Routes.IngestionRoutes
open Logger.Audit
open Model.DataIngestion
open ModelOrchestrator.StageEntryOrchestration
open Tests.Helpers
open Tests.Helpers.Railroad
open Utilities
open Utilities.AppError
open Xunit


[<Collection("SharedTestData")>]
type StageEntryPostingTests(fixture: TestDataFixture) =


    // =========================================================================
    // REQ-STG-8.1 REQ-STG-8.4 — Shadow post does not modify ledger or staging
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-8.1 REQ-STG-8.4 shadow post does not create journal entries or change staging statuses`` () =
        Assert.Fail "not implemented"


    // =========================================================================
    // REQ-STG-8.2 — Shadow post uses real domain validation
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-8.2 shadow post fails when staged entry is in closed fiscal period`` () =
        Assert.Fail "not implemented"


    // =========================================================================
    // REQ-STG-8.3 — Shadow post returns before and after trial balance
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-8.3 shadow post returns trial balance before and after`` () =
        Assert.Fail "not implemented"


    // =========================================================================
    // REQ-STG-9.1 REQ-STG-9.2 — Batch post happy path
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-9.1 REQ-STG-9.2 batch post creates journal entries through domain model`` () =
        Assert.Fail "not implemented"


    // =========================================================================
    // REQ-STG-9.3 — JE header fields mapped from staged entry
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-9.3 posted JE has description and entry_date from staged entry`` () =
        Assert.Fail "not implemented"


    // =========================================================================
    // REQ-STG-9.4 — Account code to ID resolution at posting time
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-9.4 batch post resolves account codes to IDs`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-STG-9.4 batch post fails when account code does not resolve`` () =
        Assert.Fail "not implemented"


    // =========================================================================
    // REQ-STG-9.5 — External reference constructed from source + fi_reference
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-9.5 posted JE has external reference with source name and fi_reference`` () =
        Assert.Fail "not implemented"


    // =========================================================================
    // REQ-STG-9.7 — Status set to Posted with audit record
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-9.7 batch post sets status to Posted and creates LedgerPoster audit record`` () =
        Assert.Fail "not implemented"


    // =========================================================================
    // REQ-STG-9.8 — All-or-nothing batch post
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-9.8 batch post rolls back all entries when one fails domain validation`` () =
        Assert.Fail "not implemented"


    // =========================================================================
    // REQ-STG-4.4 — fetchAllForPosting returns only postable entries
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-4.4 fetchAllForPosting returns only Classified and Reviewed entries with all lines coded`` () =
        Assert.Fail "not implemented"
