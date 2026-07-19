module Tests.Isolated.Model.Ledger.JournalEntryComment

open Model.Ledger.Journaling.JournalEntryComponent
open Xunit
open ModelOrchestrator.JournalEntryCommentOrchestration
    
// todo: these tests should really be moved to the integrated tests so the validation functions can be made private
[<Fact>]
let ``REQ-JE-1.53 validatePrimaryAndSecondaryRelationship rejects matching IDs`` () =
    let id = JournalEntryHeaderId.create ()
    let result = validatePrimaryAndSecondaryRelationship id (Some id)
    Assert.True(Result.isError result)
    
[<Fact>]
let ``REQ-JE-1.53 validatePrimaryAndSecondaryRelationship accepts different IDs`` () =
    let primary = JournalEntryHeaderId.create ()
    let secondary = JournalEntryHeaderId.create ()
    let result = validatePrimaryAndSecondaryRelationship primary (Some secondary)
    Assert.True(Result.isOk result)
    
[<Fact>]
let ``REQ-JE-1.52 validatePrimaryAndSecondaryRelationship accepts None secondary`` () =
    let primary = JournalEntryHeaderId.create ()
    let result = validatePrimaryAndSecondaryRelationship primary None
    Assert.True(Result.isOk result)
