namespace Tests.Isolated.Model.Ledger

open Xunit
open Model.Ledger.Journaling

module JournalEntryExternalReference =

    // =============================================================================
    // JournalRefFinancialInstitution
    // =============================================================================

    [<Fact>]
    let ``REQ-JE-1.42 JournalRefFinancialInstitution.create rejects empty string`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    let ``REQ-JE-1.42 JournalRefFinancialInstitution.create rejects whitespace-only string`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    let ``REQ-JE-1.49 JournalRefFinancialInstitution.create rejects string exceeding 100 characters`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    let ``REQ-JE-1.49 JournalRefFinancialInstitution.create accepts string at exactly 100 characters`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    let ``REQ-SYS-1.1 JournalRefFinancialInstitution.create trims whitespace`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    let ``REQ-JE-1.42 JournalRefFinancialInstitution.create accepts valid string`` () =
        Assert.Fail "not implemented"

    // =============================================================================
    // JournalExternalReferenceText
    // =============================================================================

    [<Fact>]
    let ``REQ-JE-1.44 JournalExternalReferenceText.create rejects empty string`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    let ``REQ-JE-1.44 JournalExternalReferenceText.create rejects whitespace-only string`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    let ``REQ-JE-1.45 JournalExternalReferenceText.create rejects string exceeding 100 characters`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    let ``REQ-JE-1.45 JournalExternalReferenceText.create accepts string at exactly 100 characters`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    let ``REQ-SYS-1.1 JournalExternalReferenceText.create trims whitespace`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    let ``REQ-JE-1.44 JournalExternalReferenceText.create accepts valid string`` () =
        Assert.Fail "not implemented"
