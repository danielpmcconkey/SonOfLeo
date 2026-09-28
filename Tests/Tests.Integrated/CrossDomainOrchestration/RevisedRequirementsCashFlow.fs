module Tests.Integrated.CrossDomainOrchestration.RevisedRequirementsCashFlow

open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type RevisedRequirementsCashFlowTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-CF-8.3 for each outgo invoice (due on the horizon end, overdue before today, fully paid, on a fulfilled instance, due the day after the horizon end, on an agreement crediting another account), the account's known outflows include its outstanding amount only for the first two`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-8.3 an income invoice otherwise meeting every known-outflow condition for an account is not among its known outflows`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-8.9 an invoice whose payments exceed its amount has an outstanding amount of zero`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-14.2 for each field (name, cadence with its next-instance date, counterparty, start date, end date, memo), updating a master agreement's field stores the new value and leaves the others unchanged`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-14.2 updating a master agreement's flow direction when every invoice's state is valid for the new direction stores the new direction`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-14.2 an update to a master agreement that sets every field to its current value is rejected with the no-op error and the agreement is unchanged`` () =
        Assert.Fail "not implemented"
