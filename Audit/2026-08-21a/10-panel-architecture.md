# corner-painting

_No findings._

## Reasoning

Thorough structural review of all 9 Src projects, 80 source files, 12 DB migrations, Definitions.md, architecture catalog, all audit conduct articles, and all 28 resolved findings. Evaluated each of the five mandated dimensions:

1. PROJECT STRUCTURE: The dependency chain (Utilities <- Model <- ModelOrchestrator <- InterfaceBridge <- SonOfLeoCli/Reports, plus DAL/Context/Logger as shared infrastructure) is clean, unidirectional, and enforced by F# compile order. Each new domain (obligations, portfolio, reconciliation) adds files to existing projects following the established per-domain pattern (component types in Model, orchestration in ModelOrchestrator, contracts/converters/routes in InterfaceBridge). The Reports binary is correctly separated from SonOfLeoCli. The structure holds.

2. GOD TYPES: AppError (164 cases) is intentionally the single application-wide error DU per Src/README.md -- not accretion, by design. StageEntryOrchestration.fs (759 lines) is the largest file but owns the StageEntry composite type's full lifecycle (a pipeline domain: ingest, deduplicate, classify, review, post), with classification-specific logic correctly split into ClassificationOrchestration.fs. JournalEntryOrchestration.fs (389 lines) is proportionally smaller because its composite lifecycle is simpler. FetchFilterAndSort.fs (82 lines) aggregates filter/sort type definitions across domains -- a structural necessity of F# compile order (these types must be visible to all subsequent orchestration files). No module accretes unrelated responsibilities.

3. DATABASE SCHEMA: The staging schema (ingestion.staged_entry, staged_entry_line, staged_entry_audit, source, classification_rule) is well-normalized with appropriate FK relationships. JSONB for classification rule_groups is appropriate for deeply nested rule structures. The classification_rule.code_at_match FK to ledger.account(code) is safe because account codes are immutable in the current design (no UpdateCode route exists). Staged entries are correctly defined as non-entities (per Definitions.md) with their own audit trail, so entity-level policies (SYS-3.1 timestamps) correctly do not apply (resolved finding DB-STAGE-1). No schema shape will block period close, reconciliation, or analytics.

4. BOUNDARIES: InterfaceBridge follows a consistent contract/converter/route pattern per domain that scales linearly. Return types are shared where operations produce the same shape (e.g., TrialBalanceReturnRow used by both the standalone TrialBalance report and the posting impact view). Input types are never shared across semantically different operations. The Reports CLI reuses ModelOrchestrator computation (fetchTrialBalanceData) cleanly. New reports add routes to ReportRoutes.fs and writers to ReportWriters/ without touching existing code.

5. COUPLING: The post function in StageEntryOrchestration accepts jeHeaderSource as a parameter (not hardcoded at the function level -- the caller provides it). Classification rules use domain types throughout and are correctly coupled to the ledger only via the code_at_match FK. The LookupCache's short-burst design with no invalidation is explicitly documented with a note that longer-lifecycle usage would need redesign -- Dan already knows this. StageEntryOrchestration.postStageEntry reuses JournalEntry.constructNewAndSaveToDb, getting all JE validations for free rather than duplicating them.

Verified Dan's statement of position against the code: all described capabilities (classification rule CRUD, ingestion source management, raw file ingestion with dedup and classification, manual stage entry updates, shadow and real posting with trial balance impact) exist and work as described. No statement-delta found.

Considered and rejected as potential findings:
- Three patterns for complex filtered reads (Account private readRowsFromDb, StageEntryHeader public fetchByQuery, ClassificationRule public reconstitute/mapRawForDbRead): style variation, not corner-painting. Each can be refactored locally. The ClassificationRule pattern exists because its filtered fetch needs JSONB-specific SQL in the orchestrator. Not a structural constraint.
- No indexes beyond PKs: normal DB tuning, not architecture. Adding indexes later is additive, not a refactor.
- No archival mechanism for posted staged entries: data management feature, not structural constraint.
- LookupCache creates its own DB connections outside Context pattern: explicitly documented as intentional for short-burst CLI, with a forward-looking note about redesign needs.
