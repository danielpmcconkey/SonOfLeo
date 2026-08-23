# idiom-auditor

## CORRECTNESS-1 — stale-reference
- **Location:** Src/ModelOrchestrator/StageEntryOrchestration.fs, line 724; Src/ModelOrchestrator/FetchFilterAndSort.fs, line 70
- **Summary:** fetchFiltered SQL query references sel.code, a column that no longer exists after the code-to-ID migration rebuilt ingestion.staged_entry_line with account_id (UUID) instead of code (varchar).
- **Resolution:** fix-code

The fetchFiltered function in StageEntryOrchestration.fs builds a CTE (all_in_stage) that selects sel.code on line 724. Migration 202608220946-RebuildStageEntryLine.sql dropped and recreated ingestion.staged_entry_line replacing the code column (varchar(10)) with account_id (uuid, FK to ledger.account). The column sel.code no longer exists in the schema. Additionally, the filter clause at line 684 builds a WHERE condition code = @code passing a CharString (account code string), but the underlying data is now a UUID. The StageEntryFetchFilter type in FetchFilterAndSort.fs still carries accountCode: AccountCode option at line 70, which is the wrong type entirely after the migration. Any call to the Ingestion FetchStageEntryFiltered CLI route will produce a PostgreSQL runtime error (column does not exist). REQ-STG-2.14 confirms the spec-level intent: the column is account_id (UUID FK), not a code string.

**Action:** In the fetchFiltered CTE, replace sel.code with a join to ledger.account a2 on sel.account_id = a2.unique_id, selecting a2.code. Change the filter from code = @code to a2.code = @code. Alternatively, change the filter type and comparison to operate on account_id directly, resolving the code at the boundary.

**Why:** This is incomplete propagation of a structural migration. In DDD, when the domain model changes (code-to-ID pivot for staged lines), every query and filter that touches the old shape must be updated. A stale column reference breaks the system at the persistence boundary -- the SQL will fail at runtime because the column no longer exists.

---

## CORRECTNESS-2 — idiom
- **Location:** Src/InterfaceBridge/BoundaryConverters/IngestionFieldConverters.fs, lines 232-233; REQ-NGUI-1.4
- **Summary:** ClassificationRuleReturn converter populates codeAtMatch with the account NAME instead of the account CODE due to calling the wrong converter function.
- **Resolution:** fix-code

In the convert [ClassificationRule] to [ClassificationRuleReturn] function (IngestionFieldConverters.fs lines 232-233), the codeAtMatch binding calls convert AccountId to AccountNameString context. It should call convert AccountId to AccountCodeString context. As a result, both codeAtMatch and accountNameAtMatch (lines 234-237) resolve to the account name. The ClassificationRuleReturn contract (IngestionContracts.fs line 113) names the field codeAtMatch, and REQ-NGUI-1.4 requires all return payloads include account codes when identifying an account. Every classification rule fetch or create that returns a ClassificationRuleReturn will contain the account name in the codeAtMatch field instead of the account code.

**Action:** Change line 233 from convert AccountId to AccountNameString to convert AccountId to AccountCodeString.

**Why:** In a DDD boundary layer, converter functions are the translation between the domain internal representation and the contract the outside world sees. A converter that maps the wrong domain accessor to a contract field silently corrupts the boundary contract. The type system cannot catch this because both converters return Result<string, AppError> -- the compiler sees them as interchangeable. This is exactly why boundary conversion code demands careful review: the types align but the semantics diverge. Additionally, this violates REQ-NGUI-1.4 which mandates account codes in return payloads.

---
