# Disposition Record

| # | Auditor | ID | Summary | Owner | Status | Ruling | Date |
|---|---------|----|---------|----- -|--------|--------|------|
| 001 | account-crud-efficacy-auditor | AQ-AC-1 | REQ-AC-3.10 FetchByParentCode happy-path test asserts only count, never inspecting the returned children's values or membership (Specimen 3). | fix-test | accepted | BD to fix | 2026-08-23 |
| 002 | data-ingestion-efficacy-auditor | STG-EFF-1 | (see 10-efficacy-DataIngestion.md) | fix-test | accepted | BD to fix | 2026-08-23 |
| 003 | data-ingestion-efficacy-auditor | STG-EFF-2 | (see 10-efficacy-DataIngestion.md) | fix-test | accepted | BD to fix | 2026-08-23 |
| 004 | data-ingestion-efficacy-auditor | STG-EFF-3 | REQ-STG-2.6 spec says "filename (not path)" but the code stores the full path, and it should — full path is more useful for provenance. | fix-spec | accepted | Hobson to fix spec to match code (full path, not filename-only) | 2026-08-23 |
| 005 | fp-efficacy-auditor | FP-AQ-1 | REQ-FP-3.2 FetchByKey happy-path test asserts only the lookup criterion, matching Specimen 8 (tautological locator). | fix-test | accepted | BD to fix | 2026-08-23 |
| 006 | fp-efficacy-auditor | FP-MR-1 | Three route-level tests exercise non-existent-key rejection for FetchByKey, Close, and Reopen, but no FP REQ describes this behavior. | fix-spec | accepted | Hobson to add the requirement | 2026-08-23 |
| 007 | je-efficacy-auditor | JE-ROUTE-EXTREF-1 | (see 10-efficacy-JournalEntryCrud.md) | fix-test | accepted | BD to fix | 2026-08-23 |
| 008 | je-efficacy-auditor | JE-FETCH-PERIOD-1 | (see 10-efficacy-JournalEntryCrud.md) | fix-test | accepted | BD to fix | 2026-08-23 |
| 009 | je-efficacy-auditor | JE-FETCH-DATERANGE-1 | (see 10-efficacy-JournalEntryCrud.md) | fix-test | accepted | BD to fix | 2026-08-23 |
| 010 | je-efficacy-auditor | JE-FETCH-REF-VARIANTS-1 | (see 10-efficacy-JournalEntryCrud.md) | fix-test | accepted | BD to fix | 2026-08-23 |
| 011 | je-efficacy-auditor | JE-ACTIVITY-VOIDED-1 | (see 10-efficacy-JournalEntryCrud.md) | fix-test | accepted | BD to fix | 2026-08-23 |
| 012 | money-efficacy-auditor | AQ-MON-1 | REQ-MON-2.3 and REQ-MON-2.4 happy-path tests assert only Assert.True(result.IsOk) without examining the returned value. | fix-test | accepted | BD to fix both | 2026-08-23 |
| 013 | ngui-test-efficacy-auditor | NGUI-AQ-2 | Hard-wired expected value "Money Market" in the REQ-NGUI-3.6 stdout payload test instead of deriving from fixture data (Specimen 1). | fix-test | accepted | BD to fix | 2026-08-23 |
| 014 | reporting-efficacy-auditor | RPT-EFF-1 | Expected date string in the REQ-RPT-2.4 test is derived via Calendar.localDateToString, which is in the call chain of the function under test (Specimen 6). | — | overruled | Not a test of Calendar — it's a test of whether the report uses Calendar correctly. Calendar is assumed correct via its own tests. | 2026-08-23 |
| 015 | opus-agent-readiness | STALE-REF-1 | The fetchFiltered CTE references the removed column sel.code, breaking the FetchStageEntryFiltered CLI route after the code-to-ID migration. | fix-code | accepted | Dan to fix | 2026-08-23 |
| 016 | opus-agent-readiness | SD-1 | Dan states the code-to-ID migration is complete but the fetchFiltered path was not migrated. | fix-code | accepted | Dan to fix (same as #015) | 2026-08-23 |
| 017 | corner-painting-auditor | SCHEMA-CR-PK | The classification_rule table rebuild dropped the PRIMARY KEY constraint on unique_id. | fix-code | accepted | Fixed by Dan (2026-08-22) | 2026-08-23 |
| 018 | corner-painting-auditor | STMT-FETCH-STALE | The fetchFiltered query references the old `code` column. | fix-code | accepted | Dan to fix (same as #015) | 2026-08-23 |
| 019 | corner-painting-auditor | CONV-CR-CODE | The ClassificationRuleReturn converter populates codeAtMatch with the account name instead of the account code. | fix-code | accepted | Dan to fix | 2026-08-23 |
| 020 | panel-customer | SD-REMEDIATION-1 | Statement delta regarding Dan's mental model. | — | overruled | Dan has been actively working on this project all month. Not a useful finding. | 2026-08-23 |
| 021 | idiom-auditor | CORRECTNESS-1 | fetchFiltered SQL query references sel.code, a column that no longer exists. | fix-code | accepted | Dan to fix (same as #015) | 2026-08-23 |
| 022 | idiom-auditor | CORRECTNESS-2 | ClassificationRuleReturn converter populates codeAtMatch with the account NAME instead of the account CODE. | fix-code | accepted | Dan to fix (same as #019) | 2026-08-23 |
| 023 | gaap-domain-auditor | STALE-SQL-1 | fetchFiltered for stage entries references the defunct `code` column. Full file/line references in the auditor report. | fix-code | accepted | Dan to fix (same as #015). Refer to 10-panel-gaap.md for file/line details. | 2026-08-23 |
| 024 | hobson-ac-spec-auditor | STALE-AC-1 | AccountCrud.md intro references 'structural specs' that do not exist. | fix-spec | accepted | Hobson to delete the vestigial sentence. Structural specs never existed; the concept is meaningless here. | 2026-08-23 |
| 025 | hobson-cr-spec-auditor | ENFORCE-CR-1 | The waivers for REQ-CR-1.1 and REQ-CR-4.2 cite a PK constraint that was missing. | fix-code | accepted | Fixed by Dan (2026-08-22) | 2026-08-23 |
| 026 | hobson-cr-spec-auditor | CONTRA-CR-1 | REQ-STG-5.2 says classification matches 'on the staged entry's description' but the engine matches on five fields. | fix-spec | accepted | Hobson to fix the spec | 2026-08-23 |
| 027 | hobson-dataingestion-audit | STALE-DEF-1 | Definitions.md still uses pre-migration 'account code' terminology. | fix-spec | accepted | Hobson to fix. Both Definitions.md and DataIngestion.md should use business terms only — "account" or "account at match", no implementation details. | 2026-08-23 |
| 028 | ngui-spec-auditor | CON-NGUI-1 | REQ-NGUI-1.1's universal 'All interface use cases' claim contradicted by Reports CLI. | — | overruled | Already ruled on in the 2026-08-21a audit | 2026-08-23 |
| 029 | statement-delta-auditor | SD-COUNT-1 | Dispute over 4/8/17 arithmetic in Dan's statement. | — | overruled | Don't care | 2026-08-23 |
| 030 | dal-source-auditor | SCHEMA-CR-PK-1 | Migration 13 rebuilds classification_rule without PRIMARY KEY. | fix-code | accepted | Fixed by Dan (2026-08-22) | 2026-08-23 |
| 031 | dal-source-auditor | SCHEMA-STG-FK-1 | Migration 14 rebuilds staged_entry_line without FK from classification_rule_id. | fix-code | accepted | Fixed by Dan (2026-08-22) | 2026-08-23 |
| 032 | interface-bridge-auditor | CORR-IB-1 | ClassificationRuleReturn.codeAtMatch populated with account name instead of code. | fix-code | accepted | Dan to fix (same as #019) | 2026-08-23 |
| 033 | code-truthfulness-auditor | CODE-STG-1 | fetchFiltered function and StageEntryFetchFilter type reference pre-migration `code` column. | fix-code | accepted | Dan to fix (same as #015) | 2026-08-23 |
| 034 | code-truthfulness-auditor | SCHEMA-CR-1 | Rebuilt classification_rule table missing PRIMARY KEY. | fix-code | accepted | Fixed by Dan (2026-08-22) | 2026-08-23 |
| 035 | code-truthfulness-auditor | STALE-DEF-1 | Definitions.md uses 'account code' terminology contradicting post-migration specs. | fix-spec | accepted | Hobson to fix using business terms (same as #027) | 2026-08-23 |
| 036 | disposition review | UNSPECCED-STG-FETCH-1 | StageEntryOrchestration.fetchFiltered and the FetchStageEntryFiltered CLI route have no REQ in DataIngestion.md. The route exists and is wired but has zero spec coverage and zero test coverage. | fix-spec | accepted | Hobson to write REQs for stage entry filtered fetch | 2026-08-23 |
| 037 | disposition review | UNTESTED-STG-FETCH-1 | No test exercises the FetchStageEntryFiltered route or StageEntryOrchestration.fetchFiltered. The broken sel.code reference survived two audits because nothing ever called the code path. | fix-test | accepted | BD to write tests after Hobson's REQs land and Dan fixes the stale code | 2026-08-23 |

## Statuses
- **pending** — not yet reviewed
- **accepted** — finding valid, action assigned
- **overruled** — finding rejected with reason
- **deferred** — acknowledged, not acting now (add revisit trigger)
