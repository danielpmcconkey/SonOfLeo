namespace Tests.Integrated.Model.Ledger

open Xunit
open Model.Audit
open Model.Ledger.Journaling
open Utilities
open Utilities.DAL
open Utilities.ResultCE
open Tests.Integrated

module JournalEntryComment =

    // =============================================================================
    // Add comment
    // =============================================================================

    [<Fact>]
    let ``REQ-JE-5.1 constructNewAndSaveToDb attaches a comment to a journal entry`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    let ``REQ-JE-5.1 constructNewAndSaveToDb attaches a comment with a secondary JE link`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    let ``REQ-JE-5.2 constructNewAndSaveToDb generates UUID and sets timestamps`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    let ``REQ-JE-1.52 constructNewAndSaveToDb accepts null secondary JE ID`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    let ``REQ-JE-1.53 constructNewAndSaveToDb rejects secondary JE ID equal to primary`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    let ``REQ-JE-5.5 constructNewAndSaveToDb allows comment on a voided entry`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    let ``REQ-JE-5.5 constructNewAndSaveToDb allows comment when fiscal period is closed`` () =
        Assert.Fail "not implemented"

    // =============================================================================
    // Update comment
    // =============================================================================

    [<Fact>]
    let ``REQ-JE-5.3 updateComment amends the comment text`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    let ``REQ-JE-5.3 REQ-SYS-3.3 updateComment updates modified_at timestamp`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    let ``REQ-JE-5.6 updateComment does not change the primary JE link`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    let ``REQ-JE-5.3 updateComment rejects empty text`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    let ``REQ-JE-5.3 updateComment rejects whitespace-only text`` () =
        Assert.Fail "not implemented"

    // =============================================================================
    // Persistence fidelity
    // =============================================================================

    [<Fact>]
    let ``REQ-SYS-5.1 comment round-trips through persistence with all fields intact`` () =
        Assert.Fail "not implemented"
