# statement-delta-auditor

## SD-1 — statement-delta
- **Location:** Dan's statement vs Src/InterfaceBridge/Routes/IngestionRoutes.fs, Src/ModelOrchestrator/StageEntryOrchestration.fs
- **Summary:** Dan lists deduplication and classification as separately invocable operations among "various UI routes," but both are coupled to ingestion with no standalone CLI path.
- **Resolution:** dan-decides

Dan's statement: "We have various UI routes for ingesting the initial raw file and staging the potential journal entries into the stage schema, deduplicating records, running classification rules, and manually setting their classification status." This phrasing presents deduplication and classification as peer capabilities alongside ingestion and manual status setting, each accessible through routes. In the repo, IngestionRoutes.fs defines 8 routes in the ingestion domain, but neither dedup nor classification has a standalone route. Both are embedded inside the IngestRawFileToStage route, which calls StageEntryOrchestration.ingestRawToStageThenDeduplicateAndClassify (line 404 of StageEntryOrchestration.fs). The orchestration functions deduplicateStagedEntries (line 333) and classifyStagedEntries (line 352) exist as module-level functions but are only called from within that combined pipeline. The 8 actual ingestion routes are: IngestRawFileToStage (bundles ingest+dedup+classify), NewClassificationRule, FetchClassificationRuleById, FetchClassificationRuleByName, FetchClassificationRuleFiltered, CreateIngestionSource, UpdateStageEntry, and PostStageEntries. Operational implication: if NoMatch entries exist and a new classification rule is added later, there is no CLI route to re-run classification against those entries; the only path is manual account assignment via UpdateStageEntry.

**Action:** Dan should decide whether standalone dedup and classify routes belong in the current slice or are deferred work. Either way, update the statement-of-position to accurately reflect that both operations are part of the ingestion pipeline, not independently invocable.

**Why:** A reader of Dan's statement would reasonably conclude that dedup and classification can be triggered independently through the CLI, which would affect how they plan operational workflows (e.g., adding rules then re-classifying existing entries). The actual architecture couples these to ingestion.

---

## SD-CONFIRMED — statement-delta
- **Location:** Dan's statement vs full repo
- **Summary:** All other claims in Dan's statement are confirmed by the repo; this finding lists what checked out.
- **Resolution:** dan-decides

Confirmed claims: (1) Data-ingestion slice is complete -- DataIngestion.md has 83 active REQs, all covered (71 tested, 11 waived, 1 unenforceable), 3 withdrawn. (2) Standardized import format is JSONL (REQ-STG-1.1, BaseStageRawRowInput contract). (3) External parser scripts produce the format (spec design note on system boundary). (4) Ingestion route exists (IngestRawFileToStage in IngestionRoutes.fs). (5) Staging into the ingestion schema (create schema ingestion in migration 202608081415). (6) Manual status/account setting via UpdateStageEntry route. (7) Shadow post exists (PostStageEntries with isShadow flag, uses runCommandRouteAndAutoRollback, returns before/after trial balances). (8) Account CRUD (10 routes: Create, FetchByCode, FetchByParentCode, FetchByAccountType, FetchAll, FetchActivity, FetchBalances, Deactivate, UpdateName, UpdateExternalReference). (9) Journal entry CRUD (11 routes: PostNew, FetchById, FetchByPeriod, FetchLinesByAccount, FetchByExternalReference, FetchByDateRange, Void, UpdateExternalReference, AddExternalReference, AddComment, UpdateComment). (10) Basic application utilities (Utilities project: AppError, ResultHelper, Clock, Calendar, FieldUpdate, FileIO, Json). (11) CLI handling (two executables per REQ-NGUI-4.1: SonOfLeoCli for commands, Reports for report generation). (12) Reporting suite starts with trial balance only (ReportRoutes.fs has one route: TrialBalance; Reporting.md confirms). (13) Fiscal period CRUD (5 routes: Create, FetchByKey, FetchAll, Close, Reopen -- close/reopen is open/closed toggle per REQ-FP-4.1/4.2). (14) No true period closing mechanics (FiscalPeriodCrud.md design note: "SonOfLeo keeps the open/closed state for posting gating but defers closing tooling until wanted"). No GAAP closing entries, adjusting entries, or reversing entries exist in the codebase.

**Action:** No action needed -- this is a confirmation summary.

**Why:** Provides Dan with evidence that the audit was thorough and that the bulk of his statement accurately reflects the repo state.

---
