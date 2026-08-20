# code-truthfulness-auditor-model-orchestrator

## SCHEMA-STG-1 — contradiction
- **Location:** DbMigrations/202608081415-CreateStageSchemaAndTables.sql, Src/Model/DataIngestion/StageEntryHeader.fs, Src/Model/DataIngestion/StageEntryLine.fs
- **Summary:** ingestion.staged_entry and ingestion.staged_entry_line lack the created_at and modified_at columns required by REQ-SYS-3.1, unlike the other staging entities (ingestion.source and ingestion.classification_rule) which do carry them.
- **Resolution:** fix-code

REQ-SYS-3.1 states: 'Every persisted entity must carry a created at and a modified at timestamp.' The resolved finding GAP-JE-2 confirms this is a system-wide requirement that applies to all persisted entities without needing restatement in each domain spec. Both staged_entry and staged_entry_line are entities per Definitions.md (created and mutated at runtime on behalf of the user — status changes, account_code updates via classification and manual review). The migration at 202608081415-CreateStageSchemaAndTables.sql defines neither created_at nor modified_at on ingestion.staged_entry or ingestion.staged_entry_line. The F# types StageEntryHeader and StageEntryLine carry no timestamp fields. Neither insertNewToDb function writes timestamp values. Neither updateDb function sets a modified_at. Meanwhile, the two other entities in the same ingestion schema — ingestion.source (IngestionSource.fs lines 16-17, 40-45, 52-53) and ingestion.classification_rule (ClassificationRule.fs lines 25-26, 62-71, 88-90) — follow REQ-SYS-3.1 correctly with both columns populated. This inconsistency within the same domain makes the omission unlikely to be a deliberate design exemption. The staged_entry_audit table provides a status-transition log, but that covers only header status changes, not line-level mutations (classification code assignment, manual review updates), and REQ-SYS-3.1 requires timestamps on the entity itself, not on an associated audit table.

**Action:** Add created_at (timestamptz NOT NULL) and modified_at (timestamptz NOT NULL) columns to ingestion.staged_entry and ingestion.staged_entry_line via a new migration. Add corresponding Instant fields to StageEntryHeader and StageEntryLine types. Populate in insertNewToDb and update modified_at in updateDb, following the patterns already established in IngestionSource and ClassificationRule.

**Why:** REQ-SYS-3.1 is a cross-cutting invariant the codebase otherwise upholds. Every other entity table — ledger.account, ledger.journal_entry, ledger.journal_entry_line, ledger.journal_entry_ext_reference, ledger.journal_entry_comment, ledger.fiscal_period, ingestion.source, ingestion.classification_rule — carries both timestamps. The two staging entity tables are the only exceptions. Without modified_at on staged_entry_line, there is no record of when a line's account_code was set by classification or manual review.

---

## SPEC-STG-1 — contradiction
- **Location:** Specs/Behavioral/DataIngestion.md — REQ-STG-3.9, REQ-STG-5.1, REQ-STG-5.6, REQ-STG-5.8 vs REQ-STG-4.1
- **Summary:** Four requirements use lowercase status value strings ('ingested', 'classified', 'conflict') while REQ-STG-4.1 defines the canonical set in PascalCase ('Ingested', 'Classified', 'Conflict'); these are stored as varchar in PostgreSQL where comparison is case-sensitive.
- **Resolution:** fix-spec

REQ-STG-4.1 defines the canonical status values as: 'Ingested', 'Classified', 'NoMatch', 'Conflict', 'Reviewed', 'Duplicate', 'Posted', 'Ignored' — all PascalCase. The transition table at lines 123-144 also uses PascalCase throughout. However, four individual requirements use lowercase in their backtick-quoted status strings: REQ-STG-3.9 uses 'ingested' twice (line 108), REQ-STG-5.1 uses 'ingested' (line 151), REQ-STG-5.6 uses 'conflict' (line 157), and REQ-STG-5.8 uses 'classified' (line 160). The code correctly follows REQ-STG-4.1's PascalCase via the StagedEntryStatus DU (StageEntryComponent.fs lines 24-43), and the values are stored as varchar in PostgreSQL where 'ingested' != 'Ingested'. The inconsistency is within the spec itself, not between spec and code.

**Action:** Correct the four requirements to use PascalCase matching REQ-STG-4.1's canonical definitions: REQ-STG-3.9 'ingested' to 'Ingested', REQ-STG-5.1 'ingested' to 'Ingested', REQ-STG-5.6 'conflict' to 'Conflict', REQ-STG-5.8 'classified' to 'Classified'.

**Why:** Spec-internal contradictions erode trust in the spec as a single source of truth. Because these values are stored as varchar in PostgreSQL, where string comparison is case-sensitive, the lowercase forms in the spec would produce actual bugs if implemented literally. The code is safe today because the DU's toString function follows REQ-STG-4.1, but the spec should not require a reader to cross-reference another requirement to know the correct casing.

---
