module Tests.Integrated.CrossDomainOrchestration.NetWorthPreLedger

open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type NetWorthPreLedgerTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-RPT-8.1 REQ-RPT-8.5 net worth as of the day before the earliest fiscal period's start date returns a result marked pre-ledger, and as of that start date returns one marked not pre-ledger`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-8.1 net worth as of a date in a gap between two fiscal periods fails with a typed error naming the date`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-8.2 REQ-RPT-8.7 net worth on a pre-ledger date is the hand-derived sum of the latest pre-ledger balances of the unlinked Asset accounts, the holdings' market values and the owned Properties' values, less the latest pre-ledger balances of the Liability accounts`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-8.7 on a pre-ledger date, an account with several pre-ledger balances takes the latest one on or before the date, ignores one dated after it, and carries that balance's date`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-8.7 on a pre-ledger date, an account whose pre-ledger balance is 0.00 is listed at 0.00, and an account with no pre-ledger balance on or before the date is not listed at all`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-8.7 on a pre-ledger date a parent account and its child, each with a pre-ledger balance, are each listed at their own balance, not summed`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-8.7 on a date within a fiscal period, an account that carries pre-ledger balances is listed at its ledger balance and no pre-ledger balance figures in the result`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-8.3 REQ-RPT-8.7 on a pre-ledger date a Property's equity is its value less its mortgage account's latest pre-ledger balance on or before the date`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-8.5 on a pre-ledger date the result gives every listed asset, liability and mortgage account the date of the pre-ledger balance it came from, and on a fiscal-period date it gives no balance dates`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-8.8 on a pre-ledger date on which only investments are absent, investments is named as the one absent component and totals 0.00`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-8.8 on a pre-ledger date on which the only Liability balance is an owned Property's mortgage, liabilities is named absent with counted ledger assets and investments, each totalling 0.00, and mortgages of owned Properties and property values are present`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-8.8 on a pre-ledger date on which property values are the only contributor, the other four components are named absent, each totalling 0.00`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-8.8 on a pre-ledger date before any record, all five components are named absent, each totalling 0.00, and net worth is 0.00`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-8.8 on a date within a fiscal period, Asset and Liability accounts whose ledger balance is 0.00 are listed at 0.00 and no component is named absent`` () =
        Assert.Fail "Not yet implemented"
