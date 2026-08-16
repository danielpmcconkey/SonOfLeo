module Tests.Isolated.Model.DataIngestion.Classifier

open Model
open Model.DataIngestion
open Model.DataIngestion.Classification
open Model.DataIngestion.Classification.ClassificationRule
open Model.DataIngestion.Classification.Classifier
open Model.Ledger.Accounts.AccountComponent
open Model.Ledger.Journaling.JournalEntryComponent
open Utilities
open Utilities.AppError
open Xunit

let private makeCandidate descriptionStr sourceStr amount lineType =
    let description =
        descriptionStr |> JournalEntryDescription.create
        |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
    let source =
        sourceStr |> JournalRefFinancialInstitution.create
        |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
    let money =
        amount |> Money.fromDecimal
        |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
    let lt =
        lineType |> JournalEntryLineType.fromString
        |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
    { headerIdOfCandidate = StageEntryHeaderId.create ()
      lineIdOfCandidate = StageEntryLineId.create ()
      ingestionSource = source
      description = description
      amount = money
      lineType = lt
      memo = None }

let private makeRule codeStr priority patternStr isActive =
    let ruleId = ClassificationRuleId.create ()
    let name =
        $"Rule-{System.Guid.NewGuid().ToString().Substring(0, 8)}"
        |> ClassificationRuleName.create
        |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
    let code =
        codeStr |> AccountCode.create
        |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
    let pattern =
        patternStr |> StringSearchPattern.create
        |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
    let chain = FieldMatchChain.create [ FieldMatch.Description pattern ]
    let group = ClassificationRuleGroup.create And chain None
    let instant = Clock.now()
    ClassificationRule.create ruleId name code priority [ group ] isActive instant instant


// =============================================================================
// REQ-STG-5.2 — Classifier evaluates candidates against rules
// =============================================================================

[<Fact>]
let ``REQ-STG-5.2 classify returns result for each candidate`` () =
    let rule = makeRule "F-5350" 100 "^DoorDash" true
    let c1 = makeCandidate "DoorDash Order" "TestBank" 25.00M "Debit"
    let c2 = makeCandidate "Amazon Purchase" "TestBank" 50.00M "Debit"
    let results = classify [ rule ] [ c1; c2 ]
    Assert.Equal(2, results |> List.length)


// =============================================================================
// REQ-STG-5.4 — Single rule match assigns code
// =============================================================================

[<Fact>]
let ``REQ-STG-5.4 classifyCandidate with one matching rule returns OneMatch`` () =
    let rule = makeRule "F-5350" 100 "^DoorDash" true
    let candidate = makeCandidate "DoorDash Order 2024-12-15" "TestBank" 45.00M "Debit"
    let result = classifyCandidate [ rule ] candidate
    match result.outcome with
    | OneMatch pm ->
        let codeVal = pm.code |> AccountCode.value
        Assert.Equal("F-5350", codeVal)
    | other -> Assert.Fail $"Expected OneMatch but got {other}"


// =============================================================================
// REQ-STG-5.5 — Multiple matches, clear priority winner
// =============================================================================

[<Fact>]
let ``REQ-STG-5.5 classifyCandidate with multiple rules and clear priority winner returns ManyMatchesClearWinner`` () =
    let broadRule = makeRule "F-5300" 1000 "^DoorDash" true
    let specificRule = makeRule "F-5350" 100 "^DoorDash" true
    let candidate = makeCandidate "DoorDash Order 2024-12-15" "TestBank" 45.00M "Debit"
    let result = classifyCandidate [ broadRule; specificRule ] candidate
    match result.outcome with
    | ManyMatchesClearWinner (winner, _) ->
        let codeVal = winner.code |> AccountCode.value
        Assert.Equal("F-5350", codeVal)
        Assert.Equal(100, winner.priority)
    | other -> Assert.Fail $"Expected ManyMatchesClearWinner but got {other}"


// =============================================================================
// REQ-STG-5.6 — Multiple matches, tied priority
// =============================================================================

[<Fact>]
let ``REQ-STG-5.6 classifyCandidate with tied priority rules returns ManyMatchesTied`` () =
    let rule1 = makeRule "F-5350" 100 "^DoorDash" true
    let rule2 = makeRule "F-5300" 100 "^DoorDash" true
    let candidate = makeCandidate "DoorDash Order 2024-12-15" "TestBank" 45.00M "Debit"
    let result = classifyCandidate [ rule1; rule2 ] candidate
    match result.outcome with
    | ManyMatchesTied matches ->
        Assert.Equal(2, matches |> List.length)
    | other -> Assert.Fail $"Expected ManyMatchesTied but got {other}"


// =============================================================================
// REQ-STG-5.7 — No match
// =============================================================================

[<Fact>]
let ``REQ-STG-5.7 classifyCandidate with no matching rules returns NoMatch`` () =
    let rule = makeRule "F-5350" 100 "^DoorDash" true
    let candidate = makeCandidate "Amazon Purchase" "TestBank" 50.00M "Debit"
    let result = classifyCandidate [ rule ] candidate
    match result.outcome with
    | ClassifierOutcome.NoMatch -> ()
    | other -> Assert.Fail $"Expected NoMatch but got {other}"


// =============================================================================
// REQ-STG-5.3 — Classifier does not act on inactive rules
// =============================================================================

[<Fact>]
let ``REQ-STG-5.3 classify filters out inactive rules before matching`` () =
    let activeRule = makeRule "F-5350" 100 "^DoorDash" true
    let inactiveRule = makeRule "F-5300" 50 "^DoorDash" false
    let candidate = makeCandidate "DoorDash Order" "TestBank" 45.00M "Debit"
    let results = classify [ activeRule; inactiveRule ] [ candidate ]
    let result = results |> List.head
    match result.outcome with
    | OneMatch pm ->
        let codeVal = pm.code |> AccountCode.value
        Assert.Equal("F-5350", codeVal)
    | other -> Assert.Fail $"Expected OneMatch (inactive rule filtered out) but got {other}"
