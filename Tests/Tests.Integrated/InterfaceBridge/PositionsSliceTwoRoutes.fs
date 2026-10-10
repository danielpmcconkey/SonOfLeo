module Tests.Integrated.InterfaceBridge.PositionsSliceTwoRoutes

open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type PositionsSliceTwoRoutesTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-POS-6.13 REQ-POS-7.5 an AccountSnapshot Record payload whose line carries three lots, two identical and one with a six-decimal quantity, returns them, and an AccountSnapshot Fetch payload gives them back, in the order sent with every quantity exactly as sent`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-8.3 the Holding FetchAsOf route returns each line's lots in the order recorded, with each acquired date, quantity and cost basis as recorded`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-13.1 REQ-POS-13.4 an InvestmentActivity Record payload with ranges for two accounts records both and returns, for each account and range, a removed count equal to the activities it replaced and a recorded count equal to the activities sent`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-13.5 REQ-POS-12.9 an InvestmentActivity List payload returns the activities in the range in the order they were sent, each six-decimal quantity and price exactly as sent`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-13.1 an InvestmentActivity Record payload whose second account's activity is invalid records nothing for the first account, as read back after the route returns`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-12.2 an InvestmentActivity Record payload with the kind 'purchase' is rejected with a typed error naming the text given, and nothing is recorded`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-12.1 REQ-POS-11.9 an InvestmentActivity Record payload naming an account that matches no Investment Account fails with a typed not-found error naming the kind of record and the name, and nothing is recorded`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-14.1 an InvestmentAccount RollForward payload for the non-balancing account returns the same rows, including each non-zero difference, as the roll-forward computation for that account and dates`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-15.4 REQ-NGUI-1.6 a PreLedgerBalance Record payload records each balance and returns each with its account code, the account's name beside it, the date, the balance and whether it replaced one`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-15.4 REQ-NGUI-1.5 a PreLedgerBalance Record payload whose account code matches no account fails with a typed error naming the code, and nothing is recorded`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-15.4 a PreLedgerBalance Record payload whose second balance is invalid records neither, as read back after the route returns`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-15.5 a PreLedgerBalance Delete payload deletes the balance for the account and date given, after which the PreLedgerBalance List route no longer returns it and still returns the account's other balances`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-15.6 REQ-NGUI-1.6 the PreLedgerBalance List route with and without an account filter returns the balances in the range ordered by account code then date, each account's name beside its code`` () =
        Assert.Fail "Not yet implemented"
