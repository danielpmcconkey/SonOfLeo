module Tests.Isolated.Model.DataIngestion.FieldMatchEvaluation

open Model
open Model.DataIngestion
open Model.DataIngestion.Classification
open Model.Ledger.Journaling.JournalEntryComponent
open Utilities.AppError
open Xunit


let private unwrap result =
    result |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))

// A candidate whose every field is independently settable, so each field match
// under test is exercised against a value chosen for it and nothing else.
let private makeCandidate descriptionStr sourceStr amount lineTypeStr memoStr =
    { headerIdOfCandidate = StageEntryHeaderId.create ()
      lineIdOfCandidate = StageEntryLineId.create ()
      ingestionSource = sourceStr |> JournalRefFinancialInstitution.create |> unwrap
      description = descriptionStr |> JournalEntryDescription.create |> unwrap
      amount = amount |> Money.fromDecimal |> unwrap
      lineType = lineTypeStr |> JournalEntryLineType.fromString |> unwrap
      memo = memoStr |> Option.map (JournalEntryLineMemo.create >> unwrap) }

let private pattern raw = raw |> StringSearchPattern.create |> unwrap

let private moneyPattern operator amount =
    { numericSearchOperator = operator
      amount = amount |> Money.fromDecimal |> unwrap }


// =============================================================================
// Source
// =============================================================================

[<Fact>]
let ``REQ-CR-2.1 REQ-CR-1.14 a Source field match is true when the candidate's institution matches the pattern regex`` () =
    let candidate = makeCandidate "DoorDash Order" "TestBank" 45.00M "Debit" None
    Assert.True(Source(pattern "^TestBank$") |> FieldMatch.doesMatch candidate)

[<Fact>]
let ``REQ-CR-2.1 REQ-CR-1.14 a Source field match is false when the candidate's institution does not match the pattern regex`` () =
    let candidate = makeCandidate "DoorDash Order" "TestBank" 45.00M "Debit" None
    Assert.False(Source(pattern "^OtherBank$") |> FieldMatch.doesMatch candidate)


// =============================================================================
// Description
// =============================================================================

[<Fact>]
let ``REQ-CR-2.1 REQ-CR-1.14 a Description field match is true when the candidate's description matches the pattern regex`` () =
    let candidate = makeCandidate "DoorDash Order 2024-12-15" "TestBank" 45.00M "Debit" None
    Assert.True(Description(pattern "^DoorDash") |> FieldMatch.doesMatch candidate)

[<Fact>]
let ``REQ-CR-2.1 REQ-CR-1.14 a Description field match is false when the candidate's description does not match the pattern regex`` () =
    let candidate = makeCandidate "Amazon Purchase" "TestBank" 45.00M "Debit" None
    Assert.False(Description(pattern "^DoorDash") |> FieldMatch.doesMatch candidate)

[<Fact>]
let ``REQ-CR-1.14 a Description field match evaluates its pattern as a regex`` () =
    let descriptionStr = "DoorDash Order 2024-12-15"
    let patternStr = "^DoorDash"
    // The caret is absent from the description as a literal character, so a
    // containment-based implementation cannot make this match succeed.
    Assert.DoesNotContain(patternStr, descriptionStr)
    let candidate = makeCandidate descriptionStr "TestBank" 45.00M "Debit" None
    Assert.True(Description(pattern patternStr) |> FieldMatch.doesMatch candidate)


// =============================================================================
// Memo
// =============================================================================

[<Fact>]
let ``REQ-CR-2.1 REQ-CR-1.14 a Memo field match is true when the candidate's memo matches the pattern regex`` () =
    let candidate = makeCandidate "DoorDash Order" "TestBank" 45.00M "Debit" (Some "tip included")
    Assert.True(Memo(pattern "^tip") |> FieldMatch.doesMatch candidate)

[<Fact>]
let ``REQ-CR-2.1 REQ-CR-1.14 a Memo field match is false when the candidate's memo is present but does not match the pattern regex`` () =
    let candidate = makeCandidate "DoorDash Order" "TestBank" 45.00M "Debit" (Some "no tip")
    Assert.False(Memo(pattern "^tip") |> FieldMatch.doesMatch candidate)

[<Fact>]
let ``REQ-CR-2.2 a Memo field match is false when the candidate has no memo, even for a pattern that matches everything`` () =
    let matchEverything = pattern ".*"
    let withMemo = makeCandidate "DoorDash Order" "TestBank" 45.00M "Debit" (Some "anything at all")
    // The pattern matches when a memo is present, so the absent-memo result
    // below cannot be explained by the pattern simply failing.
    Assert.True(Memo matchEverything |> FieldMatch.doesMatch withMemo)
    let withoutMemo = makeCandidate "DoorDash Order" "TestBank" 45.00M "Debit" None
    Assert.False(Memo matchEverything |> FieldMatch.doesMatch withoutMemo)


// =============================================================================
// LineType
// =============================================================================

[<Fact>]
let ``REQ-CR-2.1 REQ-CR-1.15 a LineType field match is true when the candidate's line type equals the pattern's line type`` () =
    let candidate = makeCandidate "DoorDash Order" "TestBank" 45.00M "Debit" None
    let debit = "Debit" |> JournalEntryLineType.fromString |> unwrap
    Assert.True(LineType debit |> FieldMatch.doesMatch candidate)

[<Fact>]
let ``REQ-CR-2.1 REQ-CR-1.15 a LineType field match is false when the candidate's line type differs from the pattern's line type`` () =
    let candidate = makeCandidate "DoorDash Order" "TestBank" 45.00M "Debit" None
    let credit = "Credit" |> JournalEntryLineType.fromString |> unwrap
    Assert.False(LineType credit |> FieldMatch.doesMatch candidate)


// =============================================================================
// Amount
//
// Each operator is exercised at three points relative to the pattern amount --
// below, equal, above -- so the true/false verdict is pinned at every position
// rather than only where the operator happens to admit.
// =============================================================================

let private amountMatches operator patternAmount candidateAmount =
    let candidate = makeCandidate "DoorDash Order" "TestBank" candidateAmount "Debit" None
    Amount(moneyPattern operator patternAmount) |> FieldMatch.doesMatch candidate

[<Fact>]
let ``REQ-CR-1.16 REQ-CR-2.1 an Amount field match with GreaterThan is true above the pattern amount and false at and below it`` () =
    Assert.False(amountMatches GreaterThan 50.00M 49.99M)
    Assert.False(amountMatches GreaterThan 50.00M 50.00M)
    Assert.True(amountMatches GreaterThan 50.00M 50.01M)

[<Fact>]
let ``REQ-CR-1.16 REQ-CR-2.1 an Amount field match with LessThan is true below the pattern amount and false at and above it`` () =
    Assert.True(amountMatches LessThan 50.00M 49.99M)
    Assert.False(amountMatches LessThan 50.00M 50.00M)
    Assert.False(amountMatches LessThan 50.00M 50.01M)

[<Fact>]
let ``REQ-CR-1.16 REQ-CR-2.1 an Amount field match with GreaterThanOrEqualTo is true at and above the pattern amount and false below it`` () =
    Assert.False(amountMatches GreaterThanOrEqualTo 50.00M 49.99M)
    Assert.True(amountMatches GreaterThanOrEqualTo 50.00M 50.00M)
    Assert.True(amountMatches GreaterThanOrEqualTo 50.00M 50.01M)

[<Fact>]
let ``REQ-CR-1.16 REQ-CR-2.1 an Amount field match with LessThanOrEqualTo is true at and below the pattern amount and false above it`` () =
    Assert.True(amountMatches LessThanOrEqualTo 50.00M 49.99M)
    Assert.True(amountMatches LessThanOrEqualTo 50.00M 50.00M)
    Assert.False(amountMatches LessThanOrEqualTo 50.00M 50.01M)

[<Fact>]
let ``REQ-CR-1.16 REQ-CR-2.1 an Amount field match with ExactlyEqual is true at the pattern amount and false both below and above it`` () =
    Assert.False(amountMatches ExactlyEqual 50.00M 49.99M)
    Assert.True(amountMatches ExactlyEqual 50.00M 50.00M)
    Assert.False(amountMatches ExactlyEqual 50.00M 50.01M)
