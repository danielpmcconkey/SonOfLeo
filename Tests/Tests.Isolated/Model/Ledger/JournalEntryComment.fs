module Tests.Isolated.Model.Ledger.JournalEntryComment

open Model.Ledger.Journaling.JournalEntryComponent
open Xunit
open ModelOrchestrator.JournalEntryCommentOrchestration
open Utilities.AppError
open Tests.Helpers.SadPath
open Tests.Helpers.Railroad

// todo: these tests should really be moved to the integrated tests so the validation functions can be made private
[<Fact>]
let ``REQ-JE-1.53 validatePrimaryAndSecondaryRelationship rejects matching IDs`` () =
    let id = JournalEntryHeaderId.create()
    isCorrectError (confirmPrimaryAndSecondaryRelationship id (Some id)) JournalEntryCommentPrimaryAndSecondaryIdsAreSame None
    |> railroadWrapper

[<Fact>]
let ``REQ-JE-1.53 validatePrimaryAndSecondaryRelationship accepts different IDs`` () =
    let primary = JournalEntryHeaderId.create()
    let secondary = JournalEntryHeaderId.create()
    let result = confirmPrimaryAndSecondaryRelationship primary (Some secondary)
    Assert.True(Result.isOk result)

[<Fact>]
let ``REQ-JE-1.52 validatePrimaryAndSecondaryRelationship accepts None secondary`` () =
    let primary = JournalEntryHeaderId.create()
    let result = confirmPrimaryAndSecondaryRelationship primary None
    Assert.True(Result.isOk result)
