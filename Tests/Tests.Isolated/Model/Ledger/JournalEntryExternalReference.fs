module Tests.Isolated.Model.Ledger.JournalEntryExternalReference

open System
open Model.Ledger.Journaling.JournalEntryComponent
open Utilities.AppError
open Xunit

// =============================================================================
// JournalRefFinancialInstitution
// =============================================================================

[<Fact>]
let ``REQ-JE-1.42 JournalRefFinancialInstitution.create rejects empty string`` () =
    let result = JournalRefFinancialInstitution.create String.Empty
    Assert.True(Result.isError result)

[<Fact>]
let ``REQ-JE-1.42 JournalRefFinancialInstitution.create rejects whitespace-only string`` () =
    let result = JournalRefFinancialInstitution.create ""
    Assert.True(Result.isError result)

[<Fact>]
let ``REQ-JE-1.49 JournalRefFinancialInstitution.create rejects string exceeding 100 characters`` () =
    let result = JournalRefFinancialInstitution.create(String('A', 101))
    Assert.True(Result.isError result)

[<Fact>]
let ``REQ-JE-1.49 JournalRefFinancialInstitution.create accepts string at exactly 100 characters`` () =
    let result = JournalRefFinancialInstitution.create(String('A', 100))
    Assert.True(Result.isOk result)

[<Fact>]
let ``REQ-SYS-1.1 JournalRefFinancialInstitution.create trims whitespace`` () =
    let trimmed = "Chase"
    let result = JournalRefFinancialInstitution.create $"  {trimmed}   "
    match result with
    | Error e -> Assert.Fail(AppError.toMessage e)
    | Ok fi -> Assert.Equal(trimmed, JournalRefFinancialInstitution.value fi)

[<Fact>]
let ``REQ-JE-1.42 JournalRefFinancialInstitution.create accepts valid string`` () =
    let result = JournalRefFinancialInstitution.create "Wells Fargo"
    Assert.True(Result.isOk result)

// =============================================================================
// JournalExternalReferenceText
// =============================================================================

[<Fact>]
let ``REQ-JE-1.44 JournalExternalReferenceText.create rejects empty string`` () =
    let result = JournalExternalReferenceText.create String.Empty
    Assert.True(Result.isError result)

[<Fact>]
let ``REQ-JE-1.44 JournalExternalReferenceText.create rejects whitespace-only string`` () =
    let result = JournalExternalReferenceText.create ""
    Assert.True(Result.isError result)

[<Fact>]
let ``REQ-JE-1.45 JournalExternalReferenceText.create rejects string exceeding 100 characters`` () =
    let result = JournalExternalReferenceText.create(String('A', 101))
    Assert.True(Result.isError result)

[<Fact>]
let ``REQ-JE-1.45 JournalExternalReferenceText.create accepts string at exactly 100 characters`` () =
    let result = JournalExternalReferenceText.create(String('A', 100))
    Assert.True(Result.isOk result)

[<Fact>]
let ``REQ-SYS-1.1 JournalExternalReferenceText.create trims whitespace`` () =
    let trimmed = "TXN-20260627-001"
    let result = JournalExternalReferenceText.create $"  {trimmed}   "
    match result with
    | Error e -> Assert.Fail(AppError.toMessage e)
    | Ok rt -> Assert.Equal(trimmed, JournalExternalReferenceText.value rt)

[<Fact>]
let ``REQ-JE-1.44 JournalExternalReferenceText.create accepts valid string`` () =
    let result = JournalExternalReferenceText.create "REF-12345"
    Assert.True(Result.isOk result)
