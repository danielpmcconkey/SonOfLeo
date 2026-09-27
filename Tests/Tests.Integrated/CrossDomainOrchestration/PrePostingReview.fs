namespace Tests.Integrated.CrossDomainOrchestration

open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type PrePostingReviewTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-RPT-7.1 the review includes every staged entry with status Classified or Reviewed`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-RPT-7.1 the review excludes staged entries with status Ingested, NoMatch, Conflict or Posted`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-RPT-7.2 each entry carries its entry date, description, source name, fi_reference and status`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-RPT-7.2 each line carries its line type, amount, memo, account code and account name, and an entry's debit lines come before its credit lines`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-RPT-7.3 a line carries the name of the rule recorded against it in its most recent classification run whose account is the line's current account`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-RPT-7.3 a line whose account no rule in its most recent classification run carries has an empty rule name, even when an older run recorded a rule with that account`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-RPT-7.3 a line no classification run has recorded has an empty rule name`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-RPT-7.4 a line linked to a Payment Agreement that no Payment references carries the Payment Agreement's and Master Agreement's names and no Payment, Invoice or Instance`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-RPT-7.4 a linked line a Payment references also carries the Payment's amount, its Invoice's invoice date, due date, amount and payment state, and its Instance's date`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-RPT-7.4 a line with no link carries no agreement, Payment, Invoice or Instance`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-RPT-7.5 a postable entry with a line that has no account fails the review with a typed error naming the entry and the line`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-RPT-7.6 entries are ordered by entry date, then source name, then fi_reference`` () =
        Assert.Fail "not implemented"
