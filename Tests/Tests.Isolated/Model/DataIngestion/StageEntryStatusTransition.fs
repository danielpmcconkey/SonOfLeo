module Tests.Isolated.Model.DataIngestion.StageEntryStatusTransition

open Model.DataIngestion
open Model.DataIngestion.StageEntryStatusTransition
open Utilities.AppError
open Xunit


// =============================================================================
// REQ-STG-4.1 — StagedEntryStatus.fromString for all valid values
// =============================================================================

[<Fact>]
let ``REQ-STG-4.1 StagedEntryStatus.fromString accepts Ingested`` () =
    Assert.Equal(Ok Ingested, StagedEntryStatus.fromString "Ingested")

[<Fact>]
let ``REQ-STG-4.1 StagedEntryStatus.fromString accepts Classified`` () =
    Assert.Equal(Ok Classified, StagedEntryStatus.fromString "Classified")

[<Fact>]
let ``REQ-STG-4.1 StagedEntryStatus.fromString accepts NoMatch`` () =
    Assert.Equal(Ok NoMatch, StagedEntryStatus.fromString "NoMatch")

[<Fact>]
let ``REQ-STG-4.1 StagedEntryStatus.fromString accepts Conflict`` () =
    Assert.Equal(Ok Conflict, StagedEntryStatus.fromString "Conflict")

[<Fact>]
let ``REQ-STG-4.1 StagedEntryStatus.fromString accepts Reviewed`` () =
    Assert.Equal(Ok Reviewed, StagedEntryStatus.fromString "Reviewed")

[<Fact>]
let ``REQ-STG-4.1 StagedEntryStatus.fromString accepts Duplicate`` () =
    Assert.Equal(Ok Duplicate, StagedEntryStatus.fromString "Duplicate")

[<Fact>]
let ``REQ-STG-4.1 StagedEntryStatus.fromString accepts Posted`` () =
    Assert.Equal(Ok Posted, StagedEntryStatus.fromString "Posted")

[<Fact>]
let ``REQ-STG-4.1 StagedEntryStatus.fromString accepts Ignored`` () =
    Assert.Equal(Ok Ignored, StagedEntryStatus.fromString "Ignored")

[<Fact>]
let ``REQ-STG-4.1 StagedEntryStatus.fromString rejects invalid string`` () =
    match StagedEntryStatus.fromString "Bogus" with
    | Error (IngestionInvalidStagedEntryStatus _) -> ()
    | Error e -> Assert.Fail $"Wrong error: {AppError.toMessage e}"
    | Ok _ -> Assert.Fail "Expected failure; got success"


// =============================================================================
// REQ-STG-4.1 — StageStatusChangeMechanism.fromString
// =============================================================================

[<Fact>]
let ``REQ-STG-4.1 StageStatusChangeMechanism.fromString accepts all valid values`` () =
    let expected = [ StageIngestion; Classifier; Deduplicator; Operator; LedgerPoster ]
    let inputs = [ "StageIngestion"; "Classifier"; "Deduplicator"; "Operator"; "LedgerPoster" ]
    let results = inputs |> List.map StageStatusChangeMechanism.fromString
    let allOk = results |> List.forall Result.isOk
    Assert.True(allOk, "All valid mechanism strings should parse successfully")
    let values = results |> List.map (fun r -> match r with Ok v -> v | Error _ -> failwith "impossible")
    Assert.Equal<StageStatusChangeMechanism list>(expected, values)

[<Fact>]
let ``REQ-STG-4.1 StageStatusChangeMechanism.fromString rejects invalid string`` () =
    match StageStatusChangeMechanism.fromString "Bogus" with
    | Error (IngestionInvalidStageStatusChangeMechanism _) -> ()
    | Error e -> Assert.Fail $"Wrong error: {AppError.toMessage e}"
    | Ok _ -> Assert.Fail "Expected failure; got success"


// =============================================================================
// REQ-STG-4.2 — Posted is terminal
// =============================================================================

[<Fact>]
let ``REQ-STG-4.2 validTransitions from Posted returns empty list`` () =
    let transitions = validTransitions (Some Posted)
    Assert.Empty(transitions)


// =============================================================================
// REQ-STG-4.6 — validTransitions covers the full state machine
// =============================================================================

[<Theory>]
[<InlineData("None", "Ingested", true)>]
[<InlineData("None", "Classified", false)>]
[<InlineData("None", "NoMatch", false)>]
[<InlineData("None", "Conflict", false)>]
[<InlineData("None", "Reviewed", false)>]
[<InlineData("None", "Duplicate", false)>]
[<InlineData("None", "Posted", false)>]
[<InlineData("None", "Ignored", false)>]
[<InlineData("Ingested", "Ingested", false)>]
[<InlineData("Ingested", "Classified", true)>]
[<InlineData("Ingested", "NoMatch", true)>]
[<InlineData("Ingested", "Conflict", true)>]
[<InlineData("Ingested", "Reviewed", false)>]
[<InlineData("Ingested", "Duplicate", true)>]
[<InlineData("Ingested", "Posted", false)>]
[<InlineData("Ingested", "Ignored", true)>]
[<InlineData("Classified", "Ingested", false)>]
[<InlineData("Classified", "Classified", false)>]
[<InlineData("Classified", "NoMatch", false)>]
[<InlineData("Classified", "Conflict", false)>]
[<InlineData("Classified", "Reviewed", true)>]
[<InlineData("Classified", "Duplicate", true)>]
[<InlineData("Classified", "Posted", true)>]
[<InlineData("Classified", "Ignored", true)>]
[<InlineData("NoMatch", "Ingested", false)>]
[<InlineData("NoMatch", "Classified", false)>]
[<InlineData("NoMatch", "NoMatch", false)>]
[<InlineData("NoMatch", "Conflict", false)>]
[<InlineData("NoMatch", "Reviewed", true)>]
[<InlineData("NoMatch", "Duplicate", true)>]
[<InlineData("NoMatch", "Posted", false)>]
[<InlineData("NoMatch", "Ignored", true)>]
[<InlineData("Conflict", "Ingested", false)>]
[<InlineData("Conflict", "Classified", false)>]
[<InlineData("Conflict", "NoMatch", false)>]
[<InlineData("Conflict", "Conflict", false)>]
[<InlineData("Conflict", "Reviewed", true)>]
[<InlineData("Conflict", "Duplicate", true)>]
[<InlineData("Conflict", "Posted", false)>]
[<InlineData("Conflict", "Ignored", true)>]
[<InlineData("Reviewed", "Ingested", false)>]
[<InlineData("Reviewed", "Classified", false)>]
[<InlineData("Reviewed", "NoMatch", false)>]
[<InlineData("Reviewed", "Conflict", false)>]
[<InlineData("Reviewed", "Reviewed", false)>]
[<InlineData("Reviewed", "Duplicate", false)>]
[<InlineData("Reviewed", "Posted", true)>]
[<InlineData("Reviewed", "Ignored", true)>]
[<InlineData("Duplicate", "Ingested", false)>]
[<InlineData("Duplicate", "Classified", false)>]
[<InlineData("Duplicate", "NoMatch", false)>]
[<InlineData("Duplicate", "Conflict", false)>]
[<InlineData("Duplicate", "Reviewed", true)>]
[<InlineData("Duplicate", "Duplicate", false)>]
[<InlineData("Duplicate", "Posted", false)>]
[<InlineData("Duplicate", "Ignored", true)>]
[<InlineData("Posted", "Ingested", false)>]
[<InlineData("Posted", "Classified", false)>]
[<InlineData("Posted", "NoMatch", false)>]
[<InlineData("Posted", "Conflict", false)>]
[<InlineData("Posted", "Reviewed", false)>]
[<InlineData("Posted", "Duplicate", false)>]
[<InlineData("Posted", "Posted", false)>]
[<InlineData("Posted", "Ignored", false)>]
[<InlineData("Ignored", "Ingested", false)>]
[<InlineData("Ignored", "Classified", false)>]
[<InlineData("Ignored", "NoMatch", false)>]
[<InlineData("Ignored", "Conflict", false)>]
[<InlineData("Ignored", "Reviewed", true)>]
[<InlineData("Ignored", "Duplicate", false)>]
[<InlineData("Ignored", "Posted", false)>]
[<InlineData("Ignored", "Ignored", false)>]
let ``REQ-STG-4.6 validTransitions permits exactly the pairs the spec's transition table lists`` (fromStr: string, toStr: string, expectedPermitted: bool) =
    let parse s =
        s
        |> StagedEntryStatus.fromString
        |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
    let fromStatus =
        match fromStr with
        | "None" -> None
        | s -> s |> parse |> Some
    let toStatus = toStr |> parse
    // List.contains is how StageEntryOrchestration consults this list, so it is the property under test
    let permitted = validTransitions fromStatus |> List.contains toStatus
    Assert.Equal(expectedPermitted, permitted)


[<Fact>]
let ``REQ-STG-4.6 validTransitions from None returns only Ingested`` () =
    let transitions = validTransitions None
    Assert.Equal(1, transitions |> List.length)
    Assert.Equal(Ingested, transitions |> List.head)
