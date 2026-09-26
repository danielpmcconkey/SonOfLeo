module Tests.Integrated.CrossDomainOrchestration.InvoiceMatching

open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type InvoiceMatchingTests(fixture: TestDataFixture) =

    // =========================================================================
    // REQ-CF-13.2 — which linked lines are candidates for an Invoice
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-13.2 a linked line whose Payment has moved to Posted is not offered to a later open Invoice whose window covers its date`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-13.2 REQ-CF-13.7 a linked line whose Payment has moved to Posted is not an orphan when no open Invoice covers its date`` () =
        Assert.Fail "not implemented"

    [<Theory>]
    [<InlineData("Duplicate")>]
    [<InlineData("Ignored")>]
    member _.``REQ-CF-13.2 a linked line whose staged entry is Duplicate or Ignored gets no Payment from an open Invoice whose window covers its date``
        (status: string) =
        Assert.Fail "not implemented"

    [<Theory>]
    [<InlineData("Duplicate")>]
    [<InlineData("Ignored")>]
    member _.``REQ-CF-13.2 REQ-CF-13.7 a linked line whose staged entry is Duplicate or Ignored is not an orphan when no open Invoice covers its date``
        (status: string) =
        Assert.Fail "not implemented"
