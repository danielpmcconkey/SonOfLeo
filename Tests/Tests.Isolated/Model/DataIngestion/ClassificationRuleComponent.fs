module Tests.Isolated.Model.DataIngestion.ClassificationRuleComponent

open Utilities.AppError
open Xunit
open Model.DataIngestion.Classification.ClassificationRuleComponent


// =============================================================================
// ClassificationRuleName
// =============================================================================

[<Theory>]
[<InlineData("")>]
[<InlineData(" ")>]
[<InlineData("   ")>]
[<InlineData("\t")>]
[<InlineData("\n")>]
[<InlineData(" \t \n ")>]
let ``REQ-CR-1.3 ClassificationRuleName.create rejects input that is empty or whitespace only`` (raw: string) =
    match raw |> ClassificationRuleName.create with
    | Error (IngestionClassificationRuleNameIsEmpty returned) -> Assert.Equal(raw, returned)
    | Error other -> Assert.Fail $"Wrong error. Expected IngestionClassificationRuleNameIsEmpty but got {other}"
    | Ok _ -> Assert.Fail "Expected failure; got success"

[<Fact>]
let ``REQ-CR-1.4 ClassificationRuleName.create accepts a name of exactly 250 characters`` () =
    let raw = String.replicate 250 "a"
    match raw |> ClassificationRuleName.create with
    | Ok name -> Assert.Equal(raw, name |> ClassificationRuleName.value)
    | Error e -> Assert.Fail $"Expected success; got {e}"

[<Fact>]
let ``REQ-CR-1.4 ClassificationRuleName.create rejects a name of 251 characters`` () =
    let raw = String.replicate 251 "a"
    match raw |> ClassificationRuleName.create with
    | Error (IngestionClassificationRuleNameTooLong (returned, limit)) ->
        Assert.Equal(raw, returned)
        Assert.Equal(250, limit)
    | Error other -> Assert.Fail $"Wrong error. Expected IngestionClassificationRuleNameTooLong but got {other}"
    | Ok _ -> Assert.Fail "Expected failure; got success"

[<Fact>]
let ``REQ-CR-1.4 ClassificationRuleName.create accepts a 254-character name that trims to 250 and carries the trimmed value`` () =
    let core = String.replicate 250 "a"
    let raw = $"  {core}  "
    Assert.Equal(254, raw.Length)
    match raw |> ClassificationRuleName.create with
    | Ok name -> Assert.Equal(core, name |> ClassificationRuleName.value)
    | Error e -> Assert.Fail $"Expected success; got {e}"


// =============================================================================
// StringSearchPattern
//
// Search patterns are deliberately NOT trimmed -- whitespace is meaningful in a
// regex. That is why the whitespace-only case below is an acceptance, not a
// rejection, and why it does not mirror the rule-name case above it.
// =============================================================================

[<Fact>]
let ``REQ-CR-1.18 StringSearchPattern.create rejects an empty string`` () =
    match "" |> StringSearchPattern.create with
    | Error (IngestionSearchPatternIsEmpty returned) -> Assert.Equal("", returned)
    | Error other -> Assert.Fail $"Wrong error. Expected IngestionSearchPatternIsEmpty but got {other}"
    | Ok _ -> Assert.Fail "Expected failure; got success"

[<Theory>]
[<InlineData(" ")>]
[<InlineData("   ")>]
[<InlineData("\t")>]
let ``REQ-CR-1.18 StringSearchPattern.create accepts a pattern consisting only of whitespace`` (raw: string) =
    match raw |> StringSearchPattern.create with
    | Ok pattern -> Assert.Equal(raw, pattern |> StringSearchPattern.value)
    | Error e -> Assert.Fail $"Expected success; got {e}"

[<Fact>]
let ``REQ-CR-1.19 StringSearchPattern.create accepts a pattern of exactly 500 characters`` () =
    let raw = String.replicate 500 "a"
    match raw |> StringSearchPattern.create with
    | Ok pattern -> Assert.Equal(raw, pattern |> StringSearchPattern.value)
    | Error e -> Assert.Fail $"Expected success; got {e}"

[<Fact>]
let ``REQ-CR-1.19 StringSearchPattern.create rejects a pattern of 501 characters`` () =
    let raw = String.replicate 501 "a"
    match raw |> StringSearchPattern.create with
    | Error (IngestionSearchPatternTooLong (returned, limit)) ->
        Assert.Equal(raw, returned)
        Assert.Equal(500, limit)
    | Error other -> Assert.Fail $"Wrong error. Expected IngestionSearchPatternTooLong but got {other}"
    | Ok _ -> Assert.Fail "Expected failure; got success"


// =============================================================================
// NumericSearchOperator
// =============================================================================

[<Fact>]
let ``REQ-CR-1.20 NumericSearchOperator.fromString maps each of the five operator names to its own distinct case`` () =
    let expected =
        [ "GreaterThan", GreaterThan
          "LessThan", LessThan
          "GreaterThanOrEqualTo", GreaterThanOrEqualTo
          "LessThanOrEqualTo", LessThanOrEqualTo
          "ExactlyEqual", ExactlyEqual ]
    let actual =
        expected
        |> List.map (fun (name, _) ->
            match name |> NumericSearchOperator.fromString with
            | Ok operator -> Some operator
            | Error _ -> None)
    Assert.Equal<NumericSearchOperator option list>(expected |> List.map (snd >> Some), actual)
    // Distinctness is the "its own" half of the claim: five names all parsing
    // to the same case would satisfy the mapping assertion above on its own
    // only if that case were repeated in the expected list, which it is not --
    // this closes the gap directly.
    Assert.Equal(5, actual |> List.choose id |> List.distinct |> List.length)

[<Theory>]
[<InlineData("greaterthan")>]
[<InlineData("GreaterThanOrEqual")>]
[<InlineData("NotEqual")>]
[<InlineData("")>]
let ``REQ-CR-1.20 NumericSearchOperator.fromString rejects a string that is not one of the five operators`` (raw: string) =
    match raw |> NumericSearchOperator.fromString with
    | Error (IngestionInvalidNumericSearchOperator returned) -> Assert.Equal(raw, returned)
    | Error other -> Assert.Fail $"Wrong error. Expected IngestionInvalidNumericSearchOperator but got {other}"
    | Ok _ -> Assert.Fail "Expected failure; got success"


// =============================================================================
// ClassificationGroupConnector
// =============================================================================

[<Fact>]
let ``REQ-CR-1.9 ClassificationGroupConnector.fromString maps "And" to And and "Or" to Or`` () =
    match "And" |> ClassificationGroupConnector.fromString with
    | Ok actual -> Assert.Equal(And, actual)
    | Error e -> Assert.Fail $"Expected success for And; got {e}"
    match "Or" |> ClassificationGroupConnector.fromString with
    | Ok actual -> Assert.Equal(Or, actual)
    | Error e -> Assert.Fail $"Expected success for Or; got {e}"

[<Theory>]
[<InlineData("and")>]
[<InlineData("or")>]
[<InlineData("Xor")>]
[<InlineData("")>]
let ``REQ-CR-1.9 ClassificationGroupConnector.fromString rejects a connector name that is neither And nor Or`` (raw: string) =
    match raw |> ClassificationGroupConnector.fromString with
    | Error (IngestionInvalidClassificationGroupConnector returned) -> Assert.Equal(raw, returned)
    | Error other -> Assert.Fail $"Wrong error. Expected IngestionInvalidClassificationGroupConnector but got {other}"
    | Ok _ -> Assert.Fail "Expected failure; got success"
