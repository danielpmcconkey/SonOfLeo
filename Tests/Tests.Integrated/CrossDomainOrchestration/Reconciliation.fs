namespace Tests.Integrated.CrossDomainOrchestration

open Xunit
open Tests.Helpers

[<Collection("SharedTestData")>]
type ReconciliationTests(fixture: TestDataFixture) =

    // =========================================================================
    // REQ-RPT-4.1 – 4.3, 4.5 — the read-only reconciliation report
    // =========================================================================

    [<Fact>]
    member _.``REQ-RPT-4.1 each input row comes back once with account code, account name, as-of date, external balance, ledger net balance and delta`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-RPT-4.1 REQ-RPT-4.5 the delta is external minus ledger, and a non-zero delta is returned as data, not an error`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-RPT-4.2 a voided journal entry contributes nothing to the ledger balance`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-RPT-4.2 an entry dated on the as-of date counts and one dated after it does not`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-RPT-4.2 the ledger balance is debits minus credits for a debit-normal account and credits minus debits for a credit-normal one`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-RPT-4.2 a parent account's ledger balance includes its descendants`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-RPT-4.2 rows in one request with different as-of dates are each computed as of their own date`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-RPT-4.3 an account code that resolves to no account fails with a typed error naming the code`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-RPT-4.3 an account code given twice fails with a typed error naming the code`` () =
        Assert.Fail "not implemented"

    // =========================================================================
    // REQ-RPT-4.4, 4.6 — the shadow reconciliation, always rolled back
    // =========================================================================

    [<Fact>]
    member _.``REQ-RPT-4.4 REQ-RPT-4.6 the shadow reconciliation's ledger balance includes every postable staged entry as if it were posted`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-RPT-4.4 after the shadow reconciliation the ledger and staging hold exactly what they held before`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-RPT-4.4 a postable entry that shadow post would reject fails the shadow reconciliation with the same error`` () =
        Assert.Fail "not implemented"
