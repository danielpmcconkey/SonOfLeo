module Tests.Integrated.CrossDomainOrchestration.Cancellation

open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type CancellationTests(fixture: TestDataFixture) =
    // Placeholders named from the spec before the implementation was read (audit 2026-10-03a, brief Part A).

    [<Fact>]
    member _.``REQ-CF-4.11 cancelling an Instance with a reason note stores the Instance as cancelled carrying exactly that note, read back from a fresh context`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-CF-4.11 REQ-CF-5.17 an Instance and an Invoice that have never been cancelled read back with no cancellation reason note`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-CF-4.11 a request to cancel an Instance with a whitespace-only reason note is rejected with a typed error and the Instance and its Invoices read back uncancelled`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-CF-4.12 cancelling an Instance cancels every Invoice it holds, each carrying the Instance's reason note`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-CF-4.12 cancelling an Instance one of whose Invoices has a Payment is rejected with a typed error naming that Invoice, and neither the Instance nor any of its Invoices is cancelled`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-CF-4.13 a second cancellation of a cancelled Instance, with a different reason note, is rejected with a typed error and the Instance keeps its original note`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-CF-4.13 adding an Invoice to a cancelled Instance is rejected with a typed error and no Invoice is stored`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-CF-5.17 cancelling one Invoice of an Instance stores that Invoice as cancelled with its reason note and leaves the Instance and its other Invoices uncancelled`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-CF-5.18 cancelling an Invoice that has a Payment is rejected with a typed error naming the Invoice, and the Invoice reads back uncancelled`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-CF-5.19 a second cancellation of a cancelled Invoice, with a different reason note, is rejected with a typed error and the Invoice keeps its original note`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-CF-5.19 updating a cancelled Invoice is rejected with a typed error and every field of the Invoice reads back unchanged`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-CF-5.19 adding a Payment to a cancelled Invoice is rejected with a typed error and no Payment is stored`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-CF-5.19 REQ-CF-5.16 adding an Invoice for a Payment Agreement whose Invoice on the same Instance is cancelled is rejected with a typed error and no second Invoice is stored`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-CF-4.9 REQ-CF-9.10 an Instance with one Invoice cancelled becomes fulfilled when a Payment brings its other Invoice to FullyPaid`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-CF-4.9 REQ-CF-9.10 an Instance whose every Invoice is cancelled, with none FullyPaid, reads back not fulfilled`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-CF-4.14 REQ-CF-7.14 REQ-CF-14.10 the projection sweep's open Instances include an uncancelled, unfulfilled Instance created before the sweep ran, and exclude a cancelled one and a fulfilled one`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-CF-4.14 REQ-CF-13.9 REQ-CF-14.10 the open Instances returned by linkage and matching include an uncancelled, unfulfilled Instance and exclude a cancelled one and a fulfilled one`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-CF-8.2 REQ-CF-14.10 a cancelled Income Invoice adds nothing to its debit account's known inflows, while an uncancelled Invoice on the same Instance adds its outstanding amount`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-CF-8.3 REQ-CF-14.10 a cancelled Outgo Invoice adds nothing to its credit account's known outflows, while an uncancelled Invoice on the same Instance adds its outstanding amount`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-CF-8.4 REQ-CF-14.10 on an open Instance, a Payment Agreement whose Invoice is cancelled is not a bill to chase while a sibling Payment Agreement with no Invoice is`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-CF-8.4 REQ-CF-14.10 a Payment Agreement with no Invoice is reported as a bill to chase on an open Instance and not on a cancelled Instance of the same agreement`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-CF-13.1 REQ-CF-14.10 a linked line whose date only a cancelled Invoice covers gets no Payment and fails the run as an orphan with the no-open-Invoice reason`` () =
        Assert.Fail "Not yet implemented"
