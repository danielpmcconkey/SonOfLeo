---
# Scout — Derived Repo State

Branch: main. HEAD: b46d244cb6f003151aefa414a19b2c508ead8ea6. Last 15 commits span wakeup/ledger updates (P4-P7 complete, P8 unblocked), naming consistency changes, clarification commits, reclassification ledger updates, P5 audit machine rebuild, traceability commit guard prep, REQ-JE-3.9.3 sort tests, and Invariant 2 backlog classification.

SPECS: 7 behavioral spec files in Specs/Behavioral/. Specs/README.md defines the documentation system (REQ ID grammar, linkage rules, commit gate, waiver/unenforceable taxonomy). Specs/Definitions.md defines domain terms (Money, Price, Quantity, Rate, Entity, Instant, Date, Calendar period, Interface, Actors, layers).

REQ COUNTS PER SPEC (active / waived / unenforceable / withdrawn):
- AccountCrud.md (AC): 77 active, 20 waived, 7 unenforceable (6 unique; REQ-AC-3.3.1 duplicated in table), 35 withdrawn
- DataAccessLayer.md (DAL): 19 active, 16 waived, 3 unenforceable, 13 withdrawn
- FiscalPeriodCrud.md (FP): 26 active, 8 waived, 0 unenforceable, 1 withdrawn
- JournalEntryCrud.md (JE): 82 active, 16 waived, 1 unenforceable, 4 withdrawn
- Money.md (MON): 27 active, 4 waived, 1 unenforceable, 0 withdrawn
- NonGraphicalInterface.md (NGUI): 19 active, 12 waived, 0 unenforceable, 2 withdrawn
- SystemWide.md (SYS): 13 active, 6 waived, 0 unenforceable, 2 withdrawn
TOTALS: 263 active (82 waived, 12 unenforceable, 169 should-be-tested), 57 withdrawn.

SRC: 8 fsproj projects under Src/: Utilities, Context, DataAccessLayer, Logger, Model, ModelOrchestrator, InterfaceBridge, SonOfLeoCli. 50 .fs files total. Compile order per fsproj is defined in each .fsproj (see listing).

TESTS: 3 test projects — Tests.Helpers (shared test infra, 6 .fs files), Tests.Isolated (unit tests, 6 .fs files), Tests.Integrated (integration tests, 22 .fs files including SonOfLeoCli executor). 323 [Fact] + 18 [Theory] = 341 test methods. Isolated tests cover Model/Ledger (AccountComponent, FiscalPeriod, JournalEntryComment, JournalEntryComponent, JournalEntryExternalReference) and Model/Money. Integrated tests cover DAL, InterfaceBridge (Account/FiscalPeriod/JournalEntry routes), Model/Ledger (Account, FiscalPeriod, JE Comment/ExtRef/Header), and ModelOrchestrator (AccountActivity/Balance/Creation/Deactivation, FiscalPeriodCreation, JE Comment/Creation/ExtRef/Fetching/Header/Line/Orchestration/Voiding).

DOMAIN COVERAGE MATRIX:
- Account (AC): specs YES, code YES (Model + Orchestrator + Routes), tests YES (isolated + integrated)
- FiscalPeriod (FP): specs YES, code YES (Model + Orchestrator + Routes), tests YES (isolated + integrated)
- JournalEntry (JE): specs YES, code YES (Model: Header/Line/ExtRef/Comment + Orchestrator + Routes), tests YES (isolated + integrated)
- Money (MON): specs YES, code YES (Model/Money.fs), tests YES (isolated)
- DAL: specs YES, code YES (6 .fs files), tests YES (integrated DalTests)
- NGUI: specs YES, code YES (InterfaceBridge: Routes, Contracts, Converters, CommandRoute, Json), tests YES (integrated route tests)
- SystemWide (SYS): specs YES (cross-cutting policies), code distributed across all layers, tests distributed

DB MIGRATIONS (10, chronological):
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
===END===