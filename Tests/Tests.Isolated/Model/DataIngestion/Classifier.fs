module Tests.Isolated.Model.DataIngestion.Classifier

open Model.DataIngestion.Classification
open Model.DataIngestion.Classification.Classifier
open Xunit


// =============================================================================
// REQ-STG-5.2 — Classifier evaluates candidates against rules
// =============================================================================

[<Fact>]
let ``REQ-STG-5.2 classify returns result for each candidate`` () =
    Assert.Fail "not implemented"


// =============================================================================
// REQ-STG-5.4 — Single rule match assigns code
// =============================================================================

[<Fact>]
let ``REQ-STG-5.4 classifyCandidate with one matching rule returns OneMatch`` () =
    Assert.Fail "not implemented"


// =============================================================================
// REQ-STG-5.5 — Multiple matches, clear priority winner
// =============================================================================

[<Fact>]
let ``REQ-STG-5.5 classifyCandidate with multiple rules and clear priority winner returns ManyMatchesClearWinner`` () =
    Assert.Fail "not implemented"


// =============================================================================
// REQ-STG-5.6 — Multiple matches, tied priority
// =============================================================================

[<Fact>]
let ``REQ-STG-5.6 classifyCandidate with tied priority rules returns ManyMatchesTied`` () =
    Assert.Fail "not implemented"


// =============================================================================
// REQ-STG-5.7 — No match
// =============================================================================

[<Fact>]
let ``REQ-STG-5.7 classifyCandidate with no matching rules returns NoMatch`` () =
    Assert.Fail "not implemented"


// =============================================================================
// REQ-STG-5.3 — Classifier does not act on inactive rules
// =============================================================================

[<Fact>]
let ``REQ-STG-5.3 classify filters out inactive rules before matching`` () =
    Assert.Fail "not implemented"
