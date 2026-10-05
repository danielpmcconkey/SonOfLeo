module Tests.Isolated.Model.CashFlow.CashFlowComponent

open App.Utility.IAppError
open Business.General
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

[<Fact>]
let ``REQ-CF-5.20 REQ-SYS-1.1 an external invoice ID of only whitespace is rejected with a typed empty-ID error`` () =
    match "   " |> ExternalInvoiceId.create with
    | Error (AsError (CashflowExternalInvoiceIdIsEmpty raw)) -> Assert.Equal("   ", raw)
    | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
    | Ok _ -> Assert.Fail "Expected failure; got success"

[<Fact>]
let ``REQ-CF-5.20 an external invoice ID of 101 characters is rejected with a typed too-long error, and one of exactly 100 characters is accepted and holds those 100 characters`` () =
    let atLimit = System.String('x', 100)
    let overLimit = System.String('x', 101)
    match overLimit |> ExternalInvoiceId.create with
    | Error (AsError (CashflowExternalInvoiceIdTooLong (raw, limit))) ->
        Assert.Equal(overLimit, raw)
        Assert.Equal(100, limit)
    | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
    | Ok _ -> Assert.Fail "Expected failure; got success"
    match atLimit |> ExternalInvoiceId.create with
    | Ok eid -> Assert.Equal(atLimit, eid |> ExternalInvoiceId.value)
    | Error e -> Assert.Fail $"Expected success; got {e.ToMessage()}"

(* The CashFlow values an operator types: flow direction and invoice state, and a cadence's week day and month.
   Payment state and posted state are never caller input (REQ-CF-9.11), and a blocker arrives as a tagged union. *)
[<Fact>]
let ``REQ-SYS-1.1 for each CashFlow value an operator gives as text, every allowed value wrapped in whitespace parses to the same case as the bare value`` () =
    let padded (text: string) = $" \t  {text} \t "
    let parsePadded (fromString: string -> Result<'a, IAppError>) (names: string list) =
        names |> List.map (fun name ->
            match padded name |> fromString with
            | Ok parsed -> parsed
            | Error e -> failwith $"'{padded name}' was rejected: {e.ToMessage()}")
    Assert.Equal<FlowDirection list>([ Income; Outgo ], parsePadded FlowDirection.fromString [ "Income"; "Outgo" ])
    Assert.Equal<InvoiceState list>(
        [ InvoiceGenerated; InvoiceSent; InvoiceExpected; InvoiceReceived ],
        parsePadded InvoiceState.fromString [ "InvoiceGenerated"; "InvoiceSent"; "InvoiceExpected"; "InvoiceReceived" ])
    Assert.Equal<Cadence.WeekDay list>(
        [ Cadence.Sunday; Cadence.Monday; Cadence.Tuesday; Cadence.Wednesday; Cadence.Thursday; Cadence.Friday
          Cadence.Saturday ],
        parsePadded Cadence.WeekDay.fromString
            [ "Sunday"; "Monday"; "Tuesday"; "Wednesday"; "Thursday"; "Friday"; "Saturday" ])
    Assert.Equal<Cadence.Month list>(
        [ Cadence.January; Cadence.February; Cadence.March; Cadence.April; Cadence.May; Cadence.June
          Cadence.July; Cadence.August; Cadence.September; Cadence.October; Cadence.November; Cadence.December ],
        parsePadded Cadence.Month.fromString
            [ "January"; "February"; "March"; "April"; "May"; "June"; "July"; "August"; "September"; "October"
              "November"; "December" ])
