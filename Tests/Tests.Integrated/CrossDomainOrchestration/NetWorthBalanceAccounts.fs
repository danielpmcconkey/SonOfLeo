module Tests.Integrated.CrossDomainOrchestration.NetWorthBalanceAccounts

open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type NetWorthBalanceAccountsTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-RPT-8.1 REQ-RPT-8.7 net worth as of the day before the earliest fiscal period's start lists no Asset or Liability account, and as of that start date lists them at their ledger balances`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-8.2 REQ-RPT-8.7 net worth on a date before the earliest fiscal period is the hand-derived holdings' market values plus owned Properties' values plus Cash Balance Account balances, less Debt Balance Account balances, with no ledger account contributing`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-8.2 REQ-POS-16.6 a Cash Balance Account adds its latest Balance on or before the date to net worth and a Debt Balance Account subtracts its own, each ignoring a Balance dated after the date`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-8.3 REQ-RPT-8.4 on a date before the earliest fiscal period a primary residence's equity is its value less its mortgage Balance Account's balance, and investable wealth is net worth less that equity, not less the value`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-8.3 REQ-RPT-8.5 a Debt Balance Account that is a mortgage of a Property owned on the date is listed under that Property and counted in the owned-property mortgages total, not among the debts`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-8.5 a Debt Balance Account that is a mortgage of a Property not owned on the date is listed among the Balance Accounts and counted in the debts total`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-8.5 each listed Balance Account carries its name, kind, owners' names, balance and the date of the Balance it came from, and Balance Accounts are listed by name from a fixture set whose stored order differs from it`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-8.5 REQ-RPT-8.8 on a date on which every Balance Account's balance is 0.00, none is listed, cash and debts each total 0.00, and neither is named absent`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-8.5 a Property's mortgage Balance Account whose balance is 0.00 is not listed under the Property and adds nothing to its mortgages, and one with a nonzero balance is listed`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-8.8 on a date before any Balance is recorded, no Balance Account is counted or listed, and cash and debts are both named absent, each totalling 0.00`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-8.8 on a date on which the only Debt balance is an owned Property's mortgage Balance Account, debts is named absent and totals 0.00, and mortgages of owned Properties is present`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-8.8 on a date before any record, all seven components are named absent in the order counted ledger assets, investments, property values, cash, liabilities, debts, mortgages of owned Properties, each totalling 0.00, and net worth is 0.00`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-8.5 with all seven totals nonzero, counted ledger assets plus investments plus property values plus cash, less liabilities, debts and owned-property mortgages, equals the net worth the computation returns`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-10.2 a range spanning the earliest fiscal period's start gives points before it with no counted ledger assets or liabilities, named absent, and points from it on with both present, each point's nine totals hand-derived`` () =
        Assert.Fail "Not yet implemented"

