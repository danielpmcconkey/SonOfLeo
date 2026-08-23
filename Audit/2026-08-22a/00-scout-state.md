# Scout — Derived Repo State

Branch: main. HEAD: 56cd387 ("Hobson notes: CashFlow domain brief, migration plan, wakeup 2026-08-22b"). Last 15 commits span classification slice completion, account-ID pivot (code-to-ID migration in stage entries and classification rules), audit remediation, and CashFlow domain brief.

SPECS: 10 behavioral spec files in Specs/Behavioral/. Specs/README.md defines the documentation system, requirement ID grammar (REQ-DOMAIN-section.n), linkage rules (test names carry REQ IDs; source code has no REQ annotations), and three requirement states (tested, waived, unenforceable). Specs/Definitions.md defines 14 domain terms (Money, Price, Quantity, Rate, Entity, Instant, Date, Calendar period, Staged entry, Staged line, Postable, Interface, Actors, layers).

REQ COUNTS PER SPEC (Active / Waived / Unenforceable / Tested / Withdrawn):
- AccountCrud.md (REQ-AC): 77 active, 23 waived, 6 unenforceable, 48 tested, 39 withdrawn
- ClassificationRuleCrud.md (REQ-CR): 54 active, 9 waived, 0 unenforceable, 45 tested, 0 withdrawn
- DataAccessLayer.md (REQ-DAL): 19 active, 16 waived, 3 unenforceable, 0 tested, 14 withdrawn
- DataIngestion.md (REQ-STG): 82 active, 11 waived, 1 unenforceable, 70 tested, 6 withdrawn
- FiscalPeriodCrud.md (REQ-FP): 26 active, 8 waived, 0 unenforceable, 18 tested, 1 withdrawn
- JournalEntryCrud.md (REQ-JE): 83 active, 18 waived, 2 unenforceable, 63 tested, 4 withdrawn
- Money.md (REQ-MON): 27 active, 4 waived, 1 unenforceable, 22 tested, 0 withdrawn
- NonGraphicalInterface.md (REQ-NGUI): 27 active, 17 waived, 0 unenforceable, 10 tested, 2 withdrawn
- Reporting.md (REQ-RPT): 23 active, 11 waived, 0 unenforceable, 12 tested, 1 withdrawn
- SystemWide.md (REQ-SYS): 12 active, 6 waived, 0 unenforceable, 6 tested, 3 withdrawn
TOTALS: 430 active REQs (123 waived, 13 unenforceable, 294 tested), 70 withdrawn.

Note: waived counts may include a few cross-referenced REQ IDs from the "Reason" column text; true unique waived IDs are slightly lower. DAL has 0 tested REQs because all 19 active are either waived (16) or unenforceable (3) — DAL requirements are architectural/environmental constraints enforced by code patterns and schema, not by named tests.

SRC PROJECTS (9 fsproj, 80 .fs files): Utilities (8 files: AppError, ResultHelper, ConfigManager, Clock, Calendar, FieldUpdate, FileIO, Json), Context (1 file), Logger (1 file: Audit), DataAccessLayer (6 files: DbConnections, DbTransaction, QueryParameters, ExecuteReader, ExecuteNonQuery, ExecuteScalar), Model (23 files across Ledger/ and DataIngestion/ subdirs including Classification/), ModelOrchestrator (15 files: Account*, FiscalPeriod*, JournalEntry*, TrialBalance, ClassificationOrchestration, StageEntryOrchestration, FetchFilterAndSort), InterfaceBridge (25 files: CommandRoute, InterfaceContracts/, BoundaryConverters/, Routes/, ReportVisualizationAssets/, ReportWriters/), SonOfLeoCli (1 file: Program.fs), Reports (1 file: Program.fs).

TESTS: 3 test projects — Tests.Helpers (8 .fs files, shared fixtures/utilities), Tests.Isolated (12 .fs test files, pure model tests), Tests.Integrated (22 .fs test files + SharedTestDataCollection.fs, DB-backed integration tests). Approximate test counts: 487 [Fact] attributes, 30 [Theory] attributes across all test files.

Tests cover: Model/Ledger (AccountComponent, FiscalPeriod, JournalEntryComponent, JournalEntryExternalReference, Money — isolated), Model/DataIngestion (ClassificationRuleComponent, ClassificationRuleGroupEvaluation, ClassificationRuleMatching, Classifier, FieldMatchEvaluation, FieldMatchChainEvaluation, StageEntryStatusTransition — isolated), Model/Ledger (Account, FiscalPeriod, JournalEntryComment, JournalEntryExternalReference, JournalEntryHeader — integrated), ModelOrchestrator (AccountActivity, AccountBalance, AccountCreation, AccountDeactivation, ClassificationRuleCrud, FiscalPeriodCreation, JournalEntry[Creation/Fetching/Voiding/Comment/ExternalReference/Header/Line]Orchestration, StageEntry[Ingestion/Classification/Posting/Update], TrialBalance — integrated), InterfaceBridge (Account/FiscalPeriod/JournalEntry/Ingestion/ReportRoutes — integrated), DataAccessLayer (DalTests — integrated), Reports/Program, SonOfLeoCli/Program.

DOMAIN COVERAGE MATRIX:
- Account: spec YES, code YES (Model/Ledger + ModelOrchestrator + InterfaceBridge), tests YES (isolated + integrated)
- FiscalPeriod: spec YES, code YES, tests YES
- JournalEntry (header/line/comment/extref): spec YES, code YES, tests YES
- Money: spec YES, code YES, tests YES (isolated)
- DataIngestion/Staging: spec YES, code YES (Model/DataIngestion + StageEntryOrchestration), tests YES (integrated)
- ClassificationRule: spec YES, code YES (Model/DataIngestion/Classification + ClassificationOrchestration), tests YES (isolated + integrated)
- Reporting: spec YES, code YES (ModelOrchestrator/TrialBalance + InterfaceBridge/ReportRoutes + Reports/Program), tests YES
- DAL: spec YES, code YES, tests YES (DalTests — 1 Theory)
- NonGraphicalInterface: spec YES, code YES (InterfaceBridge/CommandRoute + SonOfLeoCli/Program), tests YES (SonOfLeoCli/Program — 7 Facts)
- SystemWide: spec YES (cross-cutting), no dedicated code/tests — enforced per-entity

DB MIGRATIONS (14, chronological):
1. 202606131450-CreateDatabase
2. 202606131454-CreateSchemaLedger
3. 202606131456-CreateAndPopulateAccountType
4. 202606131458-CreateAccountTable
5. 202606201243-CreateFiscalPeriodTable
6. 202606210910-RecreateAccountTable
7. 202606220851-AccountActiveAsDates
8. 202606221206-CreateJeTables
9. 202606231237-RemoveAccountType
10. 202606250813-RenameCommentsComment
11. 202608081415-CreateStageSchemaAndTables
12. 202608110820-ModifyClassificationRule
13. 202608220920-RebuildClassificationRule
14. 202608220946-RebuildStageEntryLine

Most recent migrations (13-14) are from today (2026-08-22): RebuildClassificationRule and RebuildStageEntryLine, part of the account-ID pivot (code-to-ID migration in stage entries and classification rules).

ADDITIONAL DIRECTORIES: Checks/ (shell scripts for pre-commit checks), Skills/ (procedure scripts including traceability audit), CompoundedLearnings/ (guidance articles), DevDataStage/ and DevDebugPayloads/ (dev fixtures), Audit/ and BdsNotes/ and HobsonsNotes/ (historical/notes).
