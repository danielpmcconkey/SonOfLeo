module Tests.Integrated.CrossDomainOrchestration.PaymentsToPosted

open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type PaymentsToPostedTests(fixture: TestDataFixture) =

    // =========================================================================
    // REQ-CF-10.1 through 10.3 — which Payments move, and to what
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-10.1 REQ-CF-10.3 a Staged Payment whose staged line records the journal entry line it produced gets that journal entry line ID and keeps its staged line ID`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-10.1 the transition moves every Staged Payment whose staged line records a journal entry line, across every Invoice and agreement, in one run`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-10.2 a Staged Payment whose staged line records no journal entry line is left Staged, with no journal entry line ID, and is not listed by the transition`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-10.1 a Posted Payment that also carries a staged line ID, whose staged line records a journal entry line, is not changed and not listed by the transition`` () =
        Assert.Fail "not implemented"

    // =========================================================================
    // REQ-CF-10.4 — the Invoice's posted state follows
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-10.4 when the transition moves every Staged Payment of a FullyPaid, NotHandled Invoice, the Invoice's posted state becomes PostedToLedger`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-10.4 when the transition moves the last Staged Payment of a FullyPaid, PartiallyPosted Invoice, the Invoice's posted state becomes PostedToLedger`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-10.4 when the transition moves some but not all of a FullyPaid, NotHandled Invoice's Staged Payments, the Invoice's posted state becomes PartiallyPosted`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-10.4 when the transition moves every Staged Payment of a PartiallyPaid, NotHandled Invoice, the Invoice's posted state becomes PartiallyPosted, not PostedToLedger`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-10.4 an Invoice none of whose Payments the transition moves keeps its posted state`` () =
        Assert.Fail "not implemented"

    // =========================================================================
    // REQ-CF-10.5 — idempotent
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-10.5 a second transition run straight after the first moves no Payment, lists nothing, and leaves every Payment's pointers and every Invoice's posted state as the first run left them`` () =
        Assert.Fail "not implemented"

    // =========================================================================
    // REQ-CF-10.7 — what the transition returns
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-10.7 the transition lists every Payment it moved, each with its agreement name, its Invoice's amount and the journal entry line ID it now points at, and no Payment it did not move`` () =
        Assert.Fail "not implemented"
