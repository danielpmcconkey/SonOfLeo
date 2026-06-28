namespace Tests.Integrated.ModelOrchestrator

open Xunit
open Model.Audit
open Model.Ledger.Journaling
open ModelOrchestrator.JournalEntryVoiding
open Utilities
open Utilities.DAL
open Utilities.ResultCE
open Tests.Integrated

module JournalEntryVoiding =

    // =============================================================================
    // Void — happy path
    // =============================================================================

    [<Fact>]
    let ``REQ-JE-4.3 voidJournalEntryOrchestration sets voided_at on the entry`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    let ``REQ-JE-4.4 voidJournalEntryOrchestration attaches a reason comment to the voided entry`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    let ``REQ-JE-4.3 REQ-JE-4.4 voidJournalEntryOrchestration returns full aggregate with void marker and comment`` () =
        Assert.Fail "not implemented"

    // =============================================================================
    // Void — rejections
    // =============================================================================

    [<Fact>]
    let ``REQ-JE-4.5 voidJournalEntryOrchestration rejects void when fiscal period is closed`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    let ``REQ-JE-4.6 REQ-SYS-6.1 voidJournalEntryOrchestration rejects void on already-voided entry`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    let ``REQ-JE-4.4 voidJournalEntryOrchestration rejects void with empty reason`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    let ``REQ-JE-4.4 voidJournalEntryOrchestration rejects void with whitespace-only reason`` () =
        Assert.Fail "not implemented"

    // =============================================================================
    // Void — balance exclusion
    // =============================================================================

    [<Fact>]
    let ``REQ-JE-4.7 voided entry lines are excluded from account balance computation`` () =
        Assert.Fail "not implemented"
