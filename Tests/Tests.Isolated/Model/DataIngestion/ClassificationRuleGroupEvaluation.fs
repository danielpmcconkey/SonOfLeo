module Tests.Isolated.Model.DataIngestion.ClassificationRuleGroupEvaluation

open Model
open Model.DataIngestion.StageEntryComponent
open Model.DataIngestion.Classification
open Model.Ledger.JournalEntryComponent
open Utilities.AppError
open Xunit
open Model.DataIngestion.Classification.ClassificationRuleComponent
open Model.DataIngestion.Classification.FieldMatch

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

// Chains whose verdict against the candidate above is fixed and known, so a
// group's result is determined entirely by which chains it is built from.
let private matchingChain = FieldMatchChain.create [ Description(pattern "^DoorDash") ]
let private failingChain = FieldMatchChain.create [ Description(pattern "^Amazon") ]

// Sanity: the two building blocks really do evaluate opposite ways.
let private assertBuildingBlocks () =
    Assert.True(matchingChain |> FieldMatchChain.doesMatch candidate)
    Assert.False(failingChain |> FieldMatchChain.doesMatch candidate)

let private groupResult connector chainOne chainTwo =
    ClassificationRuleGroup.create connector chainOne chainTwo
    |> ClassificationRuleGroup.doesMatch candidate


// =============================================================================
// No secondary chain -- the connector must not affect the outcome
// =============================================================================

[<Fact>]
let ``REQ-CR-2.4 REQ-CR-1.11 a rule group with the And connector and no secondary chain is true when its primary chain matches`` () =
    assertBuildingBlocks ()
    Assert.True(groupResult And matchingChain None)

[<Fact>]
let ``REQ-CR-2.4 REQ-CR-1.11 a rule group with the And connector and no secondary chain is false when its primary chain does not match`` () =
    assertBuildingBlocks ()
    Assert.False(groupResult And failingChain None)

[<Fact>]
let ``REQ-CR-2.4 REQ-CR-1.11 a rule group with the Or connector and no secondary chain is true when its primary chain matches`` () =
    assertBuildingBlocks ()
    Assert.True(groupResult Or matchingChain None)

[<Fact>]
let ``REQ-CR-2.4 REQ-CR-1.11 a rule group with the Or connector and no secondary chain is false when its primary chain does not match`` () =
    assertBuildingBlocks ()
    Assert.False(groupResult Or failingChain None)


// =============================================================================
// And connector, both chains present
// =============================================================================

[<Fact>]
let ``REQ-CR-2.5 a rule group with the And connector is true when both chains match`` () =
    assertBuildingBlocks ()
    Assert.True(groupResult And matchingChain (Some matchingChain))

[<Fact>]
let ``REQ-CR-2.5 a rule group with the And connector is false when the primary chain does not match`` () =
    assertBuildingBlocks ()
    Assert.False(groupResult And failingChain (Some matchingChain))

[<Fact>]
let ``REQ-CR-2.5 a rule group with the And connector is false when the secondary chain does not match`` () =
    assertBuildingBlocks ()
    Assert.False(groupResult And matchingChain (Some failingChain))

[<Fact>]
let ``REQ-CR-2.5 a rule group with the And connector is false when neither chain matches`` () =
    assertBuildingBlocks ()
    Assert.False(groupResult And failingChain (Some failingChain))


// =============================================================================
// Or connector, both chains present
// =============================================================================

[<Fact>]
let ``REQ-CR-2.6 a rule group with the Or connector is true when both chains match`` () =
    assertBuildingBlocks ()
    Assert.True(groupResult Or matchingChain (Some matchingChain))

[<Fact>]
let ``REQ-CR-2.6 a rule group with the Or connector is true when only the primary chain matches`` () =
    assertBuildingBlocks ()
    Assert.True(groupResult Or matchingChain (Some failingChain))

[<Fact>]
let ``REQ-CR-2.6 a rule group with the Or connector is true when only the secondary chain matches`` () =
    assertBuildingBlocks ()
    Assert.True(groupResult Or failingChain (Some matchingChain))

[<Fact>]
let ``REQ-CR-2.6 a rule group with the Or connector is false when neither chain matches`` () =
    assertBuildingBlocks ()
    Assert.False(groupResult Or failingChain (Some failingChain))
