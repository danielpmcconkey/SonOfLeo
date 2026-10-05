module Tests.Isolated.Model.CashFlow.CashFlowComponent

open App.Utility.IAppError
open Business.FinancialServices.CashFlow.CashFlowComponent
open Business.FinancialServices.CashFlow.CashFlowError
open Xunit

(* The typed error names the raw value and the bound it broke: 1 for too few days, 365 for too many. *)
[<Theory>]
[<InlineData(0)>]
[<InlineData(-1)>]
[<InlineData(366)>]
let ``REQ-CF-7.1 for each of 0, -1 and 366, a sweep horizon of that many days is rejected with a typed error`` (days: int) =
    let rejectedWithTheBound =
        match days |> ProjectionHorizonInDays.create with
        | Error (AsError (CashflowProjectionHorizonInDaysBelowMin (raw, 1))) -> days < 1 && raw = days
        | Error (AsError (CashflowProjectionHorizonInDaysExceededMax (raw, 365))) -> days > 365 && raw = days
        | _ -> false
    Assert.True(rejectedWithTheBound)

[<Fact>]
let ``REQ-CF-4.11 REQ-CF-5.17 a cancellation reason note of exactly 500 characters is accepted and one of 501 characters is rejected as too long`` () =
    let atLimit = System.String('n', 500)
    let overLimit = System.String('n', 501)
    match atLimit |> CancellationReasonNote.create with
    | Ok note -> Assert.Equal(atLimit, note |> CancellationReasonNote.value)
    | Error e -> Assert.Fail $"Expected success; got {e.ToMessage()}"
    match overLimit |> CancellationReasonNote.create with
    | Error (AsError (CashflowCancellationReasonNoteTooLong (raw, limit))) ->
        Assert.Equal(overLimit, raw)
        Assert.Equal(500, limit)
    | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
    | Ok _ -> Assert.Fail "Expected failure; got success"

[<Fact>]
let ``REQ-CF-4.11 REQ-CF-5.17 for each of an empty string and a whitespace-only string, a cancellation reason note is rejected as empty`` () =
    for blank in [ ""; "   \t  " ] do
        match blank |> CancellationReasonNote.create with
        | Error (AsError (CashflowCancellationReasonNoteIsEmpty raw)) -> Assert.Equal(blank, raw)
        | Error e -> Assert.Fail $"Wrong error for '{blank}'. {e.ToMessage()}"
        | Ok _ -> Assert.Fail $"Expected failure for '{blank}'; got success"

[<Fact>]
let ``REQ-CF-4.11 REQ-CF-5.17 REQ-SYS-1.1 a cancellation reason note with leading and trailing whitespace holds the trimmed text`` () =
    match "  billed in error  " |> CancellationReasonNote.create with
    | Ok note -> Assert.Equal("billed in error", note |> CancellationReasonNote.value)
    | Error e -> Assert.Fail $"Expected success; got {e.ToMessage()}"

// Placeholders committed before the Src was read (audit 2026-10-04a remediation)

[<Fact>]
let ``REQ-CF-5.20 REQ-SYS-1.1 an external invoice ID of only whitespace is rejected with a typed empty-ID error`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-CF-5.20 an external invoice ID of 101 characters is rejected with a typed too-long error, and one of exactly 100 characters is accepted and holds those 100 characters`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-SYS-1.1 for each CashFlow value an operator gives as text, every allowed value wrapped in whitespace parses to the same case as the bare value`` () =
    Assert.Fail "Not yet implemented"
