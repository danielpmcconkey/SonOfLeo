# Plan — Positions slice 1 (Src)

Written 2026-10-04 by Hobson for a Claude Code session that can see this
repository and nothing else. Everything you need is either in this document
or in the repo. Where this plan and `Specs/Behavioral/` disagree, the spec
wins — tell Dan.

## 0. Start here — one session does Src and tests, in this order

You are doing both halves of the slice loop: Src from this plan, Tests from
`HobsonsNotes/brief-test-agent-positions-2026-10-04.md` (the brief).
README constraint 1 says tests are named from the spec by someone who hasn't
seen the implementation. You can't un-see code, so the order is the
safeguard:

1. **Name first, from the spec alone.** Read §2's reading list *except* Src,
   then the brief's §0 and Part A. Draft test names for every Part A item,
   run the name-quality check, and commit them as failing placeholders
   (`Assert.Fail "Not yet implemented"`). **Open nothing under `Src/` until
   that commit is pushed.** Existing tests may be read for naming and
   fixture conventions.
2. **Implement** §4 in order: Part A, then B, C, D, E. Commit as you go.
3. **Test.** Brief Part A: now read Src; if a committed name is aimed wrong,
   say so in your report and renegotiate it out loud (never quietly soften
   it). Write each test and watch every assertion fail before it passes
   (constraint 3). Then brief Part B.
4. **Finish** with this plan's §7 and the brief's final report, both into
   §8 of this file.

A test that fails in step 3 is either a Src bug (fix it) or a spec bug
(record it in the report with the REQ ID; don't edit the spec).

## 1. What this is for

SonOfLeo is Dan's personal-finance system: a cash-basis double-entry ledger
in F# on .NET 10 and PostgreSQL. Until now it has known only the ledger:
accounts, journal entries, ingestion, cash flow, classification. This slice
adds **Positions** — what the household holds, who owns it, and what it was
worth on a date — and two reports built on it: **net worth** and
**investment wealth history**.

Positions is a new business domain with two peer sub-domains:

- **Investments** — investment accounts at institutions, the securities they
  hold, and weekly whole-account snapshots of what the institution reported.
- **Real estate** — properties and their valuations.

Two foundations come with it: **Person** (in `Business.General`), and the
exact-decimal **Quantity** and **Price** primitives (beside `Money` in
`Business.FinancialServices`).

The spec is new; there is no prior code for any of it. Purchases, sales,
tax lots and realised gains are a later slice — the Positions spec's design
note "not yet modelled" says what that means for you (basis method exists on
a Holding now; nothing else of lots does).

## 2. Before you touch anything, read

- `README.md` — the slice loop and the three load-bearing constraints.
- `Specs/README.md` — requirement grammar, the traceability gate, the rule
  that specs never name source files and source never carries REQ
  annotations.
- **The spec for this slice:** `Specs/Behavioral/Positions.md` (REQ-POS),
  `Person.md` (REQ-PER), `QuantityAndPrice.md` (REQ-QP),
  `Reporting.md` §8 and §9 (REQ-RPT-8, REQ-RPT-9), and in
  `Specs/Definitions.md` the entries Person, User, Quantity and Price.
  `git show a3cb261 -- Specs/` shows exactly what was added.
- `Src/README.md`, and `Tests/README.md` for what tests will hold you to.
- `Skills/SonOfLeoSrcDeveloper/SKILL.md` — how Src is shaped. Follow it.
  (Some of it still says `Model/` and `ModelOrchestrator`; read those as the
  Business-tier entity modules and `Business.CrossDomainOrchestration`.)
- `Skills/ArchReviewer/SKILL.md` — run it over your own diffs before you
  report.
- `Skills/ArchiMate/SKILL.md` — you will extend the model (§4 Part E).
- In `Architecture/SonOfLeo.archimate`, the principles **"Domains Build
  Upward"** and **"Dependencies Build From the Base Up"**. They fix where
  every new project, file and reference goes. `git show 9a00e06` is the
  model change that placed Positions.
- `CompoundedLearnings/` — settled judgment calls, especially
  `orchestration-layer.md`, `type-placement-by-compile-tier.md`,
  `type-taxonomy.md`, `validation-layers.md`, `dal-errors-are-backstops.md`,
  `field-update-pattern.md`, `money-type-enforcement.md`,
  `numeric-type-taxonomy.md`, `temporal-arithmetic.md`,
  `du-case-collision-across-opens.md`.
- Precedent for the closest existing shapes: `Src/Business.FinancialServices.CashFlow/MasterAgreement.fs`
  (entity CRUD), `Business.CrossDomainOrchestration/AgreementOrchestration.fs`
  (create/update orchestration), `TrialBalance.fs` and
  `AccountBalance.fs` (as-of balances), `Ui.InterfaceBridge/Routes/ReportRoutes.fs`
  and `ReportWriters/` (report routes and HTML).

## 3. Standing rules

- **You write `Src/`, `DbMigration/Scripts/`, `Architecture/` (Part E only)
  and `Tests/`; never `Specs/`.**
- **Branch `positions`.** `git pull --rebase --autostash` before every
  commit. Only Dan merges to `main`.
- **Public repository.** No institution names, tickers of real holdings,
  account numbers, people's names, or real amounts in code, comments,
  migrations, fixtures or commit messages. Fixture people are fictional
  ("Alex Example"); fixture securities are fictional ("Example Total Market
  Index Fund", ticker "EXTMX").
- **Dan is the authority.** If a requirement looks wrong, make the case once
  in your report; don't edit a spec, and don't quietly do something else.
- **No REQ annotations in source.** A comment may name a rule to explain
  *why* code is shaped a certain way; it never tags a site.
- **Structural integrity in the schema (primary keys, foreign keys, unique
  keys); every business rule in the application layer** (REQ-DAL-3.6). A
  unique key the application also checks is a backstop, not the rule
  (`dal-errors-are-backstops.md`).
- **Infallible create, orchestrator validates.** `create` is total;
  orchestrators validate. `reconstitute` validates everything that needs no
  database read.
- **Loud beats bubblewrap.** A failure that already aborts with an error
  needs no new guard and no new error case. Only silent failures earn code.
- **Build only what the spec asks for.** There is no delete for a Person,
  Dimension Value, Security, Investment Account, Holding or Property, and no
  route that the spec has no "means to" for. Don't add one.
- **Never create a record to absorb a failed lookup** (REQ-POS-6.5 says it
  outright for Holdings). Fail loudly.
- **Code never breaks a tie** unless a requirement names the tie-break.
- **Tuple lists of domain primitives in construct functions are
  intentional.** Don't introduce "create input" records.
- **Fully qualify common DU case names** (`TaxTreatment.Taxable`,
  `Provenance.Imported`) — `Taxable`, `Rental`, `Reported` will collide
  sooner or later.
- **All currency arithmetic and comparison goes through `Money`**
  (REQ-MON-2.1). Quantity and Price get the same discipline: their own
  modules, private representation, no raw `decimal` arithmetic outside them.
  The one Quantity×Price product (REQ-QP-3.5) returns a `decimal`, and the
  0.05 tolerance check (REQ-POS-6.8) is the only place it is compared to a
  market value.
- **Every operation uses its context's initiation instant** for every
  timestamp and every derived "current date" (REQ-SYS-3.4) — the "cannot be
  later than the current date" rules in REQ-PER-1.5, REQ-POS-6.3 and
  REQ-POS-10.3 included. `Checks/check-clock.sh` enforces it.
- **Migrations:** new scripts in `DbMigration/Scripts/` named
  `YYYYMMDDHHMM-PascalCaseDescription.sql`, with a header comment saying why,
  following the grant pattern of `202609071130-CreateSchemaClassification.sql`
  and `…1135-CreateClassificationTables.sql` (`{ENV}` placeholder,
  `sonofleo_migrator` owner, read-only grant to `leobloom_hobson`). Never run
  anything against production; Dan applies migrations by hand.
- Commit subjects describe the behaviour, not the edit ("An account snapshot
  re-recorded for the same date replaces the old one"). Match the log.
- **Before every hand-off:** green build, `Tests.Isolated` green, and
  `bash Checks/run-all.sh` green, with the output in your report.
  `Tests.Integrated` needs the `sonofleo_test` database
  (`Tests/Tests.Integrated/setup-throwaway-test-db.sh` builds one from the
  migration scripts); if you can't get one, say so — don't skip or fake.

## 4. The work

Each item names the requirements it satisfies. Placement (which project,
which position in the compile order) is decided by the architecture
principles; where this plan names a file, that is the expected home, and
`ArchReviewer` is the judge. Where it doesn't, decide by
`type-placement-by-compile-tier.md` and say what you chose in the report.

### A. Foundations

1. **Quantity and Price.** (REQ-QP-1.1–3.5.) Two domain primitives in
   `Business.FinancialServices`, compiled after `Money.fs`: `Quantity.fs`,
   then `Price.fs` (Price's multiply takes a Quantity, so Quantity is the
   base). Shape them on `Money.fs`: private record over `decimal`,
   `fromDecimal` validating every rule (negative, maximum, more than six
   decimal places — **rejected, never rounded**), `amount`-style unwrap,
   the six comparisons. `Price.multiplyQuantity` (name is yours) returns the
   exact product as `decimal`. New error cases go in `BizFinServError`.
   Postgres column type for both: `numeric(16,6)`.

2. **Person.** (REQ-PER-1.1–2.4.) An entity in `Business.General`:
   `Person.fs` after `Cadence.fs` (Person uses neither ActivityPeriod nor
   Cadence, so last is simply the next free rung). PersonId, PersonName
   (trimmed, non-blank, ≤ 100) and the record, plus the usual CRUD shape.
   `Business.General` gains project references to `App.DataAccessLayer` and
   `App.Session` for persistence — App sits below Business, so that is
   legal; list them in base-first order. Errors in `BizGeneralError`.
   Person writes need an auditable action; `Business.General` has no
   auditable-action DU yet, so add `BizGeneralAuditableAction.fs` beside
   `BizGeneralError.fs`, shaped like `CashFlowAuditableAction.fs`.

3. **Schema `general`.** Migration creating schema `general` and
   `general.person`: `unique_id`, `person_name varchar(100)` unique,
   `birthdate date`, `created_at`, `modified_at`. Person is a business
   foundation, not part of Positions, so it does not live in the
   `positions` schema (see §6).

### B. Positions domain

4. **The project.** Create `Src/Business.FinancialServices.Positions/`
   (namespace `Business.FinancialServices.Positions`). Project references,
   base first: `App.Utility`, `App.DataAccessLayer`, `App.Session`,
   `Business.General`, `Business.FinancialServices`,
   `Business.FinancialServices.Ledger` (for `AccountId` on the ledger
   links). It must **not** reference DataIngestion, CashFlow or
   Classification. Add it to `SonOfLeo.slnx`. Add it as a project reference
   of `Business.CrossDomainOrchestration`, between CashFlow and
   Classification. Expected compile list:

   ```
   PositionsError.fs
   PositionsAuditableAction.fs
   PositionsComponent.fs        ← shared IDs, enums, bounded strings for both sub-domains
   DimensionValue.fs            ┐
   Security.fs                  │
   InvestmentAccount.fs         │ investments
   Holding.fs                   │
   AccountSnapshotHeader.fs     │
   AccountSnapshotLine.fs       ┘
   Property.fs                  ┐ real estate
   Valuation.fs                 ┘
   ```

   Investments and real estate are unordered peers ("Domains Build
   Upward"): **no real-estate file may reference an investments file, or
   the reverse.** Anything combining them is orchestration (Part C).
   `PositionsComponent.fs` is the shared foundation of the container; split
   it into per-sub-domain component files only if it outgrows the
   component-file convention's size guidance.

5. **Schema `positions`.** (REQ-POS-2 to 6, 9, 10 structurally.) One
   migration for the schema and one for its tables, mirroring the
   classification pair. Tables (column names are yours; the shape is the
   point):

   | Table | Columns | Keys |
   |---|---|---|
   | `dimension_value` | id, dimension `varchar(25)`, value name `varchar(100)`, created/modified | unique (dimension, name) |
   | `security` | id, name `varchar(200)`, ticker `varchar(20)` null, one nullable FK to `dimension_value` per dimension (seven), created/modified | unique name; unique ticker |
   | `investment_account` | id, name `varchar(100)`, institution `varchar(100)`, account group `varchar(100)`, tax treatment `varchar(25)`, active begin `date`, active end `date` null, ledger account FK null, created/modified | unique name; unique ledger account |
   | `investment_account_owner` | account FK, person FK (`general.person`) | PK (account, person) |
   | `holding` | id, account FK, security FK, basis method `varchar(25)` null, created/modified | unique (account, security) |
   | `account_snapshot` | id, account FK, snapshot date `date`, provenance `varchar(25)`, contribution basis `numeric(12,2)` null, created/modified | unique (account, date) |
   | `account_snapshot_line` | id, snapshot FK, holding FK, quantity `numeric(16,6)`, price `numeric(16,6)`, market value `numeric(12,2)`, reported cost basis `numeric(12,2)` null | unique (snapshot, holding) |
   | `property` | id, name `varchar(100)`, use `varchar(25)`, acquisition date, disposal date null, purchase basis `numeric(12,2)`, ledger asset account FK null, created/modified | unique name; unique ledger asset account |
   | `property_owner` | property FK, person FK | PK (property, person) |
   | `property_mortgage_account` | property FK, ledger account FK | PK (property, account); unique account |
   | `valuation` | id, property FK, valuation date, value `numeric(12,2)`, basis `varchar(100)`, created/modified | unique (property, date) |

   All foreign keys `ON DELETE RESTRICT`. No check constraints for business
   rules (dimension of a security's slot, tax treatment vs owner count,
   basis method vs tax treatment, the 0.05 tolerance, date ranges) — those
   are application rules. A third migration grants `TRUNCATE` on the
   `general` and `positions` tables to `sonofleo_test`, in the same
   `IF '{ENV}' = 'test'` block shape as `202610031500-TestRoleTruncate.sql`.

6. **Dimension Value and Security.** (REQ-POS-2.1–2.3, 3.1–3.4, 11.1,
   11.2, 11.9.) The seven dimensions are a fixed DU. A Security holds at
   most one Dimension Value per dimension; a value assigned to the wrong
   dimension's slot is rejected with the typed error REQ-POS-3.4 describes
   (it needs a read of the value's dimension, so it is checked where the
   value is resolved by dimension and name, not in `create`). Ticker is
   optional but unique when present.

7. **Investment Account.** (REQ-POS-4.1–4.9, 5.3, 5.4, 11.3, 11.4,
   11.9.) Owners are a set of PersonIds — a component of the account read
   with it, written with it, replaced whole on update ("the complete new
   set"). The rules that need other records — owner count vs tax treatment
   with both possibly changing in one update (5.4), tax treatment vs
   existing Holdings (5.3), active period vs existing snapshots (11.4), the
   ledger link's type and subtype (4.8) — are orchestration (Part C).

8. **Holding.** (REQ-POS-5.1, 5.2, 11.5.) Basis method is `AverageCost |
   SpecificLot`, present iff the account is Taxable. The check needs the
   account's tax treatment, so it runs in orchestration on create and on
   basis-method change.

9. **Account Snapshot.** (REQ-POS-6.1–6.8, 7.1–7.5.) A composite: header
   (account, date, provenance, contribution basis) plus lines, built and
   validated in orchestration (Part C). Provenance is `Reported |
   Imported`. Lines carry Quantity, Price and Money exactly as supplied;
   **nothing is derived into a snapshot and nothing is corrected** (the
   spec's design note "reported figures, recorded verbatim"). Per-line
   checks: quantity > 0, market value ≥ 0, cost basis ≥ 0 or absent,
   |quantity × price − market value| ≤ 0.05 (compare as `decimal` against
   the Money's amount — this is the one sanctioned crossing; keep it in one
   function with a comment saying why).

10. **Property and Valuation.** (REQ-POS-9.1–9.8, 10.1–10.4, 11.6–11.8,
    11.9.) Use is `PrimaryResidence | Rental`. Owned-on-date is
    acquisition ≤ d < disposal (the disposal day is *not* owned — note the
    contrast with an Investment Account's inclusive active end). Owners and
    mortgage accounts are sets replaced whole on update. REQ-POS-10.4's
    "value on a date" (latest valuation on or before, else purchase basis)
    is a pure function over a Property and its Valuations.

### C. Orchestration (`Business.CrossDomainOrchestration`)

Per "Domains Build Upward", positions-level orchestration compiles after
cash-flow orchestration (`CashFlowOps.fs`) and before
`ClassificationOrchestration.fs`. Person orchestration is foundation-level:
it compiles at the base of the project, ahead of the account-level
orchestrators. Report where you put it.

11. **Person use cases.** (REQ-PER-1.3, 1.5, 2.1–2.4.) Create, update
    (addressed by current name), list. Name uniqueness and the
    birthdate-not-in-future rule are validated here.

12. **Ledger links.** (REQ-POS-4.8, 4.9, 9.7, 9.8.) A ledger Account may
    stand for at most one Investment Account **or** Property. Checking that
    reads both sub-domains, which neither may do for the other, so the
    guard lives in orchestration and both the investment-account and the
    property use cases call it. Errors name the account code and the record
    already linked (REQ-NGUI-1.4: addressed by code, never UUID).

13. **Investment use cases.** (REQ-POS-2.x, 3.x, 4.x, 5.x, 11.1–11.5,
    11.9.) Create/rename/list Dimension Values; create/update/list
    Securities; create/update/list Investment Accounts; create/change
    basis/list Holdings. Every name given at the boundary that matches
    nothing fails with the typed not-found error REQ-POS-11.9 describes
    (kind of record + name; Dimension Values within the dimension given).

14. **Account snapshot use cases.** (REQ-POS-6.x, 7.1–7.5.) Record one or
    more snapshots **atomically** (one transaction; any failure records
    none); reject a repeated (account, date) within one request; replace an
    existing (account, date) entirely — provenance, contribution basis and
    every line — and return each snapshot as stored with a
    replaced/new flag. Replacement is a stated exception to REQ-SYS-6.1, so
    it is not an error. Delete by (account, date); fetch by (account,
    date); list dates with provenance between two dates. Line securities
    are resolved to the account's existing Holdings by Security name; a
    Security with no Holding in that account fails (6.5) — never create the
    Holding.

15. **Real estate use cases.** (REQ-POS-9.x, 10.x, 11.6–11.9.) Create,
    update, list Properties; record (replace on same date), delete, list
    Valuations. The at-most-one-primary-residence-owned-on-any-date rule
    (9.6) compares date ranges across every Property — mind the half-open
    owned interval.

16. **Holdings as of a date.** (REQ-POS-8.1–8.4.) The one cross-account
    read: for every account active on the date with a snapshot on or before
    it, that account's latest snapshot, with account fields, owners' names,
    linked ledger account code and name, snapshot date/provenance/
    contribution basis, and per line the security's name, ticker, seven
    dimension value names (or none), basis method, and the four reported
    figures. Ordered by account name, then security name. Prefer one read
    query over a per-account loop; this is called once per month-end by
    item 18.

17. **Net worth.** (REQ-RPT-8.1–8.5.) As-of date must fall in an existing
    fiscal period. Ledger balances come from the existing as-of balance
    machinery (`AccountBalance.fetchByAccountIdList` with `asOf`), each
    account's own balance, no roll-up. Counted assets = Asset accounts not
    linked to an Investment Account or a Property; investments = item 16's
    market values; property = REQ-POS-10.4 value of every Property owned on
    the date; liabilities = every Liability account. Property equity =
    value − its mortgage accounts' balances. Investable wealth = net worth −
    equity of the primary residence owned on the date, if any. Return
    everything REQ-RPT-8.5 lists, including totals by tax treatment and by
    account group. All sums through `Money`.

18. **Investment wealth history.** (REQ-RPT-9.1–9.4.) Begin ≤ end;
    grouping is one of account, account group, tax treatment, owners, or
    one of the seven dimensions. One point per month-end in range, each
    from item 16. Owners group by the complete owner set (a joint account
    is its own group). Lines with no value in a dimension group as
    "unassigned". A month-end with no holdings is a zero-total point. Not
    limited to fiscal periods.

### D. Interface (`Ui.InterfaceBridge`, `Ui.OperatorCli`, `Ui.ReportCli`)

Contracts, converters and routes each go in their layer's concept order:
Person pieces with the foundations (directly after the shared ones),
positions pieces after cash flow and before classification. Expected new
files: `PersonContracts.fs`, `PositionsContracts.fs`,
`PersonFieldConverters.fs`, `PositionsFieldConverters.fs`,
`PersonRoutes.fs`, `PositionsRoutes.fs`, `NetWorthWriter.fs`,
`InvestmentWealthHistoryWriter.fs`. Register the command routes in
`Ui.OperatorCli/Program.fs` and the report routes in `ReportRoutes.fs`.

19. **Command routes.** One per "means to" in the spec, and no others.
    Domain and verb (case-sensitive, REQ-NGUI-3.8):

    | Domain | Verbs | REQs |
    |---|---|---|
    | `Person` | `Create`, `Update`, `List` | PER-2.1–2.3 |
    | `DimensionValue` | `Create`, `Rename`, `List` | POS-11.1 |
    | `Security` | `Create`, `Update`, `List` | POS-11.2 |
    | `InvestmentAccount` | `Create`, `Update`, `List` | POS-11.3 |
    | `Holding` | `Create`, `UpdateBasisMethod`, `List`, `FetchAsOf` | POS-11.5, 8.1 |
    | `AccountSnapshot` | `Record`, `Delete`, `Fetch`, `ListDates` | POS-7.1–7.5 |
    | `Property` | `Create`, `Update`, `List` | POS-11.6 |
    | `Valuation` | `Record`, `Delete`, `List` | POS-11.8 |

    Updates use `FieldUpdate` in the contract the way existing update
    routes do (`field-update-pattern.md`); "set or clear" fields are
    `FieldUpdate<_ option>`. An update naming no field is a no-op and is
    rejected; a field re-sent at its stored value counts as named and
    succeeds (REQ-SYS-6.1). Owners and mortgage accounts, when updated,
    are given as the complete new set. Every record is addressed by name;
    ledger accounts by code, and every ledger account in a return payload
    carries its name beside its code (REQ-NGUI-1.4, 1.6). Quantities and
    prices cross the boundary as JSON numbers and convert through
    `Quantity.fromDecimal` / `Price.fromDecimal` — no rounding at the
    boundary either.

20. **Report routes.** (REQ-RPT-8.6, 9.5.) `NetWorth` and
    `InvestmentWealthHistory` in the Reports CLI, read-only
    (`Context.create NoTransaction FetchOnly`), both output modes,
    date interpolation per REQ-RPT-2.4 (`-yyyy-MM-dd` for net worth,
    `-yyyy-MM-dd_yyyy-MM-dd` for history).

21. **Report writers.** HTML tables only — no charts (charts are a later
    project). Reuse `ReportVisualizationAssets` (header with title and
    date, body, footer with the initiation instant passed in, print CSS).
    Net worth: sections for counted ledger assets, investment accounts,
    properties with their mortgages and equity, liabilities, then totals,
    then the tax-treatment and account-group subtotals. Wealth history:
    one row per month-end, one column per group value, a total column.
    REQ-RPT-3.x is scoped by its own text to the four existing reports, so
    it does not bind these two; follow its conventions anyway, since they
    cost nothing here (see §6).

### E. Architecture model

22. **Bring `Architecture/SonOfLeo.archimate` in step.** (Skill:
    `Skills/ArchiMate/SKILL.md`.) A component for every new `.fs` and the
    new project (the project component already exists — reuse it, don't
    duplicate), composition under the right project folder, serving edges
    for every new project reference and module `open`, and realization
    edges to capabilities. Capabilities: Positions use cases under the
    existing **"Positions functions"** group; Person under the
    Business.General capability group; Quantity/Price beside Money's;
    the two reports beside the existing report capabilities. Then:
    `python3 Skills/ArchiMate/validate.py` must say `VALID`, and
    `python3 Skills/ArchiMate/model_drift.py` must report nothing — today
    it reports exactly two findings (the missing `.fsproj` path and
    "Positions functions" unrealized), and this item clears both. Dan owns
    view layout; don't edit views.

## 5. Out of scope

- Specs (Hobson).
- Purchases, sales, lots, realised gains, the holdings ledger and its
  reconciliation (slice 2); market reference data (slice 3); charts; the
  simulator.
- Importing history or parsing institution files. Parsers live outside the
  repo and call `AccountSnapshot Record`; history import is Dan and Hobson's
  job after this merges.
- Any change to Ledger, DataIngestion, CashFlow or Classification behaviour.
  Positions reads ledger accounts and balances; it never writes the ledger.

## 6. Open design notes you may hit

- **Person lives in schema `general`.** Hobson's call, following the code
  placement (Business.General, beneath every domain). If you find a reason
  it can't, say so; don't move it into `positions`.
- **Tax treatment is effectively fixed once an account has Holdings.**
  REQ-POS-5.2 and 5.3 together mean a Taxable account's Holdings must have
  a basis method and a non-Taxable account's must not, and a basis method
  can only be changed within that rule. So flipping a populated account
  between Taxable and anything else is rejected. That is intended — real
  accounts don't change tax treatment. Don't add a cascade.
- **Two kinds of "active on a date."** An Investment Account's active
  period is inclusive at both ends (REQ-POS-4.7, use `ActivityPeriod`); a
  Property's owned period excludes the disposal date (REQ-POS-9.4). They
  differ on purpose.
- **Snapshot replacement is not REQ-SYS-6.1's no-op error.** Re-recording
  identical figures for a date is a replacement, returned with
  replaced = true.
- **An account with no snapshot on or before a date is absent from
  holdings as of that date**, not present with zero (REQ-POS-8.1). An empty
  snapshot (no lines) is present with zero.
- **Net worth fails outside a fiscal period; wealth history does not.**
- **Rendered-report rules.** REQ-RPT-3's opening sentence lists the four
  existing reports. If you think the new two should be bound by 3.1, 3.2,
  3.4 and 3.5, say so in the report; Hobson will amend the spec.

## 7. When you finish

Report to Dan, at the bottom of this file under `## 8. Report`, together
with the brief's final report: what you implemented (by item number and
REQs), what you didn't and why, every placement choice §4 left to you, the
migration scripts Dan must apply (in order), test status (which suites ran,
where), `run-all.sh` output, `validate.py` and `model_drift.py` output, and
any requirement you believe is wrong. Don't mark anything done that you
haven't verified.

## 8. Report

*(The session appends here.)*
