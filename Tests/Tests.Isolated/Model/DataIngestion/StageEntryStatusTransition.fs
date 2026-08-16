module Tests.Isolated.Model.DataIngestion.StageEntryStatusTransition

open Model.DataIngestion
open Model.DataIngestion.StageEntryStatusTransition
open Xunit


// =============================================================================
// REQ-STG-4.1 — StagedEntryStatus.fromString for all valid values
// =============================================================================

[<Fact>]
let ``REQ-STG-4.1 StagedEntryStatus.fromString accepts Ingested`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-STG-4.1 StagedEntryStatus.fromString accepts Classified`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-STG-4.1 StagedEntryStatus.fromString accepts NoMatch`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-STG-4.1 StagedEntryStatus.fromString accepts Conflict`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-STG-4.1 StagedEntryStatus.fromString accepts Reviewed`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-STG-4.1 StagedEntryStatus.fromString accepts Duplicate`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-STG-4.1 StagedEntryStatus.fromString accepts Posted`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-STG-4.1 StagedEntryStatus.fromString accepts Ignored`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-STG-4.1 StagedEntryStatus.fromString rejects invalid string`` () =
    Assert.Fail "not implemented"


// =============================================================================
// REQ-STG-4.1 — StageStatusChangeMechanism.fromString
// =============================================================================

[<Fact>]
let ``REQ-STG-4.1 StageStatusChangeMechanism.fromString accepts all valid values`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-STG-4.1 StageStatusChangeMechanism.fromString rejects invalid string`` () =
    Assert.Fail "not implemented"


// =============================================================================
// REQ-STG-4.2 — Posted is terminal
// =============================================================================

[<Fact>]
let ``REQ-STG-4.2 validTransitions from Posted returns empty list`` () =
    Assert.Fail "not implemented"


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
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-STG-4.2 validTransitions from None returns only Ingested`` () =
    Assert.Fail "not implemented"
