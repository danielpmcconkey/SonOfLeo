# dal-source-auditor

## SCHEMA-CR-PK-1 — enforcement-gap
- **Location:** DbMigrations/202608220920-RebuildClassificationRule.sql; REQ-CR-1.1, REQ-CR-4.2 waivers
- **Summary:** Migration 13 rebuilds `ingestion.classification_rule` without a PRIMARY KEY constraint on `unique_id`, removing the uniqueness guarantee that existed in migration 11 and that two testing waivers cite as their enforcement mechanism.
- **Resolution:** fix-code

Migration 11 (CreateStageSchemaAndTables) created `ingestion.classification_rule` with `unique_id uuid primary key`. Migration 13 (RebuildClassificationRule, dated 2026-08-22) drops the table and recreates it with `unique_id uuid NOT NULL` but no PRIMARY KEY and no UNIQUE constraint. The column is NOT NULL but nothing prevents duplicate UUIDs from being inserted.

REQ-CR-1.1 states: 'Classification rule ID is a system-generated UUID. Cannot be null. Must be unique.' The waiver for REQ-CR-1.1 in ClassificationRuleCrud.md explicitly says: 'UUID is a value type; uniqueness enforced by PK constraint. Same rationale as REQ-AC-1.21/1.22.' The waiver for REQ-CR-4.2 similarly cites 'uniqueness enforced by PK constraint. Same rationale as REQ-CR-1.1.' Both waivers rely on a constraint that no longer exists in the schema.

**Action:** Add `PRIMARY KEY (unique_id)` or inline `primary key` on the unique_id column definition in migration 13, then apply to dev and test databases.

**Why:** The waivers for REQ-CR-1.1 and REQ-CR-4.2 cite the PK constraint as the enforcement mechanism that makes testing unnecessary. Without the PK, the waiver rationale is factually wrong and the 'Must be unique' requirement in REQ-CR-1.1 has no enforcement at any layer. The application generates UUIDs via Guid.NewGuid() which makes collisions astronomically unlikely, but the spec and waivers commit to structural enforcement, and the original schema provided it.

---

## SCHEMA-STG-FK-1 — enforcement-gap
- **Location:** DbMigrations/202608220946-RebuildStageEntryLine.sql; REQ-STG-2.16
- **Summary:** Migration 14 rebuilds `ingestion.staged_entry_line` without a FK from `classification_rule_id` to `ingestion.classification_rule(unique_id)`, dropping referential integrity that existed in migration 11.
- **Resolution:** fix-code

Migration 11 (CreateStageSchemaAndTables) defined `classification_rule_id uuid REFERENCES ingestion.classification_rule (unique_id) MATCH SIMPLE ON UPDATE NO ACTION ON DELETE RESTRICT`. Migration 14 (RebuildStageEntryLine, dated 2026-08-22) defines `classification_rule_id uuid,` with no FK constraint.

REQ-STG-2.16 states: 'Staged line classification_rule_id is nullable. When set, identifies the classification rule that assigned the account.' REQ-DAL-3.6 establishes that FK and unique key constraints are the accepted forms of DB-layer enforcement. The original schema enforced this relationship; the rebuild dropped it.

Note: this finding is coupled to SCHEMA-CR-PK-1. A FK to `classification_rule.unique_id` requires a PK or UNIQUE constraint on that column. Restoring the FK requires fixing SCHEMA-CR-PK-1 first. However, this is a separate omission — the FK was absent even in the `account_id` column's neighborhood where the pattern for FKs is demonstrated correctly (`account_id uuid REFERENCES ledger.account (unique_id)`).

**Action:** After restoring the PK on `classification_rule` (SCHEMA-CR-PK-1), add `REFERENCES ingestion.classification_rule (unique_id) MATCH SIMPLE ON UPDATE NO ACTION ON DELETE RESTRICT` to the `classification_rule_id` column in migration 14, then apply to dev and test databases.

**Why:** Without the FK, a staged_entry_line can reference a classification_rule_id that does not exist in the classification_rule table, or that was somehow removed. REQ-CR-7.1 prohibits hard-deleting classification rules, so ON DELETE RESTRICT is the correct policy. The same migration correctly includes the FK for `account_id` to `ledger.account`, making the omission on `classification_rule_id` inconsistent with the table's own pattern.

---

