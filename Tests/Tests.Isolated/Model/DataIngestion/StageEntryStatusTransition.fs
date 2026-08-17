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
// REQ-STG-4.2 — validTransitions covers the full state machine
// =============================================================================

[<Theory>]
[<InlineData("Ingested", 5)>]
[<InlineData("Classified", 4)>]
[<InlineData("NoMatch", 3)>]
[<InlineData("Conflict", 3)>]
[<InlineData("Reviewed", 2)>]
[<InlineData("Duplicate", 2)>]
[<InlineData("Posted", 0)>]
[<InlineData("Ignored", 1)>]
let ``REQ-STG-4.2 validTransitions returns correct count for each status`` (statusStr: string, expectedCount: int) =
    let status =
        statusStr
        |> StagedEntryStatus.fromString
        |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
    let transitions = validTransitions (Some status)
    Assert.Equal(expectedCount, transitions |> List.length)

[<Fact>]
let ``REQ-STG-4.2 validTransitions from None returns only Ingested`` () =
    let transitions = validTransitions None
    Assert.Equal(1, transitions |> List.length)
    Assert.Equal(Ingested, transitions |> List.head)
