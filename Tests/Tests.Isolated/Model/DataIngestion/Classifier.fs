module Tests.Isolated.Model.DataIngestion.Classifier

open DataAccessLayer.DbTransaction
open InterfaceBridge.BoundaryConverters.AccountFieldConverters
open Logger.Audit
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

let private makeRule context codeStr priority patternStr isActive =
    let ruleId = ClassificationRuleId.create ()
    let name =
        $"Rule-{System.Guid.NewGuid().ToString().Substring(0, 8)}"
        |> ClassificationRuleName.create
        |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
    let code =
        codeStr |> ``convert AccountCodeString to Id`` context
        |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
    let pattern =
        patternStr |> StringSearchPattern.create
        |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
    let chain = FieldMatchChain.create [ FieldMatch.Description pattern ]
    let group = ClassificationRuleGroup.create And chain None
    let instant = Clock.now()
    ClassificationRule.create ruleId name code priority [ group ] isActive instant instant


// =============================================================================
// One result per candidate
// =============================================================================

[<Fact>]
let ``REQ-CR-3.1 classify returns one classification result per candidate, each carrying its own candidate's line id`` () =
    let context = Context.create NoTransaction FetchOnly
    let rule = makeRule context "F-5350" 100 "^DoorDash" true
    let c1 = makeCandidate "DoorDash Order" "TestBank" 25.00M "Debit"
    let c2 = makeCandidate "Amazon Purchase" "TestBank" 50.00M "Debit"
    let results = classify [ rule ] [ c1; c2 ]
    Assert.Equal(2, results |> List.length)
    // Pairing, not just arity: two copies of one candidate's result would
    // satisfy a count assertion on its own.
    let returnedLineIds = results |> List.map (fun r -> r.candidate.lineIdOfCandidate)
    Assert.Equal<StageEntryLineId list>([ c1.lineIdOfCandidate; c2.lineIdOfCandidate ], returnedLineIds)


// =============================================================================
// Single rule match
// =============================================================================

[<Fact>]
let ``REQ-CR-3.4 when exactly one active rule matches, classifyCandidate returns OneMatch carrying that rule's account code, rule id, and priority`` () =
    let context = Context.create NoTransaction FetchOnly
    let rule = makeRule context "F-5350" 100 "^DoorDash" true
    let candidate = makeCandidate "DoorDash Order 2024-12-15" "TestBank" 45.00M "Debit"
    let result = classifyCandidate [ rule ] candidate
    match result.outcome with
    | OneMatch pm ->
        let returnedString =
            pm.accountId
            |> ``convert AccountId to AccountCodeString`` context
            |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
        Assert.Equal("F-5350",  returnedString)
        Assert.Equal(rule |> ClassificationRule.classificationRuleId, pm.ruleId)
        Assert.Equal(100, pm.priority)
    | other -> Assert.Fail $"Expected OneMatch but got {other}"
        
        


// =============================================================================
// Multiple matches, clear priority winner
// =============================================================================

[<Fact>]
let ``REQ-CR-3.5 REQ-CR-1.6 classifyCandidate returns ManyMatchesClearWinner naming the lowest-priority-value rule as winner`` () =
    let context = Context.create NoTransaction FetchOnly
    let broadRule = makeRule context "F-5300" 1000 "^DoorDash" true
    let specificRule = makeRule context "F-5350" 100 "^DoorDash" true
    let candidate = makeCandidate "DoorDash Order 2024-12-15" "TestBank" 45.00M "Debit"
    let result = classifyCandidate [ broadRule; specificRule ] candidate
    match result.outcome with
    | ManyMatchesClearWinner (winner, _) ->
        let returnedString =
            winner.accountId
            |> ``convert AccountId to AccountCodeString`` context
            |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
        Assert.Equal(specificRule |> ClassificationRule.classificationRuleId, winner.ruleId)
        Assert.Equal("F-5350", returnedString)
        Assert.Equal(100, winner.priority)
    | other -> Assert.Fail $"Expected ManyMatchesClearWinner but got {other}"


// =============================================================================
// Multiple matches, tied priority
// =============================================================================

[<Fact>]
let ``REQ-CR-3.6 classifyCandidate returns ManyMatchesTied when two or more active rules share the lowest priority value`` () =
    let context = Context.create NoTransaction FetchOnly
    let rule1 = makeRule context "F-5350" 100 "^DoorDash" true
    let rule2 = makeRule context "F-5300" 100 "^DoorDash" true
    let candidate = makeCandidate "DoorDash Order 2024-12-15" "TestBank" 45.00M "Debit"
    let result = classifyCandidate [ rule1; rule2 ] candidate
    match result.outcome with
    | ManyMatchesTied _ -> ()
    | other -> Assert.Fail $"Expected ManyMatchesTied but got {other}"


// =============================================================================
// No match
// =============================================================================

[<Fact>]
let ``REQ-CR-3.3 classifyCandidate returns NoMatch when no active rule matches the candidate`` () =
    let context = Context.create NoTransaction FetchOnly
    let rule = makeRule context "F-5350" 100 "^DoorDash" true
    let candidate = makeCandidate "Amazon Purchase" "TestBank" 50.00M "Debit"
    let result = classifyCandidate [ rule ] candidate
    match result.outcome with
    | ClassifierOutcome.NoMatch -> ()
    | other -> Assert.Fail $"Expected NoMatch but got {other}"


// =============================================================================
// Classifier does not act on inactive rules
// =============================================================================

[<Fact>]
let ``REQ-CR-3.2 REQ-CR-1.8 classify returns OneMatch on the active rule when an inactive rule has a lower priority value`` () =
    let context = Context.create NoTransaction FetchOnly
    let activeRule = makeRule context "F-5350" 100 "^DoorDash" true
    let inactiveRule = makeRule context "F-5300" 50 "^DoorDash" false
    let candidate = makeCandidate "DoorDash Order" "TestBank" 45.00M "Debit"
    let results = classify [ activeRule; inactiveRule ] [ candidate ]
    let result = results |> List.head
    match result.outcome with
    | OneMatch pm ->
        let returnedString =
            pm.accountId
            |> ``convert AccountId to AccountCodeString`` context
            |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
        Assert.Equal("F-5350", returnedString)
    | other -> Assert.Fail $"Expected OneMatch (inactive rule filtered out) but got {other}"


// =============================================================================
// Inactive rules, remaining vectors
// =============================================================================

[<Fact>]
let ``REQ-CR-3.2 classify returns NoMatch when every rule that matches the candidate is inactive`` () =
    let context = Context.create NoTransaction FetchOnly
    let inactiveMatching = makeRule context "F-5350" 100 "^DoorDash" false
    let activeNonMatching = makeRule context "F-5400" 50 "^Amazon" true
    let candidate = makeCandidate "DoorDash Order" "TestBank" 45.00M "Debit"
    // The inactive rule would match on its own terms; only its active flag
    // keeps it out. Without this the NoMatch below proves nothing.
    Assert.True(inactiveMatching |> ClassificationRule.doesMatch candidate)
    Assert.False(activeNonMatching |> ClassificationRule.doesMatch candidate)
    let results = classify [ inactiveMatching; activeNonMatching ] [ candidate ]
    match (results |> List.head).outcome with
    | ClassifierOutcome.NoMatch -> ()
    | other -> Assert.Fail $"Expected NoMatch but got {other}"

[<Fact>]
let ``REQ-CR-3.2 classifyCandidate returns NoMatch for an inactive rule that would have matched the candidate`` () =
    let context = Context.create NoTransaction FetchOnly
    let inactiveRule = makeRule context "F-5350" 100 "^DoorDash" false
    let candidate = makeCandidate "DoorDash Order" "TestBank" 45.00M "Debit"
    Assert.True(inactiveRule |> ClassificationRule.doesMatch candidate)
    match (classifyCandidate [ inactiveRule ] candidate).outcome with
    | ClassifierOutcome.NoMatch -> ()
    | other -> Assert.Fail $"Expected NoMatch but got {other}"


// =============================================================================
// Payload completeness
// =============================================================================

[<Fact>]
let ``REQ-CR-3.5 ManyMatchesClearWinner carries every matching rule and no non-matching rule`` () =
    let context = Context.create NoTransaction FetchOnly
    let broadRule = makeRule context "F-5300" 1000 "^DoorDash" true
    let specificRule = makeRule context "F-5350" 100 "^DoorDash" true
    // Lowest priority value in the list, but it does not match -- so its
    // absence from the payload cannot be explained by priority.
    let nonMatching = makeRule context "F-5400" 10 "^Amazon" true
    let candidate = makeCandidate "DoorDash Order 2024-12-15" "TestBank" 45.00M "Debit"
    let result = classifyCandidate [ broadRule; specificRule; nonMatching ] candidate
    match result.outcome with
    | ManyMatchesClearWinner (_, allMatches) ->
        let expected =
            [ broadRule; specificRule ]
            |> List.map ClassificationRule.classificationRuleId
            |> Set.ofList
        let actual = allMatches |> List.map (fun m -> m.ruleId) |> Set.ofList
        Assert.Equal<Set<ClassificationRuleId>>(expected, actual)
    | other -> Assert.Fail $"Expected ManyMatchesClearWinner but got {other}"

[<Fact>]
let ``REQ-CR-3.6 ManyMatchesTied carries every matching rule and no non-matching rule, including one that matched at a higher priority value than the tied pair`` () =
    let context = Context.create NoTransaction FetchOnly
    let tiedOne = makeRule context "F-5350" 100 "^DoorDash" true
    let tiedTwo = makeRule context "F-5300" 100 "^DoorDash" true
    // Matches, but at a higher priority value than the tied pair. The payload
    // is every match, not only the ones at the lowest value.
    let alsoMatching = makeRule context "F-5200" 500 "^DoorDash" true
    let nonMatching = makeRule context "F-5400" 10 "^Amazon" true
    let candidate = makeCandidate "DoorDash Order 2024-12-15" "TestBank" 45.00M "Debit"
    let result = classifyCandidate [ tiedOne; tiedTwo; alsoMatching; nonMatching ] candidate
    match result.outcome with
    | ManyMatchesTied allMatches ->
        let expected =
            [ tiedOne; tiedTwo; alsoMatching ]
            |> List.map ClassificationRule.classificationRuleId
            |> Set.ofList
        let actual = allMatches |> List.map (fun m -> m.ruleId) |> Set.ofList
        Assert.Equal<Set<ClassificationRuleId>>(expected, actual)
    | other -> Assert.Fail $"Expected ManyMatchesTied but got {other}"
