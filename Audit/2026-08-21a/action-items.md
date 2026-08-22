# Action Items — 2026-08-21a Audit

| # | Finding | What | Owner | Status |
|---|---------|------|-------|--------|
| 1 | AC-EFF-1 | fetchAll tests assert only count, never inspect values | — | overruled — other tests in the same file verify ID, code, and name; all fetch functions resolve to the same readRowsFromDb |
| 2 | EFF-CR-1 | Remove magic string in source-pattern filter test. BD should derive the rule from fixture data (add splitDebitRule as a named fixture entity, or filter classificationRules by name containing TestSplitBank and assert non-empty) | Dan/BD | accepted |
| 3 | EFF-CR-1a | Same magic-string treatment for `REQ-CR-5.3 fetchRulesFiltered with activeOnly true omits the inactive rule...` test | Dan/BD | accepted |
| 4 | EFF-CR-1b | Organize Tests/Tests.Helpers/TestDataStage.fs — move ingestion sources created in the classification rules section up into the sources creation section | Dan/BD | accepted |
| 5 | BC-STG-1 | REQ-STG-5.4 test exercises ManyMatchesClearWinner, not OneMatch. BD should pick a fixture with one match, assert OneMatch outcome, then do existing assertions | Dan/BD | accepted |
| 6 | FP-COV-1 | REQ-FP-3.1 test asserts 2 of 7 FiscalPeriod properties. Assert the other properties | Dan/BD | accepted |
| 7 | ASSERT-JE-1 | REQ-JE-3.1 test asserts only counts on lines, refs, comments. Assert per the auditor's recommendation | Dan/BD | accepted |
| 8 | ASSERT-JE-2 | Cowardly inequality on matched-reference count | — | done |
| 9 | ASSERT-JE-3 | Four fetchByReference tests derive expected value from external reference count, not distinct JE count. Fix all 4 tests. Note: line numbers shifted from Dan's ASSERT-JE-2 fix | Dan/BD | accepted |
| 10 | TEST-MON-1 | REQ-MON-2.5.1 tests only max-exceed boundary; add the min-exceed case via add | Dan/BD | accepted |
| 11 | TEST-MON-2 | REQ-MON-2.6.1 tests only min-exceed boundary; add the max-exceed case via subtract | Dan/BD | accepted |
| 12 | SYS-EFF-1 | Test cites REQ-SYS-3.2 but asserts only UUID generation | — | done |
| 13 | STALE-DAL-EFF | DAL-EFFICACY ruling premise is factually wrong ("DAL has no behavioral spec"). Update verbiage in resolved-findings.md | Hobson | accepted |
| 14 | STALE-HOOK-1 | CompoundedLearnings articles say pre-commit hook is uninstalled; it's installed since 2026-07-31 | — | overruled — not actionable; Dan doesn't care |
| 15 | CUST-ACCTNAME-1 | Ingestion return types identify accounts by code without name, violating REQ-NGUI-1.6. Will require new specs and tests for the additional property | Dan | accepted |
| 16 | IDIOM-TZ-1 | timeZoneLocal defined independently in Clock and Calendar | — | overruled — already ruled on. Both call getConfigValue; 4 lines of duplicated config read is acceptable. Moving finances to a non-New-York timezone is not a realistic scenario |
| 17 | IDIOM-FMC-1 | FieldMatchChain.create and ClassificationRuleGroup.create accept empty lists | — | overruled with prejudice — established pattern: create functions are infallible, orchestrators validate. Same as Account.create not checking type/subtype validity |
| 18 | IDIOM-CR-1 | reconstitute and mapRawForDbRead were public, leaking persistence internals | — | done |
| 19 | AMB-STG-1 | No precedence rule when classification produces both NoMatch and Conflict on different lines. Fix spec to give Conflict priority. Write a test asserting that behavior | Hobson (spec) / Dan+BD (test) | accepted |
| 20 | IE-STG-1 | REQ-STG-5.5 omits recording classification_rule_id on the staged line. Fix spec | Hobson | accepted |
| 21 | CON-STG-1 | REQ-STG-9.4 claims FK constraint that intentionally doesn't exist. Fix spec per finding guidance | Hobson | accepted |
| 22 | AMB-JE-3.6.2 | REQ-JE-3.6.2 uses "should" instead of "must" | Hobson | accepted |
| 23 | AMB-NGUI-1 | REQ-NGUI-3.9 says "appropriate error", REQ-NGUI-4.5 says "typed error". Standardize to "typed error" | Hobson | accepted |
| 24 | AMB-RPT-1 | REQ-RPT-2.2 uses "level" instead of "generation". Fix spec — use one term or add equivalency statement | Hobson | accepted |
| 25 | SD-AUDIT-1 | Dan says all action items finished but #5 (CON-NGUI-1) is open | — | overruled — explicitly deferred; Dan knows |
| 26 | STALE-README-JSON | Src/README.md says Json is InterfaceBridge.Json; it moved to Utilities.Json. Fix both readme files | Hobson | accepted |
| 27 | NGUI-1.6-INGESTION | Ingestion return types carry account codes without names. Partially accepted — StageEntryLine excluded (no FK). ClassificationRuleReturn and PrioritizedMatchReturn accepted | Dan | accepted |
| 27a | NGUI-1.6-INGESTION | Dan: change ClassificationRule codeAtMatch reference to use account ID instead of code | Dan | accepted |
| 27b | NGUI-1.6-INGESTION | Dan: add a lookup cache on account ID → name (one way) | Dan | accepted |
| 27c | NGUI-1.6-INGESTION | Dan: add account name to ClassificationRuleReturn | Dan | accepted |
| 27d | NGUI-1.6-INGESTION | Dan: add account name to PrioritizedMatchReturn | Dan | accepted |
| 28 | CT-STG-1 | fetchFiltered errors instead of returning empty list when filter matches zero entries | — | done |
| 29 | CT-STG-2 | fetchDuplicates excludes voided JEs from ledger dedup check | — | overruled — voided entries must not block re-import. A void means the entry was wrong; re-importing the same source data is the correction path. Counting voided entries as duplicates would prevent that |

**Audit complete.** 31 auditors across 7 batches. 29 findings. 4 done, 8 overruled, 17 accepted (6 Hobson spec fixes, 5 Dan code changes, 6 Dan/BD test fixes).
