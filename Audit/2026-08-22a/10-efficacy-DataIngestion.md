# data-ingestion-efficacy-auditor

## STG-EFF-1 — test-gap
- **Location:** Tests/Tests.Integrated/ModelOrchestrator/StageEntryClassification.fs, line 113, REQ-STG-5.5
- **Summary:** REQ-STG-5.5 test verifies the classification engine identified the correct priority winner but never verifies the staged line was actually updated with that winner's account ID and rule ID.
- **Resolution:** fix-test

REQ-STG-5.5 says: 'the classifier assigns the highest-priority rule's account ID to the line and records the classification_rule_id on the staged line.' The test (line 113) checks classificationResults for the DoorDash debit line, confirms the outcome is ManyMatchesClearWinner, and confirms the winner's account maps to F-5350. But it never reads the staged line back to verify accountId = Some fixture.Data.food5350Id or that classificationRuleId = Some (DoorDash rule's ID). The companion test for REQ-STG-5.3 (line 44) only asserts Option.isSome on the debit line's classificationRuleId -- presence, not identity. And 5.4 (line 67) verifies line assignment for the single-match case (grp-011), but the multi-match-with-clear-winner case (grp-001 debit) has no line-level assertion anywhere in the suite. If the engine correctly identified the winner but a write-back bug stored a different account (e.g., the loser's account), no test would fail.

**Action:** Add assertions to the 5.5 test that read the staged line and verify accountId = Some fixture.Data.food5350Id and classificationRuleId = Some (the DoorDash rule's ID), mirroring the assertion pattern already used in the 5.4 test.

**Why:** The classification result is the engine's recommendation; the staged line is the persistent state downstream processes read. Testing the recommendation without testing the persistence means a broken write-back is invisible. The 5.4 test proves the write path works for single matches, but a code path that diverges for multi-match winners would go undetected.

---

## STG-EFF-2 — test-gap
- **Location:** Tests/Tests.Integrated/ModelOrchestrator/StageEntryIngestion.fs, line 263, REQ-STG-1.13
- **Summary:** REQ-STG-1.13 enumerates four fields that must be consistent within a group (entry_date, description, fi_source, fi_reference), but only entry_date inconsistency is tested.
- **Resolution:** fix-test

REQ-STG-1.13 says: 'All records in a group (same group_id) must carry the same entry_date, description, fi_source, and fi_reference values.' The test (line 263) creates a group with inconsistent entry_date (today vs today+1) and asserts the typed error IngestionBaseStageGroupIdDistinctDataViolation. No test sends a group with inconsistent description, inconsistent fi_source, or inconsistent fi_reference. If the validation code checks only entry_date consistency and silently accepts inconsistent descriptions, sources, or references within a group, the test suite would not catch it. Each field is a separate failure vector -- a parser that accidentally emits different descriptions for the same group_id is a different defect than one that emits different dates.

**Action:** Extend the existing test into a Theory with four InlineData rows, one per field (entry_date, description, fi_source, fi_reference), each constructing a two-record group where only that field differs between the records. All four should produce IngestionBaseStageGroupIdDistinctDataViolation.

**Why:** The requirement explicitly lists four fields. Testing one of four leaves three failure vectors uncovered. A regression that breaks consistency checking for description, fi_source, or fi_reference would pass CI.

---

## STG-EFF-3 — test-gap
- **Location:** Tests/Tests.Integrated/ModelOrchestrator/StageEntryIngestion.fs, line 145, REQ-STG-2.6
- **Summary:** REQ-STG-2.6 says source_file records the 'filename (not path)' but the test asserts a full path, and the code stores a full path, so the test cannot detect the violation.
- **Resolution:** fix-test

REQ-STG-2.6: 'Staged entry source_file cannot be null. Records the filename (not path) of the base staging format file that produced this entry.' The test setup passes '/tmp/stg-test-checking.jsonl' to SourceFile.create (StageEntryIngestion.fs line 87 via runPipeline), which stores the string verbatim (SourceFile.create at Src/Model/DataIngestion/StageEntryComponent.fs line 73 only trims and validates length; it does not extract the filename component). The assertion at line 145 then checks Assert.Equal("/tmp/stg-test-checking.jsonl", ...), which passes because the stored value IS the full path. The production route (Src/InterfaceBridge/Routes/IngestionRoutes.fs line 34) likewise passes toBeProcessedPath (a full path) to SourceFile.create. The test agrees with the code, and both disagree with the spec's '(not path)' constraint. If someone fixed the code to store only the filename, this test would FAIL -- it is actively blocking a correct fix.

**Action:** The test setup should pass just the filename ('stg-test-checking.jsonl') to SourceFile.create, and the assertion should check for that filename. The production route should extract the filename from the path before creating the SourceFile (e.g., using Path.GetFileName). This is both a test fix and a code fix -- the test-side fix alone would cause the test to fail, revealing the code-side bug.

**Why:** The '(not path)' parenthetical in REQ-STG-2.6 is an explicit design constraint, not a style note. A full path stored in ingestion.staged_entry ties the staged record to a host filesystem layout that may differ between the import machine and any future reader. The filename is the portable identity of the source file.

---
