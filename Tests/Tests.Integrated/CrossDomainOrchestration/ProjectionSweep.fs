module Tests.Integrated.CrossDomainOrchestration.ProjectionSweep

open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type ProjectionSweepTests(fixture: TestDataFixture) =

    // =========================================================================
    // REQ-CF-7.10, 7.11 — the lifecycle of an Invoice the sweep creates
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-7.10 every Invoice the sweep creates for an Outgo agreement is InvoiceExpected`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-7.11 every Invoice the sweep creates is NotYetPaid and NotHandled`` () =
        Assert.Fail "not implemented"
