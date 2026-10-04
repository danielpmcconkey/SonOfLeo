module Tests.Integrated.CrossDomainOrchestration.NetWorth

open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type NetWorthTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-RPT-8.1 net worth as of a date outside every fiscal period fails with a typed error naming the date`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-8.2 net worth is the fixture's unlinked Asset account balances plus its holdings' market values plus its owned Properties' values less its Liability account balances, each derived by hand from fixture data with voided entries and entries after the date excluded`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-8.2 an Asset account linked to an Investment Account is absent from the counted ledger assets, its cost balance is not in net worth, and the linked account's market value is`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-8.2 an Asset account linked to a Property is absent from the counted ledger assets, its cost balance is not in net worth, and the Property's value is`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-8.2 a parent Asset account is counted at its own balance, without its children's balances rolled in`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-8.2 a Property not owned on the date contributes nothing to net worth`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-8.3 a Property's equity is its value on the date less the as-of balance of each of its mortgage accounts`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-8.2 REQ-RPT-8.3 a mortgage account's balance is subtracted from net worth exactly once, and is listed under its Property rather than among the Liability accounts`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-8.4 investable wealth is net worth less the primary residence's equity, its value less its mortgage balance, and not less its value`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-8.4 as of a date on which no primary residence is owned, investable wealth equals net worth`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-8.5 the result carries the as-of date, and each counted Asset account and each non-mortgage Liability account with its code, name and balance`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-8.5 each included Investment Account carries its name, owners' names, account group, tax treatment, snapshot date, provenance, total market value and contribution basis`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-8.5 each owned Property carries its name, use, owners' names, value, the date of the Valuation its value came from or the indication that it is the purchase basis, each mortgage account's code, name and balance, and its equity`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-8.5 the totals of counted ledger assets, investments, property values and liabilities each equal the hand-summed rows they total`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-8.5 investment market value totalled by tax treatment and by account group gives, for each value present among the included accounts, the hand-summed market value of those accounts`` () =
        Assert.Fail "Not yet implemented"
