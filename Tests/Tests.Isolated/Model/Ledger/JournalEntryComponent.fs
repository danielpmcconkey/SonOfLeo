module Tests.Isolated.Model.Ledger.JournalEntryComponent

open System
open ModelOrchestrator.JournalEntryLineOrchestration
open Utilities.AppError
open Xunit
open Model.Ledger.Journaling.JournalEntryComponent
open Model
open Tests.Helpers.SadPath
open Tests.Helpers.Railroad


// =============================================================================
// CommentText
// =============================================================================
[<Fact>]
let ``REQ-JE-1.54 CommentText.create rejects empty string`` () =
    isCorrectError (CommentText.create String.Empty) JournalEntryCommentIsEmpty None
    |> railroadWrapper

[<Fact>]
let ``REQ-JE-1.54 CommentText.create rejects whitespace-only string`` () =
    isCorrectError (CommentText.create "     ") JournalEntryCommentIsEmpty None
    |> railroadWrapper

[<Fact>]
let ``REQ-JE-1.54 CommentText.create rejects string exceeding 2000 characters`` () =
    isCorrectError (CommentText.create(String('A', 2001))) JournalEntryCommentTooLong None
    |> railroadWrapper

[<Fact>]
let ``REQ-JE-1.54 CommentText.create accepts string at exactly 2000 characters`` () =
    let result = CommentText.create(String('A', 2000))
    Assert.True(Result.isOk result)

[<Fact>]
let ``REQ-SYS-1.1 CommentText.create trims whitespace`` () =
    let trimmed = "Correcting entry for June"
    let result = CommentText.create $"  {trimmed}   "
    match result with
    | Error e -> Assert.Fail(AppError.toMessage e)
    | Ok ct -> Assert.Equal(trimmed, CommentText.value ct)

[<Fact>]
let ``REQ-JE-1.54 CommentText.create accepts valid string`` () =
    let result = CommentText.create "Voided due to duplicate import"
    Assert.True(Result.isOk result)

// =============================================================================
// Description
// =============================================================================

[<Fact>]
let ``REQ-JE-1.4 REQ-SYS-1.2 Description.create rejects empty string`` () =
    isCorrectError (JournalEntryDescription.create String.Empty) JournalEntryDescriptionIsEmpty None
    |> railroadWrapper

[<Fact>]
let ``REQ-JE-1.4 REQ-SYS-1.2 Description.create rejects whitespace-only string`` () =
    isCorrectError (JournalEntryDescription.create "     ") JournalEntryDescriptionIsEmpty None
    |> railroadWrapper

[<Fact>]
let ``REQ-JE-1.5 Description.create rejects string exceeding 1000 characters`` () =
    isCorrectError (JournalEntryDescription.create(String('A', 1001))) JournalEntryDescriptionTooLong None
    |> railroadWrapper

[<Fact>]
let ``REQ-JE-1.5 Description.create accepts string at exactly 1000 characters`` () =
    let result = JournalEntryDescription.create(String('A', 1000))
    Assert.True(Result.isOk result)

[<Fact>]
let ``REQ-SYS-1.1 Description.create trims leading and trailing whitespace`` () =
    let trimmed = "Grocery run"
    let result = JournalEntryDescription.create $"  {trimmed}   "
    match result with
    | Error e -> Assert.Fail(AppError.toMessage e)
    | Ok d -> Assert.Equal(trimmed, JournalEntryDescription.value d)

[<Fact>]
let ``REQ-JE-1.3 Description.create accepts valid non-empty string`` () =
    let result = JournalEntryDescription.create "Monthly rent payment"
    Assert.True(Result.isOk result)

// =============================================================================
// Source
// =============================================================================

[<Fact>]
let ``REQ-JE-1.7 REQ-SYS-1.2 Source.create rejects empty string`` () =
    isCorrectError (JournalEntrySource.create String.Empty) JournalEntrySourceIsEmpty None
    |> railroadWrapper

[<Fact>]
let ``REQ-JE-1.7 REQ-SYS-1.2 Source.create rejects whitespace-only string`` () =
    isCorrectError (JournalEntrySource.create "     ") JournalEntrySourceIsEmpty None
    |> railroadWrapper

[<Fact>]
let ``REQ-JE-1.8 Source.create rejects string exceeding 50 characters`` () =
    isCorrectError (JournalEntrySource.create(String('A', 51))) JournalEntrySourceTooLong None
    |> railroadWrapper

[<Fact>]
let ``REQ-JE-1.8 Source.create accepts string at exactly 50 characters`` () =
    let result = JournalEntrySource.create(String('A', 50))
    Assert.True(Result.isOk result)

[<Fact>]
let ``REQ-SYS-1.1 Source.create trims leading and trailing whitespace`` () =
    let trimmed = "BankImport"
    let result = JournalEntrySource.create $"  {trimmed}   "
    match result with
    | Error e -> Assert.Fail(AppError.toMessage e)
    | Ok s -> Assert.Equal(trimmed, JournalEntrySource.value s)

// =============================================================================
// JournalEntryLineType
// =============================================================================

[<Fact>]
let ``REQ-JE-1.25 JournalEntryLineType.fromString accepts Debit`` () =
    Assert.True(Result.isOk(JournalEntryLineType.fromString "Debit"))

[<Fact>]
let ``REQ-JE-1.25 JournalEntryLineType.fromString accepts Credit`` () =
    Assert.True(Result.isOk(JournalEntryLineType.fromString "Credit"))

[<Fact>]
let ``REQ-JE-1.25 JournalEntryLineType.fromString rejects invalid string`` () =
    isCorrectError (JournalEntryLineType.fromString "Refund") JournalEntryLineTypeInvalid None
    |> railroadWrapper

[<Fact>]
let ``REQ-JE-1.25 JournalEntryLineType.fromString is case sensitive`` () =
    isCorrectError (JournalEntryLineType.fromString "debit") JournalEntryLineTypeInvalid None
    |> railroadWrapper

[<Fact>]
let ``REQ-JE-1.25 JournalEntryLineType.toString round-trips with fromString`` () =
    let original = Debit
    let roundTripped =
        original
        |> JournalEntryLineType.toString
        |> JournalEntryLineType.fromString
        |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
    Assert.Equal(original, roundTripped)

// =============================================================================
// LineMemo
// =============================================================================

[<Fact>]
let ``REQ-JE-1.27 REQ-SYS-1.2 LineMemo.create rejects empty string`` () =
    isCorrectError (JournalEntryLineMemo.create String.Empty) JournalEntryLineMemoIsEmpty None
    |> railroadWrapper

[<Fact>]
let ``REQ-JE-1.27 REQ-SYS-1.2 LineMemo.create rejects whitespace-only string`` () =
    isCorrectError (JournalEntryLineMemo.create "     ") JournalEntryLineMemoIsEmpty None
    |> railroadWrapper

[<Fact>]
let ``REQ-JE-1.28 LineMemo.create rejects string exceeding 1000 characters`` () =
    isCorrectError (JournalEntryLineMemo.create(String('A', 1001))) JournalEntryLineMemoTooLong None
    |> railroadWrapper

[<Fact>]
let ``REQ-JE-1.28 LineMemo.create accepts string at exactly 1000 characters`` () =
    let result = JournalEntryLineMemo.create(String('A', 1000))
    Assert.True(Result.isOk result)

[<Fact>]
let ``REQ-SYS-1.1 LineMemo.create trims leading and trailing whitespace`` () =
    let trimmed = "Office supplies"
    let result = JournalEntryLineMemo.create $"  {trimmed}   "
    match result with
    | Error e -> Assert.Fail(AppError.toMessage e)
    | Ok m -> Assert.Equal(trimmed, JournalEntryLineMemo.value m)

// =============================================================================
// Line amount validation (JournalEntryLine.validateAmount)
// =============================================================================

[<Fact>]
let ``REQ-JE-1.24 validateAmount rejects zero amount`` () =
    let zero = Money.fromDecimal 0.00M |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
    isCorrectError (confirmAmountIsPositive zero) JournalEntryLineNonPositiveAmount None
    |> railroadWrapper

[<Fact>]
let ``REQ-JE-1.24 validateAmount rejects negative amount`` () =
    let negative = Money.fromDecimal -5.00M |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
    isCorrectError (confirmAmountIsPositive negative) JournalEntryLineNonPositiveAmount None
    |> railroadWrapper

[<Fact>]
let ``REQ-JE-1.24 validateAmount accepts positive amount`` () =
    let positive = Money.fromDecimal 10.00M |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
    let result = confirmAmountIsPositive positive
    Assert.True(Result.isOk result)
