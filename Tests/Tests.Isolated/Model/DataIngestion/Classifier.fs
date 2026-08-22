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

(* Isolated tests never reach the database, so these IDs name nothing in any chart of
   accounts. They exist to tell the rules apart, and to give the assertions below an
   expected value the test fixes rather than one read back out of the rule under test. *)
let private specificRuleAccountId = AccountId.create()
let private broadRuleAccountId = AccountId.create()
let private nonMatchingRuleAccountId = AccountId.create()
let private alsoMatchingRuleAccountId = AccountId.create()

let private makeRule (accountId: AccountId) priority patternStr isActive =
    let ruleId = ClassificationRuleId.create ()
    let name =
        $"Rule-{System.Guid.NewGuid().ToString().Substring(0, 8)}"
        |> ClassificationRuleName.create
        |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
    let pattern =
        patternStr |> StringSearchPattern.create
        |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
    let chain = FieldMatchChain.create [ FieldMatch.Description pattern ]
    let group = ClassificationRuleGroup.create And chain None
    let instant = Clock.now()
    ClassificationRule.create ruleId name accountId priority [ group ] isActive instant instant


// =============================================================================
// One result per candidate
// =============================================================================

[<Fact>]
let ``REQ-CR-3.1 classify returns one classification result per candidate, each carrying its own candidate's line id`` () =
    let rule = makeRule specificRuleAccountId 100 "^DoorDash" true
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
let ``REQ-CR-3.4 when exactly one active rule matches, classifyCandidate returns OneMatch carrying that rule's account, rule id, and priority`` () =
    let rule = makeRule specificRuleAccountId 100 "^DoorDash" true
    let candidate = makeCandidate "DoorDash Order 2024-12-15" "TestBank" 45.00M "Debit"
    let result = classifyCandidate [ rule ] candidate
    match result.outcome with
    | OneMatch pm ->
        Assert.Equal(specificRuleAccountId, pm.accountId)
        Assert.Equal(rule |> ClassificationRule.classificationRuleId, pm.ruleId)
        Assert.Equal(100, pm.priority)
    | other -> Assert.Fail $"Expected OneMatch but got {other}"


// =============================================================================
// Multiple matches, clear priority winner
// =============================================================================

[<Fact>]
let ``REQ-CR-3.5 REQ-CR-1.6 classifyCandidate returns ManyMatchesClearWinner naming the lowest-priority-value rule as winner`` () =
    let broadRule = makeRule broadRuleAccountId 1000 "^DoorDash" true
    let specificRule = makeRule specificRuleAccountId 100 "^DoorDash" true
    let candidate = makeCandidate "DoorDash Order 2024-12-15" "TestBank" 45.00M "Debit"
    let result = classifyCandidate [ broadRule; specificRule ] candidate
    match result.outcome with
    | ManyMatchesClearWinner (winner, _) ->
        Assert.Equal(specificRule |> ClassificationRule.classificationRuleId, winner.ruleId)
        Assert.Equal(specificRuleAccountId, winner.accountId)
        Assert.Equal(100, winner.priority)
    | other -> Assert.Fail $"Expected ManyMatchesClearWinner but got {other}"


// =============================================================================
// Multiple matches, tied priority
// =============================================================================

[<Fact>]
let ``REQ-CR-3.6 classifyCandidate returns ManyMatchesTied when two or more active rules share the lowest priority value`` () =
    let rule1 = makeRule specificRuleAccountId 100 "^DoorDash" true
    let rule2 = makeRule broadRuleAccountId 100 "^DoorDash" true
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
    let rule = makeRule specificRuleAccountId 100 "^DoorDash" true
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
    let activeRule = makeRule specificRuleAccountId 100 "^DoorDash" true
    let inactiveRule = makeRule broadRuleAccountId 50 "^DoorDash" false
    let candidate = makeCandidate "DoorDash Order" "TestBank" 45.00M "Debit"
    let results = classify [ activeRule; inactiveRule ] [ candidate ]
    let result = results |> List.head
    match result.outcome with
    | OneMatch pm ->
        Assert.Equal(specificRuleAccountId, pm.accountId)
    | other -> Assert.Fail $"Expected OneMatch (inactive rule filtered out) but got {other}"


// =============================================================================
// Inactive rules, remaining vectors
// =============================================================================

[<Fact>]
let ``REQ-CR-3.2 classify returns NoMatch when every rule that matches the candidate is inactive`` () =
    let inactiveMatching = makeRule specificRuleAccountId 100 "^DoorDash" false
    let activeNonMatching = makeRule nonMatchingRuleAccountId 50 "^Amazon" true
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
    let inactiveRule = makeRule specificRuleAccountId 100 "^DoorDash" false
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
    let broadRule = makeRule broadRuleAccountId 1000 "^DoorDash" true
    let specificRule = makeRule specificRuleAccountId 100 "^DoorDash" true
    // Lowest priority value in the list, but it does not match -- so its
    // absence from the payload cannot be explained by priority.
    let nonMatching = makeRule nonMatchingRuleAccountId 10 "^Amazon" true
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
    let tiedOne = makeRule specificRuleAccountId 100 "^DoorDash" true
    let tiedTwo = makeRule broadRuleAccountId 100 "^DoorDash" true
    // Matches, but at a higher priority value than the tied pair. The payload
    // is every match, not only the ones at the lowest value.
    let alsoMatching = makeRule alsoMatchingRuleAccountId 500 "^DoorDash" true
    let nonMatching = makeRule nonMatchingRuleAccountId 10 "^Amazon" true
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
