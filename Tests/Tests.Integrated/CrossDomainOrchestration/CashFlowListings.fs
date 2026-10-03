module Tests.Integrated.CrossDomainOrchestration.CashFlowListings

open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type CashFlowListingsTests(fixture: TestDataFixture) =
    // Placeholders named from the spec before the implementation was read (audit 2026-10-03a, brief Part A).

    [<Fact>]
    member _.``REQ-CF-14.11 listing agreements returns every Master Agreement with its name, flow direction, counterparty, cadence, start date and end date, and every Payment Agreement with its name, debit and credit account code and name, expected amount and days-due, as stored`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-CF-14.11 listing agreements orders the Master Agreements by name and each agreement's Payment Agreements by name, whatever order they were created in`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-CF-14.12 fetching open Instances returns every open Instance across all Master Agreements, each with its Master Agreement's name, its Invoices and their Payments`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-CF-14.12 fetching open Instances orders them by Instance date, and Instances sharing a date by Master Agreement name, whatever order they were created in`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-CF-14.12 REQ-CF-4.14 fetching open Instances omits a fulfilled Instance and a cancelled Instance`` () =
        Assert.Fail "Not yet implemented"
