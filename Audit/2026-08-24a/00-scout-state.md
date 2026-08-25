# Scout — Derived Repo State

Branch: main. HEAD: e92156331866472ab055e369aad1b9385391b4c4. Last commit: "Repair Tests/ for the Option 4 status redesign" — recent history shows Option 4 status redesign (removing inline status column from StageEntry, deriving status from audit trail), query refactoring in StageEntry fetch, audit 2026-08-22a remediation, REQ-STG-10 filtered fetch suite, classification rule CRUD specs/tests.

SPECS: 10 behavioral spec files in Specs/Behavioral/. Specs/README.md defines the documentation system (REQ ID grammar, linkage rules, commit gate, waiver/unenforceable taxonomy). Specs/Definitions.md defines Money, Price, Quantity, Rate, Entity, Staged Entry, Staged Line, Postable, Date/Instant/Calendar period, and system layers.

REQ COUNTS PER SPEC (active / stricken / withdrawn / waived / unenforceable):
- AccountCrud.md (AC): 77 active, 34 stricken, 35 withdrawn, 20 waived, 6 unenforceable
- ClassificationRuleCrud.md (CR): 54 active, 0 stricken, 0 withdrawn, 9 waived, 0 unenforceable
- DataAccessLayer.md (DAL): 19 active, 13 stricken, 13 withdrawn, 16 waived, 3 unenforceable
- DataIngestion.md (STG): 89 active, 3 stricken, 4 withdrawn, 11 waived, 1 unenforceable
- FiscalPeriodCrud.md (FP): 27 active, 1 stricken, 1 withdrawn, 8 waived, 0 unenforceable
- JournalEntryCrud.md (JE): 83 active, 4 stricken, 4 withdrawn, 16 waived, 1 unenforceable
- Money.md (MON): 27 active, 0 stricken, 0 withdrawn, 4 waived, 1 unenforceable
- NonGraphicalInterface.md (NGUI): 27 active, 2 stricken, 2 withdrawn, 17 waived, 0 unenforceable
- Reporting.md (RPT): 23 active, 1 stricken, 1 withdrawn, 11 waived, 0 unenforceable
- SystemWide.md (SYS): 12 active, 2 stricken, 2 withdrawn, 6 waived, 0 unenforceable
TOTALS: 438 active, 60 stricken, 62 withdrawn, 118 waived, 12 unenforceable.

CODE: 9 Src projects (Context, DataAccessLayer, InterfaceBridge, Logger, Model, ModelOrchestrator, Reports, SonOfLeoCli, Utilities) containing 79 .fs files. F# / .NET. Model contains Ledger domain (Account, FiscalPeriod, JournalEntry*) and DataIngestion domain (StageEntry*, Classification*, FieldMatch*, Classifier). ModelOrchestrator has orchestration for all domains. InterfaceBridge has contracts, converters, routes, and report rendering. Two CLI entry points: SonOfLeoCli/Program.fs and Reports/Program.fs.

TESTS: 3 test projects (Tests.Helpers with 8 support files, Tests.Integrated with 33 test files, Tests.Isolated with 12 test files). Total: 509 [Fact] + 32 [Theory] = 541 test methods. Isolated tests cover: AccountComponent (93 Facts), Money (28), JournalEntryComponent (27), FieldMatchEvaluation (15), StageEntryStatusTransition (13+1T), ClassificationRuleGroupEvaluation (12), Classifier (10), ClassificationRuleComponent (8+4T), FiscalPeriod (1+1T), JournalEntryExternalReference (12), FieldMatchChainEvaluation (3+1T), ClassificationRuleMatching (3+1T). Integrated tests cover all major domains.

DOMAIN COVERAGE MATRIX:
- Account: specs YES (AccountCrud.md), code YES (Model/Ledger/Account*, ModelOrchestrator/Account*, Routes/AccountRoutes), tests YES (Isolated/AccountComponent 93F, Integrated/Account 18F, AccountCreation 3F, AccountDeactivation 6F, AccountBalance 7F, AccountActivity 10F, AccountRoutes 17F+8T)
- FiscalPeriod: specs YES, code YES, tests YES (Isolated 1F+1T, Integrated 12F, FiscalPeriodCreation 1F, FiscalPeriodRoutes 9F)
- JournalEntry: specs YES, code YES, tests YES (Isolated/JEComponent 27F, JEExternalReference 12F; Integrated: JECreation 18F+1T, JEVoiding 5F, JEFetching 14F, JEComment 3F, JEExtRef 1F, JEHeader 1F, JELine 1F, JERoutes 13F+8T)
- Money: specs YES, code YES, tests YES (Isolated/Money 28F)
- DataIngestion/StageEntry: specs YES (DataIngestion.md), code YES (Model/DataIngestion/*, StageEntryOrchestration), tests YES (Integrated: StageEntryIngestion 16F+1T, StageEntryClassification 9F, StageEntryUpdate 9F, StageEntryPosting 11F, StageEntryFetching 20F+1T; Isolated: StageEntryStatusTransition 13F+1T)
- ClassificationRule: specs YES (ClassificationRuleCrud.md), code YES (Model/DataIngestion/Classification/*), tests YES (Integrated: ClassificationRuleCrud 26F+3T; Isolated: ClassificationRuleComponent 8F+4T, FieldMatchEvaluation 15F, FieldMatchChainEvaluation 3F+1T, ClassificationRuleGroupEvaluation 12F, ClassificationRuleMatching 3F+1T, Classifier 10F)
- DAL: specs YES, code YES, tests YES (DalTests 1T)
- NGUI/CLI: specs YES, code YES (SonOfLeoCli, InterfaceBridge/Routes), tests YES (SonOfLeoCli/Program 7F)
- Reporting: specs YES, code YES (Reports, TrialBalance, ReportVisualizationAssets), tests YES (Integrated: TrialBalance 7F, Reports/Program 6F, ReportRoutes 4F)
- SystemWide: specs YES, code distributed (Utilities, Context, Logger), tests implicit (cross-cutting; waived from direct testing)

MIGRATIONS (17, chronological):
202606131450-CreateDatabase, 202606131454-CreateSchemaLedger, 202606131456-CreateAndPopulateAccountType, 202606131458-CreateAccountTable, 202606201243-CreateFiscalPeriodTable, 202606210910-RecreateAccountTable, 202606220851-AccountActiveAsDates, 202606221206-CreateJeTables, 202606231237-RemoveAccountType, 202606250813-RenameCommentsComment, 202608081415-CreateStageSchemaAndTables, 202608110820-ModifyClassificationRule, 202608220920-RebuildClassificationRule, 202608220946-RebuildStageEntryLine, 202608221902-ReAddPrimaryKeyToClassificationRule, 202608230622-StageEntryLineFKeyToClassificationRule, 202608231305-StageEntryDropStatus.

NOTE: REQ-STG-2.24 was withdrawn in the most recent work (Option 4 status redesign — status column removed from staged_entry, now derived from audit trail). The latest migration 202608231305-StageEntryDropStatus reflects this. HEAD commit "Repair Tests/ for the Option 4 status redesign" indicates tests were being updated for this change.
