namespace Tests.Integrated.ModelOrchestrator

open InterfaceBridge.CommandRoute
open Logger.Audit
open Model.DataIngestion
open Model.DataIngestion.Classification
open ModelOrchestrator.StageEntryOrchestration
open Tests.Helpers
open Tests.Helpers.Railroad
open Utilities
open Utilities.AppError
open Xunit


[<Collection("SharedTestData")>]
type StageEntryClassificationTests(fixture: TestDataFixture) =


    // =========================================================================
    // REQ-STG-5.1 — Classification runs against Ingested entries
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-5.1 classifyStagedEntries processes entries with status Ingested`` () =
        Assert.Fail "not implemented"


    // =========================================================================
    // REQ-STG-5.3 — Non-null account_code lines not modified
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-5.3 classification does not modify lines with non-null account_code`` () =
        Assert.Fail "not implemented"


    // =========================================================================
    // REQ-STG-5.4 — Single match assigns code and records rule ID
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-5.4 single rule match assigns account code and classification_rule_id to line`` () =
        Assert.Fail "not implemented"


    // =========================================================================
    // REQ-STG-5.5 — Multiple matches with clear priority winner
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-5.5 multiple rule matches with clear priority winner assigns winner`` () =
        Assert.Fail "not implemented"


    // =========================================================================
    // REQ-STG-5.6 — Multiple matches with tied priority sets Conflict
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-5.6 tied priority rule matches set entry status to Conflict`` () =
        Assert.Fail "not implemented"


    // =========================================================================
    // REQ-STG-5.7 — No rule match sets NoMatch
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-5.7 no rule match sets entry status to NoMatch`` () =
        Assert.Fail "not implemented"


    // =========================================================================
    // REQ-STG-5.8 — Fully parser-assigned entries skip classification
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-5.8 entry with all lines parser-assigned transitions to Classified with no MatchCandidates`` () =
        Assert.Fail "not implemented"
