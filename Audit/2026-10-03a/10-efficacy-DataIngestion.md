# test-efficacy-DataIngestion

## EFF-STG-1 — test-gap
- **Location:** Tests/Tests.Integrated/CrossDomainOrchestration/StagingIngestionRules.fs:344, :428, :447, :554, :795 (REQ-STG-1.17, REQ-STG-2.26, REQ-STG-3.11, REQ-STG-6.7)
- **Summary:** Five sad-path tests in StagingIngestionRules assert only `Assert.True(attempt |> Result.isError)`, although most of their names say the request is 'rejected with a typed error'.
- **Resolution:** fix-test

Specimen 4 (untyped failure) appears at five sites: (1) L344, the REQ-STG-1.17 misspelt-property theory; (2) L428, the REQ-STG-2.26 null/empty/whitespace source-name theory, named '...is rejected with a typed error and no source is stored'; (3) L447, the REQ-STG-2.26 101-character name, named '...rejected with a typed error'; (4) L554, the REQ-STG-3.11 missing-directory/missing-file theory, named '...fails with a typed error'; (5) L795, the REQ-STG-6.7 manual update to Duplicate/Ignored on a paid entry, named '...rejected with a typed error'. Each test also checks that nothing was stored or that the status did not change, which is good. But a broken DB connection, a JSON serialization fault, or the wrong validation firing would all pass. The typed cases exist in Src: DataIngestionError.IngestionPaidStageEntryCannotBeExcluded (Src/Business.FinancialServices.DataIngestion/DataIngestionError.fs; raised at StageEntryOrchestration.fs updateStageEntry), JournalRefFinancialInstitutionIsEmpty/TooLong, and IngestionFileRejected for 1.17. No test anywhere references IngestionPaidStageEntryCannotBeExcluded (grep of Tests/ returns nothing), so the typed error that REQ-STG-6.7 requires for the manual update is never confirmed.

**Action:** In each of the five tests, replace `Assert.True(attempt |> Result.isError)` with a typed match: `| Error (AsError (ExpectedCase ...)) -> ...` plus both escape arms, or isCorrectError. For 6.7, match IngestionPaidStageEntryCannotBeExcluded(headerUuid, status). For 1.17, match IngestionFileRejected and check the record's case.

**Why:** The test README forbids Result.isError: 'Never Result.isError.' Specimen 4 records four ingestion tests that turned out to be passing on a raw DAL error. When a test's name claims a typed error but its body checks for any error, a reviewer reading the name is misled.

---

## EFF-STG-2 — test-gap
- **Location:** REQ-STG-6.7; Tests/Tests.Integrated/CrossDomainOrchestration/StagingIngestionRules.fs:800-819 (assertion L818); Src/Business.CrossDomainOrchestration/StageEntryOrchestration.fs:340-362
- **Summary:** REQ-STG-6.7 says dedup reports a paid entry it skips. The only test uses an Ingested paid entry, and an Ingested entry is in the dedup result for any reason, so nothing shows a report exists. A paid Classified/NoMatch/Conflict repeat is skipped and appears nowhere.
- **Resolution:** dan-decides

REQ-STG-6.7: 'The attempt fails with a typed error (manual update) or is reported without flagging (dedup).' deduplicateStagedEntries filters out paid headers and returns `[ Ingested ] |> fetchByStatusList` (L361). The code comment says the paid entry 'stays at its status and so appears in the result below'. That holds only when the paid entry is Ingested. The test builds the paid repeat with path [] (Ingested) and asserts `Assert.Contains(paid |> headerIdOf, remaining ...)`. Every Ingested entry is in `remaining` whether or not it was skipped for a Payment, so this assertion cannot tell 'reported because paid' apart from 'Ingested'. REQ-STG-7.2 says Classified, NoMatch and Conflict entries can be flagged. REQ-CF-12.3/13.2 let a Payment land on a line whose entry is Classified, NoMatch, Conflict or Reviewed. So a paid Classified repeat is a reachable case: dedup skips it, and it is absent from the result because the result holds only Ingested entries. Nothing reports it. The test name ('lists it in its result as not flagged because of the Payment') claims a reason-bearing report, and the return type cannot carry one.

**Action:** Dan decides what 'reported' means for dedup: a dedicated list of skipped-because-paid entries in the dedup result, or the Ingested list as it stands (in which case amend REQ-STG-6.7 and accept that non-Ingested paid repeats go unreported). Then add a theory over paid repeats in Ingested, Classified, NoMatch and Conflict that asserts the agreed report.

**Why:** A test whose fixture puts the entry in the one status where the claimed behavior happens to hold by coincidence hides the cases where it does not. Smell test: delete the Payment guard from the reporting path and this assertion still passes.

---

## EFF-STG-3 — contradiction
- **Location:** REQ-STG-6.5 (Specs/Behavioral/DataIngestion.md:219); Src/Business.CrossDomainOrchestration/StageEntryOrchestration.fs:531-549 (protectionsOf); Tests/Tests.Integrated/CrossDomainOrchestration/StageEntryUpdate.fs:622-644
- **Summary:** REQ-STG-6.5 protects lines 'recorded in any classification run' and also says 'only lines no classification run has evaluated ... can be removed'. A line that was evaluated but matched no rule has no record, so the two clauses disagree, and no test settles which one the code follows.
- **Resolution:** dan-decides

REQ-STG-5.10 and REQ-CR-8.2 record only matching rules ('A line with no match produces no row'). protectionsOf derives RecordedInClassificationRun from RuleMatch.fetchByStageEntryLineIdList, so a NoMatch-evaluated line carries no protection and can be removed. That satisfies the first clause and breaks the last sentence ('Only lines no classification run has evaluated — in practice, lines the operator added — can be removed'). The REQ-STG-6.5 removal test (L622) uses the MARATHON debit line, which the classifier assigned and which therefore has match rows. It never exercises an evaluated-but-unmatched line, the one case where the two clauses diverge. The 6.4 removal test (L489) removes an operator-added line, which both clauses allow.

**Action:** Dan picks one: (a) 'recorded' governs, so amend the last sentence of REQ-STG-6.5 to 'lines with no recorded match'; or (b) 'evaluated' governs, so the code needs a record of evaluation, not only of matches. Then add a test that removes a line from a NoMatch entry after a classification run and asserts the chosen outcome.

**Why:** When a REQ has two clauses that disagree on a reachable input, the code quietly picks one and the suite never notices. Check the clauses against each other at the boundary where they diverge.

---

## EFF-STG-4 — test-gap
- **Location:** REQ-STG-1.6, REQ-STG-2.12; Src/Business.CrossDomainOrchestration/StageEntryOrchestration.fs:71-83 (confirmLinesAreAllPositive); Tests/Tests.Integrated/InterfaceBridge/IngestionRoutes.fs:275-289
- **Summary:** No test checks that a zero or negative amount is rejected at ingestion or in a manual update. IngestionStageLineNonPositiveAmount is referenced by no test.
- **Resolution:** fix-test

REQ-STG-1.6: 'amount: required, positive decimal'. REQ-STG-2.12: 'positive decimal(12,2) value (greater than zero)'. Money.fromDecimal accepts negatives (Money.fs:11, minMoney = -9999999999.99M), so the only guard is confirmLinesAreAllPositive, which raises IngestionStageLineNonPositiveAmount. Grep of Tests/ for IngestionStageLineNonPositiveAmount returns nothing. The route validation theory (IngestionRoutes.fs L276-288) covers precision, max, line type, empty and too-long values, but has no row for amount 0 or a negative amount. A balanced group of '-10.00 Debit / -10.00 Credit' passes the balance check (1.15) and the line-count check (1.14), so only the positivity check stops it. Remove that check and the suite stays green.

**Action:** Add rows for amount "0" and "-25.00" to the REQ-STG-1.6 route theory, expecting IngestionStageLineNonPositiveAmount (repeat the amount on both rows so the group stays balanced). Add one REQ-STG-6.4 manual-update case that adds a 0.00 line and expects the same typed error with the entry unchanged.

**Why:** A REQ that names a rejection criterion needs a test that sends the bad input. Here the rejection rests on one function that nothing exercises, and the type underneath it (Money) does not provide the constraint.

---

## EFF-STG-5 — test-gap
- **Location:** REQ-STG-1.4; Tests/Tests.Integrated/CrossDomainOrchestration/RevisedRequirementsStaging.fs:208-232
- **Summary:** REQ-STG-1.4 makes group_id required, but only the length limit is tested. IngestionBaseStageEntryGroupIdIsEmpty is referenced by no test.
- **Resolution:** fix-test

REQ-STG-1.4: '`group_id`: required, string, maximum 36 characters'. The citing theory covers lengths 36 and 37 only. Src has a dedicated case, IngestionBaseStageEntryGroupIdIsEmpty (DataIngestionError.fs), and grep of Tests/ finds no reference to it. The REQ-STG-1.17 misspelt-property theory (StagingIngestionRules.fs:328) covers an absent baseStageEntryGroupId, but asserts only Result.isError (see EFF-STG-1). It does not cover an empty or whitespace-only value, which REQ-STG-1.17's trimming rule makes empty.

**Action:** Add cases for "" and "   " to the REQ-STG-1.4 theory (or the route validation theory) that expect IngestionFileRejected whose records carry IngestionBaseStageEntryGroupIdIsEmpty.

**Why:** A REQ with two rejection criteria (required, maximum length) where only one has a test is partial coverage under a full citation.

---

## EFF-STG-6 — test-gap
- **Location:** REQ-STG-3.3, REQ-STG-3.10; Tests/Tests.Integrated/CrossDomainOrchestration/StageEntryIngestion.fs:425-438
- **Summary:** The only test citing REQ-STG-3.3/3.10 asserts the typed error and never checks that the valid group in the same file was left unstaged. It also runs inside a rolled-back transaction, where it could not see partial persistence anyway (Specimen 9).
- **Resolution:** fix-test

REQ-STG-3.3: 'the entire file is rejected. No staged entries or lines are created.' REQ-STG-3.10: 'either the entire file is ingested ... or no rows are created.' The test builds a file with a valid group (grp-ok) and an invalid one, then asserts only `Error (AsError (IngestionStageEntryInsufficientLines _))`. It never looks for grp-ok. It runs in runCommandRouteAndAutoRollback, so per Specimen 9 any check of absence made from inside would prove nothing. Absence is checked from outside the transaction elsewhere: IngestionRoutes.fs assertRejectsExactly (L225-236, `Assert.Empty staged`) and StagingIngestionRules.fs:345 (`Assert.Empty(stagedFrom path)` with a valid group alongside). Those tests cite only REQ-STG-3.2/3.2.1/1.17. So the all-or-nothing behavior is tested, but not by anything citing 3.3/3.10, and the test that does cite them is satisfied by any typed rejection.

**Action:** Add REQ-STG-3.3 and REQ-STG-3.10 to the name of the route test 'REQ-STG-3.2 REQ-STG-3.2.1 a file mixing valid and invalid records...' (IngestionRoutes.fs:842), which already holds a valid group and asserts nothing staged. Then delete the orchestrator-level test, or re-cite it as REQ-STG-1.14 only.

**Why:** Traceability says 3.3/3.10 are tested by a test that cannot observe the property. If the route test is later renamed or dropped, nothing citing these REQs would notice a partial ingestion.

---

## EFF-STG-7 — test-gap
- **Location:** REQ-STG-8.3; Tests/Tests.Integrated/CrossDomainOrchestration/StageEntryPosting.fs:169-233
- **Summary:** The REQ-STG-8.3 test never runs shadow post or reads the before/after trial balances shadow post returns. It calls batch post and computes its own trial balances.
- **Resolution:** fix-test

REQ-STG-8.3: 'Shadow post must produce a trial balance before posting and a trial balance after posting.' The test, named 'the difference between the two trial balances is the staged amount', calls `fetchTrialBalanceData context asOf` itself before and after `StageEntryOrchestration.post contextForPost` (L208-211). The real post, not the shadow route. It compares deltas against staged lines. That shows batch post moves the ledger by the staged amounts. It does not show that shadow post's result carries before and after balances. Swap PostStageEntriesFullResult.trialBalanceBefore and trialBalanceAfter, or return the same list for both, and this test stays green. The behavior is covered by tests citing other IDs: IngestionRoutes.fs:448-450 (REQ-STG-8.1/8.4, asserts after = before + 60.10 on F-5300 from the shadow route's result) and StagingIngestionRules.fs:879-888 (REQ-STG-8.5).

**Action:** Re-cite the route test at IngestionRoutes.fs:430 as REQ-STG-8.3 as well (it already asserts the returned before/after delta). Re-cite the StageEntryPosting test as REQ-STG-9.2/9.4 (what it actually shows), or rewrite it to go through PostStageEntries with isShadow=true and assert on the returned trialBalanceBefore and trialBalanceAfter.

**Why:** Smell test 2: 'If I deleted the operation and asserted against the untouched state...'. Here the shadow operation under test is never invoked at all.

---

## EFF-STG-8 — test-gap
- **Location:** REQ-STG-8.2; Tests/Tests.Integrated/CrossDomainOrchestration/StageEntryPosting.fs:144-161; Src/Ui.InterfaceBridge/Routes/IngestionRoutes.fs:178-179
- **Summary:** The test named 'shadow post fails when staged entry is in closed fiscal period' calls batch post (StageEntryOrchestration.post), so no test drives the shadow route with an entry that fails validation.
- **Resolution:** fix-test

REQ-STG-8.2: 'If a staged entry would fail validation ... shadow post must surface that failure identically.' At L156 the test invokes `Business.CrossDomainOrchestration.StageEntryOrchestration.post context`, the batch-post function, under the IngestShadowPostStageEntries action tag. Shadow is chosen in the route (IngestionRoutes.fs L178-179: `if input.isShadow then runCommandRouteAndAutoRollback ...`). The only route-level failure test, REQ-STG-9.8 (IngestionRoutes.fs:516), passes isShadow = false. So the shadow branch is never shown to return the domain error rather than, for example, a rolled-back 'success' carrying a trial balance. The same-function design makes a divergence unlikely, but the test name claims a path it does not exercise.

**Action:** Add a route test that stages a closed-period entry (the 9.8 poison group) and calls postThroughRoute true, asserting isCorrectError JournalEntryHeaderEntryDateInvalid. Rename the orchestrator test to say 'batch post' and cite REQ-STG-9.2 only.

**Why:** A test name says which behavior it protects. When the body exercises a different entry point, the named path has no protection while traceability reports it covered.

---

## EFF-STG-9 — test-gap
- **Location:** REQ-STG-4.5; Tests/Tests.Integrated/CrossDomainOrchestration/StageEntryIngestion.fs:773-798 (assertion L796)
- **Summary:** The REQ-STG-4.5 test's only assertion is `Assert.NotEmpty(secondResult.newDuplicates)`, a count-only check (Specimen 3) that never confirms the re-import is the entry that was flagged. Its comment wrongly says the query is 'tested in the production code'.
- **Resolution:** fix-test

newDuplicates is every Duplicate that appeared during the pass anywhere in the transaction (ingestDeduplicateAndClassify L118-125), not only entries from this file. The test never checks that the 'Reimport of ignored' entry is Duplicate, or that the Ignored original still has status Ignored and no Duplicate transition. If dedup ignored the Ignored match but flagged any other repeat present, the test would pass. The comment at L774-780 says 'the query itself is tested in the production code', but production code holds no tests. The exact behavior is properly tested in RevisedRequirementsStaging.fs:374-386 ('REQ-STG-7.2 an Ignored original keeps its status, and a later Ingested entry ... is flagged Duplicate'), which asserts both statuses exactly but cites only REQ-STG-7.2.

**Action:** Add REQ-STG-4.5 to the RevisedRequirementsStaging.fs:374 test name, and delete the StageEntryIngestion 4.5 test (or replace its assertion with `Assert.Equal(Duplicate, latestStatus reimport)` plus `duplicateTransitionCount ignoredOriginal = 0`), removing the inaccurate comment.

**Why:** A NotEmpty check on a collection that other data can feed passes on well-shaped garbage. That is smell test 1.

---

## EFF-STG-10 — test-gap
- **Location:** REQ-STG-5.1; Tests/Tests.Integrated/CrossDomainOrchestration/StageEntryClassification.fs:39-44 (assertion L43)
- **Summary:** The test 'REQ-STG-5.1 classifyStagedEntries processes entries with status Ingested' asserts only `Assert.NotEmpty(fullResult.classificationResults)`, which is Specimen 3 under a hollow name.
- **Resolution:** fix-test

The assertion holds as long as any line of any entry was evaluated. It does not show which statuses were taken up or that NoMatch/Conflict entries are re-run. The behavior is fully tested by the REQ-STG-5.1 status theory in RevisedRequirementsStaging.fs:276-305, which gives all eight statuses an explicit verdict. This test adds nothing and duplicates a vector the README says to test once.

**Action:** Delete StageEntryClassification.fs:38-45. The RevisedRequirementsStaging 5.1 theory already covers the requirement.

**Why:** README: 'Do not test the same thing twice.' A redundant, toothless test inflates the citation count without adding protection.

---

## EFF-STG-11 — test-gap
- **Location:** REQ-STG-9.1; Tests/Tests.Integrated/CrossDomainOrchestration/StageEntryPosting.fs:267-283 (assertion L281)
- **Summary:** The orchestrator-level 'REQ-STG-9.1 batch post happy path' test calls itself 'Deliberately toothless'. Its only assertion is `Assert.Equal(0, postablesAfter |> List.length)`, the same shape as the purged Specimen 9.
- **Resolution:** fix-test

After post, the only check is that nothing Classified or Reviewed remains. A post that moved every entry to Ignored, or that wrote no journal entries but changed statuses, would pass. Specimen 9's purged example was this exact assertion. REQ-STG-9.1 is properly covered at the route by IngestionRoutes.fs:472-508, which finds the journal entries outside the transaction and checks Posted. This test adds no protection.

**Action:** Delete StageEntryPosting.fs:267-283. The route test and REQ-STG-9.7 (L460) cover the behavior.

**Why:** A test that admits it is toothless is still counted as coverage by traceability. Specimen 9 already ruled on this shape.

---

## EFF-STG-12 — test-gap
- **Location:** REQ-STG-7.5; Tests/Tests.Integrated/CrossDomainOrchestration/StageEntryIngestion.fs:749-765
- **Summary:** REQ-STG-7.5 (flagging must not alter lines or their account assignments) is checked through one field of one line: the credit line's account code.
- **Resolution:** fix-test

The test asserts the entry is Duplicate and that the credit line still resolves to F-1270. It does not compare amount, line type or memo with the input, and it does not check that the debit line, which arrived null, is still null. A dedup that cleared memos, changed amounts, or (through some future refactor) let classification assign the null debit line would pass. For grp-008 the input is fixed and known (65.00 Debit null / 65.00 Credit F-1270).

**Action:** Compare the duplicate entry's full line set (amount, line type, account, memo) against the values built in buildTestRows for grp-008, for example as a sorted tuple list, including the debit line's None account.

**Why:** A REQ that says 'must not alter X' needs the whole of X compared before and after. A one-field check covers only part of the REQ.

---

## EFF-STG-13 — test-gap
- **Location:** REQ-STG-3.1/3.4/3.9; Tests/Tests.Integrated/CrossDomainOrchestration/StageEntryIngestion.fs:195
- **Summary:** Hard-wired count `Assert.Equal(12, fullResult.stagedEntries |> List.length)` (Specimen 1).
- **Resolution:** fix-test

12 is the number of distinct group_ids buildTestRows happened to contain on the day the test was written. Adding a group to the shared row list (as grp-011 and grp-012 were added) breaks the test for the wrong reason, and a pipeline that dropped one group while someone also removed a row would pass. The expected count can be computed from the same rows, for example `rows |> List.map _.baseStageEntryGroupId |> List.distinct |> List.length`.

**Action:** Derive the expected entry count from buildTestRows' distinct group IDs, and compare the set of staged descriptions or references against the rows' set, not only the count.

**Why:** Specimen 1: 'The expected value is derived from the same fixture data the code under test reads'. Test README: 'Do not ever write tests that assume the count of anything ... is a constant.'

---

## EFF-STG-14 — test-gap
- **Location:** REQ-STG-2.22; Tests/Tests.Integrated/CrossDomainOrchestration/StageEntryIngestion.fs:279-291
- **Summary:** The test named for REQ-STG-2.20–2.23 makes no assertion about changed_at (REQ-STG-2.22), even though 2.22 is not waived.
- **Resolution:** fix-test

REQ-STG-2.22: 'Audit record changed_at is a non-null Instant.' The test asserts fromStatus = None (2.20), toStatus = Ingested (2.21) and mechanism = StageIngestion (2.23), and uses the instant only as a sort key. No assertion ties the instant to anything, such as the ingestion operation's initiation instant (`context |> Context.getInitiationInstant`, which constructGroup uses at StageEntryOrchestration.fs). REQ-STG-2.22 is absent from the Waived table, while the comparable structural REQs 2.1/2.10/2.18 are waived.

**Action:** Either assert `Assert.Equal(context |> Context.getInitiationInstant, initialTransition |> StageEntryStatusTransition.instant)` in this test, or move REQ-STG-2.22 to the Waived table with the value-type rationale used for 2.1, and drop it from the test name.

**Why:** A test name that lists a REQ it never asserts tells traceability the requirement is tested when it is not.

---

## EFF-STG-15 — test-gap
- **Location:** REQ-STG-2.4; Tests/Tests.Integrated/InterfaceBridge/IngestionRoutes.fs:571-595
- **Summary:** 'REQ-STG-2.4 CreateIngestionSource route happy path' cites a staged-entry FK requirement but never creates a staged entry. It actually tests REQ-STG-3.15.
- **Resolution:** fix-test

REQ-STG-2.4: 'Staged entry must reference a source in ingestion.source (source_id foreign key, not null).' The test creates an ingestion source through the route and reads it back by name. No staged entry is involved. The behavior shown is source creation (REQ-STG-3.15), which StagingIngestionRules.fs:521 already covers with created-at/modified-at assertions. REQ-STG-2.4 is legitimately covered by StageEntryIngestion.fs:212-220 and StagingIngestionRules.fs:476-493 (staged with that source's ID).

**Action:** Rename the route test to cite REQ-STG-3.15 (or delete it as redundant with StagingIngestionRules.fs:521), and leave REQ-STG-2.4 to the tests that stage an entry and check its source.

**Why:** A citation on a test that does not exercise the cited behavior is false coverage. If the real 2.4 tests were removed, traceability would still report 2.4 tested.

---

## EFF-STG-16 — test-gap
- **Location:** REQ-STG-5.10; Tests/Tests.Integrated/CrossDomainOrchestration/StageEntryIngestion.fs:242, 257-259
- **Summary:** The only test citing REQ-STG-5.10 shows that a parser-assigned line has no match rows. It never shows that every matching rule, including losers and every rule in a tie, is recorded under the run ID.
- **Resolution:** fix-test

REQ-STG-5.10 requires 'every rule that matched it is recorded against the line under the run ID — all matching rules, including those that lost to a higher priority and every rule in a tie.' The citing test asserts only `Assert.Empty(debitLineRuleIds)` for a line classification never evaluated. The 5.5 test (StageEntryClassification.fs:164) checks only that the winner is among the recorded rules. The 5.6 tie test records nothing. Positive coverage exists at a lower layer under another spec: ClassificationClaimantsAndRuns.fs:426 and :449 (REQ-CR-8.2 winning+losing, and tie, rows). Under the layer doctrine that may be enough, but nothing citing REQ-STG-5.10 exercises its positive half.

**Action:** Add REQ-STG-5.10 to the REQ-CR-8.2 test names at ClassificationClaimantsAndRuns.fs:426 and :449, or extend the StageEntryClassification 5.5 test to assert the recorded rule set equals {DoorDash rule, generic TestBank rule} exactly.

**Why:** Behavioral coverage asks whether a citing test exercises the behavior. A negative-only citation of a REQ that is mostly about what must be recorded is partial.

---

## EFF-STG-17 — stale-reference
- **Location:** REQ-STG-10.2; Tests/Tests.Integrated/CrossDomainOrchestration/StageEntryFetching.fs:455-488
- **Summary:** A commented-out REQ-STG-10.2 'classification rule id' filter test is still in the file, with a note that the spec 'still names a classification rule id'. The spec already replaced that filter on 2026-09-26.
- **Resolution:** fix-test

The block comment says: 'Commented out pending Dan's decision. The spec's filter list for staged-entry fetches still names a classification rule id...' REQ-STG-10.2's Why line reads '...classification rule ID replaced by journal entry IDs 2026-09-26', and the active filter list names 'journal entry ID, and journal entry line ID' with no rule ID. The JE-ID filters are tested (RevisedRequirementsStaging.fs:408-426). The decision the comment waits on has already been made, and the dead block refers to StageEntryLine.accountClassificationRuleId, which no longer exists. The block also puts a REQ-STG-10.2 ID inside a comment that no test backs (README Naming rule), though other tests in the file do cover 10.2.

**Action:** Delete the commented-out block at StageEntryFetching.fs:455-488.

**Why:** A note telling agents a decision is pending, when it has already been made, risks someone reviving a filter the spec withdrew.

---

## EFF-STG-18 — test-gap
- **Location:** Tests/Tests.Integrated/CrossDomainOrchestration/RevisedRequirementsStaging.fs:46; Tests/Tests.Integrated/CrossDomainOrchestration/StagingIngestionRules.fs:182; Tests/Tests.Helpers/TestDataStage.fs:876, 901, 1039 (REQ-STG-4.1.2, REQ-STG-5.2)
- **Summary:** Comments in three files name REQ-STG-4.1.2 (and, in the fixture, REQ-STG-5.2) to explain helper or fixture design, but no test in those files covers the requirement. The Tests README forbids exactly this.
- **Resolution:** fix-test

Tests/README.md, Naming: an ID in a comment counts as a citation and is forbidden when no test in the same file backs it. The README names 'a comment explaining a fixture or helper by naming the requirement it exists to serve' as the trap that has tripped traceability before. RevisedRequirementsStaging.fs:46 and StagingIngestionRules.fs:182 cite REQ-STG-4.1.2 to justify advancing the instant; neither file has a 4.1.2 test (the real ones are StageEntryUpdate.fs:405 and :428). TestDataStage.fs, a helper project with no tests, cites REQ-STG-5.2 at L876/L901 and REQ-STG-4.1.2 at L1039. Today the real tests elsewhere keep traceability honest. If StageEntryUpdate's 4.1.2 tests were deleted, these comments alone would keep 4.1.2 reported as tested.

**Action:** Rewrite the five comments in prose without the REQ- prefix (for example 'the one-transition-per-instant rule of spec section 4').

**Why:** Traceability greps whole files, so a comment citation makes the coverage report depend on prose rather than on tests.

---

