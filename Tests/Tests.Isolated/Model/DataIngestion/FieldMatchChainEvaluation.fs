module Tests.Isolated.Model.DataIngestion.FieldMatchChainEvaluation

open Model
open Model.DataIngestion
open Model.DataIngestion.Classification
open Model.Ledger.Journaling.JournalEntryComponent
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

// Field matches whose verdict against the candidate above is fixed and known,
// so a chain's result is determined entirely by which of these it contains.
let private matching = Description(pattern "^DoorDash")
let private failing = Description(pattern "^Amazon")

// Sanity: the two building blocks really do evaluate opposite ways. Without
// this, a chain test could pass because every element behaved identically.
let private assertBuildingBlocks () =
    Assert.True(matching |> FieldMatch.doesMatch candidate)
    Assert.False(failing |> FieldMatch.doesMatch candidate)

let private chainOf matches =
    FieldMatchChain.create matches |> FieldMatchChain.doesMatch candidate


[<Fact>]
let ``REQ-CR-2.3 REQ-CR-1.12 a field match chain is true when every field match in it is true`` () =
    assertBuildingBlocks ()
    Assert.True(chainOf [ matching; matching; matching ])

[<Fact>]
let ``REQ-CR-2.3 REQ-CR-1.12 a field match chain is false when exactly one of its field matches is false`` () =
    assertBuildingBlocks ()
    Assert.False(chainOf [ matching; failing; matching ])

// Guards against an implementation that only evaluates the head of the list.
[<Theory>]
[<InlineData(0)>]
[<InlineData(1)>]
[<InlineData(2)>]
let ``REQ-CR-2.3 a field match chain is false regardless of which position the failing match occupies`` (failingIndex: int) =
    assertBuildingBlocks ()
    let matches =
        [ 0 .. 2 ] |> List.map (fun i -> if i = failingIndex then failing else matching)
    Assert.Equal(1, matches |> List.filter (fun m -> m = failing) |> List.length)
    Assert.False(chainOf matches)


// List.forall returns true on an empty list. Without the explicit guard, a chain with no
// field matches would match every candidate instead of none.
[<Fact>]
let ``REQ-CR-2.8 an empty field match chain evaluates to false`` () =
    Assert.False(chainOf [])
