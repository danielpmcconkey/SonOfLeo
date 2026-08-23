# corner-painting-auditor

## SCHEMA-CR-PK — enforcement-gap
- **Location:** DbMigrations/202608220920-RebuildClassificationRule.sql; REQ-CR-1.1
- **Summary:** The classification_rule table rebuild dropped the PRIMARY KEY constraint on unique_id, invalidating two waivers whose soundness premise is 'uniqueness enforced by PK constraint.'
- **Resolution:** fix-code

Migration 202608220920-RebuildClassificationRule.sql recreates ingestion.classification_rule with `unique_id uuid NOT NULL` but no PRIMARY KEY constraint (line 11). The original table in 202608081415-CreateStageSchemaAndTables.sql had `unique_id uuid primary key` (line 36). REQ-CR-1.1 states 'Classification rule ID is a system-generated UUID. Cannot be null. Must be unique.' Two waivers explicitly cite the PK constraint as their soundness basis: REQ-CR-1.1 waiver ('UUID is a value type; uniqueness enforced by PK constraint') and REQ-CR-4.2 waiver ('UUID generation via Guid.NewGuid() in create; uniqueness enforced by PK constraint'). With the PK gone, the uniqueness guarantee those waivers rely on does not exist at the schema level. Additionally, the missing PK cascades: the FK from staged_entry_line.classification_rule_id to classification_rule.unique_id (present in the original 202608081415 migration, line 61-65) cannot be re-established because PostgreSQL requires a UNIQUE or PRIMARY KEY constraint on the referenced column. The RebuildStageEntryLine migration (202608220946) correspondingly drops this FK (line 22: bare `classification_rule_id uuid,` with no REFERENCES clause). The migration comment says 'executed manually in dev 2026-08-22 09:21' and 'not yet' for test and prod.

**Action:** Add `PRIMARY KEY (unique_id)` or a CONSTRAINT clause to the classification_rule rebuild migration before promoting to test/prod. Once the PK exists, add `REFERENCES ingestion.classification_rule (unique_id)` back to staged_entry_line.classification_rule_id.

**Why:** Without the PK, the DB cannot prevent duplicate classification rule IDs, the two waivers citing PK enforcement are factually unsound, and the FK chain from staged_entry_line through classification_rule to ledger.account is structurally broken. This blocks any future table that needs to FK to classification_rule (e.g., rule audit history, classification analytics). The fix is a one-line schema correction before the migration is promoted.

---

## STMT-FETCH-STALE — statement-delta
- **Location:** Src/ModelOrchestrator/StageEntryOrchestration.fs lines 684, 724
- **Summary:** The fetchFiltered query references the old `code` column (renamed to `account_id` in migration 202608220946), making the FetchStageEntryFiltered CLI route a runtime failure.
- **Resolution:** fix-code

Dan's statement says the code-to-ID migration is complete. Two references in StageEntryOrchestration.fetchFiltered still use the old column name: (1) Line 724 in the CTE SELECT: `sel.code,` -- the column was renamed to `account_id` (uuid type). This will produce a PostgreSQL 'column sel.code does not exist' error at runtime. (2) Line 684 in the filter clause: `("code = @code", { name = "@code"; value = CharString(x |> AccountCode.value) })` -- references the defunct column and passes a string value into what is now a uuid column. The StageEntryFetchFilter type (FetchFilterAndSort.fs line 69) still carries `accountCode: AccountCode option`, and the boundary converter (IngestionFieldConverters.fs line 435) converts the input to AccountCode. No test exercises StageEntryOrchestration.fetchFiltered or the FetchStageEntryFiltered CLI route, so the breakage is untested. The pivot commit (378ce5e) modified StageEntryOrchestration.fs but did not update the fetchFiltered function's query or filter mapping.

**Action:** Update the CTE to reference `sel.account_id` and either (a) change the filter field and contract to accept account IDs, or (b) add a join to ledger.account in the CTE and filter by `a.code = @code`. Add at least one integration test that exercises the FetchStageEntryFiltered route.

**Why:** The FetchStageEntryFiltered CLI route is the operator's primary tool for reviewing staged entries with filters (by account, status, date, etc.). It is dead code today. When the Saturday routine or COYS bots need to inspect staged data programmatically, this route will fail. The fix requires coordinating the query, the filter model type, and the boundary converter -- more work the longer it sits, as other code may build on the current (broken) shape.

---

## CONV-CR-CODE — other
- **Location:** Src/InterfaceBridge/BoundaryConverters/IngestionFieldConverters.fs lines 232-237
- **Summary:** The ClassificationRuleReturn converter populates codeAtMatch with the account name instead of the account code because it calls the wrong lookup function.
- **Resolution:** fix-code

In `convert [ClassificationRule] to [ClassificationRuleReturn]` (IngestionFieldConverters.fs lines 226-251), both `codeAtMatch` (line 233) and `accountNameAtMach` (line 236) call `convert AccountId to AccountNameString`. The `codeAtMatch` field should call `convert AccountId to AccountCodeString` (AccountFieldConverters.fs line 32) to return the account code string (e.g. 'F-1270'). Instead, both fields return the account name (e.g. 'Groceries'). The ClassificationRuleReturn type (IngestionContracts.fs line 113) names the field `codeAtMatch: string` -- clearly intended to carry the account code. No test asserts on ClassificationRuleReturn.codeAtMatch (grep confirms zero references to this field in Tests/), so the bug is undetected. This likely originated during the code-to-ID migration when the converter was rewritten from `AccountCode` to `AccountId` lookups.

**Action:** Change line 233 from `convert AccountId to AccountNameString` to `convert AccountId to AccountCodeString`. Add an assertion in the ClassificationRuleCrud integration tests that verifies the returned codeAtMatch matches the expected account code.

**Why:** Any CLI consumer or COYS bot that reads classification rule data and uses codeAtMatch as an account code identifier will get the account name instead. This silently corrupts the contract's output. When the ingestion workflow matures and external tools (importers, the Saturday routine) read classification rules programmatically, they will receive wrong data in a field that looks correct (it is a string, it is non-null, it is not obviously wrong unless you know the difference between a code and a name).

---
