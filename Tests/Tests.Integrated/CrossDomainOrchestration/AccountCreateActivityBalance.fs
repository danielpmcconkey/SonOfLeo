module Tests.Integrated.CrossDomainOrchestration.AccountCreateActivityBalance

open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type AccountCreateActivityBalanceTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-AC-2.22 a create-account payload whose parent is given by the code of an existing Account stores the new Account with that Account as its parent`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-AC-2.22 a create-account payload whose parent code matches no Account is rejected with a typed error naming the code, and nothing is stored`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-AC-2.23 a create-account payload with an active end stores the Account with that active end`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-AC-2.23 a create-account payload with no active end stores the Account with a null active end`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-AC-3.11 for each of retrieve, update, deactivate, activity and balance, an operation naming an account code that matches no Account fails with a typed error naming the code`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-AC-3.11 a balance query whose code list mixes existing codes with one that matches no Account fails with a typed error naming that code and returns no balances`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-AC-3.11 an activity query whose account code or parent account code filter matches no Account fails with a typed error naming the code`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-AC-3.12 an activity row for a journal entry line carries the account's code, name, type, subtype, parent code and external reference, and the line's ID, amount, line type, memo, created and modified instants, and its journal entry's ID, entry date, description, source and voided-at instant, each as stored`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-AC-3.12 an Account with several journal entry lines yields exactly one activity row per line and no rows for other Accounts' lines`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-AC-3.12.1 for each activity filter (account code, fiscal period key, entry date range, source, account type, account subtype, parent account code, journal entry ID, line amount, description, unvoided-only), a query with only that filter, over rows that include some that fail it, returns exactly the rows that satisfy it`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-AC-3.12.1 an entry date range filter returns lines dated on its first and last days and excludes lines dated the day before and the day after`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-AC-3.12.1 a description filter matches a case-sensitive substring: it returns a line whose description contains the text and not one that contains it in another case`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-AC-3.12.1 a source filter returns lines whose source equals it and not lines whose source merely contains it or differs in case`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-AC-3.12.1 filters given together return exactly the rows that satisfy all of them, excluding rows that satisfy all but one`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-AC-3.12.2 without the unvoided-only flag, a line of a voided journal entry is returned carrying its voided-at instant`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-AC-3.12.2 with the unvoided-only flag set, lines of voided journal entries are omitted and lines of unvoided entries are still returned`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-AC-3.12.3 an Account with no journal entry lines is returned exactly once, carrying its account fields with no line detail, both with no filters and with only account-level filters or the unvoided-only flag applied`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-AC-3.12.3 for each filter on journal entry properties (fiscal period, date range, source, journal entry ID, amount, description), an Account with no lines is not returned when that filter is applied`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-AC-3.12.4 for each of account code, entry date and amount, ascending and descending, activity rows stored in an order matching neither direction come back in that sort order`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-AC-3.13 a balance query for several account codes returns exactly one result per code given`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-AC-3.13 a balance query for a debit-normal Account returns its code, name, total debits and total credits equal to the sums of its debit and credit lines, and a net balance of debits minus credits`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-AC-3.13 a balance query for a credit-normal Account returns its code, name, total debits and total credits equal to the sums of its debit and credit lines, and a net balance of credits minus debits`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-AC-3.13.1 an Account's totals and balance equal the sums of its unvoided lines alone when it also has lines of voided journal entries`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-AC-3.13.1 with an as-of date, a line dated on that date counts and a line dated the day after does not`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-AC-3.13.1 without an as-of date, a balance counts every unvoided line whatever its entry date`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-AC-3.13.1 an Account whose only lines are voided or dated after the as-of date has zero total debits, zero total credits and a zero balance`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-AC-3.13.2 a parent Account's balance counts only its own lines, not those of its children or deeper descendants`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-AC-3.13.3 a balance query with an empty list of account codes fails with a typed error`` () =
        Assert.Fail "not implemented"
