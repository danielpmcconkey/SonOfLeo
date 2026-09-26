module Tests.Integrated.CrossDomainOrchestration.InvoiceStateByDirection

open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type InvoiceStateByDirectionTests(fixture: TestDataFixture) =

    // =========================================================================
    // REQ-CF-5.10 — invoice state must suit the agreement's flow direction
    // =========================================================================

    [<Theory>]
    [<InlineData("InvoiceGenerated")>]
    [<InlineData("InvoiceSent")>]
    member _.``REQ-CF-5.10 a new Instance on an Outgo agreement carrying an Invoice in an Income state is rejected naming the state``
        (state: string) =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-5.10 updating an Outgo agreement's Invoice to InvoiceSent is rejected naming the state`` () =
        Assert.Fail "not implemented"
