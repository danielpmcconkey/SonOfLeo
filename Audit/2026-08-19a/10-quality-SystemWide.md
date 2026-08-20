# SystemWide-auditor

## EG-SYS-3.1a — enforcement-gap
- **Location:** Specs/Behavioral/SystemWide.md, REQ-SYS-3.1 / Specs/Behavioral/DataIngestion.md (staged_entry, staged_entry_line)
- **Summary:** REQ-SYS-3.1 requires every persisted entity to carry created_at and modified_at timestamps, but ingestion.staged_entry and ingestion.staged_entry_line are entities (per Definitions.md) that lack both columns.
- **Resolution:** dan-decides

REQ-SYS-3.1 states: 'Every persisted entity must carry a "created at" and a "modified at" timestamp.' Per Definitions.md, an Entity is 'a record type the system creates or mutates at runtime on behalf of the user.' Both staged_entry and staged_entry_line satisfy this definition -- user actions insert and update rows (ingestion, classification, manual review), and their contents cannot be regenerated from spec and code.

Verification against the schema migration (DbMigrations/202608081415-CreateStageSchemaAndTables.sql) confirms neither table has created_at or modified_at columns. The F# model types (Src/Model/DataIngestion/StageEntryHeader.fs, StageEntryLine.fs) carry no timestamp properties. The insertNewToDb functions write no timestamp values. The reconstitute/mapRawForDbRead functions read no timestamp columns from these tables.

Every other entity in the system has both columns: ledger.account, ledger.fiscal_period, ledger.journal_entry, ledger.journal_entry_line, ledger.journal_entry_ext_reference, ledger.journal_entry_comment, ingestion.source, ingestion.classification_rule.

The separate ingestion.staged_entry_audit table tracks status transitions with timestamps (REQ-STG-2.18-2.23) but is itself a distinct entity -- it does not satisfy REQ-SYS-3.1 for the entities it observes. Moreover, staged_entry_line has no audit trail mechanism at all.

REQ-SYS-3.2 reinforces this by requiring both 'created at' and 'modified at' Instant PROPERTIES on the entity at creation time, implying they are properties of the entity, not of a related table.

The DataIngestion spec neither exempts these entities from REQ-SYS-3.1 nor acknowledges the gap.

**Action:** Either add created_at and modified_at columns to ingestion.staged_entry and ingestion.staged_entry_line (with corresponding model/code changes), or add an explicit exemption in DataIngestion.md with rationale for why the staged_entry_audit trail substitutes for entity-level timestamps on staged_entry (and address how staged_entry_line is covered, since it has no audit trail).

**Why:** REQ-SYS-3.1 is a system-wide invariant that every other entity honors. Two entities silently violating it without acknowledgment means the requirement either needs enforcement or the exemption needs documenting. An undocumented exception to a universal rule erodes confidence in the rule itself.

---
