# test-efficacy-DataIngestion

## EFF-STG-9.3-LABEL — test-gap
- **Location:** Tests/Tests.Integrated/CrossDomainOrchestration/StageEntryPosting.fs:287 (assertions 320-335); REQ-STG-9.3
- **Summary:** No test asserts that a posted journal entry's source is the literal provenance label "Data ingestion import" that REQ-STG-9.3 names; the only source test checks that the label is present, is the same for two institutions, and is not an institution name.
- **Resolution:** fix-test

REQ-STG-9.3 says: 'Source is the fixed provenance label "Data ingestion import"'. The test `REQ-STG-9.3 posted JE source is one fixed provenance label whatever institution the entry came from` asserts only `Assert.True(fromBank |> Option.isSome)`, `Assert.Equal<string option>(fromBank, fromSavings)` and `Assert.NotEqual<string option>(Some name, fromBank)` for each institution name. Its comment says so on purpose: 'The label itself is not asserted: the requirement calls it fixed, not any particular string.' That misreads the requirement, which quotes the string. The other 9.3 tests check other things: StageEntryPosting.fs:268 passes its own source into postStageEntry and checks only description and date, and RevisedRequirementsStaging.fs:393 checks only description and no comments. The label is hard-coded at Src/Business.CrossDomainOrchestration/StageEntryOrchestration.fs:823 (`Some "Data ingestion import"`). If that literal were changed to "Bank import", or to any other constant, every 9.3 test would still pass.

**Action:** In the batch-post provenance test, add Assert.Equal(Some "Data ingestion import", fromBank) next to the existing equality between the two institutions.

**Why:** Smell test 1: a post() that writes any fixed string of the right shape passes. The requirement names a specific label, so a test that does not check that label does not cover the requirement.

---

## EFF-STG-7.3-NOTEQUAL — test-gap
- **Location:** Tests/Tests.Integrated/CrossDomainOrchestration/StageEntryIngestion.fs:731-740; REQ-STG-7.3
- **Summary:** The test for REQ-STG-7.3's rule that voided journal entries do not count asserts `Assert.NotEqual(Duplicate, latestStatus entry)`, a cowardly inequality (Specimen 2).
- **Resolution:** fix-test

Test `REQ-STG-7.3 dedup does not flag entry matching voided JE external reference`: both staged lines carry account codes (F-5650 / F-1270), so after the pipeline the entry should be exactly `Classified` with zero Duplicate transitions. The assertion `Assert.NotEqual(Duplicate, StageTestData.latestStatus entry)` accepts any other status, including NoMatch, Conflict or Ingested left by a classification or dedup step that went wrong. It also reads only the latest status. The sibling tests use `duplicateTransitionCount`, which also rules out a Duplicate transition followed by another transition. The voided-reference entry also has no matching entry beside it in the same pass, so a dedup pass that flagged nothing would also satisfy it (the non-voided positive case lives in a separate test, line 720).

**Action:** Replace the NotEqual with `Assert.Equal(0, StageTestData.duplicateTransitionCount entry)` and `Assert.Equal(Classified, StageTestData.latestStatus entry)`. Optionally stage a non-voided ledger match in the same pass, as the test at line 746 does.

**Why:** Specimen 2: an inequality gives up on stating the expected value. The entry's status after the pipeline is known exactly, so the test should assert it exactly.

---

## EFF-STG-10.6-FOX — test-gap
- **Location:** Tests/Tests.Integrated/InterfaceBridge/IngestionRoutes.fs:643 (assertions 676-682); Tests/Tests.Integrated/CrossDomainOrchestration/StageEntryFetching.fs:656 (assertions 675-691); REQ-STG-10.6
- **Summary:** Both REQ-STG-10.6 tests take the expected status transitions (and lines) from the same composition code they are testing (Specimen 6), and no test pins an entry's transition history independently.
- **Resolution:** fix-test
- **Prior ruling:** ROUTE-ORACLE (2026-10-03) allows a route test to use the orchestrator computation as its oracle only when that computation has its own independent tests. Here the orchestrator-level 10.6 test shares the same composition helpers, so condition (2) of that ruling is not met and the route test's self-comparison is not covered by it.

REQ-STG-10.6 requires each returned entry to include all lines and all status transitions. Route test: `ingestThroughRoute` builds `staged` by calling `fetchFilteredThroughRoute { sourceFile = ... }` (IngestionRoutes.fs:169), which is the same FetchStageEntryFiltered route under test. The test then asserts `Assert.Equal<Guid list>(staged.statusTransitions ids, returned.statusTransitions ids)`, so both sides of the comparison come from that route. Orchestrator test: `payroll` comes from `runPipeline` -> `fetchAllByFile`, which builds its result with `StageEntryLine.fetchByHeaderIdList`, `StageEntryStatusTransition.fetchByHeaderIdList` and `compileFromSubLists` (StageEntryOrchestration.fs:229-240). `fetchFiltered` builds its result with exactly the same three functions (StageEntryOrchestration.fs:1010-1018). If `fetchByHeaderIdList` dropped all but the latest transition, or `compileFromSubLists` misassigned transitions between headers, the expected and actual values would be wrong in the same way and both tests would pass. Of the remaining assertions, `Assert.NotEmpty` and `Assert.Contains(currentStatus, toStatuses)` are satisfied by a single latest transition. The payroll entry is known to have exactly two transitions (Ingested/StageIngestion, then Classified/Classifier), but no test asserts that.

**Action:** In StageEntryFetching's 10.6 test, derive the expected transitions independently, as (toStatus, mechanism) pairs [Ingested/StageIngestion; Classified/Classifier] for the payroll entry, which is fully parser-assigned and so classified straight to Classified. Assert on those pairs instead of on IDs read through the shared composition helpers. In the route test, compare against values the test set up, not against a second call to the same route.

**Why:** Specimen 6: no function in the call chain of the function under test may produce the expected value. The transition history is the whole point of 10.6, and at present it is checked only against itself.

---

## EFF-STG-5.11-7.5.1-FOX — test-gap
- **Location:** Tests/Tests.Integrated/CrossDomainOrchestration/StagingIngestionRules.fs:711 (assertions 727-732) and :861 (assertions 871-875); REQ-STG-5.11, REQ-STG-7.5.1
- **Summary:** The 'every entry of these statuses' equality in the REQ-STG-5.11 and REQ-STG-7.5.1 return-value tests uses `fetchByStatusList`, the same call each operation uses to build its own return list (Specimen 6).
- **Resolution:** fix-test

`applyAccountClassification` returns `stagedEntries = [Ingested; Classified; NoMatch; Conflict; Reviewed] |> fetchByStatusList context` (StageEntryOrchestration.fs, end of applyAccountClassification). The 5.11 test's expected set is `[Ingested; Classified; NoMatch; Conflict; Reviewed] |> fetchByStatusList s.Context`, followed by `Assert.Equal<Set<StageEntryHeaderId>>(expected ids, returnedIds)`. Likewise `deduplicateStagedEntries` returns `[Ingested] |> fetchByStatusList context` (StageEntryOrchestration.fs:357), and the 7.5.1 test's expected value is `[ StagedEntryStatus.Ingested ] |> fetchByStatusList s.Context`. For every staged entry the test did not create itself, the 'every ... and no entry of any other status' claim depends entirely on `fetchByStatusList` (and the latest-status derivation in `StageEntryHeader.fetchByStatus`) agreeing with itself. The membership checks on the test's own entries (Contains / DoesNotContain) do test independently, but only for those entries.

**Action:** Derive the expected universe without the function under test: for example, assert membership and non-membership only for entries the test created with known statuses, and drop the whole-set equality to fetchByStatusList. Or compute the expected set from each entry's latest transition read through fetchByStageEntryHeaderId, which uses a different read path.

**Why:** Specimen 6: if fetchByStatusList mis-derives status (for example, by picking the wrong audit row), both sides of the equality are wrong in the same way and the test passes.

---

## EFF-STG-2.6-NEG — test-gap
- **Location:** REQ-STG-2.6; Tests/Tests.Integrated/CrossDomainOrchestration/RevisedRequirementsStaging.fs:252; Src/Ui.InterfaceBridge/Routes/IngestionRoutes.fs:84
- **Summary:** REQ-STG-2.6's rejection of a null or whitespace-only source_file has no test; only the 150/151-character length boundary is tested.
- **Resolution:** fix-test

REQ-STG-2.6: 'Staged entry source_file cannot be null or whitespace only, and cannot exceed 150 characters.' `SourceFile.create` returns `IngestionSourceFileIsEmpty` for blank input (StageEntryComponent.fs:76-77), but grepping Tests/ for `IngestionSourceFileIsEmpty` finds nothing. The only 2.6 tests are the length theory (RevisedRequirementsStaging.fs:252, which matches `IngestionSourceFileTooLong`) and a happy-path field read (StageEntryIngestion.fs:240). A caller can reach a blank value: the UpdateStageEntry route converts `input.sourceFileUpdate` through `SourceFile.create` (IngestionRoutes.fs:84), and the fetch filter's `sourceFile` goes through the same constructor (IngestionFieldConverters.fs:217).

**Action:** Add a theory ("", "   ") that sends a manual update through the UpdateStageEntry route with sourceFileUpdate set to that value. It should match `IngestionSourceFileIsEmpty` and assert the entry is unchanged.

**Why:** Negative coverage: the requirement states a rejection criterion and no test proves the rejection happens. A failure that is never exercised can break silently.

---

## EFF-STG-7.2-ORIGINAL-STATUSES — test-gap
- **Location:** REQ-STG-7.2; Tests/Tests.Integrated/CrossDomainOrchestration/StageEntryIngestion.fs:575, :630; RevisedRequirementsStaging.fs:374
- **Summary:** Two of the statuses REQ-STG-7.2 says count as the original are never tested on their own: 'Duplicate', and 'Posted' with a voided journal entry. In every existing test, another rule would also produce the asserted flag.
- **Resolution:** fix-test

REQ-STG-7.2: 'Every staged entry counts when establishing the original, whatever its status — including Ignored, Posted, Reviewed and Duplicate.' The rationale under REQ-STG-7.3 adds: 'A re-import of a transaction that did come through staging is flagged under REQ-STG-7.2 whether or not its journal entry was voided.' Coverage: Ignored as original (RevisedRequirementsStaging.fs:374) and Reviewed as original (StageEntryIngestion.fs:536) are each tested on their own. Posted as original (StageEntryIngestion.fs:575) is not: its own comment notes the posted journal entry makes the ledger arm (7.3) match, so the re-import would be flagged by 7.3 even if Posted entries were left out of the 7.2 ordering. The only test that isolates it would void the journal entry and re-import, and grepping the STG test files shows no such test. Duplicate as original: in the test at line 630 the earliest entry (`first`) is never Duplicate, so a 7.2 ordering that left out Duplicate entries would still flag `third` against `first`.

**Action:** Add two dedup tests. (1) Post a staged entry, void its journal entry, re-ingest the same source and reference, run dedup, and assert the re-import is Duplicate. Here only 7.2 can flag it, because 7.3 ignores voided entries. (2) Stage an entry that is Duplicate and was ingested first, then a later Ingested entry with the same key, and assert the later one is flagged.

**Why:** A requirement that lists several cases is covered only when each case is tested where no other rule could produce the same outcome. Here two of the four listed statuses are always masked by another rule.

---

## EFF-STG-8.4-PARTIAL — test-gap
- **Location:** Tests/Tests.Integrated/InterfaceBridge/IngestionRoutes.fs:430 (assertions 459-461) and :574 (603-604); REQ-STG-8.4
- **Summary:** REQ-STG-8.4 says shadow post leaves 'any staging data' unchanged, but the tests check only the latest status of one of the two staged entries. They never check that the staging writes posting makes (journal entry IDs on the header and lines, from REQ-STG-9.10) are absent afterwards.
- **Resolution:** fix-test

REQ-STG-8.4 (amended 2026-10-03): 'Shadow post leaves no change to any staged entry's status or any staging data; any staging writes it makes roll back with the ledger.' The path shadow post shares with batch post writes `journalEntryHeaderIdUpdate` on the staged header and `updateJournalEntryLineId` on each line (postStageEntry, StageEntryOrchestration.fs). The shadow route test refetches only `(ingested.stagedEntries |> List.head)` and asserts `Assert.Equal(Classified, refetched |> latestStatusOf)`. The second staged entry, the header's journalEntryHeaderId, the lines' journalEntryLineId and the transition count are not asserted. The REQ-STG-8.2 shadow test (line 603) has the same gap. If an implementation committed the 9.10 links but rolled back the ledger, the staged entries would point at journal entries that do not exist, and these tests would still pass.

**Action:** After the shadow route returns, refetch every ingested entry and assert it equals the entry as read before the shadow post (Assert.Equal(before, after), as the update tests do). At minimum, assert that journalEntryHeaderId is None and every line's journalEntryLineId is None.

**Why:** The amendment added 'any staging writes' to this requirement precisely because the posting path writes staging data. Checking only status leaves the amended clause untested.

---

## EFF-STG-1.17-UNTYPED-RECORDS — test-gap
- **Location:** Tests/Tests.Integrated/CrossDomainOrchestration/StagingIngestionRules.fs:332 (assertions 349-355); REQ-STG-1.17
- **Summary:** The test for a property spelled by its column name instead of its documented key matches IngestionFileRejected and the rejected line numbers, but never the per-record error, so any record-level failure on lines 1-2 passes (partial Specimen 4).
- **Resolution:** fix-test

The test asserts `Error (AsError (IngestionFileRejected (rejectedPath, records)))`, the path, and `Assert.Equal<Set<int>>(set [1; 2], records |> List.collect _.lineNumbers |> Set.ofList)`. It never inspects `r.error`. Any rejection of those two records for a reason other than the misspelt property passes: for example a source-resolution error, a group-consistency error, or a JSON failure on an unrelated field. Other rejection tests in this suite check the error case on each record (IngestionRoutes.fs `assertRejectsExactly` compares (lines, group_id, error.CaseName)).

**Action:** Assert the error case on each rejected record, for example by comparing (lineNumbers, groupId, error.CaseName) as assertRejectsExactly does. Assert the field it names too, if the JSON error now names the missing property.

**Why:** Specimen 4: the wrapper error is typed, but the actual cause is not checked. If you cannot name the error case, the test does not know what the code does.

---

## EFF-STG-1.14-DUP — test-gap
- **Location:** Tests/Tests.Integrated/CrossDomainOrchestration/StageEntryIngestion.fs:413 and :454; REQ-STG-1.14
- **Summary:** Two REQ-STG-1.14 tests check the same failure: a single-record group rejected with IngestionStageEntryInsufficientLines. The second test's name ('even alongside a valid group') makes a claim it never asserts.
- **Resolution:** fix-test

Line 413 ingests one single-record group and matches `IngestionStageEntryInsufficientLines`. Line 454 adds a valid two-record group and matches the same error, with nothing else asserted. Its comment says the all-or-nothing half 'belongs to the route test', so the valid group contributes nothing to what the test checks. Tests/README.md lists 'Do not test the same thing twice' as an unacceptable practice. The name implies a property about the valid group (that it is not staged) which the body does not check.

**Action:** Delete the test at line 454, or rename it and make it assert something distinct.

**Why:** A duplicate test with a name that promises more than its body checks gives false confidence and adds maintenance cost for no extra coverage.

---

## EFF-STG-1.16-WAIVER — test-gap
- **Location:** Specs/Behavioral/DataIngestion.md Waived table (REQ-STG-1.16); Tests/Tests.Integrated/CrossDomainOrchestration/StageEntryIngestion.fs:352
- **Summary:** REQ-STG-1.16's waiver says there is 'no persistent state to assert against', but a test already asserts its behaviour (a group_id reused in a later file makes a separate staged entry) on persisted state, under REQ-STG-1.3.
- **Resolution:** dan-decides

Waiver for REQ-STG-1.16 (Dan, 2026-10-03): 'Enforced by construction ... so records from different files can never merge. No persistent state to assert against.' The test `REQ-STG-1.3 the same group_id in a second file produces a separate staged entry` ingests two files with group_id "grp-reused" and asserts `Assert.True(firstId <> secondId, "A reused group_id in a later file must not join the earlier entry")`, with each side read back by `fetchAllByFile`. That is the 'Not globally unique' clause of REQ-STG-1.16, checked against persisted rows. So the stated reason for the waiver is not accurate.

**Action:** Choose one: cite REQ-STG-1.16 on this test and remove the waiver, or reword the waiver reason so the test no longer contradicts it.

**Why:** A waiver reason contradicted by an existing test misrepresents the requirement's status (waived vs tested) in the traceability record.

---

## EFF-STG-4.1-MISCITE — test-gap
- **Location:** Tests/Tests.Isolated/Model/DataIngestion/StageEntryStatusTransition.fs:66, :76; REQ-STG-4.1 vs REQ-STG-2.23
- **Summary:** Two tests that check change-mechanism parsing cite REQ-STG-4.1, which is about staged-entry statuses. The behaviour they test is REQ-STG-2.23's.
- **Resolution:** fix-test

`REQ-STG-4.1 StageStatusChangeMechanism.fromString accepts all valid values` and `REQ-STG-4.1 StageStatusChangeMechanism.fromString rejects invalid string` check the five change mechanisms (StageIngestion, Classifier, Deduplicator, Operator, LedgerPoster) and the rejection of anything else. REQ-STG-4.1 lists the eight statuses. The mechanism list, including 'Must be one of', is REQ-STG-2.23. The only test citing 2.23 (StageEntryIngestion.fs:306) checks that one ingestion transition carries StageIngestion. It does not check the closed set or the rejection.

**Action:** Re-cite both tests to REQ-STG-2.23.

**Why:** Traceability counts a test only under the REQ it names. As cited, REQ-STG-2.23's 'must be one of' rejection appears untested, while REQ-STG-4.1 is credited with coverage of behaviour that is not its own.

---

