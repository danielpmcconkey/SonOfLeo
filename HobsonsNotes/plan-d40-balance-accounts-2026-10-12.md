# Plan — D40: Balance Accounts replace pre-ledger balances (spec, Src, tests, migration)

Written 2026-10-12 by Hobson for one Claude Code session that can see this
repository and nothing else, working unattended. Branch
`d40-balance-accounts`, which already carries the spec change and this
plan. Where this plan and `Specs/Behavioral/` disagree, the
spec wins: record it in §5.6.

## 0. Start here

Dan ruled (design record D40, 2026-10-11 and 2026-10-12) that net worth is
always what Positions holds plus the ledger's assets less its liabilities,
with no cut-over logic in code. Cash and debts from before the ledger began
become **Balance Accounts**, a new Positions concept, and the pre-ledger
balance feature is removed entirely. Read the spec change first
(`git diff main -- Specs/`), whole requirements, not just the changed lines:

- `Specs/Behavioral/Positions.md`: the intro and the design note on balance
  accounts; §1 Balance accounts; REQ-POS-9.9; REQ-POS-11.6, 11.9, 11.11
  amended; §15 withdrawn (REQ-POS-15.1–15.8 are in the Withdrawn table);
  new §16 (REQ-POS-16.1–16.10).
- `Specs/Behavioral/Reporting.md`: §8 (REQ-RPT-8.1, 8.2, 8.3, 8.5, 8.7, 8.8
  amended) and §10 (REQ-RPT-10.2, 10.3, 10.4).
- `Specs/Definitions.md`: "Pre-ledger date" removed.

Then:

1. Read §1 and §2.
2. Rebuild `sonofleo_test` from scratch
   (`Tests/Tests.Integrated/setup-throwaway-test-db.sh`) and record the
   baseline in §5.1: build warnings, `Tests.Isolated` and
   `Tests.Integrated` counts, `Checks/run-all.sh`, traceability.
3. **Name first.** Before opening any Src, draft test names for every new
   or amended requirement (§3 Part C lists them) from the spec alone, run
   the name-quality check, and commit them as failing placeholders
   (`Assert.Fail "Not yet implemented"`). Existing tests may be read for
   naming and fixture conventions.
4. Do Parts A–E **in order**. **Commit and push after every numbered item**
   and append that item's §5 entry in the same commit, so that after a
   context compaction §5 and `git log` tell you where you are. Re-read §2
   and the current item after any compaction.
5. Finish with §4.

**Never stop for a question.** Nobody will answer until Dan is back. If an
item can't be done as written, do the closest thing that keeps every rule
in §2, record exactly what and why in §5.6, and carry on.

## 1. Read before you touch anything

- `README.md`, `Specs/README.md`, `Src/README.md`, `Tests/README.md`.
- `Skills/SonOfLeoSrcDeveloper/SKILL.md` — how Src is shaped. Follow it.
- `Skills/ArchReviewer/SKILL.md` — run it over your own diff before §4.
- `Skills/SonOfLeoRequirementsAudit/resolved-findings.md`, especially
  LOUD-FAILURE and REPORT-RECORDS.
- `CompoundedLearnings/articles/architecture/type-taxonomy.md` and
  `validation-layers.md`.
- `HobsonsNotes/plan-audit-refactors-2026-10-11b.md` §2 "Name resolution
  rules" and §5.6: they apply here unchanged (IDs below the bridge, names
  resolved in `Ui.InterfaceBridge`, the stale-cache rule, and the F#
  private-record and open-order quirks found last slice).

## 2. Standing rules

- **You write `Src/`, `Tests/`, `Checks/`, `DbMigration/Scripts/`,
  `Architecture/` (as Part E says) and the alias table in
  `Skills/SonOfLeoSrcDeveloper/SKILL.md`; never `Specs/`.**
- **Migrations:** new scripts in `DbMigration/Scripts/` named
  `YYYYMMDDHHMM-PascalCaseDescription.sql` with a header comment saying why.
  Copy `202610111000-CreateInvestmentActivityCoverageTable.sql` for
  conventions (`{ENV}`, `sonofleo_migrator` owner, the `leobloom_hobson`
  read grant, the `sonofleo_{ENV}` grant, the test-only TRUNCATE grant).
  Apply them only to the throwaway `sonofleo_test`. **Never run anything
  against production.** Hobson reviews; Dan applies by hand.
- **Structural integrity in the schema; every business rule in the
  application** (REQ-DAL-3.6): primary keys, foreign keys, the unique keys
  the spec's uniqueness rules need. No check constraints for kind, sign or
  dates.
- `git pull --rebase --autostash` before every commit. Only Dan merges to
  `main`.
- **Public repository.** No institution names, real people's names, account
  numbers or real amounts anywhere: code, comments, fixtures, commits.
- **Dan is the authority.** If a requirement looks wrong, make the case once
  in §5.6; don't edit a spec, and don't quietly do something else.
- **No REQ annotations in source.** **Loud beats bubblewrap**: no new guard,
  error case or test for a failure that already aborts loudly with nothing
  written, beyond what the spec names. **Code never breaks a tie** unless a
  requirement names it. **Counts only alongside values.** **Every operation
  uses its context's initiation instant** (REQ-SYS-3.4).
- **Every new or changed assertion is seen failing before it passes.**
  Perturb Src or the expected value, watch it fail, restore.
- **Route payloads change in this slice** (net worth and net worth history
  lose their pre-ledger fields and gain cash and debts). That is the spec's
  doing. Every other route's payload stays as it is.
- Commit subjects describe the behaviour, not the edit. Match the log.

## 3. The work

File references were checked on 2026-10-12; **verify before editing**.

### A. Remove pre-ledger balances

1. **Delete the feature.** Prod holds no pre-ledger balances, so nothing is
   migrated.
   - `Src/Business.FinancialServices.Positions/PreLedgerBalance.fs` and
     `Src/Business.CrossDomainOrchestration/PreLedgerBalanceOrchestration.fs`,
     with their `.fsproj` entries; `PreLedgerBalanceId`
     (`PositionsComponent.fs` ~74); the eleven `PositionsPreLedger…` error
     cases and messages (`PositionsError.fs` ~79–89, ~221–231); the two
     auditable actions (`PositionsAuditableAction.fs` ~13, ~18).
   - The guards: REQ-POS-15.7 in `FiscalPeriodCreation.fs` (~13–31, called
     ~44); REQ-POS-15.8 `confirmCarriesNoPreLedgerBalance`
     (`PositionsLedgerLinks.fs` ~23, called from
     `InvestmentAccountOrchestration.fs` ~43 and `RealEstateOrchestration.fs`
     ~114). Fiscal-period creation then reads nothing from Positions.
   - The bridge: the six contracts (`PositionsContracts.fs` ~272–291), the
     two converters (`PositionsFieldConverters.fs` ~476–494) and the
     `PreLedgerBalance` Record, Delete and List routes
     (`PositionsRoutes.fs`).
   - `Checks/check-peer-guard.sh`: drop `PreLedgerBalance` from `shared`
     (Part B3 changes the guard further).
   - `Skills/SonOfLeoSrcDeveloper/SKILL.md` alias table: remove
     `positions.pre_ledger_balance | plb`.
   - Tests: delete `PreLedgerBalances.fs` (13), `PreLedgerBalanceGuards.fs`
     (13) and their `.fsproj` entries; the five pre-ledger route tests in
     `PositionsSliceTwoRoutes.fs` (~326–393) and their helpers (~94–104);
     the REQ-POS-15.2 isolated test (`LotsAndActivity.fs` ~288);
     `Cleanup.cleanUpPreLedgerBalance`; the nine pre-ledger balances in
     `PositionsFixture.fs` (~462–475). `NetWorthPreLedger.fs` (14) is
     handled in Part C. Fix the comment in `JournalEntryVoiding.fs` ~197.
   - The suite must build and pass after this item, except tests that
     depend on pre-ledger figures in net worth: mark those you cannot yet
     fix as placeholders pointing at item C2, and list them in §5.2.
2. **Migration: drop the table.**
   `YYYYMMDDHHMM-DropPreLedgerBalanceTable.sql` drops
   `positions.pre_ledger_balance` (the header says why: D40, no rows in any
   environment's use, the feature removed). Remove it from the test
   fixture's TRUNCATE list (`TestDataStage.fs` ~195). Leave the older
   migrations that created it and granted on it untouched.

### B. Balance Accounts (Positions §16, REQ-POS-9.9)

1. **Migration: create the tables.**
   `YYYYMMDDHHMM-CreateBalanceAccountTables.sql`, timestamped after A2's:
   - `positions.balance_account` (`unique_id`, `balance_account_name`
     unique, `kind`, `institution`, `created_at`, `modified_at`);
   - `positions.balance_account_owner` (`balance_account_id`, `person_id`;
     PK on both; FKs) — copy `positions.property_owner`;
   - `positions.balance_account_balance` (`unique_id`,
     `balance_account_id`, `balance_date`, `amount`, `created_at`,
     `modified_at`; UNIQUE (`balance_account_id`, `balance_date`); FK on
     delete restrict);
   - `positions.property_mortgage_balance_account` (`property_id`,
     `balance_account_id`; PK on both; UNIQUE (`balance_account_id`); FKs)
     — copy `positions.property_mortgage_account`.
   Grants as the convention file; test-only TRUNCATE on all four; add all
   four to the fixture's TRUNCATE list in dependency order.
2. **Types and entity modules** in `Business.FinancialServices.Positions`:
   - In `PositionsComponent`: `BalanceAccountId`, `BalanceAccountName`
     (REQ-POS-16.1), `BalanceAccountKind` (`Cash | Debt` with `toString`
     and `fromString`), `BalanceAccountBalanceId`. Reuse `Institution` if
     its rule matches REQ-POS-16.3 (it does for Investment Accounts); say
     so in §5.2.
   - `BalanceAccount.fs`, shaped like `Property.fs`: owners as a child set
     via the same `persistChildren`/`deleteChildren` pattern; persist,
     private `reconstitute`, private `query`, fetches by ID, by name, and
     all; `update` (name, institution, owners; **no kind**, REQ-POS-16.2);
     `delete`; a private cache with a typed `idToName` accessor
     (`BalanceAccountId → BalanceAccountName`), per `Src/README.md`.
   - `BalanceAccountBalance.fs`, shaped like `Valuation.fs`: persist,
     update (replace the amount), delete, fetches by account and date, by
     account between two dates, and the latest on or before a date for a
     set of accounts (one query, for net worth); `balanceOn` per
     REQ-POS-16.6.
   - Add a `PositionsAuditableAction` case for each new write.
3. **Peer rule.** Positions now has three groups. In
   `Checks/check-peer-guard.sh` add a `balance=` list (`BalanceAccount
   BalanceAccountBalance`) and enforce: investments reference neither real
   estate nor balance modules; balance modules reference neither
   investments nor real estate; real-estate modules may reference balance
   modules (REQ-POS-9.9) but not investments. Show it failing on a planted
   reference from a balance module to `Property`, and from an investment
   module to `BalanceAccount`.
4. **Orchestration and routes.** `BalanceAccountOrchestration.fs` in
   `Business.CrossDomainOrchestration`:
   - create, update and list (REQ-POS-16.7); owners validated like
     `RealEstateOrchestration.ownerSetOf`;
   - record Balances (REQ-POS-16.8), atomic, the repeat check first, the
     future-date check (REQ-POS-16.5), replace-or-create per balance,
     returning each with `replacedExisting` (the old
     `RecordedPreLedgerBalance` shape is the model);
   - delete a Balance and list Balances (REQ-POS-16.9), the reversed-range
     error per the existing precedent;
   - delete a Balance Account (REQ-POS-16.10): refused while it has any
     Balance or is a Property's mortgage, naming the Property.
   New `PositionsError` cases for each typed error the spec names; follow
   the naming of their neighbours. Routes: `BalanceAccount Create`,
   `Update`, `Delete`, `List`; `BalanceAccountBalance Record`, `Delete`,
   `List`. Contracts address by name; return names resolved at the bridge.
5. **Property mortgages** (REQ-POS-9.9, 11.6, 11.11). `Property` gets the
   mortgage Balance Account set (child table, persisted and replaced like
   the ledger mortgage accounts); `RealEstateOrchestration` validates it
   like `mortgageAccountSetOf`/`confirmMortgageLink` (exists, kind `Debt`,
   no repeat, at most one Property). Create and update take it as a
   complete set by name; the list returns its names; delete removes the
   links. The bridge resolves names.

### C. Net worth (Reporting §8, §10)

1. **Dates and ledger balances** (REQ-RPT-8.1, 8.7).
   - Replace `NetWorth.classifyDate` and `NetWorthDate` with one check: the
     date is within a fiscal period, or earlier than the earliest fiscal
     period's start, or there is no fiscal period at all; otherwise
     `PositionsNetWorthDateOutsideFiscalPeriods` as today. Within a period,
     ledger balances are computed as now; otherwise there are no ledger
     rows. Delete `NetWorthDate` from `PositionsComponent` and every
     `netWorthDate`/`isPreLedger`/`balanceDate` field: `NetWorth`,
     `NetWorthPoint`, `ReportsContracts`, `ReportConverters`,
     `NetWorthWriter` (the pre-ledger branches and note),
     `NetWorthHistoryWriter` (the pre-ledger row class, CSS and "Source"
     column), `ReportRoutes` descriptions.
   - The isolated test asserting `classifyDate []` errors
     (`LotsAndActivity.fs` ~297) now contradicts REQ-RPT-8.1: replace it
     with one asserting any date is accepted when no fiscal period exists.
   - **11b #045:** with `balanceDate` gone, `LedgerAccountBalance` is an
     account and a balance. Replace it with `AccountBalance` if that fits
     every use (asset and liability rows, a Property's ledger mortgages),
     otherwise a record of `accountId` and balance; code and name resolve
     at the bridge (as R4 did for the trial balance).
2. **Balance Accounts in net worth** (REQ-RPT-8.2, 8.3, 8.5, 8.8).
   - Fetch every Balance Account and its balance on the date (one query
     for the latest Balance on or before the date across all accounts).
   - Cash adds; Debt subtracts. A Debt Balance Account that is a mortgage
     of a Property owned on the date counts in that Property's mortgages
     and equity (REQ-RPT-8.3), not in debts.
   - New totals `totalCash` and `totalDebts`; `totalOwnedPropertyMortgages`
     includes mortgage Balance Accounts. The REQ-RPT-8.5 identity must
     hold; assert it.
   - Listing: a Balance Account section (name, kind, owners' names,
     balance, the date of the Balance it came from), Balance Accounts with
     a zero balance left out but counted; a Property's mortgage Balance
     Accounts listed under it, also only when not zero. Order by name.
   - `NetWorthComponent` gains `Cash` and `Debts`; REQ-RPT-8.8's seven
     components in its stated order; a zero Balance is a balance.
   - Contracts, converters and `NetWorthWriter` render the new section and
     totals; names resolve through caches (Balance Account `idToName`,
     Person).
3. **Net worth history** (REQ-RPT-10.2–10.4): drop the pre-ledger flag;
   add the cash and debts totals; the month-end date check per the new
   REQ-RPT-10.3; the rendered table loses the pre-ledger marking and gains
   the two columns.
4. **Tests.** Fill every placeholder from the names step. At least:
   - REQ-POS-16.1–16.10 and 9.9, each rule's rejection where the spec
     names a typed error, and the happy paths, at orchestration level, plus
     one route test per new route.
   - Net worth on a date before the first fiscal period: Balance Accounts
     and positions only, no ledger rows, ledger components absent, cash and
     debts totals, a Property's equity from a mortgage Balance Account, and
     investable wealth removing that equity.
   - Net worth within a fiscal period with Balance Accounts carrying zero:
     counted, not listed, cash and debts present (not absent).
   - A Balance Account with no Balance on or before the date: not counted,
     not listed, and its component absent if it is the only one.
   - Net worth with no fiscal period at all: accepted.
   - The REQ-RPT-8.5 identity with all seven totals non-zero.
   - Net worth history over a range spanning the first fiscal period's
     start: the points before it have no ledger figures and the points
     after carry both.
   - Rework `NetWorthPreLedger.fs` into these: keep each test whose
     behaviour survives under Balance Accounts (renamed and re-cited),
     delete the rest, and say which in §5.2. Rework `NetWorthHistory.fs`
     ~47 and ~93 and the four touched tests in `PositionsReportRoutes.fs`.
   - Add the Balance Account and Balance to the REQ-SYS-3.2 create theory
     and the Balance replace to the REQ-SYS-3.3 update theory.
   - Fixture: replace the nine pre-ledger balances with Balance Accounts
     and Balances that give net worth the same coverage (cash, a card, a
     loan, a Property's mortgage, zero closing balances on the first
     fiscal period's start). Invented names and amounts only.

### D. Checks and the model

1. `Checks/run-all.sh` green, including the peer-guard change (B3).
2. **Model** (`Architecture/SonOfLeo.archimate`, `Skills/ArchiMate/SKILL.md`):
   - remove "Record pre-ledger balances", "PreLedgerBalance" and
     "PreLedgerBalanceOrchestration" and their relationships;
   - add components for the new modules and functions "Maintain balance
     accounts" and "Record balance account balances" under Positions
     functions, realized by the new modules;
   - correct the documentation of "Write net worth history report",
     "Create and persist a new fiscal period", "Maintain investment
     accounts", "Maintain properties", "Compute net worth as of a date" and
     "Compute net worth history" so none mentions pre-ledger balances, and
     the net worth ones describe Balance Accounts;
   - in the "Domains Build Upward" principle's documentation, place balance
     accounts inside Positions: investments and real estate unordered
     peers, balance accounts below real estate (a Property may name one as
     its mortgage), and investments and balance accounts unrelated. This
     settles 11a #056.
   - `validate.py` VALID, `model_drift.py` NO DRIFT.

### Out of scope

- Importing any data (Balance Accounts for 2020–2025, the property
  history). That is Hobson's, after merge.
- Spec edits. Rows owned by Hobson in the disposition records; Hobson
  closes the D40-deferred rows after review.

## 4. When you finish

- Build: 0 warnings, 0 errors.
- `Tests.Isolated` and `Tests.Integrated` pass, the latter against a
  `sonofleo_test` rebuilt from scratch with both new migrations. If you
  can't get the database, say so; don't skip or fake.
- `bash Checks/run-all.sh` green.
  `Skills/SonOfLeoRequirementsAudit/traceability-audit.sh .` Invariants 1
  and 2 clean: every withdrawn REQ-POS-15.x has no test citing it, and
  every new requirement has one.
- `validate.py` VALID, `model_drift.py` NO DRIFT.
- No `Not yet implemented` placeholder left.
- ArchReviewer over `git diff main...HEAD -- Src Checks DbMigration`; act on
  what it finds or say why not in §5.5.
- Push, and complete §5.

## 5. Report

Written by the session that did this plan. Fill it in as you go (§0 step 4).

### 5.1 Outcome
(baseline first; final counts and check outputs at the end)

**Baseline** (d693680, `sonofleo_test` rebuilt from scratch with
`setup-throwaway-test-db.sh`; .NET SDK 10.0.113 from the Ubuntu archive,
since the container had none):

- Build: 0 warnings, 0 errors.
- `Tests.Isolated` 530 passed; `Tests.Integrated` 1778 passed.
- `Checks/run-all.sh`: 12 passed, 0 failed, 1 skipped (traceability, not
  on `main`).
- Traceability audit: Invariant 1 lists every REQ-POS-15.1–15.8 citation as
  withdrawn (PreLedgerBalances.fs, PreLedgerBalanceGuards.fs, the five route
  tests in PositionsSliceTwoRoutes.fs, LotsAndActivity.fs ~288); Invariant 2
  lists REQ-POS-9.9 and REQ-POS-16.1–16.10 as untested (11 of 815).

### 5.2 Per item

**Names.** Drafted from the spec diff alone, before any Src was opened, and
checked against the hollow-names table: each says what would have to break
for it to go red. Committed as `Assert.Fail "Not yet implemented"`
placeholders in five new files: `Tests.Isolated/Model/Positions/BalanceAccounts.fs`
(8), `CrossDomainOrchestration/BalanceAccountMaintenance.fs` (27),
`PropertyMortgageBalanceAccounts.fs` (8), `NetWorthBalanceAccounts.fs` (14)
and `InterfaceBridge/BalanceAccountRoutes.fs` (12). The REQ-SYS-3.2 and 3.3
placeholders are separate facts for now; C4 folds them into the existing
theories as the plan says. Placeholders whose natural home is an existing
file (net worth route and history tests) sit in the new files and move in C4
if that reads better.

**A1. Pre-ledger balances deleted.** Gone: `PreLedgerBalance.fs`,
`PreLedgerBalanceOrchestration.fs` and their `.fsproj` entries,
`PreLedgerBalanceId`, the eleven `PositionsPreLedger…` cases and messages,
the two auditable actions, the fiscal-period guard (fiscal-period creation
no longer opens Positions), `confirmCarriesNoPreLedgerBalance` and its two
callers, the six contracts, two converters and three routes, `PreLedgerBalance`
from the peer guard's `shared` list, and the `plb` alias. Tests:
`PreLedgerBalances.fs` (13) and `PreLedgerBalanceGuards.fs` (13) deleted, the
five route tests and their helpers (and the now-unused `ledger` and
`codeNotFound` helpers and `LedgerError` open), the isolated REQ-POS-15.2
test, `Cleanup.cleanUpPreLedgerBalance`, the fixture's nine balances, its
doc block, and the two fixture fields only they used
(`operatingCash1280Id`, `loanPayable2230Id`). The JournalEntryVoiding comment
now says only that month -6 has no fiscal period yet.
To keep the build whole, `NetWorth.balancesOn` returns no balance for a date
before the ledger (what C1 makes permanent); `NetWorthDate` itself stays
until C1. Twelve tests depend on pre-ledger figures and are placeholders
saying "reworked in item C2": eight in `NetWorthPreLedger.fs`
(REQ-RPT-8.5 balance dates; 8.7 parent and child; 8.8 only liability is a
mortgage; 8.2/8.7 pre-ledger sum; 8.7 latest of several; 8.8 only
investments absent; 8.7 zero listed; 8.3/8.7 equity), `NetWorthHistory.fs`
REQ-RPT-10.2 (last pre-ledger month-end to first fiscal month-end), and three
in `PositionsReportRoutes.fs` (8.6/8.5 rendered pre-ledger; 10.4 rendered
history; 8.6/8.5 data-only pre-ledger). Build 0 warnings; Isolated 529 pass
and 8 placeholders; Integrated 1735 pass, 61 named placeholders and these 12.

**A2. The table is dropped.** `202610241000-DropPreLedgerBalanceTable.sql`
(timestamped on the day the work was done, after every existing script)
drops `positions.pre_ledger_balance`; its grants go with it, and the two
older scripts that created it and granted on it are untouched. The
fixture's TRUNCATE list no longer names it. `sonofleo_test` rebuilt from
scratch: the table is gone and the suite is as after A1 (1735 pass, only the
73 placeholders fail).

**B1. The tables exist.** `202610241010-CreateBalanceAccountTables.sql`
creates `positions.balance_account` (name unique), `balance_account_owner`
(PK on both, FKs, copied from `property_owner`), `balance_account_balance`
(UNIQUE account and date, FK on delete restrict, `amount numeric(12,2)` as
`valuation_value`) and `property_mortgage_balance_account` (PK on both,
UNIQUE balance account, FKs, copied from `property_mortgage_account`). No
check constraints. Grants as the convention file, test-only TRUNCATE on all
four, and all four in the fixture's TRUNCATE list, each parent before its
child as the list already orders them. `sonofleo_test` rebuilt from scratch
with both new scripts: the four tables exist and the suite is unchanged.

**B2. Types and entity modules.** `PositionsComponent` gains
`BalanceAccountId`, `BalanceAccountBalanceId`, `BalanceAccountName` (trimmed,
at most 100, REQ-POS-16.1) and `BalanceAccountKind` (`Cash | Debt`,
`toString`/`fromString`). **`Institution` is reused**: its rule (trimmed, not
empty, at most 100) is REQ-POS-16.3's word for word, and its error cases
(`PositionsInstitutionIsEmpty`/`TooLong`) name no Investment Account.
`BalanceAccount.fs` follows `Property.fs`: owners as a child set (one table,
so a private `persistOwners`/`deleteOwners` pair instead of the generic
`persistChildren`), persist, private `reconstitute`/`query`, `fetchById`,
`fetchByName`, `fetchAll`, `update` (name, institution, owners; no kind),
`delete` (owners first) and a private cache behind a typed `idToName`.
`BalanceAccountBalance.fs` follows `Valuation.fs`: persist, `update` (the
amount, through a one-field `FieldUpdates` record so it keeps the canon
shape and no-op guard), `delete`, `fetchById`, `fetchByAccountAndDate`,
`fetchBetween` (one account, both ends included, date order),
`existsForAccount` (for the delete guard) and `fetchLatestOnOrBefore`, one
query for every account's latest Balance on or before a date (see §5.6), and
`balanceOn` per REQ-POS-16.6, returning the Balance itself so net worth has
its date. Both compile before `Property.fs`. New error cases:
`PositionsBalanceAccountBalanceIdDoesntExist`,
`PositionsBalanceAccountBalanceUpdateNoOp`,
`PositionsBalanceAccountHasNoOwners`, `PositionsBalanceAccountIdDoesntExist`,
`PositionsBalanceAccountNameIsEmpty`, `PositionsBalanceAccountNameTooLong`,
`PositionsBalanceAccountUpdateNoOp`, `PositionsInvalidBalanceAccountKind`.
Auditable actions: `PositionsCreateBalanceAccount`,
`PositionsUpdateBalanceAccount`, `PositionsDeleteBalanceAccount`,
`PositionsRecordBalanceAccountBalances`,
`PositionsDeleteBalanceAccountBalance`. Aliases `bacc`, `baow`, `babal` (and
`pmba` in B5) added to the skill's table. Build 0 warnings. The peer guard
fails at this commit (the two new modules are in no list) and B3, the next
commit, lists them.

### 5.3 Fail-then-pass evidence

### 5.4 The migrations Dan must apply
(file names, what each does, in order)

1. `202610241000-DropPreLedgerBalanceTable.sql`: drops
   `positions.pre_ledger_balance` (empty in every environment).
2. `202610241010-CreateBalanceAccountTables.sql`: creates
   `positions.balance_account`, `balance_account_owner`,
   `balance_account_balance` and `property_mortgage_balance_account`, with
   grants.

### 5.5 ArchReviewer

### 5.6 Disagreements, deviations, things for Dan
