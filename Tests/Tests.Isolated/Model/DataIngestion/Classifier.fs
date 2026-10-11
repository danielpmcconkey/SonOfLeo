module Tests.Isolated.Business.FinancialServices.DataIngestion.Classifier

open App.Session
open Business.General
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.CrossDomainOrchestration
open Business.FinancialServices.DataIngestion.StageEntryComponent
open Business.FinancialServices.Classification
open Business.FinancialServices.Classification.Classifier
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Ledger.JournalEntryComponent
open App.Utility
open App.Utility.IAppError
open Tests.Helpers.TestError
open Tests.Helpers.SadPath
open Xunit
open Business.FinancialServices.Classification.ClassificationComponent

(* The classifier returns a Result so that a pattern past its time limit is a typed error, not an exception. None of
   these tests reaches that path, so an Error here is a test failure. *)
let private classifyCandidate rules candidate =
    Classifier.classifyCandidate rules candidate |> Result.defaultWith (fun e -> failwith (e.ToMessage()))

let private classify rules candidates =
    Classifier.classify rules candidates |> Result.defaultWith (fun e -> failwith (e.ToMessage()))

let private makeCandidate descriptionStr sourceStr amount lineType =
    let description =
        descriptionStr |> JournalEntryDescription.create
        |> Result.defaultWith (fun (e: IAppError) -> failwith(e.ToMessage()))
    let source =
        sourceStr |> JournalRefFinancialInstitution.create
        |> Result.defaultWith (fun (e: IAppError) -> failwith(e.ToMessage()))
    let money =
        amount |> Money.fromDecimal
        |> Result.defaultWith (fun (e: IAppError) -> failwith(e.ToMessage()))
    let lt =
        lineType |> JournalEntryLineType.fromString
        |> Result.defaultWith (fun (e: IAppError) -> failwith(e.ToMessage()))
    MatchCandidate.create (StageEntryHeaderId.create ()) (StageEntryLineId.create ()) source description money lt None

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
        |> Result.defaultWith (fun (e: IAppError) -> failwith(e.ToMessage()))
    let pattern =
        patternStr |> StringSearchPattern.create
        |> Result.defaultWith (fun (e: IAppError) -> failwith(e.ToMessage()))
    let chain = FieldMatchChain.create [ FieldMatch.Description pattern ]
    let group = ClassificationRuleGroup.create And chain None
    let instant = Clock.now()
    ClassificationRule.create ruleId name (ClassificationClaimant.Account accountId) priority [ group ] isActive instant instant


// =============================================================================
// One result per candidate
// =============================================================================

[<Fact>]
let ``REQ-CR-3.1 classify returns one classification result per candidate, each carrying its own candidate's line id`` () =
    let rule = makeRule specificRuleAccountId (RulePriority.create 100) "^DoorDash" true
    let c1 = makeCandidate "DoorDash Order" "TestBank" 25.00M "Debit"
    let c2 = makeCandidate "Amazon Purchase" "TestBank" 50.00M "Debit"
    let results = classify [ rule ] [ c1; c2 ]
    Assert.Equal(2, results |> List.length)
    // Pairing, not just arity: two copies of one candidate's result would
    // satisfy a count assertion on its own.
    let returnedLineIds = results |> List.map (fun r -> (r |> ClassificationResult.candidate |> MatchCandidate.lineIdOfCandidate))
    Assert.Equal<StageEntryLineId list>([ (c1 |> MatchCandidate.lineIdOfCandidate); (c2 |> MatchCandidate.lineIdOfCandidate) ], returnedLineIds)


// =============================================================================
// Single rule match
// =============================================================================

[<Fact>]
let ``REQ-CR-3.4 when exactly one active rule matches, classifyCandidate returns OneMatch carrying that rule's account, rule id, and priority`` () =
    let rule = makeRule specificRuleAccountId (RulePriority.create 100) "^DoorDash" true
    let candidate = makeCandidate "DoorDash Order 2024-12-15" "TestBank" 45.00M "Debit"
    let result = classifyCandidate [ rule ] candidate
    match (result |> ClassificationResult.outcome) with
    | OneMatch pm ->
        Assert.Equal(ClassificationClaimant.Account specificRuleAccountId, (pm |> PrioritizedMatch.claimant))
        Assert.Equal(rule |> ClassificationRule.classificationRuleId, (pm |> PrioritizedMatch.ruleId))
        Assert.Equal(RulePriority.create 100, (pm |> PrioritizedMatch.priority))
    | other -> Assert.Fail $"Expected OneMatch but got {other}"


// =============================================================================
// Multiple matches, clear priority winner
// =============================================================================

[<Fact>]
let ``REQ-CR-3.5 REQ-CR-1.6 classifyCandidate returns ManyMatchesClearWinner naming the lowest-priority-value rule as winner`` () =
    let broadRule = makeRule broadRuleAccountId (RulePriority.create 1000) "^DoorDash" true
    let specificRule = makeRule specificRuleAccountId (RulePriority.create 100) "^DoorDash" true
    let candidate = makeCandidate "DoorDash Order 2024-12-15" "TestBank" 45.00M "Debit"
    let result = classifyCandidate [ broadRule; specificRule ] candidate
    match (result |> ClassificationResult.outcome) with
    | ManyMatchesClearWinner (winner, _) ->
        Assert.Equal(specificRule |> ClassificationRule.classificationRuleId, (winner |> PrioritizedMatch.ruleId))
        Assert.Equal(ClassificationClaimant.Account specificRuleAccountId, (winner |> PrioritizedMatch.claimant))
        Assert.Equal(RulePriority.create 100, (winner |> PrioritizedMatch.priority))
    | other -> Assert.Fail $"Expected ManyMatchesClearWinner but got {other}"


// =============================================================================
// Multiple matches, tied priority
// =============================================================================

[<Fact>]
let ``REQ-CR-3.6 classifyCandidate returns ManyMatchesTied when two or more active rules share the lowest priority value`` () =
    let rule1 = makeRule specificRuleAccountId (RulePriority.create 100) "^DoorDash" true
    let rule2 = makeRule broadRuleAccountId (RulePriority.create 100) "^DoorDash" true
    let candidate = makeCandidate "DoorDash Order 2024-12-15" "TestBank" 45.00M "Debit"
    let result = classifyCandidate [ rule1; rule2 ] candidate
    match (result |> ClassificationResult.outcome) with
    | ManyMatchesTied _ -> ()
    | other -> Assert.Fail $"Expected ManyMatchesTied but got {other}"


// =============================================================================
// No match
// =============================================================================

[<Fact>]
let ``REQ-CR-3.3 classifyCandidate returns NoMatch when no active rule matches the candidate`` () =
    let rule = makeRule specificRuleAccountId (RulePriority.create 100) "^DoorDash" true
    let candidate = makeCandidate "Amazon Purchase" "TestBank" 50.00M "Debit"
    let result = classifyCandidate [ rule ] candidate
    match (result |> ClassificationResult.outcome) with
    | ClassifierOutcome.NoMatch -> ()
    | other -> Assert.Fail $"Expected NoMatch but got {other}"


// =============================================================================
// Classifier does not act on inactive rules
// =============================================================================

[<Fact>]
let ``REQ-CR-3.2 REQ-CR-1.8 classify returns OneMatch on the active rule when an inactive rule has a lower priority value`` () =
    let activeRule = makeRule specificRuleAccountId (RulePriority.create 100) "^DoorDash" true
    let inactiveRule = makeRule broadRuleAccountId (RulePriority.create 50) "^DoorDash" false
    let candidate = makeCandidate "DoorDash Order" "TestBank" 45.00M "Debit"
    let results = classify [ activeRule; inactiveRule ] [ candidate ]
    let result = results |> List.head
    match (result |> ClassificationResult.outcome) with
    | OneMatch pm ->
        Assert.Equal(ClassificationClaimant.Account specificRuleAccountId, (pm |> PrioritizedMatch.claimant))
    | other -> Assert.Fail $"Expected OneMatch (inactive rule filtered out) but got {other}"


// =============================================================================
// Inactive rules, remaining vectors
// =============================================================================

[<Fact>]
let ``REQ-CR-3.2 classify returns NoMatch when every rule that matches the candidate is inactive`` () =
    let inactiveMatching = makeRule specificRuleAccountId (RulePriority.create 100) "^DoorDash" false
    let activeNonMatching = makeRule nonMatchingRuleAccountId (RulePriority.create 50) "^Amazon" true
    let candidate = makeCandidate "DoorDash Order" "TestBank" 45.00M "Debit"
    // The inactive rule would match on its own terms; only its active flag
    // keeps it out. Without this the NoMatch below proves nothing.
    Assert.True(inactiveMatching |> ClassificationRule.doesMatch candidate)
    Assert.False(activeNonMatching |> ClassificationRule.doesMatch candidate)
    let results = classify [ inactiveMatching; activeNonMatching ] [ candidate ]
    match (results |> List.head |> ClassificationResult.outcome) with
    | ClassifierOutcome.NoMatch -> ()
    | other -> Assert.Fail $"Expected NoMatch but got {other}"

[<Fact>]
let ``REQ-CR-3.2 classifyCandidate returns NoMatch for an inactive rule that would have matched the candidate`` () =
    let inactiveRule = makeRule specificRuleAccountId (RulePriority.create 100) "^DoorDash" false
    let candidate = makeCandidate "DoorDash Order" "TestBank" 45.00M "Debit"
    Assert.True(inactiveRule |> ClassificationRule.doesMatch candidate)
    match (classifyCandidate [ inactiveRule ] candidate |> ClassificationResult.outcome) with
    | ClassifierOutcome.NoMatch -> ()
    | other -> Assert.Fail $"Expected NoMatch but got {other}"


// =============================================================================
// Payload completeness
// =============================================================================

[<Fact>]
let ``REQ-CR-3.5 ManyMatchesClearWinner carries every matching rule and no non-matching rule`` () =
    let broadRule = makeRule broadRuleAccountId (RulePriority.create 1000) "^DoorDash" true
    let specificRule = makeRule specificRuleAccountId (RulePriority.create 100) "^DoorDash" true
    // Lowest priority value in the list, but it does not match -- so its
    // absence from the payload cannot be explained by priority.
    let nonMatching = makeRule nonMatchingRuleAccountId (RulePriority.create 10) "^Amazon" true
    let candidate = makeCandidate "DoorDash Order 2024-12-15" "TestBank" 45.00M "Debit"
    let result = classifyCandidate [ broadRule; specificRule; nonMatching ] candidate
    match (result |> ClassificationResult.outcome) with
    | ManyMatchesClearWinner (_, allMatches) ->
        let expected =
            [ broadRule; specificRule ]
            |> List.map ClassificationRule.classificationRuleId
            |> Set.ofList
        let actual = allMatches |> List.map (fun m -> (m |> PrioritizedMatch.ruleId)) |> Set.ofList
        Assert.Equal<Set<ClassificationRuleId>>(expected, actual)
    | other -> Assert.Fail $"Expected ManyMatchesClearWinner but got {other}"

[<Fact>]
let ``REQ-CR-3.6 ManyMatchesTied carries every matching rule and no non-matching rule, including one that matched at a higher priority value than the tied pair`` () =
    let tiedOne = makeRule specificRuleAccountId (RulePriority.create 100) "^DoorDash" true
    let tiedTwo = makeRule broadRuleAccountId (RulePriority.create 100) "^DoorDash" true
    // Matches, but at a higher priority value than the tied pair. The payload
    // is every match, not only the ones at the lowest value.
    let alsoMatching = makeRule alsoMatchingRuleAccountId (RulePriority.create 500) "^DoorDash" true
    let nonMatching = makeRule nonMatchingRuleAccountId (RulePriority.create 10) "^Amazon" true
    let candidate = makeCandidate "DoorDash Order 2024-12-15" "TestBank" 45.00M "Debit"
    let result = classifyCandidate [ tiedOne; tiedTwo; alsoMatching; nonMatching ] candidate
    match (result |> ClassificationResult.outcome) with
    | ManyMatchesTied allMatches ->
        let expected =
            [ tiedOne; tiedTwo; alsoMatching ]
            |> List.map ClassificationRule.classificationRuleId
            |> Set.ofList
        let actual = allMatches |> List.map (fun m -> (m |> PrioritizedMatch.ruleId)) |> Set.ofList
        Assert.Equal<Set<ClassificationRuleId>>(expected, actual)
    | other -> Assert.Fail $"Expected ManyMatchesTied but got {other}"
