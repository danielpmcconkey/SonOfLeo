module Tests.Integrated.CrossDomainOrchestration.AgreementUpdate

open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type AgreementUpdateTests(fixture: TestDataFixture) =

    // =========================================================================
    // REQ-CF-3.6 — a leg cannot debit and credit the same account
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-3.6 updating a leg's credit account to its debit account is rejected naming that account`` () =
        Assert.Fail "not implemented"

    // =========================================================================
    // REQ-CF-14.2 — a direction change must leave every Invoice's state valid
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-14.2 REQ-CF-5.10 changing an Outgo agreement with an InvoiceReceived Invoice to Income is rejected naming the Invoice`` () =
        Assert.Fail "not implemented"
