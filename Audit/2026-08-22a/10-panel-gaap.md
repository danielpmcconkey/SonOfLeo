# gaap-domain-auditor

## STALE-SQL-1 — stale-reference
- **Location:** Src/ModelOrchestrator/StageEntryOrchestration.fs lines 684, 724; Src/ModelOrchestrator/FetchFilterAndSort.fs line 69; Src/InterfaceBridge/BoundaryConverters/IngestionFieldConverters.fs line 435
- **Summary:** fetchFiltered for stage entries references the defunct `code` column that was replaced by `account_id` during the code-to-ID migration, causing a runtime SQL error on every invocation.
- **Resolution:** fix-code

The code-to-ID migration (migrations 202608220920, 202608220946) replaced the `code varchar(10)` column on `ingestion.staged_entry_line` with `account_id uuid` (FK to `ledger.account`). The model types (`StageEntryLine.fs`), create/update paths, and ingestion boundary converters were all updated. However, `StageEntryOrchestration.fetchFiltered` was not updated:

1. Line 724: The `all_in_stage` CTE unconditionally selects `sel.code` from `ingestion.staged_entry_line sel`. This column no longer exists in the table, so ANY call to fetchFiltered will fail with a PostgreSQL column-not-found error, regardless of whether an accountCode filter is applied.

2. Line 684-685: The filter clause maps `filter.accountCode` to SQL `"code = @code"`, referencing the same non-existent column.

3. FetchFilterAndSort.fs line 69: `StageEntryFetchFilter` carries `accountCode: AccountCode option` instead of an `AccountId option`. After the migration, filtering by account requires either joining to `ledger.account` and comparing against `a.code`, or resolving the code to an ID at the boundary (as the ingestion boundary already does for ingestion input).

4. IngestionFieldConverters.fs line 435: The boundary converter for this filter creates an `AccountCode` via `AccountCode.create`, rather than resolving it to an `AccountId` via `convert AccountCodeString Option to AccountId Option` (as the raw-row ingestion converter does at line 393).

This is called from `IngestionRoutes.fs` line 212 for the `FetchFilteredStageEntries` CLI command. The function is dead code until the SQL and types are corrected.

**Action:** Update fetchFiltered: (1) replace `sel.code` with `sel.account_id` in the CTE and add a left join to `ledger.account` if code-based display is needed in the CTE; (2) change `StageEntryFetchFilter.accountCode` to `accountId: AccountId option`; (3) update the boundary converter to resolve the code to an ID; (4) update the SQL filter clause to use `account_id = @account_id`.

**Why:** The fetchFiltered function is the only way to query stage entries with complex filters (by account, status, date range, etc.). It is reachable via the CLI and is the mechanism the Saturday review surface uses to inspect staged data. Every invocation currently fails with a database error.

---
