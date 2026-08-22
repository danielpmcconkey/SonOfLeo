# Scout — Derived Repo State

Branch: main. HEAD: f15c79aff177506276852d35616eb675242b7edc. Last 15 commits focus on Batch B classification rule CRUD tests, classification rule sort/coverage, stale waiver fix, mutation evidence, bug fixes in fetchRulesFiltered parameterization, REQ-CR-1.21 waiver, REQ-CR-4.4/4.8 split, REQ-CR-2.8/2.9 empty chain/groups evaluation, and wakeup notes (2026-08-21b).

SPECS: 10 behavioral spec files in Specs/Behavioral/. Specs/README.md defines the documentation system, REQ ID grammar, linkage rules, and requirement lifecycle (tested/waived/unenforceable). Specs/Definitions.md defines Money, Price, Quantity, Rate, Entity, Instant, Date, Calendar period, Staged entry, Staged line, Postable, Interface, Actors, and the three architectural layers.

REQ COUNTS PER SPEC (active/waived/unenforceable/stricken/withdrawn):
- AccountCrud.md (AC): 77 active, 20 waived, 6 unenforceable, 34 stricken, 35 withdrawn. Tested=51.
- ClassificationRuleCrud.md (CR): 54 active, 9 waived, 0 unenforceable, 0 stricken, 0 withdrawn. Tested=45.
- DataAccessLayer.md (DAL): 19 active, 16 waived, 3 unenforceable, 13 stricken, 13 withdrawn. Tested=0.
- DataIngestion.md (STG): 81 active, 11 waived, 1 unenforceable, 3 stricken, 3 withdrawn. Tested=69.
- FiscalPeriodCrud.md (FP): 26 active, 8 waived, 0 unenforceable, 1 stricken, 1 withdrawn. Tested=18.
- JournalEntryCrud.md (JE): 83 active, 16 waived, 1 unenforceable, 4 stricken, 4 withdrawn. Tested=66.
- Money.md (MON): 27 active, 4 waived, 1 unenforceable, 0 stricken, 0 withdrawn. Tested=22.
- NonGraphicalInterface.md (NGUI): 27 active, 17 waived, 0 unenforceable, 2 stricken, 2 withdrawn. Tested=10.
- Reporting.md (RPT): 23 active, 11 waived, 0 unenforceable, 1 stricken, 1 withdrawn. Tested=12.
- SystemWide.md (SYS): 12 active, 6 waived, 0 unenforceable, 2 stricken, 2 withdrawn. Tested=6.
TOTALS: 429 active (118 waived, 12 unenforceable, 299 tested), 60 stricken, 61 withdrawn.

NOTE: DAL has 0 tested requirements (all 19 active are waived or unenforceable). This is by design -- DAL requirements are structural/architectural facts verified by code review, schema inspection, or the integration test harness itself, not by named tests.

SRC PROJECTS (9 fsproj): Utilities, Context, DataAccessLayer, Logger, Model, ModelOrchestrator, InterfaceBridge, SonOfLeoCli, Reports. 80 .fs files total.

TESTS: 3 test projects (Tests.Helpers shared library, Tests.Isolated pure-logic tests, Tests.Integrated DB-backed tests). 484 [<Fact>] attributes + 30 [<Theory>] attributes = 514 total test methods across 48 test files. Tests.Isolated covers: Money, AccountComponent, FiscalPeriod, JournalEntryComponent, JournalEntryExternalReference, ClassificationRuleComponent, ClassificationRuleGroupEvaluation, ClassificationRuleMatching, Classifier, FieldMatchEvaluation, FieldMatchChainEvaluation, StageEntryStatusTransition. Tests.Integrated covers: DAL, AccountRoutes, FiscalPeriodRoutes, JournalEntryRoutes, IngestionRoutes, ReportRoutes, Account model, FiscalPeriod model, JE Comment/ExtRef/Header model, AccountActivity/Balance/Creation/Deactivation orchestrator, ClassificationRuleCrud, FiscalPeriodCreation, JE Comment/Creation/ExtRef/Fetching/Header/Line/Orchestration/Voiding orchestrator, StageEntryClassification/Ingestion/Posting/Update orchestrator, TrialBalance, Reports Program, SonOfLeoCli Program.

DB MIGRATIONS (12, chronological): 202606131450-CreateDatabase, 202606131454-CreateSchemaLedger, 202606131456-CreateAndPopulateAccountType, 202606131458-CreateAccountTable, 202606201243-CreateFiscalPeriodTable, 202606210910-RecreateAccountTable, 202606220851-AccountActiveAsDates, 202606221206-CreateJeTables, 202606231237-RemoveAccountType, 202606250813-RenameCommentsComment, 202608081415-CreateStageSchemaAndTables, 202608110820-ModifyClassificationRule.

DOMAIN COVERAGE MATRIX:
- Money: spec YES, code YES (Model/Money.fs), tests YES (isolated + integrated).
- Account: spec YES, code YES (Model/Ledger/Account*.fs, orchestrator Account*.fs), tests YES (isolated + integrated).
- FiscalPeriod: spec YES, code YES (Model/Ledger/FiscalPeriod*.fs, orchestrator), tests YES (isolated + integrated).
- JournalEntry: spec YES, code YES (Model/Ledger/JournalEntry*.fs, orchestrator JE*.fs), tests YES (isolated + integrated).
- DataAccessLayer: spec YES, code YES (DataAccessLayer/*.fs), tests YES (DalTests integrated).
- NonGraphicalInterface: spec YES, code YES (InterfaceBridge/*, SonOfLeoCli, Reports), tests YES (route tests + CLI tests).
- Reporting: spec YES, code YES (ModelOrchestrator/TrialBalance.fs, InterfaceBridge/ReportWriters/*, Reports/Program.fs), tests YES (TrialBalance + ReportRoutes + Reports/Program).
- DataIngestion (STG): spec YES, code YES (Model/DataIngestion/*, ModelOrchestrator/StageEntryOrchestration.fs + ClassificationOrchestration.fs), tests YES (StageEntry* + Classification* integrated + isolated).
- ClassificationRuleCrud (CR): spec YES, code YES (Model/DataIngestion/Classification/*.fs, ModelOrchestrator/ClassificationOrchestration.fs), tests YES (ClassificationRuleCrud integrated + Classification* isolated).
- SystemWide: spec YES, no dedicated code (cross-cutting, enforced per-entity), no dedicated tests (waived; enforced per-entity).

All 10 spec domains have corresponding code and tests (except DAL and SYS which are cross-cutting and covered implicitly).
