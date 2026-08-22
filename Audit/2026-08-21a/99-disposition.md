# Disposition Record

| # | Auditor | ID | Summary | Owner | Status | Ruling | Date |
|---|---------|----|---------|----- -|--------|--------|------|
| 001 | ac-efficacy-auditor | AC-EFF-1 | REQ-AC-3.7 fetchAll tests assert only count at both the model and route layers, never inspecting any returned account value (Specimen 3). | fix-test | pending | | |
| 002 | efficacy-cr | EFF-CR-1 | The source-pattern filter test hard-wires its expected rule names instead of deriving them from fixture data, matching Specimen 1. | fix-test | pending | | |
| 003 | test-efficacy-stg | BC-STG-1 | The REQ-STG-5.4 test exercises the ManyMatchesClearWinner code path, not OneMatch, because the entry it picks (DoorDash) matches two classification rules. | fix-test | pending | | |
| 004 | fp-efficacy | FP-COV-1 | REQ-FP-3.1 test asserts 2 of 7 FiscalPeriod properties; five are unverified on the read path at any layer. | fix-test | pending | | |
| 005 | je-efficacy | ASSERT-JE-1 | REQ-JE-3.1 test asserts only counts on lines, refs, and comments without inspecting any values (Specimen 3). | fix-test | pending | | |
| 006 | je-efficacy | ASSERT-JE-2 | Assert.True(matchedCount > 0) is a cowardly inequality (Specimen 2) on the matched-reference count within a returned entry. | fix-test | pending | | |
| 007 | je-efficacy | ASSERT-JE-3 | Expected-value derivation in four fetchByReference tests counts matching external references instead of distinct journal entries, comparing the wrong quantity against the actual entry count. | fix-test | pending | | |
| 008 | money-efficacy | TEST-MON-1 | REQ-MON-2.5.1 tests only the max-exceed boundary; the reachable min-exceed case via add is untested. | fix-test | pending | | |
| 009 | money-efficacy | TEST-MON-2 | REQ-MON-2.6.1 tests only the min-exceed boundary; the reachable max-exceed case via subtract is untested. | fix-test | pending | | |
| 010 | sys-efficacy | SYS-EFF-1 | Test REQ-AC-2.13 REQ-SYS-3.2 constructNew generates UUID cites REQ-SYS-3.2 but asserts only UUID generation, not timestamp behavior. | fix-test | pending | | |
| 011 | stale-ruling-auditor | STALE-DAL-EFF | DAL-EFFICACY ruling's premise -- "The DAL has no behavioral spec of its own" -- is factually wrong and broad enough to suppress non-efficacy findings about DataAccessLayer.md. | fix-spec | pending | | |
| 012 | agentic-readiness | STALE-HOOK-1 | Two CompoundedLearnings articles state the pre-commit hook is uninstalled, but it was re-enabled on 2026-07-31 and is currently installed and working. | fix-spec | pending | | |
| 013 | customer-gap | CUST-ACCTNAME-1 | The data-ingestion slice's return types identify accounts by code without including the account name, violating REQ-NGUI-1.6 and degrading the customer's review surface. | fix-code | pending | | |
| 014 | code-quality | IDIOM-TZ-1 | timeZoneLocal is defined independently in both Clock and Calendar modules, duplicating the same config read and time zone construction. | fix-code | pending | | |
| 015 | code-quality | IDIOM-FMC-1 | FieldMatchChain.create and ClassificationRuleGroup.create accept illegal states (empty chains/groups) without validation, relying on a silent backstop in doesMatch rather than failing at construction. | fix-code | pending | | |
| 016 | code-quality | IDIOM-CR-1 | ClassificationRule.reconstitute and mapRawForDbRead are public, leaking persistence internals to the orchestration layer, contrary to the pattern established by every other domain module. | fix-code | pending | | |
| 017 | spec-audit-data-ingestion | AMB-STG-1 | No precedence rule when classification produces both NoMatch and Conflict on different lines of the same entry. | fix-spec | pending | | |
| 018 | spec-audit-data-ingestion | IE-STG-1 | REQ-STG-5.5 omits recording classification_rule_id on the staged line, unlike the parallel REQ-STG-5.4. | fix-spec | pending | | |
| 019 | spec-audit-data-ingestion | CON-STG-1 | REQ-STG-9.4 claims staged line account codes are FK-constrained against the chart of accounts, but the schema intentionally has no such FK. | fix-spec | pending | | |
| 020 | JournalEntryCrud Spec Auditor | AMB-JE-3.6.2 | REQ-JE-3.6.2 uses "should" where every other capability requirement in the spec uses "must," leaving the mandatory/optional status of the as-of date filter ambiguous. | fix-spec | pending | | |
| 021 | ngui-spec-auditor | AMB-NGUI-1 | REQ-NGUI-3.9 and REQ-NGUI-4.5 describe the same error scenario for parallel CLIs but use different error-qualification language: "appropriate error" vs "typed error." | fix-spec | pending | | |
| 022 | spec-audit-reporting | AMB-RPT-1 | REQ-RPT-2.2 introduces the term "level" for the boundary-type row field without mapping it to "generation," the term defined and used everywhere else in the spec for the same concept. | fix-spec | pending | | |
| 023 | statement-delta | SD-AUDIT-1 | Dan says all audit action items are finished, but action item #5 (CON-NGUI-1) remains open. | dan-decides | pending | | |
| 024 | utilities-auditor | STALE-README-JSON | Src/README.md identifies the Json module as InterfaceBridge.Json, but the module was moved to Utilities.Json during the classification-rule slice. | fix-code | pending | | |
| 025 | interface-bridge-auditor | NGUI-1.6-INGESTION | Three ingestion-domain return types carry account codes without account names, violating REQ-NGUI-1.6. | fix-code | pending | | |
| 026 | code-truthfulness | CT-STG-1 | StageEntryOrchestration.fetchFiltered errors instead of returning an empty list when the filter matches zero staged entries. | fix-code | pending | | |
| 027 | code-truthfulness | CT-STG-2 | The fetchDuplicates query excludes voided journal entries from the ledger dedup check, narrowing REQ-STG-7.3 beyond what the spec permits. | dan-decides | pending | | |

## Statuses
- **pending** — not yet reviewed
- **accepted** — finding valid, action assigned
- **overruled** — finding rejected with reason
- **deferred** — acknowledged, not acting now (add revisit trigger)
