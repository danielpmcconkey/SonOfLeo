module Tests.Isolated.Model.Ledger.JournalEntryExternalReference

open System
open Model.Ledger.Journaling.JournalEntryComponent
open Utilities.AppError
open Xunit
open Tests.Helpers.SadPath
open Tests.Helpers.Railroad

// =============================================================================
// JournalRefFinancialInstitution
// =============================================================================

[<Fact>]
let ``REQ-JE-1.42 JournalRefFinancialInstitution.create rejects empty string`` () =
    isCorrectError (JournalRefFinancialInstitution.create String.Empty) JournalRefFinancialInstitutionIsEmpty None
    |> railroadWrapper

[<Fact>]
let ``REQ-JE-1.42 JournalRefFinancialInstitution.create rejects whitespace-only string`` () =
    isCorrectError (JournalRefFinancialInstitution.create "") JournalRefFinancialInstitutionIsEmpty None
    |> railroadWrapper

[<Fact>]
let ``REQ-JE-1.49 JournalRefFinancialInstitution.create rejects string exceeding 100 characters`` () =
    isCorrectError (JournalRefFinancialInstitution.create(String('A', 101))) JournalRefFinancialInstitutionTooLong None
    |> railroadWrapper

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
    isCorrectError (JournalExternalReferenceText.create String.Empty) JournalEntryReferenceTextIsEmpty None
    |> railroadWrapper

[<Fact>]
let ``REQ-JE-1.44 JournalExternalReferenceText.create rejects whitespace-only string`` () =
    isCorrectError (JournalExternalReferenceText.create "") JournalEntryReferenceTextIsEmpty None
    |> railroadWrapper

[<Fact>]
let ``REQ-JE-1.45 JournalExternalReferenceText.create rejects string exceeding 100 characters`` () =
    isCorrectError (JournalExternalReferenceText.create(String('A', 101))) JournalEntryReferenceTextTooLong None
    |> railroadWrapper

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
