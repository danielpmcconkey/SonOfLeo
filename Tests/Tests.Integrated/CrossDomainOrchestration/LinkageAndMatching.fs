module Tests.Integrated.CrossDomainOrchestration.LinkageAndMatching

open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type LinkageAndMatchingTests(fixture: TestDataFixture) =

    // =========================================================================
    // REQ-CF-12.1 — what a link carries
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-12.1 REQ-SYS-3.2 a link the operator creates reads back with a system-generated ID, the Payment Agreement and staged line it was created for, and created and modified instants both equal to the creating operation's initiation instant`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-12.1 REQ-SYS-3.2 a link a classification run creates reads back with a system-generated ID, the claimed Payment Agreement and line, and created and modified instants both equal to the run's initiation instant`` () =
        Assert.Fail "not implemented"

    // =========================================================================
    // REQ-CF-12.2 — one Payment Agreement per line
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-12.2 REQ-CF-12.3 a classification run leaves an already-linked line with its one existing link when a rule for a different Payment Agreement claims it`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-12.2 REQ-CF-12.5 a line that rules of different priority for two Payment Agreements each claim, and that is the only claimant of each, is linked to the higher-priority rule's Payment Agreement and not the other`` () =
        Assert.Fail "not implemented"

    // =========================================================================
    // REQ-CF-12.3 — which lines linkage considers
    // =========================================================================

    [<Theory>]
    [<InlineData("Ingested")>]
    [<InlineData("Classified")>]
    [<InlineData("NoMatch")>]
    [<InlineData("Conflict")>]
    member _.``REQ-CF-12.3 for each of Ingested, Classified, NoMatch and Conflict, an unlinked line of a staged entry in that status that an active Payment Agreement rule claims is linked to that rule's Payment Agreement`` (status: string) =
        Assert.Fail "not implemented"

    [<Theory>]
    [<InlineData("Posted")>]
    [<InlineData("Duplicate")>]
    [<InlineData("Ignored")>]
    member _.``REQ-CF-12.3 for each of Posted, Duplicate and Ignored, a line of a staged entry in that status that an active Payment Agreement rule claims is neither linked nor reported as a claim`` (status: string) =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-12.3 a line that an inactive Payment Agreement rule matches is neither linked nor reported as a claim, and the same rule made active links it`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-12.3 a line already assigned an account is linked when an active Payment Agreement rule claims it`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-12.3 a line matched only by an active rule whose claimant is an account, not a Payment Agreement, is neither linked nor reported as a claim`` () =
        Assert.Fail "not implemented"

    // =========================================================================
    // REQ-CF-12.4 — leg selection
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-12.4 with no claiming rule constraining line type, an Outgo agreement's claim links the entry's Debit line on the agreement's debit account and no other line`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-12.4 with no claiming rule constraining line type, an Income agreement's claim links the entry's Credit line on the agreement's credit account and no other line`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-12.4 when a claiming rule constrains line type to Credit, an Outgo agreement's claim links the Credit line that rule matched, not the Debit line on the agreement's debit account`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-12.4 when one claiming rule constrains line type and another does not, the claim links the line the constraining rule matched`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-12.4 with no line-type constraint, a claim on an entry with no line of the direction's line type on the agreement's account creates no link and is reported unlinked with the no-line reason`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-12.4 with no line-type constraint, a claim on an entry with two lines of the direction's line type on the agreement's account creates no link and is reported unlinked with the several-lines reason`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-12.4 when a line-type-constrained rule matched no line of the entry, the claim creates no link and is reported unlinked with the no-line reason`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-12.4 when a line-type-constrained rule matched two lines of the entry, the claim creates no link and is reported unlinked with the several-lines reason`` () =
        Assert.Fail "not implemented"

    // =========================================================================
    // REQ-CF-12.5 — resolution by Payment Agreement
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-12.5 a Payment Agreement that two lines from different entries claim links neither line, and both lines are reported as contested claimants of that Payment Agreement`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-12.5 a line whose claim is a tie between two equal-priority rules for different Payment Agreements is linked to neither, and it is reported as a contested claimant of both`` () =
        Assert.Fail "not implemented"

    // =========================================================================
    // REQ-CF-12.6 — linkage leaves staging statuses alone
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-12.6 a run leaves every staged entry's status unchanged, whether its lines were linked, reported unlinked, contested, or unclaimed`` () =
        Assert.Fail "not implemented"

    // =========================================================================
    // REQ-CF-12.7 — operator link maintenance
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-12.7 creating a link for an unlinked staged line leaves the line with exactly one link, to the named Payment Agreement`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-12.7 creating a link for a line that is already linked is rejected with a typed error naming the existing link's Payment Agreement, and the existing link is unchanged`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-12.7 re-pointing a link to a different Payment Agreement leaves the line with exactly one link, to the new Payment Agreement`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-12.7 deleting a link leaves the line with no link, and the next run links it again when a rule claims it`` () =
        Assert.Fail "not implemented"

    // =========================================================================
    // REQ-CF-12.8 — the run's matches are recorded
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-12.8 fetching a classification run by its ID returns, for every line the run matched, every rule that matched it with that rule's priority, and no match from any other run`` () =
        Assert.Fail "not implemented"

    // =========================================================================
    // REQ-CF-13.1 — candidate Invoices and their order
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-13.1 REQ-CF-13.4 a line that two open Invoices' windows both cover is paid to the Invoice with the older due date, whichever Invoice was created first, and the other Invoice gets no Payment from it`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-13.1 an older Invoice whose payment state is FullyPaid is passed over, and a linked line both Invoices' windows cover is paid to the newer Invoice`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-13.1 an older Invoice on a fulfilled Instance is passed over, and a linked line both Invoices' windows cover is paid to the newer Invoice`` () =
        Assert.Fail "not implemented"

    // =========================================================================
    // REQ-CF-13.2 — candidate lines
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-13.2 a linked line whose Staged Payment already pays one Invoice gets no Payment from another open Invoice whose window covers its date, and the run does not fail`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-13.2 a linked line gets no Payment from an open Invoice on a different Payment Agreement whose window covers its date`` () =
        Assert.Fail "not implemented"

    // =========================================================================
    // REQ-CF-13.3 — grace periods by cadence
    // =========================================================================

    [<Theory>]
    [<InlineData("Daily", 0)>]
    [<InlineData("Weekly", 2)>]
    [<InlineData("EveryOtherWeek", 4)>]
    [<InlineData("Monthly", 7)>]
    [<InlineData("Annually", 7)>]
    member _.``REQ-CF-13.3 for each cadence, a linked line dated exactly its grace period before the Invoice date is paid to that Invoice`` (cadence: string, graceDays: int) =
        Assert.Fail "not implemented"

    [<Theory>]
    [<InlineData("Daily", 0)>]
    [<InlineData("Weekly", 2)>]
    [<InlineData("EveryOtherWeek", 4)>]
    [<InlineData("Monthly", 7)>]
    [<InlineData("Annually", 7)>]
    member _.``REQ-CF-13.3 for each cadence, a linked line dated exactly its grace period after the due date is paid to that Invoice`` (cadence: string, graceDays: int) =
        Assert.Fail "not implemented"

    [<Theory>]
    [<InlineData("Daily", 0)>]
    [<InlineData("Weekly", 2)>]
    [<InlineData("EveryOtherWeek", 4)>]
    [<InlineData("Monthly", 7)>]
    [<InlineData("Annually", 7)>]
    member _.``REQ-CF-13.3 for each cadence, a linked line dated one day more than its grace period before the Invoice date fails the run as an orphan of that agreement with the no-covering-Invoice reason`` (cadence: string, graceDays: int) =
        Assert.Fail "not implemented"

    [<Theory>]
    [<InlineData("Daily", 0)>]
    [<InlineData("Weekly", 2)>]
    [<InlineData("EveryOtherWeek", 4)>]
    [<InlineData("Monthly", 7)>]
    [<InlineData("Annually", 7)>]
    member _.``REQ-CF-13.3 for each cadence, a linked line dated one day more than its grace period after the due date fails the run as an orphan of that agreement with the no-covering-Invoice reason`` (cadence: string, graceDays: int) =
        Assert.Fail "not implemented"

    // =========================================================================
    // REQ-CF-13.4 through 13.6 — what matching does with its candidates
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-13.4 an Invoice with exactly one candidate line gets exactly one Payment, Staged, pointing at that line`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-13.4 REQ-CF-13.5 a line that lost a contested Invoice is paid to a later Invoice for which it is the only candidate`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-13.5 an Invoice with more than one candidate line gets no Payment and is reported with every candidate line`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-13.6 a Payment that takes an Invoice's paid total above its amount is reported as an overpayment of that Invoice, and the run creates no journal entry and no record other than its links and Payments`` () =
        Assert.Fail "not implemented"

    // =========================================================================
    // REQ-CF-13.8 — a lost candidate is not an orphan
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-13.8 a line that was one of several candidates for an Invoice and got no Payment does not fail the run as an orphan`` () =
        Assert.Fail "not implemented"

    // =========================================================================
    // REQ-CF-13.9 — the run's result
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-13.9 a line the run links is paid in that same run to the open Invoice whose window covers its date`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-13.9 the classification run ID in the result fetches a run whose recorded matches include every line the result lists as linked or as an unlinked claim`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-13.9 the result lists every link and every Payment the run created, and no link or Payment that existed before the run`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-13.9 the result's open Instances are exactly the unfulfilled Instances after matching, so an Instance the run fulfilled is absent and one it left unfulfilled is present`` () =
        Assert.Fail "not implemented"
