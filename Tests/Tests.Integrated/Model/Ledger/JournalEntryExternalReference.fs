namespace Tests.Integrated.Model.Ledger

open Xunit
open Model.Audit
open Model.Ledger.Journaling
open Utilities
open Utilities.DAL
open Utilities.ResultCE
open Tests.Integrated

module JournalEntryExternalReference =

    // =============================================================================
    // Update external reference
    // =============================================================================

    [<Fact>]
    let ``REQ-JE-4.9 updateFiAndReferenceText updates FI and value on existing reference`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    let ``REQ-JE-4.9 REQ-SYS-3.3 updateFiAndReferenceText updates modified_at timestamp`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    let ``REQ-JE-4.9 updateFiAndReferenceText rejects invalid FI — empty string`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    let ``REQ-JE-4.9 updateFiAndReferenceText rejects invalid reference — empty string`` () =
        Assert.Fail "not implemented"

    // =============================================================================
    // Add external reference to existing entry
    // =============================================================================

    [<Fact>]
    let ``REQ-JE-4.10 constructNewAndSaveToDb appends a reference to an existing entry`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    let ``REQ-JE-4.10 constructNewAndSaveToDb generates a unique UUID for the new reference`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    let ``REQ-JE-4.10 appending a reference is permitted on a voided entry`` () =
        Assert.Fail "not implemented"

    // =============================================================================
    // Persistence fidelity
    // =============================================================================

    [<Fact>]
    let ``REQ-SYS-5.1 external reference round-trips through persistence with all fields intact`` () =
        Assert.Fail "not implemented"
