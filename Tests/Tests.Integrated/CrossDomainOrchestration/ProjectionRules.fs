module Tests.Integrated.CrossDomainOrchestration.ProjectionRules

open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type ProjectionRulesTests(fixture: TestDataFixture) =

    [<Theory>]
    [<InlineData(1)>]
    [<InlineData(365)>]
    member _.``REQ-CF-8.1 for each of 1 and 365, a ProjectCashFlow payload with that horizon returns a projection rather than an error`` (horizon:int) =
        Assert.Fail "not implemented"

    [<Theory>]
    [<InlineData(0)>]
    [<InlineData(-1)>]
    [<InlineData(366)>]
    member _.``REQ-CF-8.1 for each of 0, -1 and 366, a ProjectCashFlow payload with that horizon is rejected with a typed error naming the horizon and the bound it breaks`` (horizon:int) =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-8.1 for an account with nonzero current balance, known inflows and known outflows, projected low equals current balance plus known inflows minus known outflows`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-8.1 a journal entry dated today on a managed cash account moves that account's current balance by its net amount, while one dated tomorrow does not move it`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-8.2 a partially paid Income Invoice on an unfulfilled Instance due within the horizon adds its outstanding amount, not its amount, to the known inflows of its Payment Agreement's debit account, and to no other account's inflows or outflows`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-8.2 an unpaid Income Invoice due on the horizon end adds its outstanding amount to known inflows, while one due the day after adds nothing`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-8.2 an unpaid Income Invoice due 60 days ago adds its outstanding amount to known inflows`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-8.2 a FullyPaid Income Invoice due within the horizon adds nothing to known inflows`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-8.4 an unfulfilled Instance within the horizon with one of its two Payment Agreements invoiced yields exactly one bill to chase, for the other, reporting its agreement name, payment agreement name, Instance date and cadence`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-8.4 an unfulfilled Instance with no Invoices dated on the horizon end yields a bill to chase for each of its Payment Agreements, while one dated the day after yields none`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-8.4 an unfulfilled Instance with no Invoices dated 60 days ago yields a bill to chase for each of its Payment Agreements`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-8.4 an unfulfilled Instance with an Invoice for every Payment Agreement yields no bill to chase for that Instance`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-8.4 a fulfilled Instance within the horizon, with one of its two Payment Agreements invoiced and that Invoice FullyPaid, yields no bill to chase for the other`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-8.5 two projections with the same horizon over unchanged data give identical results, including the order of accounts, Invoices and bills to chase`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-8.6 every active account with subtype Cash appears in the projection exactly once, including one with no Invoices, and no account of another subtype appears`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-8.6 an inactive account with subtype Cash does not appear in the projection`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-8.7 each projected account reports its code and name as stored, and lists exactly the Invoices that contributed to its known inflows and outflows`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-8.8 a ProjectCashFlow call adds, alters and removes no row of any Account, Master Agreement, Payment Agreement, Instance, Invoice, Payment or journal entry`` () =
        Assert.Fail "not implemented"
