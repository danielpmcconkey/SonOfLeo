module Tests.Integrated.CrossDomainOrchestration.PaymentAgreementLinkGuard

open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type PaymentAgreementLinkGuardTests(fixture: TestDataFixture) =
    // Placeholders named from the spec before the implementation was read (audit 2026-10-03a, brief Part A).

    [<Fact>]
    member _.``REQ-CF-12.9 re-pointing a Payment Agreement Link whose staged line a Payment references is rejected with a typed error naming the Payment, and the link still names its original Payment Agreement`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-CF-12.9 deleting a Payment Agreement Link whose staged line a Payment references is rejected with a typed error naming the Payment, and the link still exists`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-CF-12.9 REQ-CF-14.6 deleting the Payment that blocked a link's re-point or delete removes that link along with the Payment`` () =
        Assert.Fail "Not yet implemented"
