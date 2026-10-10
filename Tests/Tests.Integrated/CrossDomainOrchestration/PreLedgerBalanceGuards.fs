module Tests.Integrated.CrossDomainOrchestration.PreLedgerBalanceGuards

open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type PreLedgerBalanceGuardsTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-POS-15.7 creating a fiscal period whose start date is the date of a pre-ledger balance is rejected with a typed error naming the period and that date as both the earliest and latest balance date, and no fiscal period is created`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-15.7 creating a fiscal period whose start date is the day after the latest pre-ledger balance date succeeds and the fiscal period exists`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-15.7 creating a fiscal period that starts after some pre-ledger balance dates and on or before others is rejected with a typed error naming the period and the earliest and latest of the balance dates on or after its start, none of those before it, and no fiscal period is created`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-15.7 creating a fiscal period that starts before every pre-ledger balance date, with the earliest and latest balances on different ledger accounts, is rejected with a typed error naming the period and the earliest and latest of all balance dates, no fiscal period is created and every pre-ledger balance is unchanged`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-15.7 ensuring fiscal periods exist for a range of months whose first missing month starts on or before a pre-ledger balance date is rejected with a typed error naming that first missing month's period and the earliest and latest such balance dates, and no fiscal period in the range is created`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-15.8 creating an Investment Account linked to a ledger account that carries pre-ledger balances is rejected with a typed error naming the account code and its earliest and latest balance dates, and no Investment Account is created`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-15.8 updating an Investment Account to link a ledger account that carries pre-ledger balances on two dates is rejected with a typed error naming the account code and its earliest and latest balance dates, and the Investment Account's link is unchanged`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-15.8 creating a Property whose asset accounts include a ledger account that carries pre-ledger balances is rejected with a typed error naming the account code and its earliest and latest balance dates, and no Property is created`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-15.8 updating a Property's asset accounts to a set that adds a ledger account carrying pre-ledger balances on two dates is rejected with a typed error naming the account code and its earliest and latest balance dates, and the Property's asset accounts are unchanged`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-15.8 creating a Property whose mortgage accounts include a ledger account carrying pre-ledger balances succeeds and the Property is stored with that mortgage account`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-15.8 updating a Property's mortgage accounts to add a ledger account carrying pre-ledger balances succeeds and the Property's mortgage accounts include it`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-15.8 once a ledger account's only pre-ledger balance is deleted, linking it to an Investment Account succeeds while other ledger accounts still carry pre-ledger balances, and the Investment Account is linked to it`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-15.8 once a ledger account's only pre-ledger balance is deleted, adding it to a Property's asset accounts succeeds while other ledger accounts still carry pre-ledger balances, and the Property's asset accounts include it`` () =
        Assert.Fail "Not yet implemented"
