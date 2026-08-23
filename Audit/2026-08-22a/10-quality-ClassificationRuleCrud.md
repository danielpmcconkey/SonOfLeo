# hobson-cr-spec-auditor

## ENFORCE-CR-1 — enforcement-gap
- **Location:** Specs/Behavioral/ClassificationRuleCrud.md, Waived table, REQ-CR-1.1 and REQ-CR-4.2; DbMigrations/202608220920-RebuildClassificationRule.sql
- **Summary:** The waivers for REQ-CR-1.1 and REQ-CR-4.2 claim uniqueness is enforced by a PK constraint, but the classification_rule table has no PRIMARY KEY.
- **Resolution:** fix-code

REQ-CR-1.1 states 'Must be unique' and its waiver says 'UUID is a value type; uniqueness enforced by PK constraint.' REQ-CR-4.2's waiver says 'Same rationale as REQ-CR-1.1.' The original table creation (migration #11, 202608081415-CreateStageSchemaAndTables.sql, line 37) had 'unique_id uuid primary key'. Migration #13 (202608220920-RebuildClassificationRule.sql) drops and recreates the table, but the new DDL has 'unique_id uuid NOT NULL' with no PRIMARY KEY constraint. The PK was lost in the rebuild. The waiver's stated enforcement mechanism does not exist in the current schema. Per the 'check-schema-before-questioning-waivers' conduct article, the schema is the ground truth for waiver soundness.

**Action:** Add the missing PRIMARY KEY constraint to ingestion.classification_rule.unique_id (either amend the migration or issue an ALTER TABLE). Once the PK exists, the waiver reasons are sound again.

**Why:** The waivers excuse REQ-CR-1.1 and REQ-CR-4.2 from testing on the grounds that the database enforces uniqueness. Without the PK, nothing enforces uniqueness at the persistence layer. UUID collisions are statistically negligible, but the waiver's stated reason is factually incorrect as of the current schema, which means the three-state rule's coverage claim for these two requirements rests on a premise that does not hold.

---

## CONTRA-CR-1 — contradiction
- **Location:** REQ-STG-5.2 (Specs/Behavioral/DataIngestion.md line 152) vs REQ-CR-1.13 (Specs/Behavioral/ClassificationRuleCrud.md)
- **Summary:** REQ-STG-5.2 says classification matches 'on the staged entry's description' but the classification rules engine matches on five fields.
- **Resolution:** fix-spec

REQ-STG-5.2 reads: 'Classification evaluates each staged line whose account is null against the vendor classification rules, matching on the staged entry's description.' REQ-CR-1.13 defines FieldMatch as targeting any of Source, Description, Memo, LineType, or Amount. The code confirms all five are present: the MatchCandidate record (ClassificationRuleComponent.fs lines 98-106) carries ingestionSource, description, amount, lineType, and memo, and StageEntryOrchestration.fs (lines 385-392) populates all five when building candidates. Tests in FieldMatchEvaluation.fs exercise every field type. REQ-STG-5.2's singular mention of 'description' is narrower than the actual rules engine surface defined in ClassificationRuleCrud.md.

**Action:** Update REQ-STG-5.2 to reflect the full set of matchable fields, e.g. 'matching against the candidate's field values as defined in ClassificationRuleCrud.md' or list all five explicitly.

**Why:** A developer implementing the classification step from DataIngestion.md alone would build the candidate with only the description. They would need to read ClassificationRuleCrud.md to discover the other four matchable fields. The two specs should agree on what the rules engine matches against to prevent divergent implementations.

---
