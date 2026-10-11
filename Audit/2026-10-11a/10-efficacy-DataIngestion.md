# test-efficacy-DataIngestion

## EFF-STG-6.2-FIELDS — test-gap
- **Location:** REQ-STG-6.2; Tests/Tests.Integrated/CrossDomainOrchestration/StageEntryUpdate.fs:165,185,209,459; Tests/Tests.Integrated/InterfaceBridge/IngestionRoutes.fs:340,1083; Src/Business.FinancialServices.DataIngestion/StageEntryHeader.fs:400-424; Src/Ui.InterfaceBridge/Routes/IngestionRoutes.fs:84-94
- **Summary:** REQ-STG-6.2 lets the operator set any field on the staged entry and its lines. No test ever successfully changes entry_date, ingestion source, fi_reference, source_file, or a line's line type through the manual update.
- **Resolution:** fix-test

REQ-STG-6.2: 'The manual update mechanism allows the operator to set any field on the staged entry and its lines, including status, except the journal entry ID and journal entry line IDs'. The citing tests exercise only status (StageEntryUpdate.fs:209, :287), account (StageEntryUpdate.fs:165, :185; IngestionRoutes.fs:340), amount and added or removed lines (StageEntryUpdate.fs:459, :488), description (StageEntryUpdate.fs:488; IngestionRoutes.fs:1083) and memo (IngestionRoutes.fs:1083). I grepped Tests/ for `entryDateUpdate`, `ingestionSourceUpdate`, `fiReferenceUpdate` and `sourceFileUpdate` set to anything other than NoChange, and for the route contract's `entryDate`, `ingestionSource`, `fiReference` and `sourceFileUpdate` set with `SetTo`. There are zero hits. `entryTypeUpdate = SetTo` appears only at StageEntryUpdate.fs:661 and :689, and both are REQ-STG-6.5 rejection cases. So these code paths have never run under test: the `entry_date = @entry_date`, `source_id = @source_id`, `fi_reference = @fi_reference` and `source_file = @source_file` SET clauses in StageEntryHeader.update (StageEntryHeader.fs:400-424), and the route's name-to-source conversion for `ingestionSource` (Src IngestionRoutes.fs:86-93, which calls IngestionSource.fetchByName). A wrong column name, a swapped parameter, or a source lookup that resolved the wrong row would all pass the suite. Of the multiple behaviours this REQ describes, only some are covered.

**Action:** Add one theory, at the route (UpdateStageEntry), with one case each for entryDate, ingestionSource (by name: TestBank to TestSavings), fiReference, sourceFileUpdate and a balanced line-type swap. Each case sets one field, refetches the entry outside the route's transaction, and asserts the new value. Locate the entry by its header ID, not by the field being changed.

**Why:** A REQ that lists several behaviours is covered only when each one is exercised. Without a test, the unexercised SET clauses could be broken (wrong column, wrong parameter) and the suite would stay green. Correcting a mis-dated or mis-sourced entry in Saturday review relies on exactly these fields, and fi_reference and source both feed dedup (REQ-STG-7.2/7.3).

---

## EFF-STG-9.4-LINE-UNNAMED — test-gap
- **Location:** REQ-STG-9.4, REQ-STG-4.4; Tests/Tests.Integrated/CrossDomainOrchestration/StageEntryPosting.fs:373 (assertion line 392); Src/Business.CrossDomainOrchestration/StageEntryOrchestration.fs:788; Src/Business.FinancialServices.DataIngestion/DataIngestionError.fs:32
- **Summary:** The test that batch post fails loudly on a postable entry with a null account matches `IngestionNoneAccount _` and discards the payload. REQ-STG-4.4 promises an error 'naming the line', and nothing checks which line is named.
- **Resolution:** fix-test

REQ-STG-4.4: a null-account line on a postable entry 'fails shadow post, the pre-posting review and batch post loudly, with a typed error naming the line'. REQ-STG-9.4 also calls a null account at posting time 'a loud failure'. The test `REQ-STG-9.4 batch post fails loudly when a postable entry carries an uncoded line` strips the account from a known line, `entry |> seLines |> List.head` via stripAccountCodeFromFirstLine (lines 91-98), so the test knows that line's ID. It then asserts only `| Error (AsError (IngestionNoneAccount _)) -> Ok ()` (line 392). `IngestionNoneAccount of Guid` carries the line ID (DataIngestionError.fs:32, filled at StageEntryOrchestration.fs:788). A poster that reported the wrong line (the header ID, or a sibling line's ID) would pass this test. The operator would then be sent to fix the wrong line. No other test in the STG suites inspects this payload (I grepped Tests/ for IngestionNoneAccount).

**Action:** Bind the payload and assert it equals the stripped line's ID: `| Error (AsError (IngestionNoneAccount uuid)) -> Assert.Equal(strippedLineId |> StageEntryLineId.value, uuid)`.

**Why:** Specimen 4 sibling: the case is typed but the payload goes unread. Here the payload is the requirement's own promise ('naming the line'). The identity of the error, including what it names, is the thing under test.

---

## EFF-STG-7.2-IGNORED-REPEAT — test-gap
- **Location:** REQ-STG-7.2; Tests/Tests.Integrated/CrossDomainOrchestration/RevisedRequirementsStaging.fs:374; Tests/Tests.Integrated/CrossDomainOrchestration/StageEntryIngestion.fs:546; Src/Business.FinancialServices.DataIngestion/StageEntryHeader.fs:366
- **Summary:** REQ-STG-7.2 says entries already 'Ignored' are left as they are by dedup. The only Ignored entry any dedup test stages is the original, at ordinal 1, which the query never flags whatever its status, so the Ignored exclusion is never exercised.
- **Resolution:** fix-test

The dedup query flags `where ais.current_status not in ('Duplicate', 'Posted', 'Ignored', 'Reviewed') and (ais.ordinal > 1 or ail.journal_entry_id is not null)` (StageEntryHeader.fs:366-367). An entry is protected by the status exclusion only when it is a later repeat (ordinal > 1) or matches a non-voided ledger external reference. The tests cover each excluded status as follows. Posted: the test at StageEntryIngestion.fs:585 posts for real so the ledger arm matches, and says why in its comment. Duplicate: the test at :640 uses a second-ingested Duplicate. Reviewed: the paid Reviewed repeat in StagingIngestionRules.fs:1076. Ignored: the only test is RevisedRequirementsStaging.fs:374 (`REQ-STG-4.5 REQ-STG-7.2 an Ignored original keeps its status...`), where the Ignored entry is ingested first, so ordinal = 1, and it carries a fresh tag with no ledger match. Its `Assert.Equal(Some Ignored, statusOf originalAfter)` holds even with 'Ignored' deleted from the exclusion list. The Reviewed test at StageEntryIngestion.fs:546 has the same blind spot: its Reviewed entry is also the original, the trap the Posted test's comment describes. Reviewed, though, is covered by the 7.5.1 test. A regression that dropped 'Ignored' from the list would make dedup attempt Ignored to Duplicate on any later Ignored repeat. That transition is illegal (confirmValidTransition), so every dedup pass would abort while such an entry exists. No test would catch this before an operator hit it.

**Action:** Add a dedup test with an Ingested original, then a later-ingested repeat moved to Ignored (and optionally an Ignored entry whose source and reference match a non-voided fixture JE, such as TestBank/TXN-001). Run dedup and assert it succeeds, that the Ignored repeat has zero Duplicate transitions and is still Ignored, and that in the same pass an Ingested repeat with the same key is flagged, so 'flagged nothing' cannot explain the result.

**Why:** A requirement that lists cases is covered only where each case is tested in a position where it alone decides the outcome. Smell test 2: deleting the behaviour (the 'Ignored' exclusion) leaves the citing test green.

---

## EFF-STG-7.1-COUNT — test-gap
- **Location:** REQ-STG-7.1, REQ-STG-7.2; Tests/Tests.Integrated/CrossDomainOrchestration/StageEntryIngestion.fs:528 (assertions 535, 540)
- **Summary:** The REQ-STG-7.1/7.2 dedup happy-path test asserts only that two DoorDash entries exist and that exactly one of them is Duplicate. It never says which one, so a pass that flagged the original instead of the later repeat passes.
- **Resolution:** fix-test

Test `REQ-STG-7.1 REQ-STG-7.2 dedup flags entry with same source and fi_reference as existing staged entry` filters the pipeline's entries by description 'DD DoorDash Order 9917223' and asserts `Assert.Equal(2, ddEntries |> List.length)` and `Assert.Equal(1, dupCount)`, where dupCount is the number with currentStatus = Some Duplicate. REQ-STG-7.2 says the earliest-ingested entry is the original and every later one is flagged. In runPipeline, grp-009 is in the first file and grp-010 in the second, later file (StageEntryIngestion.fs:195-208), so grp-010 is the entry that must be flagged. Neither assertion distinguishes grp-009 from grp-010. A dedup that ordered by entry date (grp-010 is dated today, grp-009 today-2, so that ordering happens to agree), or by uuid, or that flagged the earliest entry, could still produce '1 of 2' depending on the data. These are Specimen 1 and Specimen 3 counts with no value assertion. The ordering rule is tested properly elsewhere (RevisedRequirementsStaging.fs:358 makes the later-ingested entry carry the earlier date), so the gap is in this test's assertions, not in REQ coverage.

**Action:** Identify the two entries by their source file (grp-010 is staged from /tmp/stg-test-checking-second.jsonl). Assert that the second-file entry's latest status is Duplicate and that the first-file entry has zero Duplicate transitions. Drop the bare counts, or keep them only alongside those value assertions.

**Why:** Specimen 3: a count that never looks inside. One of two flagged is satisfied by flagging the wrong one. Counts are allowed alongside value assertions, never instead of them.

---

