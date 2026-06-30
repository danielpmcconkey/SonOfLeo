namespace Tests.Isolated.Model.Ledger

open System
open Xunit
open Model.Ledger.Journaling
open Model.Ledger.Journaling.JournalEntryComment

module JournalEntryComment =

    // =============================================================================
    // CommentText
    // =============================================================================

    [<Fact>]
    let ``REQ-JE-1.54 CommentText.create rejects empty string`` () =
        let result = CommentText.create String.Empty
        Assert.True(Result.isError result)

    [<Fact>]
    let ``REQ-JE-1.54 CommentText.create rejects whitespace-only string`` () =
        let result = CommentText.create "     "
        Assert.True(Result.isError result)

    [<Fact>]
    let ``REQ-JE-1.54 CommentText.create rejects string exceeding 2000 characters`` () =
        let result = CommentText.create (String('A', 2001))
        Assert.True(Result.isError result)

    [<Fact>]
    let ``REQ-JE-1.54 CommentText.create accepts string at exactly 2000 characters`` () =
        let result = CommentText.create (String('A', 2000))
        Assert.True(Result.isOk result)

    [<Fact>]
    let ``REQ-SYS-1.1 CommentText.create trims whitespace`` () =
        let trimmed = "Correcting entry for June"
        let result = CommentText.create $"  {trimmed}   "
        match result with
        | Error e -> Assert.Fail e
        | Ok ct -> Assert.Equal(trimmed, CommentText.value ct)

    [<Fact>]
    let ``REQ-JE-1.54 CommentText.create accepts valid string`` () =
        let result = CommentText.create "Voided due to duplicate import"
        Assert.True(Result.isOk result)

    // =============================================================================
    // Primary/secondary relationship validation
    // =============================================================================

    [<Fact>]
    let ``REQ-JE-1.53 validatePrimaryAndSecondaryRelationship rejects matching IDs`` () =
        let id = Guid.NewGuid()
        let result = validatePrimaryAndSecondaryRelationship id (Some id)
        Assert.True(Result.isError result)

    [<Fact>]
    let ``REQ-JE-1.53 validatePrimaryAndSecondaryRelationship accepts different IDs`` () =
        let primary = Guid.NewGuid()
        let secondary = Guid.NewGuid()
        let result = validatePrimaryAndSecondaryRelationship primary (Some secondary)
        Assert.True(Result.isOk result)

    [<Fact>]
    let ``REQ-JE-1.52 validatePrimaryAndSecondaryRelationship accepts None secondary`` () =
        let primary = Guid.NewGuid()
        let result = validatePrimaryAndSecondaryRelationship primary None
        Assert.True(Result.isOk result)
