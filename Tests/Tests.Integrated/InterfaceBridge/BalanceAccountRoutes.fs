module Tests.Integrated.InterfaceBridge.BalanceAccountRoutes

open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type BalanceAccountRoutesTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-POS-16.7 a BalanceAccount Create payload creates the Balance Account, and the return carries its name, kind, institution and owners' names`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-16.7 a BalanceAccount Update payload naming only the institution changes it, leaves every other field unchanged, and the return carries the result`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-16.7 the BalanceAccount List route returns every Balance Account ordered by name with its kind, institution and owners' names`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-16.10 a BalanceAccount Delete payload removes only the Balance Account named: the BalanceAccount List route no longer returns it and still returns every other`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-16.8 a BalanceAccountBalance Record payload records each Balance and returns each with its Balance Account's name, the date, the amount and whether it replaced one`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-16.8 a BalanceAccountBalance Record payload whose second Balance is dated after today records neither, as read back after the route returns`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-16.9 a BalanceAccountBalance Delete payload deletes the Balance for the Balance Account and date given, after which the BalanceAccountBalance List route no longer returns it and still returns the account's other Balances`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.6 a Property Create payload naming two mortgage Balance Accounts creates the Property linked to both, and the return and the Property List route each carry their names`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-8.6 REQ-RPT-8.5 the NetWorth route in data-only mode returns the Balance Accounts with their owners' names, the cash and debts totals and the absent components the computation gives`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-8.6 REQ-RPT-8.5 the rendered net worth shows a Balance Account section and the cash and debts totals, and the totals it shows add up to the net worth it shows`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-10.4 the NetWorthHistory route in data-only mode for a range spanning the earliest fiscal period's start returns the same points, totals including cash and debts, and absent components as the net worth history computation`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-10.4 the rendered net worth history has one row per month-end and one column per total, including cash and debts, and marks no point as pre-ledger`` () =
        Assert.Fail "Not yet implemented"

