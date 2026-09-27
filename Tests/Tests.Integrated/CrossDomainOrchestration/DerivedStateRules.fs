module Tests.Integrated.CrossDomainOrchestration.DerivedStateRules

open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type DerivedStateRulesTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-CF-9.1 an Invoice whose Payments sum to one cent less than its amount derives PartiallyPaid, not FullyPaid`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-9.1 an Invoice whose Payments sum to more than its amount derives PartiallyPaid, not FullyPaid`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-9.2 an Invoice whose every Payment is Posted but whose Payments sum to less than its amount derives PartiallyPosted, not PostedToLedger`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-9.3 adding a Payment that would make an Invoice carrying a blocker FullyPaid is rejected with a typed error, no Payment is written, and the Invoice keeps its prior payment state`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-9.3 reducing a blocked Invoice's amount to equal its Payment sum is rejected and the Invoice keeps its prior amount`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-9.4 setting a blocker on a FullyPaid Invoice is rejected with a typed error, and the Invoice keeps no blocker`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-9.5 REQ-CF-9.10 deleting an Invoice's only Payment returns it to NotYetPaid and NotHandled and its Instance to unfulfilled, not PartiallyPaid`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-9.6 a FullyPaid Invoice with one Posted and one Staged Payment derives PartiallyPosted, not PostedToLedger`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-9.7 an Invoice with Payments but none Posted derives NotHandled, not PartiallyPosted, whether PartiallyPaid or FullyPaid`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-9.10 deleting one Payment from a FullyPaid, PostedToLedger Invoice returns it to PartiallyPaid and PartiallyPosted and its Instance to unfulfilled, with no call other than the delete`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-9.10 adding the Payment that completes an existing Invoice's amount makes it FullyPaid and its Instance fulfilled, with no call other than the add`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-9.10 an Instance with one FullyPaid and one PartiallyPaid Invoice is not fulfilled`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-9.10 a Payment change whose composite validation fails leaves the Payment, the Invoice's derived states and the Instance's is-fulfilled exactly as before`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-9.11 a CreateInstance payload supplying FullyPaid, PostedToLedger and is-fulfilled true for an Invoice with no Payments stores NotYetPaid, NotHandled and unfulfilled when re-fetched`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-9.11 an UpdateInvoice payload supplying FullyPaid and PostedToLedger for an Invoice with no Payments leaves it NotYetPaid and NotHandled when re-fetched`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-9.11 a CreatePayment payload supplying payment state and posted state for its Invoice leaves the Invoice with the states its Payments derive when re-fetched`` () =
        Assert.Fail "not implemented"
