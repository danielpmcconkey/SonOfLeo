module Tests.Integrated.CrossDomainOrchestration.DerivedInvoiceState

open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type DerivedInvoiceStateTests(fixture: TestDataFixture) =

    // =========================================================================
    // REQ-CF-9.8 through 9.10 — a new Instance's derived state
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-9.8 REQ-CF-9.9 REQ-CF-9.10 a new Instance whose only Invoice carries a Posted Payment for its full amount is created FullyPaid, PostedToLedger and fulfilled`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-9.8 REQ-CF-9.9 REQ-CF-9.10 a new Instance whose Invoice carries a Posted Payment for part of its amount is created PartiallyPaid, PartiallyPosted and unfulfilled`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-9.8 REQ-CF-9.10 a new Instance whose Invoice has no Payments is created NotYetPaid and unfulfilled`` () =
        Assert.Fail "not implemented"
