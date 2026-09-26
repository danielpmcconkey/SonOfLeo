module Tests.Integrated.CrossDomainOrchestration.CashFlowProjection

open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type CashFlowProjectionTests(fixture: TestDataFixture) =

    // =========================================================================
    // REQ-CF-8.3, 8.9, 8.10 — projected invoices count what is still owed
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-8.3 REQ-CF-8.9 a partly paid Outgo Invoice contributes only its outstanding amount to projected outflows`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-8.10 each projected Invoice carries both its amount and its outstanding amount`` () =
        Assert.Fail "not implemented"
