module Tests.Integrated.InterfaceBridge.PositionsRoutes

open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type PositionsRoutesTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-POS-11.1 a DimensionValue Create payload creates the value in the named dimension, and the return carries its dimension and name`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-2.1 a DimensionValue Create payload naming a dimension outside the seven is rejected with a typed error naming the text given`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.1 a DimensionValue Rename payload renames the value addressed by dimension and current name, and the return carries the new name`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.1 the DimensionValue List route returns every value of the named dimension ordered by name, and none of another dimension`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.2 a Security Create payload creates the Security with its ticker and Dimension Values, and the return carries each of them`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.2 a Security Update payload naming the name and one dimension changes those two, leaves the ticker and the other dimensions unchanged, and the return carries the result`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.2 REQ-SYS-6.1 a Security Update payload naming no field is rejected with a typed no-change error, and the Security is unchanged`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.2 REQ-SYS-6.1 a Security Update payload naming only the stored ticker succeeds, leaving the Security as stored`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.2 the Security List route returns every Security ordered by name, with its ticker and its value in each dimension`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.3 an InvestmentAccount Create payload creates the account, and the return carries every field with the linked ledger account's name beside its code`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-4.4 an InvestmentAccount Create payload with a tax treatment outside the four is rejected with a typed error naming the text given`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-4.7 an InvestmentAccount Create payload with no active begin is rejected with a typed error naming the missing active begin`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-4.8 REQ-NGUI-1.4 an InvestmentAccount Create payload whose ledger account code matches no account fails with a typed error naming the code, and no account is created`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.3 an InvestmentAccount Update payload naming the institution and the owners changes those two, leaves every other field unchanged, and the return carries the result`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.3 REQ-SYS-6.1 an InvestmentAccount Update payload naming no field is rejected with a typed no-change error, and the account is unchanged`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.3 REQ-SYS-6.1 an InvestmentAccount Update payload naming only the stored institution succeeds, leaving the account as stored`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.3 REQ-NGUI-1.6 the InvestmentAccount List route returns every account ordered by name, each linked ledger account's name beside its code`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.5 a Holding Create payload creates the Holding, and the return carries the account, Security and basis method`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.5 a Holding UpdateBasisMethod payload changes the basis method, and the return carries the new one`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.5 the Holding List route with no account filter returns every Holding ordered by account name then Security name`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.5 the Holding List route with an account filter returns every Holding of that account ordered by Security name, and none of another account`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-8.1 REQ-NGUI-1.6 the Holding FetchAsOf route returns the same accounts and lines as the holdings-as-of computation for the date given, each linked ledger account's name beside its code`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-7.1 REQ-POS-7.3 an AccountSnapshot Record payload with one new snapshot and one for a date already recorded records both and returns each as stored, the first marked new and the second marked a replacement`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-7.1 an AccountSnapshot Record payload whose second snapshot has a line outside the tolerance records neither snapshot, as read back after the route returns`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-QP-1.3 an AccountSnapshot Record payload with a seven-decimal quantity is rejected with a typed error naming the value, and nothing is recorded`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-QP-2.3 an AccountSnapshot Record payload with a seven-decimal price is rejected with a typed error naming the value, and nothing is recorded`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-6.7 an AccountSnapshot Record payload's six-decimal quantity and price come back in the return exactly as sent`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-7.4 an AccountSnapshot Delete payload deletes the snapshot for the account and date given, after which that account and date has no snapshot and the account's other snapshots remain`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-7.5 an AccountSnapshot Fetch payload returns the provenance, contribution basis and every line of the snapshot for the account and date given, exactly as recorded`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-7.5 an AccountSnapshot ListDates payload returns the account's snapshot dates and provenance in the range given, in date order, and no date outside the range`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.6 a Property Create payload creates the Property, and the return carries every field with each linked ledger account's name beside its code`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-9.4 a Property Create payload with no acquisition date is rejected with a typed error naming the missing acquisition date`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-9.5 a Property Create payload with no purchase basis is rejected with a typed error naming the missing purchase basis`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-9.7 REQ-NGUI-1.4 a Property Create payload whose asset account code matches no account fails with a typed error naming the code`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-9.8 REQ-NGUI-1.4 a Property Create payload whose mortgage account code matches no account fails with a typed error naming the code`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.6 a Property Update payload naming the purchase basis and the mortgage accounts changes those two, leaves every other field unchanged, and the return carries the result`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.6 REQ-SYS-6.1 a Property Update payload naming no field is rejected with a typed no-change error, and the Property is unchanged`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.6 REQ-SYS-6.1 a Property Update payload naming only the stored use succeeds, leaving the Property as stored`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.6 REQ-NGUI-1.6 the Property List route returns every Property ordered by name, each linked ledger account's name beside its code`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.8 a Valuation Record payload records the Valuation, and the return carries its date, value and basis`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.8 a Valuation Delete payload deletes the Valuation for the Property and date given, after which the Property has none on that date and its other Valuations remain`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.8 the Valuation List route returns every Valuation of the named Property in date order, and none of another Property`` () =
        Assert.Fail "Not yet implemented"
