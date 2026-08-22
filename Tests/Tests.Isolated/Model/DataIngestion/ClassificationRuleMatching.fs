module Tests.Isolated.Model.DataIngestion.ClassificationRuleMatching

open DataAccessLayer.DbTransaction
open InterfaceBridge.BoundaryConverters.AccountFieldConverters
open Logger.Audit
open Model
open Model.DataIngestion
open Model.DataIngestion.Classification
open Model.DataIngestion.Classification.ClassificationRule
open Model.Ledger.Accounts.AccountComponent
open Model.Ledger.Journaling.JournalEntryComponent
open Utilities
open Utilities.AppError
open Xunit


let private unwrap result =
    result |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))

let private candidate =
    { headerIdOfCandidate = StageEntryHeaderId.create ()
      lineIdOfCandidate = StageEntryLineId.create ()
      ingestionSource = "TestBank" |> JournalRefFinancialInstitution.create |> unwrap
      description = "DoorDash Order" |> JournalEntryDescription.create |> unwrap
      amount = 45.00M |> Money.fromDecimal |> unwrap
      lineType = "Debit" |> JournalEntryLineType.fromString |> unwrap
      memo = None }

let private pattern raw = raw |> StringSearchPattern.create |> unwrap

let private groupOf patternStr =
    ClassificationRuleGroup.create And (FieldMatchChain.create [ Description(pattern patternStr) ]) None

// Groups whose verdict against the candidate above is fixed and known, so a
// rule's result is determined entirely by which groups it is built from.
let private matchingGroup = groupOf "^DoorDash"
let private failingGroup = groupOf "^Amazon"

// Sanity: the two building blocks really do evaluate opposite ways.
let private assertBuildingBlocks () =
    Assert.True(matchingGroup |> ClassificationRuleGroup.doesMatch candidate)
    Assert.False(failingGroup |> ClassificationRuleGroup.doesMatch candidate)

let private ruleOf groups (context: Context.Context) =
    let instant = Clock.now ()
    let accountId = "F-5350" |> ``convert AccountCodeString to Id`` context |> unwrap
    ClassificationRule.create
        (ClassificationRuleId.create ())
        ("Rule under test" |> ClassificationRuleName.create |> unwrap)
        accountId
        100
        groups
        true
        instant
        instant
    |> ClassificationRule.doesMatch candidate


[<Fact>]
let ``REQ-CR-2.7 a classification rule is true when every one of its rule groups is true`` () =
    let context = Context.create NoTransaction FetchOnly
    assertBuildingBlocks ()
    Assert.True(ruleOf [ matchingGroup; matchingGroup; matchingGroup ] context)

[<Fact>]
let ``REQ-CR-2.7 a classification rule is false when any one of its rule groups is false`` () =
    let context = Context.create NoTransaction FetchOnly
    assertBuildingBlocks ()
    Assert.False(ruleOf [ matchingGroup; failingGroup; matchingGroup ] context)

// Guards against an implementation that only evaluates the head of the list.
[<Theory>]
[<InlineData(0)>]
[<InlineData(1)>]
[<InlineData(2)>]
let ``REQ-CR-2.7 a classification rule is false regardless of which of its rule groups is the failing one`` (failingIndex: int) =
    let context = Context.create NoTransaction FetchOnly
    assertBuildingBlocks ()
    let groups =
        [ 0 .. 2 ] |> List.map (fun i -> if i = failingIndex then failingGroup else matchingGroup)
    Assert.Equal(1, groups |> List.filter (fun g -> g = failingGroup) |> List.length)
    Assert.False(ruleOf groups context)


// Same vacuous-truth hazard as the empty chain: List.forall over no rule groups returns true,
// so without the guard a rule with an empty groups list would match every candidate.
[<Fact>]
let ``REQ-CR-2.9 a rule with an empty rule groups list evaluates to false`` () =
    let context = Context.create NoTransaction FetchOnly
    Assert.False(ruleOf [] context)
