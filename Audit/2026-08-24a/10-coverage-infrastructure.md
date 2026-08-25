# code-outward-coverage-auditor

_No findings._

## Reasoning

Examined all 11 source files in scope. Read all 12 audit conduct articles and the resolved findings ledger before assessing.

OBSERVABLE ENTRY POINTS CHECKED:
- Reports/Program.fs: The only true CLI entry point in scope. `route` function dispatches by report name (REQ-NGUI-4.5 tested). `main` function handles three arg-parsing branches: stdin payload (REQ-NGUI-4.3 waived), --file flag (REQ-NGUI-3.10 waived), and no-args usage exit (covered by REQ-NGUI-4.4 general error behavior). Success/failure exit codes and stdout/stderr output tested by 6 test methods in Tests/Tests.Integrated/Reports/Program.fs.

INTERNAL INFRASTRUCTURE MODULES CHECKED (not flagged per "DO NOT flag internal helper functions that are only reachable through a tested public API"):
- Context.fs: `create`, `getDatabaseTransaction`, `getInitiationInstant`, `updateInitiationInstant` -- every route calls Context.create; getDatabaseTransaction/getInitiationInstant are called from 40+ locations across ModelOrchestrator and Model; updateInitiationInstant is called from StageEntryOrchestration and exercised by StageEntryPosting, StageEntryUpdate, StageEntryIngestion, and ClassificationRuleCrud tests. The `ExistingTransaction` match case in create is currently uncalled (no production code constructs ExistingTransaction), but it is a trivial pass-through on a DU required for exhaustive matching -- forward-looking infrastructure, not a testable behavior.
- Audit.fs: AuditEnvelope.create called by Context.create (transitively tested by every test). Accessors `action` and `instant` used by Context functions. `uniqueId` accessor exists but is uncalled -- the field is allocated but not read, planned for future audit logging per the todo comment. Not flagged: the accessor is a trivial dead-code accessor on infrastructure with a clear forward-looking purpose, and "nice to have" cleanup is not a finding.
- AppError.fs: Type definition (DU) and toMessage formatter. Used by every error path in the system. Transitively tested by every error-path assertion across 541 test methods.
- Calendar.fs: `today()` used in 60+ locations across production and test code. `dateFromInstant` used in AccountCreation and AccountDeactivation. `localDateToString` used in ReportHeader and TrialBalanceWriter. All transitively tested.
- Clock.fs: `now()` (with microsecond truncation for REQ-SYS-5.1 persistence fidelity) used by AuditEnvelope.create and directly in IngestionRoutes. `instantToString` used in IngestionRoutes (file timestamping) and ReportFooter. All transitively tested.
- ConfigManager.fs: `getConfigValue` used by Clock and Calendar at module initialization. Transitively tested (the application cannot start without it). No direct tests found, but the function is internal infrastructure whose failure mode prevents any test from running at all.
- FieldUpdate.fs: Type and helper functions used across all update routes (Account, JE, StageEntry, ClassificationRule updates). Transitively tested through 10+ test files that exercise update operations.
- FileIO.fs: `createFullPath`, `confirmFileExists`, `confirmDirectoryExists`, `readTextFileLines`, `moveFile` all used by IngestionRoutes.fs (ingestRawEntries). `writeTextFile` used by TrialBalanceWriter. All transitively tested through IngestionRoutes and Reports integrated tests.
- Json.fs: `fromJson` and `toJson` used by every route handler for payload marshalling. Transitively tested by every route test.
- ResultHelper.fs: `ResultBuilder` (result CE) used in virtually every function. `convertListOfResultsToResultsList` used in ingestion and report routes. `convertOptionToDesiredTypeWithFallibleConverter` used by FieldUpdate. All transitively tested.

STATEMENT-DELTA CHECK: Dan's statement mentions the Option 4 status redesign (removing inline status from stage_entry, deriving from audit trail). The files in my scope (Context, Audit, Utilities) are not directly involved in the status redesign -- that work lives in Model/DataIngestion/StageEntryHeader.fs and its orchestration, which are outside my scope. No contradiction found between Dan's statement and the code in my scope.

CONSIDERED BUT NOT RAISED:
- Duplicate `timeZoneLocal` in Clock.fs and Calendar.fs: identical code reading the same config key. This is a code-quality observation (duplication), not an unspecced behavior or untested code path. Outside my finding categories.
- Mutable cache in ConfigManager.fs with boxing/unboxing: potential type-safety concern if same key is read as different types, but caught by try/catch and never happens in practice (each key is always read as the same type). Implementation detail, not an observable behavior gap.
- AuditEnvelope.uniqueId never read: planned for future audit logging. Todo comment explicitly notes this. Per resolved finding SS-3, todo remarks are intentional and not audit findings.
