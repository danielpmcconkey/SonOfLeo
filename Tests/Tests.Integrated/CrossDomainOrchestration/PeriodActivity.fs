namespace Tests.Integrated.CrossDomainOrchestration

open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type PeriodActivityTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-RPT-6.1 a Revenue account's period activity net total is its credits minus its debits over the lines dated in the range, including debits against its normal balance``() =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-RPT-6.1 an Expense account's period activity net total is its debits minus its credits over the lines dated in the range, including credits against its normal balance``() =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-RPT-6.1 a journal entry line dated on the begin date is included in its account's period activity lines and net total``() =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-RPT-6.1 a journal entry line dated on the end date is included in its account's period activity lines and net total``() =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-RPT-6.1 a journal entry line dated before the begin date is in no account's period activity lines and adds nothing to any net total``() =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-RPT-6.1 a journal entry line dated after the end date is in no account's period activity lines and adds nothing to any net total``() =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-RPT-6.1 period activity lists every Revenue and Expense account with a line dated in the range, each exactly once with its code and name``() =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-RPT-6.1 period activity lists no Revenue or Expense account whose lines all fall outside the range``() =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-RPT-6.1 period activity lists a Revenue or Expense account whose in-range lines net to zero, with a net total of zero``() =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-RPT-6.1 period activity lists no Asset, Liability or Equity account even when it has lines dated in the range``() =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-RPT-6.1 each period activity account carries exactly its lines dated in the range, each with its entry date, journal entry ID, journal entry description, line type, amount and memo``() =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-RPT-6.2 a voided journal entry's lines are in no account's period activity lines and add nothing to any net total``() =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-RPT-6.2 period activity lists no account whose only activity in the range is on voided journal entries``() =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-RPT-6.3 period activity accounts are in trial balance order: top-level accounts by code, each parent immediately before its children, children by code``() =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-RPT-6.3 lines within a period activity account are ordered by entry date, then by journal entry ID for lines on the same date``() =
        Assert.Fail "not implemented"
