# Disposition Record

| # | Auditor | ID | Summary | Owner | Status | Ruling | Date |
|---|---------|----|---------|----- -|--------|--------|------|
| 001 | AccountCrud Efficacy Auditor | AQ-AC-1 | REQ-AC-3.4 FetchByCode happy path test contains zero value assertions (Specimen 7). | fix-test | pending | | |
| 002 | DataIngestion Test Efficacy Auditor | STG-ASSERT-1 | The validTransitions Theory test asserts only the count of valid transitions per status, never inspecting which transitions are returned. | fix-test | pending | | |
| 003 | DataIngestion Test Efficacy Auditor | STG-MISLABEL-1 | The test cites REQ-STG-5.3 but exercises inactive-rule filtering, not the non-null account_code protection behavior the requirement describes. | fix-test | pending | | |
| 004 | DataIngestion Test Efficacy Auditor | STG-CONTRA-1 | The 'Postable' definition in Definitions.md includes a line-level account_code condition that REQ-STG-4.4 explicitly excludes. | fix-spec | pending | | |
| 005 | FiscalPeriodCrud Efficacy Auditor | FP-AQ-1 | REQ-FP-3.4 route test uses a cowardly inequality with a hard-wired floor (Specimen 2 + Specimen 1). | fix-test | pending | | |
| 006 | FiscalPeriodCrud Efficacy Auditor | FP-AQ-2 | Date-derivation tests assert month and day but never assert the year component of the derived start and end dates. | fix-test | pending | | |
| 007 | JournalEntryCrud Test-Efficacy Auditor | IDIOM-JE-1 | REQ-JE-3.6.1 net-balance-orientation test uses a cowardly inequality (Specimen 2) instead of asserting the exact expected amount. | fix-test | pending | | |
| 008 | JournalEntryCrud Test-Efficacy Auditor | IDIOM-JE-2 | REQ-JE-3.4 has no happy-path test; the only citing test exercises sad-path validation, leaving the actual line-retrieval behavior of JournalEntryLine.fetchByAccountId untested. | fix-test | pending | | |
| 009 | JournalEntryCrud Test-Efficacy Auditor | IDIOM-JE-3 | Test cites REQ-JE-4.9 (external reference update) but exercises comment update no-op rejection, which is a different behavior. | fix-test | pending | | |
| 010 | JournalEntryCrud Test-Efficacy Auditor | IDIOM-JE-4 | Six creation tests (Specimen 7) prove the call returned Ok but discard the returned entry and contain no assertion — they cannot detect silent data corruption of nullable/optional fields. | fix-test | pending | | |
| 011 | money-test-efficacy-auditor | MON-SPEC4-1 | All 13 sad-path tests in Money.fs use Assert.True(result.IsError) instead of typed DU matching, which is Specimen 4 verbatim. | fix-test | pending | | |
| 012 | NGUI Test-Efficacy Auditor | NGUI-AQ-1 | The SonOfLeoCli stderr test uses Assert.Contains on error text, matching Specimen 4's string-matching sibling pattern, while the Reports equivalent for the same requirement uses Assert.Equal. | fix-test | pending | | |
| 013 | NGUI Test-Efficacy Auditor | NGUI-COV-1 | REQ-NGUI-1.3.1 describes two behaviors -- error message in payload (tested) and full stack trace for system exceptions (not tested, not waived). | dan-decides | pending | | |
| 014 | reporting-test-efficacy-auditor | RPT-SORT-1 | The REQ-RPT-1.6 test verifies flat alphabetical code sort instead of depth-first tree ordering, and the todo comment on line 110 confirms the test is known to be wrong. | fix-test | pending | | |
| 015 | SystemWide Test Efficacy Auditor | SYS-5.1-PARTIAL | Account REQ-SYS-5.1 round-trip test asserts only 2 of 10 entity properties, failing the smell test for the 8 unchecked fields. | fix-test | pending | | |
| 016 | agentic-readiness | STALE-REF-1 | DataIngestion.md says the classification rule entity "is specified separately" in two places, but no separate behavioral spec exists in Specs/Behavioral/. | fix-spec | pending | | |
| 017 | agentic-readiness | TEST-GAP-1 | Four of eight ingestion CLI routes (all classification rule CRUD routes) have no route-level tests. | fix-test | pending | | |
| 018 | corner-painting-auditor | DB-STAGE-1 | The ingestion.staged_entry and ingestion.staged_entry_line tables are persisted entities that lack the created_at and modified_at columns required by REQ-SYS-3.1. | fix-code | pending | | |
| 019 | Hobson (Customer Audit — NEAR/MID/FAR) | CUST-FETCH-1 | The data-ingestion slice has no CLI route to query staged entries by status, source file, or ID, despite the orchestrator layer exposing fetchAllByFile, fetchByStageEntryHeaderId, and fetchAllForPosting. | fix-code | pending | | |
| 020 | Hobson (Customer Audit — NEAR/MID/FAR) | CON-POSTABLE-1 | Definitions.md defines Postable as requiring both status AND all-lines-coded; REQ-STG-4.4 explicitly states that no line-level account_code filtering is applied — the postability check is status-only. | fix-spec | pending | | |
| 021 | code-quality-auditor | IDIOM-FC-1 | All-match semantics implemented via map/filter/count instead of List.forall in FieldMatchChain.doesMatch and ClassificationRule.doesMatch. | fix-code | pending | | |
| 022 | code-quality-auditor | IDIOM-CL-1 | Classifier.classifyCandidate matches on List.length instead of pattern matching on list structure, requiring a separate List.head call. | fix-code | pending | | |
| 023 | code-quality-auditor | MAINTAINABILITY-TZ-1 | The system timezone is independently defined in both Clock.eastern and Calendar.localTimeZone as separate lookups of America/New_York. | fix-code | pending | | |
| 024 | DataIngestion-spec-auditor | CON-STG-1 | Six requirements cite status values in lowercase that do not match the PascalCase canonical set defined by REQ-STG-4.1. | fix-spec | pending | | |
| 025 | DataIngestion-spec-auditor | CON-STG-2 | Definitions.md defines Postable as requiring both status AND all-lines-coded; REQ-STG-4.4 explicitly excludes the all-lines-coded criterion. | dan-decides | pending | | |
| 026 | JournalEntryCrud Auditor | CON-JE-1 | REQ-JE-4.2's "only" clause excludes the external-reference operations that REQ-JE-4.9 and REQ-JE-4.10 explicitly permit. | fix-spec | pending | | |
| 027 | NGUI Spec Auditor | CON-NGUI-1 | REQ-NGUI-1.1's universal quantifier "All interface use cases" claims every use case follows the domain+verb+payload trigger pattern, but Section 4's Reports CLI accepts only a report name and payload -- no domain, no verb. | fix-spec | pending | | |
| 028 | reporting-auditor | TEST-GAP-RPT-1 | REQ-RPT-1.6 is classified as tested but the test verifies flat alphabetical sort, not the depth-first tree order the requirement specifies. | fix-test | pending | | |
| 029 | SystemWide-auditor | EG-SYS-3.1a | REQ-SYS-3.1 requires every persisted entity to carry created_at and modified_at timestamps, but ingestion.staged_entry and ingestion.staged_entry_line are entities (per Definitions.md) that lack both columns. | dan-decides | pending | | |
| 030 | statement-delta-auditor | SD-1 | Dan lists deduplication and classification as separately invocable operations among "various UI routes," but both are coupled to ingestion with no standalone CLI path. | dan-decides | pending | | |
| 031 | statement-delta-auditor | SD-CONFIRMED | All other claims in Dan's statement are confirmed by the repo; this finding lists what checked out. | dan-decides | pending | | |
| 032 | code-truthfulness-auditor-model-orchestrator | SCHEMA-STG-1 | ingestion.staged_entry and ingestion.staged_entry_line lack the created_at and modified_at columns required by REQ-SYS-3.1, unlike the other staging entities (ingestion.source and ingestion.classification_rule) which do carry them. | fix-code | pending | | |
| 033 | code-truthfulness-auditor-model-orchestrator | SPEC-STG-1 | Four requirements use lowercase status value strings ('ingested', 'classified', 'conflict') while REQ-STG-4.1 defines the canonical set in PascalCase ('Ingested', 'Classified', 'Conflict'); these are stored as varchar in PostgreSQL where comparison is case-sensitive. | fix-spec | pending | | |

## Statuses
- **pending** — not yet reviewed
- **accepted** — finding valid, action assigned
- **overruled** — finding rejected with reason
- **deferred** — acknowledged, not acting now (add revisit trigger)
