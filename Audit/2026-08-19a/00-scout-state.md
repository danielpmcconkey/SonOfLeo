# Scout — Derived Repo State

Branch: main. HEAD: eb0ee16 ("Add wakeup notes for 2026-08-19a"). Last 15 commits span wakeup notes, REQ renumbering (STG-1.4a/1.4b to 1.4/1.16), test-name grading, slice-loop codification, REQ-STG-9.3 source-clause rewording, REQ-STG-4.4 status-only filter test, cosmetic/validation fixes for StageEntry and AccountCode None handling.

SPECS: 9 behavioral specs in Specs/Behavioral/. Definitions.md defines Money/Price/Quantity/Rate/Entity/Instant/Date/CalendarPeriod/StagedEntry/StagedLine/Postable/Interface/Actors and layer terms. README.md describes the documentation system, REQ ID grammar, linkage rules, and commit gate.

REQ COUNTS PER SPEC (active=tested+waived+unenforceable; withdrawn separate):
- AccountCrud.md [REQ-AC]: 111 active (85 tested, 20 waived, 6 unenforceable), 35 withdrawn
- DataAccessLayer.md [REQ-DAL]: 32 active (13 tested, 16 waived, 3 unenforceable), 13 withdrawn
- DataIngestion.md [REQ-STG]: 83 active (71 tested, 11 waived, 1 unenforceable), 3 withdrawn
- FiscalPeriodCrud.md [REQ-FP]: 27 active (19 tested, 8 waived, 0 unenforceable), 1 withdrawn
- JournalEntryCrud.md [REQ-JE]: 87 active (70 tested, 16 waived, 1 unenforceable), 4 withdrawn
- Money.md [REQ-MON]: 27 active (22 tested, 4 waived, 1 unenforceable), 0 withdrawn
- NonGraphicalInterface.md [REQ-NGUI]: 28 active (12 tested, 16 waived, 0 unenforceable), 2 withdrawn
- Reporting.md [REQ-RPT]: 24 active (13 tested, 11 waived, 0 unenforceable), 1 withdrawn
- SystemWide.md [REQ-SYS]: 14 active (8 tested, 6 waived, 0 unenforceable), 2 withdrawn
TOTALS: 433 active (313 tested, 108 waived, 12 unenforceable), 61 withdrawn.

SRC: 9 projects under Src/. Compile orders from fsproj files:
- Utilities (7 files): AppError, ResultHelper, Clock, Calendar, FieldUpdate, FileIO, Json
- Context (1 file): Context.fs
- Logger (1 file): Audit.fs
- DataAccessLayer (6 files): DbConnections, DbTransaction, QueryParameters, ExecuteReader, ExecuteNonQuery, ExecuteScalar
- Model (23 files): LookupCache, Money, Ledger/{AccountComponent,Account,FiscalPeriodComponent,FiscalPeriod,JournalEntryComponent,JournalEntryHeader,JournalEntryLine,JournalEntryExternalReference,JournalEntryComment}, DataIngestion/{StageEntryComponent,StageEntryStatusTransition,Classification/ClassificationRuleComponent,Classification/FieldMatch,IngestionSource,StageEntryHeader,StageEntryLine,BaseStageEntry,Classification/FieldMatchChain,Classification/ClassificationRuleGroup,Classification/ClassificationRule,Classification/Classifier}
- ModelOrchestrator (15 files): FetchFilterAndSort, AccountDeactivation, AccountActivity, AccountBalance, AccountCreation, FiscalPeriodCreation, JournalEntryCommentOrchestration, JournalEntryExternalReferenceOrchestration, JournalEntryLineOrchestration, JournalEntryHeaderOrchestration, JournalEntryOrchestration, JournalEntryVoiding, TrialBalance, ClassificationOrchestration, StageEntryOrchestration
- InterfaceBridge (27 files): CommandRoute, contracts (Shared/Account/FiscalPeriod/Journal/Reports/Ingestion), converters (Account/JournalEntry/Money/Orchestration/FiscalPeriod/Report/Ingestion), routes (Account/FiscalPeriod/JournalEntry/Ingestion), report visualization (Css/HtmlWrapper/ReportHeader/ReportBody/ReportFooter), TrialBalanceWriter, ReportRoutes
- SonOfLeoCli (1 file): Program.fs
- Reports (1 file): Program.fs
Total: 82 .fs source files.

TESTS: 2 test projects. 410 [<Fact>] + 21 [<Theory>] = 431 test methods.
- Tests.Helpers (8 files, no tests -- shared utilities): Cleanup, CliExecutor, EntityFunctions, GenericTestProperties, Railroad, RouteResolver, SadPath, TestDataStage
- Tests.Isolated (7 test files): Model/DataIngestion/{Classifier(6F),StageEntryStatusTransition(13F,1T)}, Model/Ledger/{AccountComponent(93F),FiscalPeriod(1F,1T),JournalEntryComponent(27F),JournalEntryExternalReference(12F)}, Model/Money(26F)
- Tests.Integrated (33 files, 1 shared data collection): DAL/DalTests(1T), InterfaceBridge/{AccountRoutes(17F,8T),FiscalPeriodRoutes(9F),IngestionRoutes(6F,1T),JournalEntryRoutes(14F,8T),ReportRoutes(4F)}, Model/Ledger/{Account(17F),FiscalPeriod(12F),JournalEntryComment(11F),JournalEntryExternalReference(9F),JournalEntryHeader(1F)}, ModelOrchestrator/{AccountActivity(10F),AccountBalance(7F),AccountCreation(3F),AccountDeactivation(6F),FiscalPeriodCreation(1F),JournalEntryCommentOrchestration(3F),JournalEntryCreation(18F,1T),JournalEntryFetching(14F),JournalEntryVoiding(5F),StageEntryClassification(7F),StageEntryIngestion(18F),StageEntryPosting(11F),StageEntryUpdate(9F),TrialBalance(7F)}, Reports/Program(6F), SonOfLeoCli/Program(7F)

DOMAIN COVERAGE MATRIX (spec / code / tests):
- Account: spec YES (AccountCrud.md) / code YES (Model/Ledger/Account*, ModelOrchestrator/Account*) / tests YES (Isolated+Integrated)
- JournalEntry: spec YES (JournalEntryCrud.md) / code YES (Model/Ledger/JournalEntry*, ModelOrchestrator/JournalEntry*) / tests YES (Isolated+Integrated)
- FiscalPeriod: spec YES (FiscalPeriodCrud.md) / code YES (Model/Ledger/FiscalPeriod*, ModelOrchestrator/FiscalPeriodCreation) / tests YES (Isolated+Integrated)
- Money: spec YES (Money.md) / code YES (Model/Money.fs) / tests YES (Isolated)
- DataIngestion/Staging: spec YES (DataIngestion.md) / code YES (Model/DataIngestion/*, ModelOrchestrator/StageEntry*, ClassificationOrchestration) / tests YES (Isolated+Integrated)
- DAL: spec YES (DataAccessLayer.md) / code YES (DataAccessLayer/*) / tests YES (Integrated/DalTests)
- NonGraphicalInterface/CLI: spec YES (NonGraphicalInterface.md) / code YES (InterfaceBridge/*, SonOfLeoCli/Program) / tests YES (Integrated/InterfaceBridge/*, SonOfLeoCli/Program)
- Reporting: spec YES (Reporting.md) / code YES (InterfaceBridge/ReportVisualizationAssets/*, ReportWriters/*, Reports/Program) / tests YES (Integrated/Reports/Program, ReportRoutes)
- SystemWide: spec YES (SystemWide.md) / code YES (cross-cutting, Utilities/*, Context/*, Logger/*) / tests YES (covered across all test files)

DB MIGRATIONS (12, chronological):
1. 202606131450-CreateDatabase.sql
2. 202606131454-CreateSchemaLedger.sql
3. 202606131456-CreateAndPopulateAccountType.sql
4. 202606131458-CreateAccountTable.sql
5. 202606201243-CreateFiscalPeriodTable.sql
6. 202606210910-RecreateAccountTable.sql
7. 202606220851-AccountActiveAsDates.sql
8. 202606221206-CreateJeTables.sql
9. 202606231237-RemoveAccountType.sql
10. 202606250813-RenameCommentsComment.sql
11. 202608081415-CreateStageSchemaAndTables.sql
12. 202608110820-ModifyClassificationRule.sql
