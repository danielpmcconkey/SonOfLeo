namespace Tests.Isolated.Model.Ledger

open System
open Xunit
open Model.Ledger.Journaling.JournalEntryComponent
open Model.Ledger.Journaling.JournalEntryLine
open Model

module JournalEntryComponent =

    // =============================================================================
    // Description
    // =============================================================================

    [<Fact>]
    let ``REQ-JE-1.4 REQ-SYS-1.2 Description.create rejects empty string`` () =
        let result = JournalEntryDescription.create String.Empty
        Assert.True(Result.isError result)

    [<Fact>]
    let ``REQ-JE-1.4 REQ-SYS-1.2 Description.create rejects whitespace-only string`` () =
        let result = JournalEntryDescription.create "     "
        Assert.True(Result.isError result)

    [<Fact>]
    let ``REQ-JE-1.5 Description.create rejects string exceeding 1000 characters`` () =
        let result = JournalEntryDescription.create (String('A', 1001))
        Assert.True(Result.isError result)

    [<Fact>]
    let ``REQ-JE-1.5 Description.create accepts string at exactly 1000 characters`` () =
        let result = JournalEntryDescription.create (String('A', 1000))
        Assert.True(Result.isOk result)

    [<Fact>]
    let ``REQ-SYS-1.1 Description.create trims leading and trailing whitespace`` () =
        let trimmed = "Grocery run"
        let result = JournalEntryDescription.create $"  {trimmed}   "
        match result with
        | Error e -> Assert.Fail e
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
        let result = JournalEntrySource.create String.Empty
        Assert.True(Result.isError result)

    [<Fact>]
    let ``REQ-JE-1.7 REQ-SYS-1.2 Source.create rejects whitespace-only string`` () =
        let result = JournalEntrySource.create "     "
        Assert.True(Result.isError result)

    [<Fact>]
    let ``REQ-JE-1.8 Source.create rejects string exceeding 50 characters`` () =
        let result = JournalEntrySource.create (String('A', 51))
        Assert.True(Result.isError result)

    [<Fact>]
    let ``REQ-JE-1.8 Source.create accepts string at exactly 50 characters`` () =
        let result = JournalEntrySource.create (String('A', 50))
        Assert.True(Result.isOk result)

    [<Fact>]
    let ``REQ-SYS-1.1 Source.create trims leading and trailing whitespace`` () =
        let trimmed = "BankImport"
        let result = JournalEntrySource.create $"  {trimmed}   "
        match result with
        | Error e -> Assert.Fail e
        | Ok s -> Assert.Equal(trimmed, JournalEntrySource.value s)

    // =============================================================================
    // JournalEntryLineType
    // =============================================================================

    [<Fact>]
    let ``REQ-JE-1.25 JournalEntryLineType.fromString accepts Debit`` () =
        Assert.True(Result.isOk (JournalEntryLineType.fromString "Debit"))

    [<Fact>]
    let ``REQ-JE-1.25 JournalEntryLineType.fromString accepts Credit`` () =
        Assert.True(Result.isOk (JournalEntryLineType.fromString "Credit"))

    [<Fact>]
    let ``REQ-JE-1.25 JournalEntryLineType.fromString rejects invalid string`` () =
        let result = JournalEntryLineType.fromString "Refund"
        Assert.True(Result.isError result)

    [<Fact>]
    let ``REQ-JE-1.25 JournalEntryLineType.fromString is case sensitive`` () =
        let result = JournalEntryLineType.fromString "debit"
        Assert.True(Result.isError result)

    [<Fact>]
    let ``REQ-JE-1.25 JournalEntryLineType.toString round-trips with fromString`` () =
        let original = Debit
        let roundTripped =
            original
            |> JournalEntryLineType.toString
            |> JournalEntryLineType.fromString
            |> Result.defaultWith failwith
        Assert.Equal(original, roundTripped)

    // =============================================================================
    // LineMemo
    // =============================================================================

    [<Fact>]
    let ``REQ-JE-1.27 REQ-SYS-1.2 LineMemo.create rejects empty string`` () =
        let result = LineMemo.create String.Empty
        Assert.True(Result.isError result)

    [<Fact>]
    let ``REQ-JE-1.27 REQ-SYS-1.2 LineMemo.create rejects whitespace-only string`` () =
        let result = LineMemo.create "     "
        Assert.True(Result.isError result)

    [<Fact>]
    let ``REQ-JE-1.28 LineMemo.create rejects string exceeding 1000 characters`` () =
        let result = LineMemo.create (String('A', 1001))
        Assert.True(Result.isError result)

    [<Fact>]
    let ``REQ-JE-1.28 LineMemo.create accepts string at exactly 1000 characters`` () =
        let result = LineMemo.create (String('A', 1000))
        Assert.True(Result.isOk result)

    [<Fact>]
    let ``REQ-SYS-1.1 LineMemo.create trims leading and trailing whitespace`` () =
        let trimmed = "Office supplies"
        let result = LineMemo.create $"  {trimmed}   "
        match result with
        | Error e -> Assert.Fail e
        | Ok m -> Assert.Equal(trimmed, LineMemo.value m)

    // =============================================================================
    // Line amount validation (JournalEntryLine.validateAmount)
    // =============================================================================

    [<Fact>]
    let ``REQ-JE-1.24 validateAmount rejects zero amount`` () =
        let zero = Money.fromDecimal 0.00M |> Result.defaultWith failwith
        let result = validateAmount zero
        Assert.True(Result.isError result)

    [<Fact>]
    let ``REQ-JE-1.24 validateAmount rejects negative amount`` () =
        let negative = Money.fromDecimal -5.00M |> Result.defaultWith failwith
        let result = validateAmount negative
        Assert.True(Result.isError result)

    [<Fact>]
    let ``REQ-JE-1.24 validateAmount accepts positive amount`` () =
        let positive = Money.fromDecimal 10.00M |> Result.defaultWith failwith
        let result = validateAmount positive
        Assert.True(Result.isOk result)
