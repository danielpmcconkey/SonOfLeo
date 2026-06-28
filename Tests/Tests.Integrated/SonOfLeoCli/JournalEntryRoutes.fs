namespace Tests.Integrated.SonOfLeoCli

open Xunit
open Tests.Integrated
open Tests.Integrated.SonOfLeoCli.CliExecutor
open Model.UI.Json

module JournalEntryRoutes =

    // =============================================================================
    // PostNew route
    // =============================================================================

    [<Fact>]
    let ``REQ-JE-2.13 PostNew route creates a journal entry and returns it as JSON`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    let ``REQ-JE-2.12 PostNew route returns exit code 1 and error on invalid input`` () =
        Assert.Fail "not implemented"

    // =============================================================================
    // FetchById route
    // =============================================================================

    [<Fact>]
    let ``REQ-JE-3.2 FetchById route returns the correct entry as JSON`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    let ``REQ-JE-3.2 FetchById route returns exit code 1 for nonexistent ID`` () =
        Assert.Fail "not implemented"

    // =============================================================================
    // FetchByPeriod route
    // =============================================================================

    [<Fact>]
    let ``REQ-JE-3.3 FetchByPeriod route returns entries for a given period key`` () =
        Assert.Fail "not implemented"

    // =============================================================================
    // FetchByExternalReference route
    // =============================================================================

    [<Fact>]
    let ``REQ-JE-3.5 FetchByExternalReference route returns matching entries`` () =
        Assert.Fail "not implemented"

    // =============================================================================
    // Void route
    // =============================================================================

    [<Fact>]
    let ``REQ-JE-4.3 Void route voids an entry and returns it with void marker`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    let ``REQ-JE-4.6 Void route returns exit code 1 for already-voided entry`` () =
        Assert.Fail "not implemented"

    // =============================================================================
    // UpdateExternalReference route
    // =============================================================================

    [<Fact>]
    let ``REQ-JE-4.9 UpdateExternalReference route updates FI and value`` () =
        Assert.Fail "not implemented"

    // =============================================================================
    // AddExternalReference route
    // =============================================================================

    [<Fact>]
    let ``REQ-JE-4.10 AddExternalReference route appends a reference to an existing entry`` () =
        Assert.Fail "not implemented"

    // =============================================================================
    // AddComment route
    // =============================================================================

    [<Fact>]
    let ``REQ-JE-5.1 AddComment route attaches a comment to an entry`` () =
        Assert.Fail "not implemented"

    // =============================================================================
    // UpdateComment route
    // =============================================================================

    [<Fact>]
    let ``REQ-JE-5.3 UpdateComment route amends comment text`` () =
        Assert.Fail "not implemented"
