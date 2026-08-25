# code-truthfulness-auditor-model-orchestrator

## DEDUP-1 — contradiction
- **Location:** Src/Model/DataIngestion/StageEntryHeader.fs lines 320-329, REQ-STG-4.6, REQ-STG-7.2
- **Summary:** The fetchDuplicates SQL query's exclusion list omits 'Reviewed', allowing the dedup operation to attempt the forbidden Reviewed -> Duplicate transition.
- **Resolution:** fix-code

The duplicates CTE in fetchDuplicates filters `where ais.current_status not in ('Duplicate', 'Posted', 'Ignored')`. This correctly excludes three statuses whose -> Duplicate transition is forbidden by REQ-STG-4.6. But it omits 'Reviewed', which also has no permitted -> Duplicate transition (Reviewed only permits -> Posted and -> Ignored per the transition table at lines 143-144 of DataIngestion.md).

The deduplicateStagedEntries function (StageEntryOrchestration.fs lines 303-318) takes every header returned by fetchDuplicates and calls updateHeaderStatus with toStatus = Duplicate. updateHeaderStatus calls confirmValidTransition (StageEntryStatusTransition.fs line 137), which checks the validTransitions function (line 31). For Reviewed, the valid targets are [Posted; Ignored] -- Duplicate is not in the list. The transition is rejected.

Because deduplicateStagedEntries aggregates results with convertListOfResultsToResultsList, one invalid transition causes the entire dedup to fail, and because dedup runs inside ingestRawToStageThenDeduplicateAndClassify (which is wrapped in runCommandRouteAndAutoCompleteTransaction), the entire ingestion rolls back.

The scenario is reachable: (1) Two entries with the same (source_id, fi_reference) are ingested. (2) The second is flagged Duplicate. (3) The operator overrides it to Reviewed via REQ-STG-6.3 (Duplicate -> Reviewed, the legitimate-duplicate override). (4) A subsequent file containing the same key is ingested. (5) The dedup query returns the operator-approved Reviewed entry (ordinal > 1, not excluded) alongside the new Ingested entry. (6) The Reviewed -> Duplicate transition fails. (7) Ingestion rolls back completely.

**Action:** Add 'Reviewed' to the fetchDuplicates exclusion list: change `not in ('Duplicate', 'Posted', 'Ignored')` to `not in ('Duplicate', 'Posted', 'Ignored', 'Reviewed')`. Reviewed entries should still participate in the partition for ordinal calculation (they act as matches per REQ-STG-7.2), but should not be returned as transition candidates. This respects the operator's override decision and aligns the detection logic with the permitted transitions in REQ-STG-4.6.

**Why:** An operator-approved entry (the explicit Duplicate -> Reviewed override path in REQ-STG-6.3) can make subsequent file imports fail entirely. The operator's approval decision is silently ignored by the dedup pass, which tries to re-flag the entry and hits the transition guard. The blast radius is the whole ingestion -- not just the one duplicate -- because the all-or-nothing error propagation rolls back every entry in the file.

---

## SD-1 — statement-delta
- **Location:** Dan's statement (this run), StageEntryHeader.fs (updateHeaderStatus, insertNewToDb), IngestionRoutes.fs (all status-writing routes)
- **Summary:** Dan says he has not checked whether all status-updating routes use auto-commit transactions and says the Option 4 redesign 'doesn't completely solve' the header/audit desync problem -- but for all current code paths, it does.
- **Resolution:** dan-decides

Dan's statement: 'This doesn't completely solve the problem that we can have a header row and a status table that are out of sync in the database. If one write fails and the other succeeds, and the calling route doesn't use an auto-commit transaction, our data will be in a bad state. I believe all of the current routes that update status *do* use such a mechanism, but I haven't actually checked.'

Verification of every route that writes to both staged_entry and staged_entry_audit:

1. ingestRawEntries (IngestionRoutes.fs line 27): uses runCommandRouteAndAutoCompleteTransaction. Calls ingestRawToStageThenDeduplicateAndClassify which writes headers (insertNewToDb -> staged_entry INSERT + staged_entry_audit INSERT via updateHeaderStatus), runs dedup (staged_entry_audit INSERTs), and runs classification (staged_entry_audit INSERTs). All under one transaction.

2. updateStageEntry (IngestionRoutes.fs line 136): uses runCommandRouteAndAutoCompleteTransaction. Can update header fields (staged_entry UPDATE) and status (staged_entry_audit INSERT via updateHeaderStatus). All under one transaction.

3. post / shadowPost (IngestionRoutes.fs line 195): uses runCommandRouteAndAutoCompleteTransaction (or runCommandRouteAndAutoRollback for shadow). Writes JE to ledger AND updates status to Posted (staged_entry_audit INSERT). All under one transaction.

The three write routes that use NoTransaction (newClassificationRule, updateClassificationRule, createNewSource) perform single-table writes that do not involve status transitions.

With the status column removed from staged_entry (migration 202608231305), there is no denormalized value to drift. Status is derived purely from the audit trail via a CTE at read time. The only remaining desync vector -- an orphaned staged_entry with no audit trail -- cannot occur because insertNewToDb writes both the header row and the initial audit record within the same auto-complete transaction. Dan's belief is correct, and his broader concern is fully addressed by the current design.

**Action:** No code change needed. Dan can update his mental model: the Option 4 redesign does completely solve the header/audit desync problem for all current code paths. The remaining architectural concern (a future route might not use a transaction) could be addressed by a compile-time or convention guard, but that is a future-proofing measure, not a current gap.

**Why:** Dan explicitly asked the audit team to verify this. His belief is confirmed. The statement that it 'doesn't completely solve the problem' understates the effectiveness of his own design -- it is fully solved for every code path that exists today.

---
