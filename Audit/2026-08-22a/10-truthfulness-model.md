# code-truthfulness-auditor

## CODE-STG-1 — stale-reference
- **Location:** Src/ModelOrchestrator/StageEntryOrchestration.fs (fetchFiltered, lines ~605-756), Src/ModelOrchestrator/FetchFilterAndSort.fs (StageEntryFetchFilter, line 69)
- **Summary:** The fetchFiltered function and StageEntryFetchFilter type still reference the pre-migration `code` column and AccountCode type, but the schema now uses `account_id uuid`.
- **Resolution:** fix-code

Migration 202608220946-RebuildStageEntryLine.sql changed the `staged_entry_line` table from `code character varying(10)` to `account_id uuid REFERENCES ledger.account (unique_id)`. However, StageEntryOrchestration.fetchFiltered was not updated: (1) The `all_in_stage` CTE selects `sel.code` (line ~724), referencing a column that no longer exists in the schema. This causes a runtime SQL error on ANY invocation of fetchFiltered, regardless of which filters are active, because the CTE runs unconditionally. (2) The filter clause (lines ~682-685) generates `code = @code` with a `CharString(x |> AccountCode.value)` parameter -- even if the column existed, it would be comparing a string AccountCode to what is now a UUID column. (3) The StageEntryFetchFilter type (FetchFilterAndSort.fs line 69) defines `accountCode: AccountCode option` instead of the post-migration `accountId: AccountId option`. (4) The boundary converter in IngestionFieldConverters.fs (line 435) passes through accountCode from input to filter unchanged. This function is called from IngestionRoutes.fs line 212 (the `FetchStageEntries` route), meaning the CLI's stage entry fetch command is broken. Dan's statement says 'The code-to-ID migration was the largest structural change' with 'no new features since the remediation,' implying the migration is complete -- this stale reference shows it is not.

**Action:** Update fetchFiltered CTE to select `sel.account_id` instead of `sel.code`. Change StageEntryFetchFilter.accountCode to `accountId: AccountId option`. Update the filter clause to generate `account_id = @account_id` with a UniqueId parameter. Update the boundary converter and IngestionContracts accordingly.

**Why:** Any call to the fetchFiltered function will produce a runtime SQL error because the CTE references a column (`code`) that was dropped and replaced by `account_id` during the code-to-ID migration. The stage entry fetch CLI command is non-functional.

---

## SCHEMA-CR-1 — enforcement-gap
- **Location:** DbMigrations/202608220920-RebuildClassificationRule.sql, Specs/Behavioral/ClassificationRuleCrud.md (REQ-CR-1.1 waiver)
- **Summary:** The rebuilt classification_rule table is missing its PRIMARY KEY constraint on unique_id, breaking the waiver justification for REQ-CR-1.1.
- **Resolution:** fix-code

The original schema (202608081415-CreateStageSchemaAndTables.sql) created `ingestion.classification_rule` with `unique_id uuid primary key`. Migration 202608220920-RebuildClassificationRule.sql drops and recreates the table with `unique_id uuid NOT NULL` -- no PRIMARY KEY. REQ-CR-1.1 states 'Classification rule ID is a system-generated UUID. Cannot be null. Must be unique.' The NOT NULL is present, but uniqueness is not enforced at the schema level. The waiver for REQ-CR-1.1 in ClassificationRuleCrud.md explicitly states its justification as 'UUID is a value type; uniqueness enforced by PK constraint.' That PK constraint no longer exists. The waiver's own stated rationale is broken by the current schema. While UUID collisions are astronomically unlikely (Guid.NewGuid()), the PK also provides an index that read operations (fetchById, fetchByName) rely on for efficient ExactlyOne lookups.

**Action:** Add PRIMARY KEY (unique_id) to the classification_rule table, either via an ALTER TABLE or by correcting migration 202608220920.

**Why:** The waiver for REQ-CR-1.1 depends on a PK constraint that no longer exists. Without it, the uniqueness guarantee cited in the waiver is not structurally enforced, and the table lacks a primary-key index for its single-row lookups.

---

## STALE-DEF-1 — stale-reference
- **Location:** Specs/Definitions.md lines 46 and 49 (Staged line, Postable definitions)
- **Summary:** Definitions.md still uses 'account code' terminology in the Staged line and Postable definitions, contradicting the behavioral spec's post-migration 'account_id' terminology.
- **Resolution:** fix-spec

Definitions.md (authority level 2) contains two stale references from the pre-migration era: (1) Staged line definition (line 46): 'carries an amount, direction (line_type), and an account code that may be null until classification or manual review fills it in.' (2) Postable definition (line 49): 'validates that every staged line has a non-null account_code that resolves to an account in the chart of accounts.' The behavioral spec DataIngestion.md (authority level 3) was updated during the code-to-ID migration: REQ-STG-2.14 now reads 'Staged line account is nullable (account_id foreign key to ledger.account). When set, identifies the target account by UUID.' The design note explicitly states 'Staged lines and classification rules reference accounts by UUID internally. The boundary layer resolves account codes to account IDs at ingestion time.' The Definitions.md wording ('account code that resolves') describes the old model where codes were stored on lines and resolved at posting time. The current model stores resolved account IDs from ingestion onward. As a higher-authority document, Definitions.md could mislead implementers about the domain model. Dan's statement describes the migration as complete, but these definitions were not updated.

**Action:** Update Definitions.md: Staged line should reference 'account ID' (nullable UUID FK to ledger.account) instead of 'account code.' Postable should reference 'non-null account_id' instead of 'non-null account_code that resolves.'

**Why:** Definitions.md is authority level 2, above behavioral specs. Its stale 'account code' wording describes a pre-migration model that no longer exists. A developer reading Definitions.md without also reading the design note would implement code-based resolution at posting time, contradicting the current design (ID-based, resolved at ingestion).

---
