# test-efficacy-Reporting

## EFF-RPT-8.5-1 — test-gap
- **Location:** REQ-RPT-8.5 (amended 2026-10-04), REQ-RPT-8.2; Tests/Tests.Integrated/CrossDomainOrchestration/NetWorth.fs:131-139, 150-156, 159-177; Tests/Tests.Helpers/PositionsFixture.fs:318-324
- **Summary:** No test covers the clause added to REQ-RPT-8.5 at HEAD: a mortgage Account of a Property no longer owned on the date is listed with the other Liability accounts and still subtracted from net worth.
- **Resolution:** fix-test

At da96f73, REQ-RPT-8.5 changed to list 'each Liability account not a mortgage Account of a Property owned on the date' because 'a mortgage of a Property no longer owned still counts in net worth under REQ-RPT-8.2'. The commit touched only the spec. The code does it (NetWorth.fs:107 builds ownedMortgageIds only from owned properties, and line 118 keeps every other Liability). No test reaches that path. In the fixture the only disposed Property, 7 Former Example Road, is created with `[]` mortgages (PositionsFixture.fs:319-320). Every NetWorth test dated monthEnd3 has both mortgaged properties owned, so the 8.2/8.3 test at NetWorth.fs:131-139 only asserts that owned mortgages F-2310 and F-2320 are absent from liabilityAccounts. The one test on a date when a mortgaged property is not owned (NetWorth.fs:150-156, the day before residenceAcquired) asserts only the property list and investableWealth = netWorth. It never looks at liabilityAccounts, and F-2310 holds 0.00 then anyway. Smell test: change line 107 to collect mortgages from all properties, owned or not, and a disposed property's mortgage silently drops out of both the liability list and net worth. Every citing test stays green.

**Action:** Add a NetWorth test (Form 3, rolled back) that disposes a Property carrying a mortgage Account with a non-zero as-of balance, or add that archetype to PositionsFixture. Assert the mortgage appears in liabilityAccounts with its hand-derived balance, appears under no Property, and is subtracted exactly once from netWorth.

**Why:** The requirement was amended to fix exactly this case, and nothing would catch a regression to the earlier behaviour. A behaviour that changed in the latest commit and has no test is the thinnest coverage in the slice.

---

## EFF-RPT-8.5-2 — test-gap
- **Location:** REQ-RPT-8.5, REQ-RPT-8.2; Tests/Tests.Integrated/CrossDomainOrchestration/NetWorth.fs:159-177 (also the 207,945.00 / 5,000.00 / -25.00 literals at lines 77, 86, 90, 98, 136, 138, 215)
- **Summary:** The net worth ledger-row test hard-wires the whole shared fixture's list of Asset and Liability accounts, which Tests/README forbids and Specimen 1 describes.
- **Resolution:** fix-test

NetWorth.fs:163-176 asserts exact equality against the literal lists [F-1000, F-1250, F-1270, F-1275, F-1280, F-1290] and [F-2000, F-2210, F-2220, F-2230]. Most of these accounts are not Positions archetypes. They come from TestDataStage.fs (core ledger and cash-flow fixtures), and their zero balances as of monthEnd3 hold only because, as the header comment at line 33 notes, 'every other Asset and Liability account: 0.00'. Tests/README.md names this exact case: 'Do not ever write tests that assume the count of anything in the database is a constant (ex: number of accounts of type "Asset")', and 'Derive expected values from fixture data using list operations'. Adding any Asset or Liability account to the shared fixture for an unrelated domain, or any fixture entry dated in months -4 to -2, breaks this test and the 207,945.00 net-worth literal it shares with six other tests, for a reason unrelated to net worth. That is Specimen 1's 'encodes what the fixture happened to contain'. The Positions-specific figures (market values, valuations, mortgage balances) are legitimately hand-derived from PositionsFixture's documented archetypes. The objection is to the parts that snapshot the shared chart of accounts.

**Action:** Derive the expected counted-asset and liability rows from fixture.Data.accounts (filter by type, remove the IDs linked in fixture.Data.positions, compute each balance from fixture.Data.journalEntries dated on or before monthEnd3 and not voided, using list operations), and build the net-worth expectation from those plus the hand-derived Positions figures.

**Why:** An expected value that snapshots the shared fixture fails whenever another domain adds an archetype. Each such failure trains people to update the literal rather than question the code, which is how Specimen 1 tests stop guarding anything.

---

## EFF-RPT-1.5-1 — test-gap
- **Location:** REQ-RPT-1.5; Tests/Tests.Integrated/CrossDomainOrchestration/TrialBalance.fs:102-130; Tests/Tests.Integrated/CrossDomainOrchestration/Reconciliation.fs:36-44, 202-209
- **Summary:** The recursive half of REQ-RPT-1.5 ('a grandparent's totals include its children's rolled-up totals') is never exercised, because no account two or more levels below a rolled-up parent has any journal entry activity.
- **Resolution:** fix-test

The REQ-RPT-1.5 test rolls up F-5000. Its descendants with lines are F-5350, F-5650 and F-5700, all direct children (TestDataStage.fs:322-411, 500-765). The only deeper subtree, F-5300 > F-5310 > F-5311/5312/5313, has no lines at all. The Positions fixture's accounts are also direct children of F-1000, F-2000 and F-3000. The Reconciliation ledger (RC-1000 > RC-1010) is one level deep. The test's expected value correctly walks descendants recursively (lines 107-117), but it sums to the same number a non-recursive implementation would give: one that added only each direct child's own balance, ignoring grandchildren, would still produce F-5000's expected debits, credits and net. The 'own values plus descendants' half is covered by Reconciliation (RC-1000's own 30.00 plus its child's 100.00). Recursion through a grandparent is not covered anywhere. Smell test: replace `compiledChildren` sums in TrialBalance.fs:72-83 with sums of each child's own AccountBalance, and every citing test passes.

**Action:** Post a line to an account at least two levels deep (e.g. F-5311) inside a rolled-back test, or add one to the fixture, and assert that F-5310, F-5300 and F-5000 each carry it in their totals, with expected values derived from fixture lines.

**Why:** The requirement states recursion explicitly. A test whose data cannot tell a recursive roll-up from a one-level one covers only the shallow case.

---

## EFF-RPT-5.4-1 — test-gap
- **Location:** REQ-RPT-5.4; Tests/Tests.Integrated/CrossDomainOrchestration/BalanceSheetIntegrity.fs:331-399
- **Summary:** Every REQ-RPT-5.4 test passes as-of = today, so none shows that the deactivated-account look-back is anchored on the operation's current date rather than on the integrity as-of date.
- **Resolution:** fix-test

REQ-RPT-5.4 says the list is computed 'as of the current date (the calendar date of the operation's initiation instant, REQ-SYS-3.4)', independent of the as-of date §5.1 takes. The implementation does this (BalanceSheetIntegrity.fs:41 takes `today` from Context.getInitiationInstant, and line 52 passes `Some today` to the balance fetch). All four 5.4 tests call `computeBalanceSheetIntegrity context today` (lines 341, 362, 382, 396), so the integrity as-of date and the initiation date are always the same. An implementation that used `asOf` for the deactivation cut-off and the balance date would pass all four. Smell test: change line 41 to use the asOf parameter and nothing goes red. The route-level REQ-SYS-3.4 tests in ReportRoutes.fs cover the footer and the pre-posting run date, not this list.

**Action:** Add a 5.4 test that calls computeBalanceSheetIntegrity with an as-of date earlier than the retired account's active end (e.g. today-20, with retiredEnd = today-10) and asserts BI-1100 is still listed with its current-date balance. Optionally add the mirror case: an as-of date in the future does not list an account whose active end is today.

**Why:** The requirement deliberately separates the look-back's date from the report's as-of date. Without a test where the two differ, the separation is unverified.

---

## EFF-RPT-6.4-1 — test-gap
- **Location:** REQ-RPT-6.4 with REQ-RPT-5.4; Tests/Tests.Integrated/InterfaceBridge/ReportRoutes.fs:413-440; Src/Ui.InterfaceBridge/BoundaryConverters/ReportConverters.fs:98-111, 128-130
- **Summary:** The integrity route's data-only test compares every field except deactivatedAccountsWithBalance, and the converter for that field is exercised by no test with a non-empty list.
- **Resolution:** fix-test

The REQ-RPT-6.4 data-only route test asserts asOf, totals, the equality flag, the five type balances, netIncome and residual against the computation (lines 426-436). It never compares row.deactivatedAccountsWithBalance. The shared fixture's only deactivated account, F-1290 Closed Bank, nets to zero (TestDataStage.fs:570-593), so the list is empty at the route regardless. A grep of Tests/ for deactivatedAccountsWithBalance and entriesAfterActiveEnd finds them only in the orchestrator test file. ``convert [DeactivatedAccountWithBalance] to [DeactivatedAccountWithBalanceReturnRow]`` (code, name, activeEnd, balance, and per entry journalEntryId, entryDate, description, postedAt, voidedAt) has therefore never run in a test. Tests/README's hierarchy calls for a happy path at each layer. The only data-only consumer of the weekly look-back, the operator's Saturday read, goes through this converter.

**Action:** Add a route test in Form 4 (the route uses its own connection), or extend the existing one with a committed deactivated account holding a balance, cleaned up in finally. Assert that the returned deactivatedAccountsWithBalance rows carry the expected code, name, active end, balance and entry IDs.

**Why:** A converter no test runs can swap or drop fields silently. Here that would hide the residue the requirement exists to surface.

---

## EFF-RPT-3.1-1 — test-gap
- **Location:** REQ-RPT-3.1; Tests/Tests.Integrated/InterfaceBridge/ReportRoutes.fs:306-342 (pre-posting review header), 559-575 (period activity header)
- **Summary:** REQ-RPT-3.1 requires every rendered report's header to show the report title, but the period activity and pre-posting review header tests assert only dates and counts, never the title.
- **Resolution:** fix-test

The preamble to Reporting.md §3 says 3.1 applies to every rendered report, including period activity and pre-posting review. For trial balance (line 239), balance-sheet integrity (line 255), net worth (PositionsReportRoutes.fs:191) and investment wealth history (PositionsReportRoutes.fs:244), the tests assert `titleIn header` equals the title. The period activity header test (lines 559-575) asserts only `Assert.Contains(b, header)` and `Assert.Contains(e, header)`. The pre-posting review report test (lines 336-339) asserts only today's date and the entries/lines count. The writers do render titles ('Period Activity', PeriodActivityWriter.fs:152; 'Pre-Posting Review', PrePostingReviewWriter.fs:165), but a writer that rendered an empty or wrong <h1> for either report would pass every test that cites 3.1 for it.

**Action:** Add `Assert.Equal("Period Activity", titleIn header)` to the period-activity header test and `Assert.Equal("Pre-Posting Review", titleIn header)` to the pre-posting review header test, reusing the existing headerOf and titleIn helpers.

**Why:** Of REQ-RPT-3.1's two obligations, title and date, these two reports test only the date, so the title is unverified for them.

---

## EFF-RPT-1.11-1 — test-gap
- **Location:** REQ-RPT-1.11, REQ-RPT-1.2; Tests/Tests.Integrated/CrossDomainOrchestration/TrialBalance.fs:63-82, 201-215
- **Summary:** The REQ-RPT-1.11 test repeats the zero-row assertions of the REQ-RPT-1.2 test on the same never-used account, so the 'no qualifying activity' case it names (activity that exists but is voided or dated after the as-of date) is never exercised at the trial-balance layer.
- **Resolution:** fix-test

Lines 76-79 (the REQ-RPT-1.2 test) and lines 209-212 (the REQ-RPT-1.11 test) find the row for retirement3030Id and assert 0M debits, credits and net, the same three assertions on the same account against the same prefetched trial balance. Tests/README calls this out: 'Do not test the same thing twice.' REQ-RPT-1.11 is about an account with 'no qualifying journal entry activity as of the report date', which goes beyond an account with no activity at all. No trial-balance test asks for a date at which an account's only lines are excluded (e.g. F-5700 as of today-4, before its three temporal entries at -3, -1 and 0, TestDataStage.fs:733-767) and checks that it appears with zero values rather than being dropped. This matters because TrialBalance.fs:47-50 takes `List.head` of the balance row for every account. If AccountBalance ever omitted an account whose only lines were filtered out, the report would throw instead of showing zeros, and the current 1.11 test could not detect it.

**Action:** Change the REQ-RPT-1.11 test to an as-of date before F-5700's first entry (or another account whose only lines are voided or later) and assert that the account's row exists with zero credits, debits and net. Leave the never-used-account assertion to the REQ-RPT-1.2 test.

**Why:** As written, the citation for 1.11 duplicates 1.2's evidence, and the case 1.11 actually adds, an account whose activity is all excluded, goes unexamined.

---
