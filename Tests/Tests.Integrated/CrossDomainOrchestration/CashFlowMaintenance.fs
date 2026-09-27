module Tests.Integrated.CrossDomainOrchestration.CashFlowMaintenance

open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type CashFlowMaintenanceTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-SYS-1.4 master agreement name filter returns every record containing search text with a literal %, _ or \ and no record lacking it`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-SYS-1.4 invoice blocker filter returns every record containing search text with a literal %, _ or \ and no record lacking it`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-SYS-6.2 updating an invoice by an ID no invoice holds fails with a typed not-found error naming the kind of record and the ID`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-SYS-6.2 updating a payment agreement link by an ID no link holds fails with a typed not-found error naming the kind of record and the ID`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-SYS-6.2 deleting a payment agreement link by an ID no link holds fails with a typed not-found error naming the kind of record and the ID`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-SYS-6.2 deleting a payment by an ID no payment holds fails with a typed not-found error naming the kind of record and the ID`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-SYS-6.3 creating a payment pointing at a staged line that does not exist fails with a typed not-found error naming the missing referent, and nothing is written or changed`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-SYS-6.3 creating a payment pointing at a journal entry line that does not exist fails with a typed not-found error naming the missing referent, and nothing is written or changed`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-SYS-6.3 creating an invoice on an instance that does not exist fails with a typed not-found error naming the missing referent, and nothing is written or changed`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-SYS-6.3 creating an invoice for a payment agreement that does not exist fails with a typed not-found error naming the missing referent, and nothing is written or changed`` () =
        Assert.Fail "not implemented"
