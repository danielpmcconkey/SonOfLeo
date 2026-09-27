namespace Tests.Integrated.CrossDomainOrchestration

open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type BalanceSheetIntegrityTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-RPT-5.1 REQ-RPT-5.2 a journal entry line dated before the as-of date adds its amount to integrity total debits or total credits by its line type, leaves the other total unchanged, and moves its account type's net balance by its amount in that type's normal-balance direction``() =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-RPT-5.1 REQ-RPT-5.2 a journal entry line dated on the as-of date adds its amount to integrity total debits or total credits by its line type, leaves the other total unchanged, and moves its account type's net balance by its amount in that type's normal-balance direction``() =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-RPT-5.1 REQ-RPT-5.2 a journal entry line dated after the as-of date leaves integrity total debits, total credits and every account-type net balance unchanged``() =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-RPT-5.1 REQ-RPT-5.2 a voided journal entry leaves integrity total debits, total credits and every account-type net balance unchanged``() =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-RPT-5.1 integrity reports debits equal to credits as true when non-zero total debits equal total credits``() =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-RPT-5.1 integrity reports debits equal to credits as false when total debits differ from total credits``() =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-RPT-5.2 each account-type net balance is its debits minus credits for Asset and Expense and its credits minus debits for Liability, Equity and Revenue``() =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-RPT-5.2 integrity net income is the Revenue net balance minus the Expense net balance``() =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-RPT-5.2 integrity residual is Assets minus the sum of Liabilities, Equity and net income``() =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-RPT-5.3 integrity returns unequal debits and credits as data carrying both totals rather than as an error``() =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-RPT-5.3 integrity returns a non-zero residual as data carrying that residual rather than as an error``() =
        Assert.Fail "not implemented"
