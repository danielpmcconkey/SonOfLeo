# opus-agent-readiness

## STALE-REF-1 — stale-reference
- **Location:** Src/ModelOrchestrator/StageEntryOrchestration.fs, lines 684 and 724; Src/ModelOrchestrator/FetchFilterAndSort.fs, line 69
- **Summary:** The fetchFiltered CTE references the removed column sel.code, breaking the Ingestion FetchStageEntryFiltered CLI route after the code-to-ID migration.
- **Resolution:** fix-code

Migration 202608220946-RebuildStageEntryLine.sql replaced the code varchar(10) column with account_id uuid on ingestion.staged_entry_line. The fetchFiltered function in StageEntryOrchestration.fs was not updated during the code-to-ID migration (commit 378ce5e touched 41 lines in this file but left fetchFiltered untouched).

Three stale artifacts remain:
1. Line 724: the all_in_stage CTE selects sel.code -- a column that no longer exists. PostgreSQL will error on query parse.
2. Lines 682-685: the WHERE-clause builder emits "code = @code" with a CharString parameter (AccountCode.value), but the column is now account_id (uuid type).
3. FetchFilterAndSort.fs line 69: StageEntryFetchFilter carries accountCode: AccountCode option. After the migration, this should either be an AccountId (with code-to-ID resolution at the boundary) or the SQL should join to ledger.account to resolve the code.

Because sel.code appears in the CTE's SELECT list unconditionally, EVERY call to FetchStageEntryFiltered fails -- not just calls with an accountCode filter.

No test exists for this route. grep across Tests/ for FetchStageEntryFiltered, fetchStageEntryFiltered, and StageEntryOrchestration.fetchFiltered returns zero hits. The existing guardrails (build, traceability audit, Checks/) all pass because SQL strings are runtime-interpreted and the route has no REQ, so the traceability system has no obligation to flag it as uncovered.

**Action:** Fix the CTE to select sel.account_id (or join to ledger.account for code). Update the filter type and WHERE clause to match. Add at least one test exercising the route's happy path so the traceability gap is closed for future regressions.

**Why:** This is the Saturday review surface -- the route operators (and BD, in future) use to query staged entries. It is totally broken and no guardrail detects it. From an agent-readiness standpoint, this is a textbook failure-amplification vector: a small migration edit passed build+tests but corrupted a user-facing capability. BD could introduce further regressions in this function and nothing in the system would notice.

---

## SD-1 — statement-delta
- **Location:** Dan's statement vs Src/ModelOrchestrator/StageEntryOrchestration.fs fetchFiltered; Src/ModelOrchestrator/FetchFilterAndSort.fs StageEntryFetchFilter
- **Summary:** Dan states the code-to-ID migration is complete ('codes resolved at the boundary'), but the fetchFiltered path was not migrated.
- **Resolution:** fix-code

Dan's statement: 'The code-to-ID migration was the largest structural change: staged lines and classification rules now carry account IDs with FK to ledger.account, codes resolved at the boundary. No new features since the remediation.'

The migration WAS applied to:
- The schema (account_id uuid with FK to ledger.account)
- The model types (StageEntryLine.accountId: AccountId option)
- The ingestion boundary (IngestionFieldConverters.fs line 393: rawInputRow.accountCode resolved to AccountId)
- The update boundary (IngestionFieldConverters.fs line 358-360: accountCode resolved to AccountId)
- The output boundary (IngestionFieldConverters.fs line 71-74: accountId resolved to code string for display)

The migration was NOT applied to:
- StageEntryFetchFilter.accountCode (still AccountCode option, not AccountId option)
- The fetchFiltered CTE SQL (still references sel.code, still uses string comparison)
- The boundary converter for filters (line 435: resolves to AccountCode, not AccountId)

Dan's mental model that 'codes resolved at the boundary' is correct for 4 of 5 boundary paths, but incorrect for the filter/fetch path.

**Action:** Update Dan's mental model. The fix in STALE-REF-1 resolves the delta.

**Why:** Accurate mental models matter because Dan makes design decisions and prioritization calls based on what he believes the system does. An incomplete migration that he believes is complete will not be scheduled for completion.

---
