# test-efficacy-auditor-Reporting

## EFF-RPT-1 — test-gap
- **Location:** REQ-RPT-3.1; Tests/Tests.Integrated/InterfaceBridge/ReportRoutes.fs:461-475
- **Summary:** REQ-RPT-3.1 is tested only for the period-activity header's date range. No test checks the report title, the trial balance header's as-of date, or the integrity report's header.
- **Resolution:** fix-test

REQ-RPT-3.1 sits in §3, 'Trial balance HTML rendering', and reads: 'The rendered HTML report must contain a header section displaying the report title and the as-of Calendar Date.' It is not in the waiver table; 3.2 through 3.6 are, 3.1 is not. Only one test in the repo cites it: ReportRoutes.fs:461, `REQ-RPT-6.4 REQ-RPT-3.1 the period activity rendered report header shows the begin and end dates`. That test cuts out `<header ... </header>` and asserts `Assert.Contains(b, header)` and `Assert.Contains(e, header)`. That covers the §6.4 range variant. No test opens a rendered trial balance and inspects its header. The only trial-balance report-mode test (ReportRoutes.fs:153) never reads the file. No test asserts a report title in any header, and none checks the integrity report header (§6.4 extends §2/§3 to integrity, as of a single date). If TrialBalanceWriter dropped the title or printed the wrong date, every test would still pass.

**Action:** Add a ReportRoutes test that renders the trial balance in report mode, extracts the <header> element, and asserts it contains the report title and the as-of date in the rendered format. Add the same assertion for the integrity report.

**Why:** 3.1 is the one §3 rendering requirement Dan chose not to waive. As tested, its main subject (the trial balance header and title) has no coverage. The citation exists only because a §6.4 test also names it.

---

## EFF-RPT-2 — test-gap
- **Location:** REQ-RPT-7.7; Tests/Tests.Integrated/InterfaceBridge/ReportRoutes.fs:226-243; Src/Ui.InterfaceBridge/ReportWriters/PrePostingReviewWriter.fs:157-163
- **Summary:** The REQ-RPT-7.7 clause saying the rendered header shows the run date and the number of entries and lines is untested.
- **Resolution:** fix-test

REQ-RPT-7.7 says the review 'supports the output modes of §2. Date interpolation (REQ-RPT-2.4) appends the date the report runs, and the rendered header (REQ-RPT-3.1) shows that date and the number of entries and lines.' Three tests cite 7.7. The data-only test (line 208) checks entry fields. The interpolation test (line 246) checks the file path. The report-mode test (line 227) asserts only `Assert.Contains("Pre-posting review route 7.7 report", html)` and `Assert.DoesNotContain("tag not implemented", html)`. No test extracts the header or checks the run date, entry count or line count in it. The code does implement this: PrePostingReviewWriter.fs:157-163 computes `lineCount` and emits `" · {entries.Length} entries, {lineCount} lines"`. A wrong count, a count taken before or after filtering, or a missing date would all pass.

**Action:** In the report-mode test, compute the expected entry and line counts from the entries the test committed (or from the data-only return of the same run), extract the <header> element, and assert it contains today's date and '<n> entries, <m> lines' exactly.

**Why:** Smell test: a writer that printed '0 entries, 0 lines', or no date, passes every current test. The operator reads that count to judge how much is about to post.

---

## EFF-RPT-3 — test-gap
- **Location:** REQ-RPT-2.3; Tests/Tests.Integrated/InterfaceBridge/ReportRoutes.fs:152-171
- **Summary:** The trial balance report-mode test checks only that a file exists and that the path contains ".html". It never checks the file's content, has no stale-file guard, and does not check the path is fully qualified.
- **Resolution:** fix-test

REQ-RPT-2.3: 'In report mode, the operation renders the trial balance to an HTML file and returns the fully qualified file path to the written file.' The only trial-balance test for it (line 153) asserts `Assert.True(System.IO.File.Exists pathReturn.fullyQualifiedPath)` and `Assert.Contains(".html", pathReturn.fullyQualifiedPath)`. Problems: (a) `Contains(".html")` is the string-containment pattern Specimen 4 warns about; the path is never compared exactly. The non-interpolated trial-balance path is never asserted exactly anywhere; the integrity, activity and pre-posting siblings each assert it with Assert.Equal. (b) The test does not delete `rpt-2-3-test.html` before the call. If an assertion fails, the file is left behind, so a later run where the writer returns a path without writing still passes File.Exists. The integrity sibling at line 344 guards against exactly this ('a file left behind by an earlier run would satisfy File.Exists'). (c) `IsPathFullyQualified` is not asserted, though the REQ requires it. (d) The file is never read, so an empty or non-HTML file passes. The integrity and activity siblings at least assert a doctype and no 'tag not implemented'.

**Action:** Delete the target file before the call, assert Path.IsPathFullyQualified, Assert.Equal the path to Path.Combine(testOutputDir, "rpt-2-3-test.html"), read the file, and assert it starts with <!doctype html> and contains a fixture account code.

**Why:** Smell test: a writer that touched an empty file, or returned a stale path, passes this test. For the trial balance, the oldest report, it is the only report-mode test.

---

## EFF-RPT-4 — test-gap
- **Location:** REQ-RPT-2.2; Tests/Tests.Integrated/InterfaceBridge/ReportRoutes.fs:110-150
- **Summary:** The REQ-RPT-2.2 data-only test never asserts the boundary row's account name or generation. It checks the row count and the three money fields of one row.
- **Resolution:** fix-test

REQ-RPT-2.2 lists six boundary fields: 'account code (string), account name (string), generation (int), total credits (decimal), total debits (decimal), and net balance (decimal)'. The test asserts `Assert.Equal(expectedCount, rows |> List.length)`, then on a single row located by code it asserts totalDebits, totalCredits and netBalance. `leafRow.accountName` and `leafRow.generation` are never asserted, and no row's generation is checked at the boundary. The test name claims 'expected field types' but only the decimals are examined. A converter that mapped name to code, or set generation to 0 for every row, would pass. TrialBalance.fs (REQ-RPT-1.7) checks generation on the domain row, not the boundary row, so the conversion step itself is unchecked for these two fields.

**Action:** Also assert leafRow.accountName equals the fixture account's name, and leafRow.generation equals the generation derived from the fixture's parent chain (for food5350 under the F-5000 tree). Preferably check generation for every row against a fixture-derived value.

**Why:** The REQ names six fields, and only three of them, plus the code used to find the row, are verified. Two of the boundary mappings could be broken without any test failing.

---

## EFF-RPT-5 — test-gap
- **Location:** REQ-RPT-7.1; Tests/Tests.Integrated/CrossDomainOrchestration/PrePostingReview.fs:195-199 and 203-221; Src/Business.CrossDomainOrchestration/PrePostingReview.fs:90
- **Summary:** The REQ-RPT-7.1 'exactly what batch post would post' check derives its expected set from fetchAllForPosting, which the review itself calls (Specimen 6). The exclusion Theory also omits the Duplicate and Ignored statuses.
- **Resolution:** fix-test

REQ-RPT-7.1: the review covers 'every postable staged entry (DataIngestion REQ-STG-4.4): exactly the entries batch post would post'. Two weaknesses. (1) PrePostingReview.fs:196-199 computes `let! postable = StageEntryOrchestration.fetchAllForPosting context` and asserts `Assert.Equal<Set<StageEntryHeaderId>>(postable |> ..., review |> ...)`. fetchPrePostingReview's first step is `let! entries = StageEntryOrchestration.fetchAllForPosting context` (Src PrePostingReview.fs:90). Both sides run the same selector, so this assertion can only fail if the review drops or adds entries after selecting them; it cannot catch a wrong selector. This is Specimen 6. (2) The independent coverage, the status-based tests, is incomplete. Line 183 proves Classified and Reviewed are included. The Theory at 203-208 proves exclusion only for `Ingested`, `NoMatch`, `Conflict` and `Posted`. StagedEntryStatus (StageEntryComponent.fs:14-22) has eight cases, so `Duplicate` and `Ignored` are never tested for exclusion. A selector that let Duplicate or Ignored entries through would pass every 7.1 test, because assertion (1) shares the bug.

**Action:** Add InlineData("Duplicate") and InlineData("Ignored") to the exclusion Theory, so all six non-postable statuses are excluded explicitly (a Specimen 10 style truth table over all eight statuses is better). Drop the fetchAllForPosting set equality, or replace it with an expected set derived from the statuses the test itself staged.

**Why:** The one assertion that reads like 'and nothing else' is tautological. The real 'nothing else' guarantee rests on the Theory, which covers four of the six statuses that must be excluded.

---

## EFF-RPT-6 — test-gap
- **Location:** REQ-RPT-7.6; Tests/Tests.Integrated/CrossDomainOrchestration/PrePostingReview.fs:392-411
- **Summary:** In the REQ-RPT-7.6 ordering test, the entries' descriptions sort in the same order as the expected source-name and fi_reference order. A sort by entry date then description would pass.
- **Resolution:** fix-test

REQ-RPT-7.6: 'Entries are ordered by entry date, then source name, then fi_reference.' The test stages four entries with descriptions "7.6 later date", "7.6 TestCreditCardCo B", "7.6 TestCreditCardCo A" and "7.6 TestBank", and expects `[ testBank; testCreditCardCoA; testCreditCardCoB; laterDate ]`. Within the earlier date, sorting by description ("7.6 TestBank" < "7.6 TestCreditCardCo A" < "7.6 TestCreditCardCo B") gives exactly the expected order. So an implementation sorting by (entryDate, description) passes, and neither the source-name key nor the fi_reference key is isolated from description. Staging in reverse order does rule out insertion order, but not this wrong key.

**Action:** Give the staged entries descriptions whose order contradicts the expected order, for example by reversing the description labels, or give all four the same description, so that only source name and fi_reference can produce the asserted order.

**Why:** Smell test: a review sorted on the wrong key passes. The test name claims three specific keys, and the fixture cannot tell two of them apart from a fourth.

---

## EFF-RPT-7 — missing-requirement
- **Location:** REQ-RPT-7.3; Tests/Tests.Integrated/CrossDomainOrchestration/PrePostingReview.fs:271-288; Src/Business.CrossDomainOrchestration/PrePostingReview.fs:52-83
- **Summary:** The REQ-RPT-7.3 test asserts a tie-break (highest priority wins, then rule name) that the REQ does not state. The code also defines 'most recent run' differently from the REQ text.
- **Resolution:** dan-decides

REQ-RPT-7.3: 'the rule recorded against the line in the most recent classification run that evaluated it (DataIngestion REQ-STG-5.10) whose account is the line's current account.' Under REQ-STG-5.10, a run records every matching rule, including those that lost on priority. So more than one rule in the latest run can carry the line's account, and the REQ says 'the rule' without saying which. The test at line 272 records `[ lowerPriority; winner; otherAccount ]`, where two rules carry the account, and asserts `Assert.Equal(Some (winner |> ClassificationRule.classificationRuleName), reviewed.ruleName)` with the comment 'the higher priority one is named'. The code (PrePostingReview.fs:79-80) sorts by `-(priority)` and then by rule name. Neither tie-break appears in REQ-RPT-7.3 or elsewhere in Reporting.md. Separately, the code comment at lines 52-55 redefines 'most recent run that evaluated it' as 'the most recent run that recorded an account rule against it'. It ignores PaymentAgreement-claimant runs and any later run that evaluated the line without matching it. That is a narrower reading than the REQ text, and no test cites or pins it.

**Action:** Dan decides whether to amend REQ-RPT-7.3 to state (a) the tie-break when several rules in the latest run carry the line's account (highest priority, then name), and (b) that 'most recent run' means the most recent run that recorded an account-claimant match. Then cite those clauses explicitly.

**Why:** The test exercises behavior with no REQ behind it, which Tests/README forbids ('If the code you are testing does something uncited by the REQs, stop and point that out'). The run-recency reading changes which rule name the operator sees.

---

## EFF-RPT-8 — test-gap
- **Location:** REQ-RPT-4.4; Tests/Tests.Integrated/CrossDomainOrchestration/Reconciliation.fs:278-298; Tests/Tests.Integrated/InterfaceBridge/IngestionRoutes.fs:921-934; Src/Business.CrossDomainOrchestration/StageEntryOrchestration.fs:807
- **Summary:** Both shadow-reconciliation 'untouched' tests compute the expected shadow movement from fetchAllForPosting, the selector that the shadow post they test calls (Specimen 6). Both guard that value with a `>=` inequality (Specimen 2).
- **Resolution:** fix-test

REQ-RPT-4.4 requires the shadow variant to reconcile against the ledger 'as it would stand after posting every postable staged entry'. Reconciliation.fs:278-289 and IngestionRoutes.fs:921-930 both do `let! postable = fetchAllForPosting ...` and sum the F-5650 lines into `expectedMovement`, then assert `Assert.Equal(before + expectedMovement, shadow)`. reconcileAfterPostingStagedEntries calls `StageEntryOrchestration.post`, which selects its entries with `fetchAllForPosting` (StageEntryOrchestration.fs:807). If that selector over- or under-selects, expected and actual move together. Both tests also assert `Assert.True(expectedMovement >= 21.40M / 25.00M, ...)`. That is a floor rather than an exact value, and it tolerates other postable F-5650 lines leaking into the amount. The rollback half of each test (`Assert.Equal(before, after)`, `Assert.Empty(posted)`) is sound. The 'includes every postable entry' half is independently proven only by Reconciliation.fs:242, which hand-derives 140.00 and 170.00, and that test stages a single Classified entry.

**Action:** Derive the expected movement from the amounts the test itself staged (21.40 / 25.00) together with an explicit, separately computed set of other postable F-5650 lines. Or reconcile against a test-owned account no other staged entry touches, so the expected movement is exactly the staged amount, and assert it with Assert.Equal.

**Why:** The rule is that no function in the call chain of the function under test may appear in the derivation of the expected value. These are the only tests that observe the shadow reconciliation from outside its transaction.

---

## EFF-RPT-9 — test-gap
- **Location:** REQ-RPT-4.1, REQ-RPT-6.4; Tests/Tests.Integrated/InterfaceBridge/ReportRoutes.fs:263-293, 314-341, 381-418
- **Summary:** Three route-level report tests get their expected values by calling the same computation the route calls: fetchTrialBalanceData, computeBalanceSheetIntegrity and fetchPeriodActivity. Under the Specimen 6 rule as written, that is a violation.
- **Resolution:** dan-decides

ReportRoutes.fs:273-274 computes `TrialBalanceReport.fetchTrialBalanceData context today/yesterday` and asserts the Reconciliation route's `ledgerBalance`, `accountName` and `delta` against it. Reconciliation.reconcile calls fetchTrialBalanceData itself (Src Reconciliation.fs:58). ReportRoutes.fs:319 computes `BalanceSheetIntegrity.computeBalanceSheetIntegrity context yesterday` and asserts every field of the integrity route against it. ReportRoutes.fs:385 computes `PeriodActivity.fetchPeriodActivity` and asserts the activity route equals it; its precondition `Assert.True(expected |> List.length >= 2)` is a floor (Specimen 2). Each of these tests passes whatever the computation returns, provided the route passes it through. The underlying computations are checked against hand-derived values in Reconciliation.fs, BalanceSheetIntegrity.fs and PeriodActivity.fs, so the exposure is limited to the route layer. But Tests/README and the specimens doc say the rule 'applies to all expected values', with no exception for route-to-orchestrator pass-through comparisons.

**Action:** Dan rules whether 'route output equals the orchestrator output for the same input' is an accepted pattern for route-layer happy paths (and records it in resolved-findings.md), or whether route tests must derive their expected values from fixture data like the CDO tests.

**Why:** The doctrine is absolute, but the layer hierarchy (test the happy path at each layer, failures once at the lowest) makes the pass-through comparison tempting. An explicit ruling stops each future audit from re-litigating it, or stops agents from copying a pattern that may not be acceptable.

---

## EFF-RPT-10 — test-gap
- **Location:** REQ-RPT-1.7; Tests/Tests.Integrated/CrossDomainOrchestration/TrialBalance.fs:138-149
- **Summary:** The REQ-RPT-1.7 generation test hard-wires four fixture codes and their generations (Specimen 1) instead of deriving each row's generation from the fixture's parent chain, so all other rows go unchecked.
- **Resolution:** fix-test

REQ-RPT-1.7: 'Top-level accounts (those with no parent) are at generation 0. Each level of nesting increments the generation by 1.' The test asserts `Assert.Equal(0, findGen "F-5000")`, `Assert.Equal(1, findGen "F-5300")`, `Assert.Equal(2, findGen "F-5310")` and `Assert.Equal(3, findGen "F-5311")`. Both the codes and the expected depths are literals. Specimen 1 names the magic-code form of this ('The magic string "F-1000" has the same disease'). Only one chain of one subtree is examined. A generation computation that is right on the F-5000 chain but wrong elsewhere, such as other top-level trees or accounts whose parent is inactive, passes. The sibling REQ-RPT-1.6 test in the same file already shows the right approach: it walks fixture.Data.accounts recursively to build the expected order.

**Action:** Compute expected generation for every fixture account by walking Account.parentId in fixture.Data.accounts (0 when None, else parent's + 1), and assert it against every row's generation with Assert.Equal on the full (code, generation) list.

**Why:** The expected values are snapshots of the fixture, not derived from it. The assertion covers four rows out of the whole chart of accounts.

---


