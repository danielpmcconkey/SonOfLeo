# Plan — Audit 2026-10-04a remediation (Src)

Written 2026-10-05 by Hobson for a Claude Code session that can see this
repository and nothing else. Everything you need is either in this document
or in the repo. Where this plan and `Specs/Behavioral/` disagree, the spec
wins — tell Dan.

## 0. Start here — one session does Src and tests, in this order

You are doing both halves of the slice loop: Src from this plan, Tests from
`HobsonsNotes/brief-test-agent-audit-remediation-2026-10-05.md` (the brief).
README constraint 1 says tests are named from the spec by someone who hasn't
seen the implementation. You can't un-see code, so the order is the
safeguard:

1. **Name first, from the spec alone.** Read §2's reading list *except* Src,
   then the brief's §0 and Part A. Draft test names for every Part A item,
   run the name-quality check, and commit them as failing placeholders
   (`Assert.Fail "Not yet implemented"`). **Open nothing under `Src/` until
   that commit is pushed.** Existing tests may be read for naming and
   fixture conventions.
2. **Implement** Part A of this plan, then Part B, then Part C. Commit as you go.
3. **Test.** Brief Part A: now read Src; if a committed name is aimed wrong,
   say so in your report and renegotiate it out loud (never quietly soften
   it). Write each test and watch every assertion fail before it passes
   (constraint 3). Then brief Part B (existing-test repairs).
4. **Finish** with §7 of this plan and the brief's final report, both into
   §8 of this file.

A test that fails in step 3 is either a Src bug (fix it) or a spec bug
(record it in the report with the REQ ID; don't edit the spec).

## 1. What this is for

SonOfLeo is Dan's personal-finance system: a cash-basis double-entry ledger
in F# on .NET 10 and PostgreSQL. The most recent slice added Person
(`Business.General`), exact six-place Quantity and Price, and the Positions
domain (investment securities and real estate, with net worth and investment
wealth history reports).

Audit `2026-10-04a` ran 45 auditors over the code, tests and specs. Every
finding has been ruled on in `Audit/2026-10-04a/99-disposition.md` (columns:
`#`, finding, owner, status, ruling). Hobson has already applied the spec
side (commit `22af7f4` on this branch). **This
plan is the Src side:** every row owned by `impl-agent`, plus the code the
amended specs now require. Items cite their disposition rows as `#NNN`; read
the row before starting an item — the ruling is the instruction. Rows marked
`[dup of #NNN]` are handled under the canonical row.

## 2. Before you touch anything, read

- `README.md` — the slice loop and the three load-bearing constraints.
- `Specs/README.md` — requirement grammar, the traceability gate, the rule
  that specs never name source files and source never carries REQ
  annotations.
- `Src/README.md`, and `Tests/README.md` for what tests will hold you to.
- `Skills/SonOfLeoSrcDeveloper/SKILL.md` — how Src is shaped. Follow it.
- `Skills/ArchReviewer/SKILL.md` — run it over your own diffs before you
  report.
- `CompoundedLearnings/` — settled judgment calls, especially
  `validation-layers.md`, `dal-errors-are-backstops.md`,
  `field-update-pattern.md`.
- The archimate model's principles and constraints (via
  `Skills/ArchiMate/`) — Part C is conformance to them.
- `git show 22af7f4 -- Specs/` — exactly what changed in the spec.
- `Audit/2026-10-04a/99-disposition.md` — the rulings.
- `Skills/SonOfLeoRequirementsAudit/resolved-findings.md` — settled rulings,
  including three new ones that bound Part C (public report rows,
  cross-schema report reads, DB-backed uniqueness).

## 3. Standing rules

- **You write `Src/`, `DbMigration/Scripts/`, `Checks/`, `Src/README.md` and
  `Tests/`; never `Specs/`.** During implementation (§0 step 2), when a Src
  deletion, rename or move breaks the build of an existing test, make the
  minimal mechanical fix (re-point a call, delete a test of a deleted
  function) and list it; the real test work waits for step 3.
- **Branch `audit-remediation-2026-10-05`.** `git pull --rebase --autostash`
  before every commit. Only Dan merges to `main`.
- **Public repository.** No institution names, account numbers, people's
  names, or real amounts in code, comments, migrations or commit messages.
- **Dan is the authority.** If a ruling or requirement looks wrong, make the
  case once in your report; don't edit a spec, and don't quietly do
  something else.
- **No REQ annotations in source.** A comment may name a rule to explain
  *why* code is shaped a certain way; it never tags a site.
- **Structural integrity in the schema (foreign keys, unique keys); every
  business rule in the application layer** (REQ-DAL-3.6).
- **Infallible create, orchestrator validates.** `create` is total;
  orchestrators validate. `reconstitute` validates everything decidable from
  the row alone (10-03a #311).
- **Loud beats bubblewrap.** A failure that already aborts with an error
  needs no new guard and no new error case. Only silent failures earn code.
- **Dead code goes.** No caller in Src and no requirement → delete it.
- **Code never breaks a tie** unless a requirement names the tie-break.
- **Tuple lists of domain primitives in construct functions are
  intentional.** Don't introduce "create input" records (and Part C removes
  the ones the slice introduced).
- **Investments and real estate are peers.** Neither may open, name or read
  the other; only net worth (and the reports built on it) combines them.
- **All ledger currency arithmetic and comparison goes through `Money`**
  (REQ-MON-2.1), except the one sanctioned comparison against an exact
  Quantity × Price product (REQ-MON-2.1 as amended, REQ-POS-6.8).
- **Every operation uses its context's initiation instant** for every
  timestamp and every derived "current date" (REQ-SYS-3.4).
- **Migrations:** new scripts in `DbMigration/Scripts/` named
  `YYYYMMDDHHMM-PascalCaseDescription.sql`, with a header comment saying
  why. They must carry existing rows forward. Never run anything against
  production; Dan applies migrations by hand.
- Commit subjects describe the behaviour, not the edit. Match the log.
- **Before every hand-off:** green build, `Tests.Isolated` green, and
  `bash Checks/run-all.sh` green, with the output in your report.
  `Tests.Integrated` needs the `sonofleo_test` database; if you don't have
  it, say so — don't skip or fake.

## 4. The work

File references are where the problem was found on 2026-10-04; **verify
before editing**. Do Part A first, then Part B, then Part C. Part C moves
and reshapes the Positions and Person orchestration that A touches; doing A
first means its behaviour is pinned by tests before the reshape.

### A. Behaviour — the amended specs require these

1. **A Property links a set of FixedAsset ledger accounts.** (#199;
   REQ-POS-9.7, REQ-RPT-8.2 as amended.) Today `positions.property` carries
   one `ledger_asset_account_id`. Migration: a
   `positions.property_asset_account` table shaped like
   `property_mortgage_account` (a component row: no timestamps — see
   Definitions), a unique key on the ledger account so it links to at most
   one Property, existing non-null values copied in, then the column
   dropped. Contracts, converters and the Property view carry a list of
   accounts (complete-new-set update semantics, as mortgages). Net worth
   excludes every linked asset account of an owned Property. Implement the
   link check per peer as item C.1 describes; don't extend the shared
   helper.
2. **Repeated members are rejected.** (#010, #128; REQ-POS-9.8 as amended.)
   A mortgage account (and, under A.1, an asset account) repeated in one
   request is rejected with a typed error naming the code, as repeated
   owners already are. Past that check, owner, mortgage and asset collections
   are `Set<_>`, and the `List.distinct`/sort normalisation goes.
3. **Deletes.** (#007; Positions.md as amended.) A Property delete, refused
   with a typed error while the Property has Valuations; a Holding delete,
   refused with a typed error while any Account Snapshot line references
   it. Operator routes for both. No delete for Dimension Value, Security or
   Investment Account.
4. **Tax treatment away from Roth.** (#197; the new REQ beside REQ-POS-5.3.)
   Rejected while any of the account's snapshots carries a contribution
   basis; the error names the earliest and latest such snapshot dates.
5. **Snapshot recording and listing.** (#020; REQ-POS-7.1, REQ-SYS-6.1.) An
   empty snapshot list is rejected with a typed error. (#172; REQ-POS-7.5.)
   Listing snapshot dates with an end date before the begin date is rejected
   with a typed error naming both.
6. **Dimension Value renamed to its own name succeeds.** (#009; REQ-SYS-6.1.)
   Give the free-name check a self-exclusion, as its siblings have.
7. **Net worth totals add up.** (#198; REQ-RPT-8.5.) A separate total for
   the mortgages of owned Properties, in the computation, the return
   contract and the writer's Totals block.
8. **Investment wealth history.** (#103; REQ-RPT-9.2/9.5 as amended.) Every
   group value present at any point in the range appears at every point; a
   group with no holdings at a point carries 0.00, in the data and in the
   table (not blank).
9. **Same-file duplicate key.** (#150; the new REQ-STG §1 rule.) Two groups
   in one file sharing source and fi_reference reject the file with a typed
   error naming both groups.
10. **Staged-entry fetch by source.** (#151; REQ-STG-10.2/10.7.) The source
    filter names a source; a name matching no source fails with a typed
    error, as the account filter does.
11. **Rule listing order.** (#145; REQ-CR-5.4.) Ties on the sort key break
    by rule name in every sort order.
12. **Enum inputs are trimmed.** (#190; REQ-SYS-1.1.) Every enum `fromString`
    an operator reaches trims first: Positions, the wealth grouping,
    CashFlow, Classification and DataIngestion (Ledger already does).
13. **Reconstitute re-checks row-local rules.** (#094, #108.) Rename
    `AccountSnapshotLine.checkFigures` to `confirmFigures` and make the
    row-local part callable from `reconstitute` (the names need a DB read;
    label by ids instead). AccountSnapshotLine (quantity > 0, market value
    and cost basis not negative, the 0.05 tolerance), InvestmentAccount
    (owners non-empty, a single owner unless Taxable), Property (owners
    non-empty), and HoldingsAsOf's own line build. No corrupted-row tests
    (10-03a #258).

### B. Dead code, conformance, small fixes

14. **DAL dead surface.** (#002, #003, #185.) Delete `Zero` and `OneOrMany`
    and their arms; delete the 12 uncalled unboxers (keep `stringUnboxing`
    and `longUnboxing`), their now-unraised `DalError` cases,
    `getBoolOption`, and `NullableBoolean`/`NullableJsonb`. Make
    `confirmNumRows` public and have `JournalEntryOrchestration` call it in
    place of its hand copy.
15. **Other dead code.** (#013) `Security.dimensionValueIn`. (#022) The
    unreachable "Payment belongs to another Invoice" check in Instance
    orchestration, both arms, and `CashflowPaymentNotUnderInvoice`.
16. **One Property-value function.** (#127.) Return the value with its
    source (latest Valuation, else purchase basis); NetWorth matches on it
    instead of re-deriving the rule.
17. **DAL naming.** (#187.) The case raised for an empty connection-string
    setting is named for that condition; `DalEnvVarNotSet`'s message drops
    "or empty".
18. **Docs that agents read.** (#186, and #102's `Src/README.md` half.)
    `Src/README.md`: add `Business.FinancialServices.Positions`, Quantity and
    Price, and Person in `Business.General`; describe `LookupCache` as the
    generic route-lifetime column↔ID cache factory that entity modules bind.
    Fix the bracket name to `runCommandRouteAndAutoRollback` in the README
    and the `CommandRoute` doc comment.
19. **Checks.** (#101) The traceability gate counts only REQ IDs inside
    backtick test-method names, not comments or strings; confirm it still
    reports clean on this branch's tests before you change anything else,
    and say what moved. (#100) A peer-guard check: a real-estate file
    (Property, Valuation, real-estate orchestration) may not open or name an
    investment module, nor the reverse; allowlist only the net worth and
    wealth/holdings reports that legitimately combine them. (#114) An
    open-order check: `open` blocks follow compile order. (#108) Widen
    `check-confirm-naming` to flag unit-returning `check[A-Z]` functions.
    Wire all of them into `run-all.sh`.

### C. Architecture — after A and B

20. **Peer link checks.** (#104.) Split the ledger-link check per peer: the
    investment check reads only Investment Accounts, the real-estate check
    reads only Properties. Delete `LinkingRecord`, the shared pool and its
    comment; `self` becomes a plain id. Only the ledger-account lookup is
    shared.
21. **Business tier takes IDs; tuples; constructNewAndPersist.** (#105,
    #106, #107 — one piece of work.) Name→ID resolution for Person and
    Positions moves into the boundary converters, using module-owned name→ID
    lookups as CashFlow's converters do; orchestrators take IDs.
    Name-uniqueness checks stay in Business. Replace `NewInvestmentAccount`,
    `NewProperty`, `SnapshotInput` and `SnapshotLineInput` with primitive
    tuples on `constructNewAndPersist`; replace the two update records with
    the entities' own `FieldUpdates`. Rename the `create<X>` orchestration
    functions to `constructNewAndPersist`, splitting `InvestmentOrchestration`
    into per-entity orchestration modules so each module has one (10-03a
    #165). After this the `Positions*IdDoesntExist` and `PersonIdDoesntExist`
    cases become reachable through the converters' lookups; say in the report
    which ones are.
22. **Update backbones.** (#109.) DimensionValue, Valuation,
    AccountSnapshotHeader and Holding get a FieldUpdates-driven `update`
    that guards no-ops and re-fetches; callers use it.
23. **Type placement.** (#110) `Business.General/PersonComponent.fs` holds
    `PersonId` and `PersonName`, compiled before `Person.fs`. (#111)
    `WealthGrouping`, `WealthGroup` and `PropertyValueSource` move into
    `PositionsComponent`.
24. **Open order.** (#114.) Reorder the eight open blocks the row names to
    build order (the check from item 19 should then pass).
25. **Input contracts.** (#115.) A `SecurityDimensionValueInput` for Security
    create, and an `AccountSnapshotLineInput` separate from the return line
    type.

After Part C, run `Skills/ArchiMate/validate.py` and `model_drift.py`, and
update the model's element names if the module split or renames moved
anything the model names.

## 5. Out of scope

- Specs, `resolved-findings.md` and the archimate model's principles
  (Hobson).
- Rows the disposition overruled.
- The history import and institution parsers (they live outside this repo).

## 6. Open design notes you may hit

- **Public report rows.** Read-only report and view output records may stay
  public and be read by field (resolved-findings, Dan 2026-10-05; precedent
  `TrialBalanceReport`). Don't add accessors to them.
- **HoldingsAsOf's single cross-schema SELECT stays** (resolved-findings,
  Dan 2026-10-05). Item 13 changes its line build, not its query shape.
- **Uniqueness backed by a DB constraint** needs no typed error and no
  update-path test (resolved-findings).
- **Component rows** (snapshot lines, owner/mortgage/asset sets) are not
  entities: no `created_at`/`modified_at` (Definitions, amended).
- **Legacy zero-quantity history.** Not this branch's problem: the history
  importer drops them. A Quantity of zero stays illegal (REQ-POS-6.7).

## 7. When you finish

Report to Dan, at the bottom of this file under `## 8. Report`, together
with the brief's final report: what you implemented (by item number, rows
and REQs), what you didn't and why, every test file you touched
mechanically, the migration scripts Dan must apply, test status (which
suites ran, where), `run-all.sh` output, the traceability audit output, the
ArchiMate validation output, and any requirement you believe is wrong.
Don't mark anything done that you haven't verified.

## 8. Report

Written 2026-10-05 by the remediation session. Branch `audit-remediation-2026-10-05`.

### 8.1 Verdict

Every gate in "done" is green on the branch head:

- `dotnet build`: 0 warnings, 0 errors.
- Tests.Isolated **505/505**. Tests.Integrated **1603/1603**, run twice in a row against a freshly built
  `sonofleo_test` (local throwaway PostgreSQL 16, `setup-throwaway-test-db.sh`).
- `bash Checks/run-all.sh`: 12 passed, 0 failed, 1 skipped (`check-traceability` skips off main by
  design). Run directly, `traceability-audit.sh .` gives Invariant 1 clean, Invariant 2 clean, and no
  stale-waivers section.
- `Skills/ArchiMate/validate.py`: VALID, 655 elements, 2715 relationships, 0 findings.
  `model_drift.py`: NO DRIFT.
- No placeholder (`Not yet implemented`) remains.

**Dan must apply one migration:** `DbMigration/Scripts/202610051000-PropertyAssetAccountSet.sql`. It
creates `positions.property_asset_account`, copies every existing link into it, then drops
`positions.property.ledger_asset_account_id`.

### 8.2 Order kept (§0)

1. The names were drafted from the spec, graded, and committed as failing placeholders (d4c2a97)
   before anything under Src was opened.
2. Src work: Part A (dc7b150), Part B (2dbe95c), Part C (cdc1a0f, ef01120, 6804fc0, 5cbe664), then
   the ArchiMate model (6994e50).
3. Then the tests. These were written in five areas on separate branches, each against its own copy
   of the test database, and merged here (47979a3, 3372ba1, a180885, 00277ef, 8584984, 66276ab).

### 8.3 Src, by plan item

**Part A** (dc7b150)

- **1. Asset-account set.** Done: migration above, `Set` in Property, contracts and converters carry a
  list, net worth excludes every linked asset account of an owned Property. The link check is per
  peer (see item 20).
- **2. Repeated members.** A repeated mortgage or asset account is a typed error naming the code. Past
  the check, owners, mortgages and assets are `Set<_>`.
- **3. Deletes.** Property delete is refused while Valuations exist. Holding delete is refused while
  snapshot lines reference it. Both have routes. There is no delete for Dimension Value, Security or
  Investment Account.
- **4. Roth guard.** Implemented. The error names the account and the earliest and latest
  basis-carrying dates.
- **5. Snapshots.** An empty list gives `PositionsSnapshotListIsEmpty`. End before begin gives
  `PositionsSnapshotDatesEndBeforeBegin(begin, end)`.
- **6. Dimension Value renamed to its own name.** The free-name check now excludes the value itself.
- **7. Net worth.** An owned-property mortgages total, in the computation, the contract and the
  Totals block.
- **8. Wealth history.** Every group present at any point appears at every point, 0.00 where it holds
  nothing, in data and table.
- **9. Same-file duplicate key.** The file is rejected with a typed error naming both groups.
- **10. Source filter.** It takes a source name; an unknown name gives `IngestionSourceNameNotFound`.
  **A bug in my implementation, caught by the tests:** the where clause named `source_id`, but the
  `all_in_stage` CTE in `StageEntryOrchestration.fetchFiltered` never selected it, so any source
  filter failed with Postgres 42703. Fixed in 8584984 by adding `se.source_id` to the CTE. The broken
  staging fixture had been hiding it.
- **11. Rule order.** Every sort order adds `rule_name asc`. Ties break ascending by name in both
  directions, which is what REQ-CR-5.4 says. The test that used to be called "exact reverse" compared
  keys only, so it was correct; it was renamed so it no longer over-claims (§8.5).
- **12. Enum trim.** Every operator-facing `fromString` trims first: Positions, WealthGrouping,
  CashFlow (including Cadence's weekday and month), Classification, DataIngestion.
- **13. Reconstitute.** `checkFigures` is renamed `confirmFigures`. Its row-local part, labelled by
  ids, runs from `reconstitute` and from HoldingsAsOf's line build. InvestmentAccount and Property
  reconstitute re-check their owner rules. No corrupted-row tests.

**Part B** (2dbe95c)

- **14. DAL.**
  - `Zero`/`OneOrMany` and their arms are gone.
  - The 12 uncalled unboxers, their DalError cases, `getBoolOption` and
    `NullableBoolean`/`NullableJsonb` are gone.
  - `confirmNumRows` is public, and JournalEntryOrchestration calls it.
- **15. Dead code.** `Security.dimensionValueIn` is gone. The "Payment belongs to another Invoice"
  check is gone with both arms and `CashflowPaymentNotUnderInvoice`. That includes its use inside
  `confirmInvoiceComposite`, which was unreachable as well.
- **16.** One Property-value function returns `(value, PropertyValueSource)`, and NetWorth matches on
  it.
- **17.** `DalConnectionStringEnvVarSettingIsEmpty`, and `DalEnvVarNotSet`'s message drops "or empty".
- **18.** `Src/README.md` updated as listed. The bracket name is `runCommandRouteAndAutoRollback` in
  the README and the doc comment.
- **19. Checks.**
  - The traceability gate counts only REQ IDs inside backtick test names. Before the change it
    reported clean; after it, nothing moved.
  - New `check-peer-guard.sh`: real estate and investment may not open or name each other; only the
    net worth and wealth/holdings reports are allowlisted.
  - New `check-open-order.sh`: externals first, then dependency projects, then the same project in
    compile order; a namespace ranks before its modules.
  - `check-confirm-naming` also flags `check[A-Z]` functions returning `Result<unit` or with no
    return annotation.
  - All three are wired into `run-all.sh`, and each was shown to fail on a planted violation.
  - **Outside §3's write list:** item 19's gate change required editing
    `Skills/SonOfLeoRequirementsAudit/traceability-audit.sh`.

**Part C**

- **20.** One per-peer link check each (investment reads Investment Accounts only, real estate reads
  Properties only). `LinkingRecord` and the shared pool are gone; only the ledger-account lookup is
  shared (dc7b150).
- **21.** (6804fc0)
  - Name→ID resolution moved into the boundary converters (`PersonFieldConverters`,
    `PositionsFieldConverters`). The orchestrators take IDs.
  - The tuple-based `constructNewAndPersist` replaces the input records, and the entities' own
    FieldUpdates replace the update records.
  - `InvestmentOrchestration` is split into DimensionValue-, Security-, InvestmentAccount- and
    HoldingOrchestration.
  - Person's `createPerson` is now `constructNewAndPersist`.
- **22.** FieldUpdates-driven `update` with a no-op guard and re-fetch for DimensionValue, Holding,
  Valuation and AccountSnapshotHeader (ef01120). The six new PositionsError cases are listed in that
  commit.
- **23.** `Business.General/PersonComponent.fs` holds PersonId and PersonName (cdc1a0f).
  WealthGrouping, WealthGroup and PropertyValueSource are in PositionsComponent.
- **24.** Open blocks reordered (5cbe664). Two files beyond the auditor's eight also needed it:
  StageEntryOrchestration and JournalEntryFieldConverters.
- **25.** `SecurityDimensionValueInput`, and `AccountSnapshotLineInput` split from
  `AccountSnapshotLineReturn` (cdc1a0f).
- **ArchiMate** (6994e50):
  - InvestmentOrchestration is replaced by the four modules, each composed by
    CrossDomainOrchestration and realizing its own Maintain capability.
  - PersonComponent is added, composed by Business.General and realizing "Maintain persons".
  - Serving edges follow drift: 78 added, 4 removed. Principles untouched.

**Reachability of the IdDoesntExist cases (item 21 asks).**

- **Reachable only by a Business caller passing an unknown ID, never through a route** (the route
  converters resolve names and fail first with the `…NameDoesntMatch` cases):
  - PositionsInvestmentAccountIdDoesntExist
  - PositionsSecurityIdDoesntExist
  - PositionsPropertyIdDoesntExist
  - PositionsDimensionValueIdDoesntExist
  - PersonIdDoesntExist
- **Not reachable at all** (each is looked up only right after a fetch that found it):
  - PositionsHoldingIdDoesntExist
  - PositionsValuationIdDoesntExist
  - PositionsAccountSnapshotIdDoesntExist
- **No route can produce the new `…UpdateNoOp` cases** (every route update names a field).

`run-all.sh`'s report-only AppError coverage lists these cases as untested. They are loud, so they
got no test.

### 8.4 Tests, by brief item

Every placeholder name was implemented as committed; none was reworded. Three placeholder Facts became
Theories with one row per case, so each case fails on its own:
- REQ-SYS-3.2 and REQ-SYS-3.3 for the slice
- the REQ-POS-9.7 wrong subtype or type test

**Part A**

- **A.1.1 REQ-POS-5.5.** The refusal records three basis snapshots out of date order; the error
  names the account and the earliest and latest dates, and the account stays Roth. The success case
  reads back TaxDeferred.
- **A.1.2 REQ-POS-11.10/11.11.**
  - Orchestrator and route deletes, refusals with their typed errors, and a not-found case for each.
  - A deleted Property's former asset and mortgage accounts link to a new Property afterwards.
- **A.1.3 REQ-POS-9.7/11.6.** Route tests for:
  - create with two asset accounts
  - update replacing the set
  - a repeated code
  - an account already linked to another Property, naming that Property
  - wrong subtype and wrong type (a theory)

  These tests use their own committed-and-cleaned accounts T-1594..T-1599, which keeps them clear of
  the LookupCache hazard (§8.7).
- **A.1.4 REQ-POS-9.8.** A repeated mortgage code is refused, and nothing is stored.
- **A.1.5.** REQ-POS-4.9 is dropped from the Property asset-link test, which keeps 9.7. The
  Investment Account 4.9 test still describes what it asserts.
- **A.1.6.** An empty snapshot list is refused (route). End before begin is refused, naming both
  dates.
- **A.2.7 REQ-RPT-8.2.** A rolled-back Property is linked to two funded FixedAsset accounts. Neither
  account is counted, and net worth counts the Property's value once.
- **A.2.8 REQ-RPT-8.5.**
  - The owned-property mortgages total is 480,000 from fixture data; a disposed rental's mortgage is
    excluded.
  - The identity holds on the fixture date, both in data and in the rendered Totals block.
- **A.2.9 REQ-RPT-9.2/9.5.**
  - One account goes from holdings to 0.00, and another from 0.00 to holdings.
  - The rendered row shows "0.00", not a blank.
  - The header test covers the grouping. The existing range-header test already cited 9.5 and 3.1.
- **A.3.10 REQ-STG-1.18.** The whole file is refused naming both groups. A valid third group in the
  same file is not staged.
- **A.3.11 REQ-STG-10.2/10.7.**
  - Unknown source name: `IngestionSourceNameNotFound`.
  - No existing test passed a name; the orchestrator test passes an ID, which is right for that
    layer. A route test now resolves two source names, each to its own entries.
- **A.3.12 REQ-CF-5.20.** `"   "` gives the empty-ID error. 101 characters gives the too-long error.
  Exactly 100 is accepted and stored as given.
- **A.3.13.** The repeat-and-compare projection test is re-cited from REQ-CF-8.5 to REQ-CF-8.11.
- **A.3.14 REQ-CF-9.10.** Three trigger tests, each with a before-check: amount raised, Invoice
  added, Invoice cancelled.
- **A.3.15 REQ-CF-4.12.** An Invoice that was already cancelled keeps its own note.
- **A.3.16 REQ-CF-14.8.** Naming another Master Agreement's leg gives
  `CashflowPaymentAgreementNotUnderMasterAgreement`, and both agreements are unchanged.
- **A.3.17 REQ-CR-5.4.** Exact whole-list order for ties on account code and on priority, ascending
  and descending. #145's "adjacent" test now asserts an exact order.
- **A.4.18 REQ-SYS-1.1.** Isolated trim tests for:
  - Positions: Dimension, TaxTreatment, BasisMethod, Provenance, PropertyUse
  - WealthGrouping
  - CashFlow: FlowDirection, InvoiceState, Cadence weekday and month
  - Classification: numeric operator, group connector, claimant type
  - DataIngestion: 8 statuses, 5 change mechanisms

  PaymentState and PostedState are left out: no caller supplies them as text (REQ-CF-9.11).
- **A.4.19.** Renaming a Dimension Value to its own name succeeds; the name and dimension are
  unchanged and modified-at advances.
- **A.4.20.** The Valuation-delete miss test is re-cited from REQ-SYS-6.1 to REQ-SYS-6.2.
- **A.4.21 REQ-SYS-3.2/3.3.** Person, Dimension Value, Security, Investment Account, Holding, Account
  Snapshot, Property and Valuation.
- **A.5.**
  - REQ-JE-3.8's three tests:
    - FI-only: REQ-JE-3.5, with set equality of header IDs.
    - Reference-only: REQ-JE-3.5, with set equality.
    - Both-None: REQ-JE-3.5.1.

    None was deleted, because the matching tests elsewhere are at the route layer.
  - REQ-JE-5.6's test is deleted.
  - The three REQ-NGUI-1.4 tests cite REQ-NGUI-1.5 and keep their REQ-POS IDs.
  - REQ-STG-1.16 is added to "the same group_id in a second file produces a separate staged entry".

**Part B rows.** All were done; none was skipped.

- **#006.** `RouteResolver` uses the shipped `Ui.OperatorCli` `Program.commandRoutes`, and the
  hand-copied table is gone. Removing Person from Program made every Person route test fail with
  CliUnknownCommand.
- **#066.** Exact stderr and `Assert.Equal(1, exitCode)`.
- **#067.** The DataOnly rows equal `routeReportingCommandForTesting "TrialBalance"`.
- **#070, #072.** Exact messages built from the input type's `FullName`: Person, and the three
  Positions missing-field tests.
- **#071.** A whitespace-only Create personName is refused with PersonNameIsEmpty, and nothing is
  stored.
- **#024, #025, #026.**
  - REQ-AC-4.3 and 2.7 are theories with an active end of today or later.
  - The 2.7 test now asserts parentId.
  - 4.2 goes through `deactivateAndReadBack`.
- **#027, #028, #029.** Set equality against fixture data. #028 includes voided lines (it also cites
  3.12.2); #029 excludes them.
- **#031.** The duplicate 1.40 test is deleted, and 2.6's test cites both.
- **#032.** Re-cited to REQ-AC-2.14 and REQ-SYS-3.2.
- **#088.** Exact amounts instead of `> zero`.
- **#033.** An Income-agreement theory for InvoiceExpected and InvoiceReceived.
- **#035.** Zero-amount and duplicate-name theories on the update and add paths. The duplicate name
  accepts the DAL non-query error without pinning the constraint (UNIQUE-DB).
- **#037.** An UpdateAgreement row is added to the REQ-CF-14.7 theory and its name.
- **#038.** Source-pattern filter: exactly the chainOne-Source and chainTwo-Source rules.
- **#040.** An ExactlyOne update touching two rows gives
  `DalResultantRowsDidntMatchExpectation("ExactlyOne", 2)`.
- **#041.** The cache test builds fresh caches from the public LookupCache factories. Its comment and
  message are fixed.
- **#002, #003, #042.** Done during the Src work: the uncited 27-case DAL theory is deleted, and with
  it the malformed-SQL and unboxer rows.
- **#012.** `SourceFile.create` with `""` or `"   "` gives IngestionSourceFileIsEmpty.
- **#043.** Asserts `Some "Data ingestion import"`.
- **#044.** Asserts a Duplicate-transition count of 0, and latest status Classified.
- **#045.** Independently derived (from, to, mechanism) sequences and line values, at both layers.
- **#046.** The `fetchByStatusList` equality is removed.
- **#048.** Two tests: a voided-JE Posted original, and a Duplicate original.
- **#049.** Full before/after snapshot of every ingested entry around the shadow post.
- **#050.** The exact per-record error.
- **#051.** The duplicate 1.14 test is deleted.
- **#053.** Re-cited to REQ-STG-2.23.
- **#055.** Exact sorted keys.
- **#056.** The returned id and key are asserted.
- **#057.** First and last day of an open period are assigned the right fixture period. 2.5 and 1.11
  are dropped from the 2.6 test.
- **#060.**
  - The RevisedRequirementsLedger test is kept and now cites 2.11, 2.12 and 2.15.
  - Duplicates are deleted: the JournalEntryRoutes bothNull row, the 3.7 begin-after-end test, the
    5.1 secondary-not-found test, the CommentsAndReads secondary-not-found row, and the SYS-8.1
    single-post row.
- **#061.** The ExternalReference no-op test cites REQ-SYS-6.1, and 2.7 is dropped from the read-path
  test.
- **#062.** (account, amount, lineType) set equality. Descriptions come from `fixture.Data`.
- **#063.** The two constant-derived Money tests are deleted, and REQ-MON-2.2.1 is added to the
  literal-limit tests.
- **#077.** The row ("9999999999.999999", "999999.999999", "9999999999989999.000000000001") is added.
- **#068.** Rename-only and birthdate-only updates leave the other field unchanged.
- **#069.** An unknown owner name on Investment Account and Property updates gives
  PersonNameDoesntMatchId, and the owners are unchanged.
- **#016.** A mortgage set given as {existing, new} links both.
- **#075.** Exact (security, basis method) pairs, and the account re-read as Roth.
- **#018** (also #078 and #095). A disposed Property's mortgage is listed once among liabilities at
  its balance and subtracted once. The Property is absent from the owned Properties.
- **#079.** The 8.5 rows test derives its lists from `fixture.Data`.
- **#080.** A posting to F-5311 is in the F-5310, F-5300 and F-5000 roll-ups.
- **#081.** An as-of date before a retired account's active end still lists the account, at its
  current-date balance.
- **#082.** The integrity route returns a deactivated account with a balance: code, name, end,
  balance and entry IDs.
- **#083.** The titles are asserted, and 3.1 is added to the 7.7 test's name.
- **#084.** The 1.11 test uses an account whose only lines fall after the as-of date.

**Existing tests renamed under a row** (none of them were placeholders):

- The REQ-CR-5.4 "exact reverse" test is now "…sorted descending returns those keys in exactly the
  reverse order". The rules themselves don't reverse, because ties stay ascending by name.
- The #145 priority-order test.
- The #079 rows test: "each Liability account not a mortgage of a Property owned on the date".
- The #083 header tests.
- The #084 1.11 test.
- The #027, #028, #055 and #088 tests.

### 8.5 Mechanical test edits

**From the Src work:**

- **Part A:**
  - Property payloads moved to the asset-account set shape: PositionsRoutes, PropertyMaintenance,
    PositionsFixture, Isolated RealEstate and Investments.
  - Cleanup and TestDataStage cover the new table.
  - StageEntryFetching's source-filter tests pass names. One was renamed to "REQ-STG-10.2 for each
    exact-match filter (source file, FI reference, memo)…" because the source is no longer one of
    its exact-match text filters.
- **Part B:** the DalTests 27-case theory is deleted (#042).
- **Part C:**
  - The test adapters call the new ID-taking APIs: PersonMaintenance, SecurityMaintenance,
    HoldingMaintenance, InvestmentAccountMaintenance, PropertyMaintenance, AccountSnapshotRecording,
    InvestmentWealthHistory, and the `createSecurity` helper in InterfaceBridge/PositionsRoutes.
  - A `PositionsLookups` module is added in PositionsFixture.

**From the test work:**

- **Staging fixture repair.** grp-010 and grp-pk6 each move to a second file, through a new
  `ingestFilesDeduplicateAndClassify` helper. The pair tests still mean what they meant; the pk5/pk6
  test now asserts exactly which one is flagged, where before it accepted either.
- The existing wealth-history per-account test gains the 0.00 entries that REQ-RPT-9.2 now requires.
- Opens added in TrialBalance, ReportRoutes, NetWorth and Reports/Program.
- The roll-up derivation moved into a shared helper.
- The InvoiceStateByDirection helper takes a direction.

### 8.6 Spec findings

None from the tests: no requirement was found wrong or untestable.

### 8.7 For Dan's attention

- **LookupCache hazard (pre-existing).** The route-lifetime caches, e.g. Account code→ID, are
  process-global. In tests they outlive rolled-back transactions, so a code cached during one
  rolled-back test can point at a row that no longer exists in the next.
  - It surfaced during item 21 as a failure in "REQ-POS-11.6 a Property Create payload…".
  - The test adapters now avoid it: they don't round-trip a rolled-back ledger account through a
    cached converter, and route tests use committed-and-cleaned accounts.
  - The Person and Positions name→ID converters use uncached fetches.
  - The cache itself is unchanged. In production one process is one operation, so it is a test
    hazard only.
- **Outside §3's write list:** the `Skills/` script edit for item 19, and the ArchiMate model update
  the plan sanctions.
- **#145 and REQ-CR-5.4** agree with each other.

### 8.8 Watched failures (constraint 3)

Every new or changed assertion was made to fail by perturbing its expected value, then restored and
re-run green. Pure re-citations and deletions were not perturbed. The logs follow, one per test area.

#### 8.8.1 Positions

#### Watch log: POSITIONS worker (audit 2026-10-04a remediation)

Each new or changed assertion was perturbed, the test run, the failure read, then restored and re-run green.
Two rounds: round 1 perturbed the primary assertion of each test, round 2 the after-the-refusal or second assertion.
Integrated runs on database sonofleo_test_pos. After restoring, all 208 tests in the touched classes passed.

##### HoldingMaintenanceTests.REQ-POS-11.10 deleting a Holding that no snapshot line references removes it, and listing the account's Holdings no longer shows that Security while the account's other Holdings remain
- Round 1. Perturbed: expected list after the delete: dropped the account's other Holding (bond fund)
- Failure:
```
Assert.Equal() Failure: Collections differ
Expected: [Tuple ("Alex Roth IRA", "Example Total Market Index Fund", null)]
Actual:   [Tuple ("Alex Roth IRA", "Example Bond Fund", null), Tuple ("Alex Roth IRA", "Example Total Market Index Fund", null)]
```

##### HoldingMaintenanceTests.REQ-POS-11.10 deleting a Holding that a snapshot line references is rejected with a typed error naming the account and the Security, and the Holding remains
- Round 1. Perturbed: expected error payload: Security international -> total market
- Failure:
```
Assert.Equal() Failure: Values differ
Expected: Tuple ("Alex Brokerage", "Example Total Market Index Fund")
Actual:   Tuple ("Alex Brokerage", "Example International Index Fund")
```

##### HoldingMaintenanceTests.REQ-POS-11.10 deleting a Holding that a snapshot line references is rejected with a typed error naming the account and the Security, and the Holding remains
- Round 2. Perturbed: expected Holdings after the refusal: dropped the international Holding
- Failure:
```
Assert.Equal() Failure: Collections differ
Expected: [Tuple ("Alex Brokerage", "Example Total Market Index Fund", Some(AverageCost))]
Actual:   [Tuple ("Alex Brokerage", "Example International Index Fund", Some(SpecificLot)), Tuple ("Alex Brokerage", "Example Total Market Index Fund", Some(AverageCost))]
```

##### HoldingMaintenanceTests.REQ-POS-11.10 REQ-SYS-6.2 deleting a Holding of a Security the account does not hold fails with a typed not-found error naming Holding, the account and the Security
- Round 1. Perturbed: expected error payload: Security total market -> bond fund
- Failure:
```
Assert.Equal() Failure: Values differ
Expected: Tuple ("Jordan Custodial", "Example Bond Fund")
Actual:   Tuple ("Jordan Custodial", "Example Total Market Index Fund")
```

##### InvestmentAccountMaintenanceTests.REQ-POS-5.3 changing the tax treatment of a TaxDeferred account with Holdings to Roth succeeds, since its Holdings carry no basis method under either
- Round 1. Perturbed: #075: expected (security, basis method) pairs: bond fund None -> Some AverageCost
- Failure:
```
Assert.Equal() Failure: Collections differ
Expected: [Tuple ("Example Bond Fund", Some(AverageCost)), Tuple ("Example Total Market Index Fund", null)]
Actual:   [Tuple ("Example Bond Fund", null), Tuple ("Example Total Market Index Fund", null)]
```

##### InvestmentAccountMaintenanceTests.REQ-POS-11.3 REQ-PER-2.4 an Investment Account update giving an owner name that matches no Person fails with a typed error naming that name, and the owners are unchanged
- Round 1. Perturbed: #069: expected error name "Ghost Example" -> "Ghost Examples"
- Failure:
```
Assert.Equal() Failure: Strings differ
                        ↓ (pos 13)
Expected: "Ghost Examples"
Actual:   "Ghost Example"
```

##### InvestmentAccountMaintenanceTests.REQ-POS-11.3 REQ-PER-2.4 an Investment Account update giving an owner name that matches no Person fails with a typed error naming that name, and the owners are unchanged
- Round 2. Perturbed: #069: expected owners after the refusal [Alex; Sam] -> [Alex]
- Failure:
```
Assert.Equal() Failure: Collections differ
Expected: ["Alex Example"]
Actual:   ["Alex Example", "Sam Example"]
```

##### InvestmentAccountMaintenanceTests.REQ-POS-5.5 changing a Roth account's tax treatment to TaxDeferred while three of its snapshots, recorded out of date order, carry a contribution basis is rejected with a typed error naming the account and the earliest and latest of those snapshot dates, and the account stays Roth
- Round 1. Perturbed: expected (account, earliest, latest) (d1, d3) -> (d3, d2), the first and last recorded
- Failure:
```
Assert.Equal() Failure: Values differ
Expected: Tuple ("Basis Roth", Monday, 10 August 2026, Friday, 10 July 2026)
Actual:   Tuple ("Basis Roth", Wednesday, 10 June 2026, Monday, 10 August 2026)
```

##### InvestmentAccountMaintenanceTests.REQ-POS-5.5 changing a Roth account's tax treatment to TaxDeferred while three of its snapshots, recorded out of date order, carry a contribution basis is rejected with a typed error naming the account and the earliest and latest of those snapshot dates, and the account stays Roth
- Round 2. Perturbed: expected tax treatment after the refusal Roth -> TaxDeferred
- Failure:
```
Assert.Equal() Failure: Values differ
Expected: TaxDeferred
Actual:   Roth
```

##### InvestmentAccountMaintenanceTests.REQ-POS-5.5 a Roth account with snapshots, none of which carries a contribution basis, changes to TaxDeferred and reads back TaxDeferred
- Round 1. Perturbed: expected read-back treatment TaxDeferred -> Roth
- Failure:
```
Assert.Equal() Failure: Values differ
Expected: Roth
Actual:   TaxDeferred
```

##### AccountSnapshotRecordingTests.REQ-POS-7.5 listing an account's snapshot dates with the end date the day before the begin date fails with a typed error naming both dates
- Round 1. Perturbed: expected error payload (begin, end) swapped
- Failure:
```
Assert.Equal() Failure: Values differ
Expected: Tuple (Thursday, 09 July 2026, Friday, 10 July 2026)
Actual:   Tuple (Friday, 10 July 2026, Thursday, 09 July 2026)
```

##### SecurityMaintenanceTests.REQ-POS-11.1 REQ-SYS-6.1 renaming a Dimension Value to the name it already has succeeds, the value is stored in its dimension under that same name, and its modified-at advances
- Round 1. Perturbed: expected modified-at: the operation's instant -> the value's modified-at before the rename
- Failure:
```
Assert.Equal() Failure: Values differ
Expected: 2026-10-05T14:14:10Z
Actual:   2026-10-05T14:14:15Z
```

##### SecurityMaintenanceTests.REQ-POS-11.1 REQ-SYS-6.1 renaming a Dimension Value to the name it already has succeeds, the value is stored in its dimension under that same name, and its modified-at advances
- Round 2. Perturbed: expected Region values [Domestic; International] -> [Domestic]
- Failure:
```
Assert.Equal() Failure: Collections differ
Expected: ["Domestic"]
Actual:   ["Domestic", "International"]
```

##### PropertyMaintenanceTests.REQ-POS-11.6 an update giving a Property's mortgage accounts as the one it already has plus a new one leaves it linked to both
- Round 1. Perturbed: #016: expected mortgages: dropped F-2210 (the new one)
- Failure:
```
Assert.Equal() Failure: Collections differ
Expected: [Tuple ("F-2310", "Fixture Residence Mortgage")]
Actual:   [Tuple ("F-2210", "Mortgage Payable"), Tuple ("F-2310", "Fixture Residence Mortgage")]
```

##### PropertyMaintenanceTests.REQ-POS-11.6 REQ-PER-2.4 a Property update giving an owner name that matches no Person fails with a typed error naming that name, and the owners are unchanged
- Round 1. Perturbed: #069: expected error name "Ghost Example" -> "Ghost Examples"
- Failure:
```
Assert.Equal() Failure: Strings differ
                        ↓ (pos 13)
Expected: "Ghost Examples"
Actual:   "Ghost Example"
```

##### PropertyMaintenanceTests.REQ-POS-11.6 REQ-PER-2.4 a Property update giving an owner name that matches no Person fails with a typed error naming that name, and the owners are unchanged
- Round 2. Perturbed: #069: expected owners after the refusal [Alex; Sam] -> [Sam]
- Failure:
```
Assert.Equal() Failure: Collections differ
Expected: ["Sam Example"]
Actual:   ["Alex Example", "Sam Example"]
```

##### PropertyMaintenanceTests.REQ-POS-11.11 deleting a Property with no Valuation removes it with its owners and ledger links, after which its former asset and mortgage accounts can each be linked to another Property
- Round 1. Perturbed: setup: removed the delete (and the gone check), so the successor Property links the same accounts while the first still holds them
- Failure:
```
Ledger account T-1593 is already an asset account of Property "Short Lived Chalet".
```

##### PropertyMaintenanceTests.REQ-POS-11.11 deleting a Property that has a Valuation is rejected with a typed error naming the Property, and the Property, its owners and its ledger links remain
- Round 1. Perturbed: expected error name residence -> rental
- Failure:
```
Assert.Equal() Failure: Strings differ
           ↓ (pos 0)
Expected: "34 Example Avenue"
Actual:   "12 Example Street"
           ↑ (pos 0)
```

##### PropertyMaintenanceTests.REQ-POS-11.11 deleting a Property that has a Valuation is rejected with a typed error naming the Property, and the Property, its owners and its ledger links remain
- Round 2. Perturbed: expected Property after the refusal: asset accounts [F-1510] -> []
- Failure:
```
Assert.Equal() Failure: Values differ
Expected: Tuple ("12 Example Street", PrimaryResidence, [···], Friday, 05 June 2026, null, 400000.00, [···], [···])
Actual:   Tuple ("12 Example Street", PrimaryResidence, [···], Friday, 05 June 2026, null, 400000.00, [···], [···])
```

##### PropertyMaintenanceTests.REQ-POS-11.11 REQ-SYS-6.2 deleting a Property by a name that matches no Property fails with a typed not-found error naming Property and the name
- Round 1. Perturbed: expected error name "99 Missing Street" -> "99 Missing Road"
- Failure:
```
Assert.Equal() Failure: Strings differ
                      ↓ (pos 11)
Expected: "99 Missing Road"
Actual:   "99 Missing Street"
                      ↑ (pos 11)
```

##### OperationInstantAndAtomicityTests.REQ-SYS-3.2 for each of Person, Dimension Value, Security, Investment Account, Holding, Account Snapshot, Property and Valuation, creating one under a clock that advances on every read sets its created-at and modified-at both to the operation's initiation instant(kind: "Dimension Value")
- Round 1. Perturbed: expected modified-at: creating instant -> creating instant + 10 ticks (all eight cases failed; xUnit prints Instants to the second, so expected and actual look alike)
- Failure:
```
Assert.Equal() Failure: Values differ
Expected: Tuple (2026-10-05T14:14:14Z, 2026-10-05T14:14:14Z)
Actual:   Tuple (2026-10-05T14:14:14Z, 2026-10-05T14:14:14Z)
```

##### OperationInstantAndAtomicityTests.REQ-SYS-3.3 for each update to a Person, Dimension Value, Security, Investment Account, Holding, Account Snapshot (re-recorded), Property and Valuation (re-recorded), under a clock that advances on every read, the record's modified-at is set to the operation's initiation instant and its created-at is unchanged(kind: "Valuation")
- Round 1. Perturbed: setup: removed the update call (all eight cases failed: modified-at stayed the creating instant; xUnit prints Instants to the second)
- Failure:
```
Assert.Equal() Failure: Values differ
Expected: Tuple (2026-10-05T14:14:14Z, 2026-10-05T14:14:14Z)
Actual:   Tuple (2026-10-05T14:14:14Z, 2026-10-05T14:14:14Z)
```

##### OperationInstantAndAtomicityTests.REQ-SYS-3.3 for each update to a Person, Dimension Value, Security, Investment Account, Holding, Account Snapshot (re-recorded), Property and Valuation (re-recorded), under a clock that advances on every read, the record's modified-at is set to the operation's initiation instant and its created-at is unchanged(kind: "Valuation")
- Round 2. Perturbed: expected created-at: creating instant -> updating instant (all eight cases failed)
- Failure:
```
Assert.Equal() Failure: Values differ
Expected: Tuple (2026-10-05T14:18:23Z, 2026-10-05T14:18:23Z)
Actual:   Tuple (2026-10-05T14:18:23Z, 2026-10-05T14:18:23Z)
```

##### PositionsRoutesTests.REQ-POS-11.10 a Holding Delete payload removes only the Holding of the account and Security given: the Holding List route no longer returns it and still returns the account's other Holdings
- Round 1. Perturbed: expected List route after the delete: added back the deleted Holding
- Failure:
```
Assert.Equal() Failure: Collections differ
Expected: [Tuple ("Route Account c338adbcc30e45b7848ef9a68da29d8e", "Example International Index Fund", Some(SpecificLot)), Tuple ("Route Account c338adbcc30e45b7848ef9a68da29d8e", "Example Total Market Index Fund", Some(AverageCost))]
Actual:   [Tuple ("Route Account c338adbcc30e45b7848ef9a68da29d8e", "Example Total Market Index Fund", Some(AverageCost))]
```

##### PositionsRoutesTests.REQ-POS-11.11 a Property Delete payload removes only the Property named: the Property List route no longer returns it and still returns every other Property
- Round 1. Perturbed: expected List route names: dropped 7 Former Example Road
- Failure:
```
Assert.Equal() Failure: Collections differ
Expected: ["12 Example Street", "34 Example Avenue"]
Actual:   ["12 Example Street", "34 Example Avenue", "7 Former Example Road"]
```

##### PositionsRoutesTests.REQ-POS-11.6 REQ-POS-9.7 a Property Create payload with two asset accounts creates the Property linked to both, and the return and the Property List route each carry both codes with their names
- Round 1. Perturbed: expected asset accounts: dropped T-1595
- Failure:
```
Assert.Equal() Failure: Collections differ
Expected: [{ code = "T-1594"
  name = "Route House at Cost" }]
Actual:   [{ code = "T-1594"
  name = "Route House at Cost" }, { code = "T-1595"
  name = "Route House Improvements" }]
```

##### PositionsRoutesTests.REQ-POS-11.6 REQ-POS-9.7 a Property Update payload giving a new set of two asset accounts replaces the stored set, so the Property is linked to exactly those two and not to the one it had
- Round 1. Perturbed: expected asset accounts: added the replaced T-1596
- Failure:
```
Assert.Equal() Failure: Collections differ
Expected: [{ code = "T-1596"
  name = "Route Barn at Cost" }, { code = "T-1597"
  name = "Route Land at Cost" }, { code = "T-1598"
  name = "Route Building at Cost" }]
Actual:   [{ code = "T-1597"
```

##### PositionsRoutesTests.REQ-POS-9.7 a Property Create payload giving the same asset account code twice is rejected with a typed error naming the code, and no Property is created
- Round 1. Perturbed: expected code T-1599 -> T-1594
- Failure:
```
Assert.Equal() Failure: Values differ
Expected: Tuple ("Route Property 9448324331b4449e844880ec5b0f4125", "T-1594")
Actual:   Tuple ("Route Property 9448324331b4449e844880ec5b0f4125", "T-1599")
```

##### PositionsRoutesTests.REQ-POS-9.7 a Property Create payload naming as an asset account one already linked to another Property is rejected with a typed error naming the code and that Property, and no Property is created
- Round 1. Perturbed: expected linked Property residence -> rental
- Failure:
```
Assert.Equal() Failure: Values differ
Expected: Tuple ("F-1510", "34 Example Avenue")
Actual:   Tuple ("F-1510", "12 Example Street")
```

##### PositionsRoutesTests.REQ-POS-9.7 for each of an Asset account of a subtype other than FixedAsset and an account of a type other than Asset, a Property Create payload naming it as an asset account is rejected with a typed error naming the code and what is wrong, and no Property is created(code: "F-2220", accountType: "Liability", subtype: "LongTermLiability")
- Round 1. Perturbed: InlineData subtypes Cash -> Bank and CurrentLiability -> LongTermLiability (both cases failed)
- Failure:
```
Assert.Equal() Failure: Values differ
Expected: Tuple ("F-2220", "Liability", Some(LongTermLiability))
Actual:   Tuple ("F-2220", "Liability", Some(CurrentLiability))
Assert.Equal() Failure: Values differ
Expected: Tuple ("F-1270", "Asset", Some(Bank))
Actual:   Tuple ("F-1270", "Asset", Some(Cash))
```

##### PositionsRoutesTests.REQ-POS-9.8 a Property Create payload giving the same mortgage account code twice is rejected with a typed error naming the code, and no Property is created
- Round 1. Perturbed: expected code F-2230 -> F-2210
- Failure:
```
Assert.Equal() Failure: Values differ
Expected: Tuple ("Route Property 39886d91770b4eb1a56356964956ab3f", "F-2210")
Actual:   Tuple ("Route Property 39886d91770b4eb1a56356964956ab3f", "F-2230")
```

##### PositionsRoutesTests.REQ-POS-7.1 REQ-SYS-6.1 an AccountSnapshot Record payload with an empty list of snapshots is rejected with a typed no-snapshots error
- Round 1. Perturbed: expected error case PositionsSnapshotListIsEmpty -> PositionsAccountSnapshotUpdateNoOp
- Failure:
```
Wrong error. At least one Account Snapshot must be given to record.
```

##### (isolated) REQ-SYS-1.1 for each Positions value ...
- Round 1. Perturbed: one expected case (Roth -> Hsa; Owners -> ByAccount)
- Failure:
```
Assert.Equal() Failure: Values differ
Expected: Hsa
Actual:   Roth
```

##### (isolated) REQ-SYS-1.1 every investment wealth grouping ...
- Round 1. Perturbed: one expected case (Roth -> Hsa; Owners -> ByAccount)
- Failure:
```
Assert.Equal() Failure: Values differ
Expected: ByAccount
Actual:   ByOwners
```

##### (isolated) Tests.Isolated.Model.Positions.PositionsComponent.REQ-SYS-1.1 every investment wealth grouping wrapped in whitespace parses to the same grouping as the bare value
- Round 2. Perturbed: the padded text given to the parser had "x" appended, so the padded parse is shown to run
- Failure:
```
System.Exception : Invalid investment wealth grouping of " 	Accountx  ".
```

##### (isolated) Tests.Isolated.Model.Positions.PositionsComponent.REQ-SYS-1.1 for each Positions value an operator gives as text (dimension, tax treatment, basis method, provenance, use), every allowed value wrapped in whitespace parses to the same case as the bare value
- Round 2. Perturbed: the padded text given to the parser had "x" appended, so the padded parse is shown to run
- Failure:
```
System.Exception : Invalid dimension of " 	InvestmentTypex  ".
```

##### Re-cited tests (names only, assertions unchanged)
- PositionsRoutes: three tests REQ-NGUI-1.4 -> REQ-NGUI-1.5 (#065); PropertyMaintenance: dropped REQ-POS-4.9 from the Property asset-link test; Valuation-delete miss REQ-SYS-6.1 -> REQ-SYS-6.2 (#181). No assertion changed, no watch needed.

#### 8.8.2 Reports

#### Watch log — REPORTS worker (DB sonofleo_test_rpt), 2026-10-05

Each entry: test, what was perturbed, the failure from the run. Every perturbation was restored
(`git checkout -- Tests`) and the subset re-run green (103/103) afterwards. Month-ends in the
output are this run's fixture dates (monthEnd4 = 2026-06-30 ... monthEnd1 = 2026-09-30).

##### Round 1 (expected values)

1. NetWorth `REQ-RPT-8.2 a Property linked to two asset accounts that both carry ledger balances ...`
   Perturbed expected net worth 357945.00 -> 507945.00 (value counted twice).
   `Assert.Equal() Failure: Values differ / Expected: 507945.00 / Actual: 357945.00`

2. NetWorth `REQ-RPT-8.5 the owned-property mortgages total sums the as-of balances ... excludes the mortgage of a Property disposed before the date`
   Perturbed 480000.00 -> 530000.00.
   `Assert.Equal() Failure: Values differ / Expected: 530000.00 / Actual: 480000.00`

3. NetWorth `REQ-RPT-8.5 REQ-RPT-8.2 the mortgage of a Property disposed of before the date is listed among the Liability accounts ...` (row #018)
   Perturbed listed balance 50000.00 -> 50001.00.
   `Assert.Equal() Failure: Collections differ / Expected: [Tuple ("RPT-8.5M", "Disposed property mortgage", 50001.00)] / Actual: [Tuple ("RPT-8.5M", "Disposed property mortgage", 50000.00)]`

4. NetWorth `REQ-RPT-8.5 counted ledger assets plus investments plus property values, less liabilities and less owned-property mortgages, equals the net worth ...`
   Perturbed the identity's expected value 207945.00 -> 687945.00 (mortgages not subtracted).
   `Assert.Equal() Failure: Values differ / Expected: 687945.00 / Actual: 207945.00`

5. NetWorth `REQ-RPT-8.5 the result carries the as-of date, and each counted Asset account and each Liability account not a mortgage of a Property owned on the date, ...` (row #079, derivation changed)
   Perturbed the derivation: excluded-mortgage set emptied.
   `Assert.Equal() Failure: Collections differ / Expected: [..., Tuple ("F-2230", "Fixture Loan Payable", 0), Tuple ("F-2310", "Fixture Residence Mortgage", 300000.00), ···] / Actual: [Tuple ("F-2000", "Liabilities", 0), Tuple ("F-2210", "Mortgage Payable", -25.00), Tuple ("F-2220", "Credit Card", 0), Tuple ("F-2230", "Fixture Loan Payable", 0)]`

6. InvestmentWealthHistory `REQ-RPT-9.2 grouped by account, each point's per-account totals and grand total equal the hand-summed ...` (repaired expectation)
   Perturbed: dropped the monthEnd4 Joint Brokerage 0.00 entry.
   `Assert.Equal() Failure: Collections differ / Expected: [Tuple (Tuesday, 30 June 2026, [···], 8300.00), ...] / Actual: [Tuple (Tuesday, 30 June 2026, [···], 8300.00), ...]` (the per-point maps differ)

7. InvestmentWealthHistory `REQ-RPT-9.2 grouped by account, over a range in which one account has holdings at the first month-end and none at the last, ...`
   Perturbed Some 0.00 -> None at the last point.
   `Expected: [Tuple (Friday, 31 July 2026, Some(300.00)), Tuple (Monday, 31 August 2026, null)] / Actual: [Tuple (Friday, 31 July 2026, Some(300.00)), Tuple (Monday, 31 August 2026, Some(0))]`

8. InvestmentWealthHistory `REQ-RPT-9.2 grouped by account, an account whose first holdings fall after the first month-end of the range appears at every earlier point with 0.00`
   Perturbed Some 0.00 -> None at the first point.
   `Expected: [Tuple (Tuesday, 30 June 2026, null), Tuple (Friday, 31 July 2026, Some(5500.00))] / Actual: [Tuple (Tuesday, 30 June 2026, Some(0)), Tuple (Friday, 31 July 2026, Some(5500.00))]`

9. PositionsReportRoutes `REQ-RPT-8.5 REQ-RPT-8.6 the rendered net worth Totals block shows the owned-property mortgages total, ...`
   Perturbed shown mortgages total 480000.00 -> 480001.00.
   `Assert.Equal() Failure: Values differ / Expected: 480001.00 / Actual: 480000.00`

10. PositionsReportRoutes `REQ-RPT-9.5 the rendered wealth history shows 0.00, not a blank cell, ...`
    Perturbed the International cell "0.00" -> "".
    `Expected: ["2026-09-30", "9,782.50", "", "3,075.29", "12,857.79"] / Actual: ["2026-09-30", "9,782.50", "0.00", "3,075.29", "12,857.79"]`

11. PositionsReportRoutes `REQ-RPT-9.5 REQ-RPT-3.1 the rendered wealth history header shows the begin date, end date and grouping, and no as-of date`
    Perturbed expected end date monthEnd2 -> monthEnd1.
    `Expected: ["2026-06-30", "2026-09-30"] / Actual: ["2026-06-30", "2026-08-31"]`

12. TrialBalance `REQ-RPT-1.5 an entry posted to an account three levels below a parent is in the rolled-up totals of its parent, grandparent and great-grandparent` (row #080)
    Perturbed: dropped the posted 412.37 from the expected debits.
    `Expected: [Tuple (AccountCode "F-5310", 0, 0), Tuple (AccountCode "F-5300", 0, 0), Tuple (AccountCode "F-5000", 444.51, 524.41)] / Actual: [Tuple (AccountCode "F-5310", 412.37, 0), Tuple (AccountCode "F-5300", 412.37, 0), Tuple (AccountCode "F-5000", 856.88, 524.41)]`

13. TrialBalance `REQ-RPT-1.11 an account whose only lines are dated after the as-of date appears with zero credits debits and net` (row #084)
    Perturbed expected debits 0 -> 61.50.
    `Assert.Equal() Failure: Values differ / Expected: 61.50 / Actual: 0`

14. BalanceSheetIntegrity `REQ-RPT-5.4 with an as-of date earlier than a retired account's active end, the account is still listed, at its balance as of the operation's current date` (row #081)
    Perturbed expected balance 30.00 -> 40.00.
    `Assert.Equal() Failure: Values differ / Expected: { amount = 40.00M } / Actual: { amount = 30.00M }`

15. ReportRoutes `REQ-RPT-6.4 REQ-RPT-5.4 the integrity report route in data-only mode returns a deactivated account holding a balance ...` (row #082)
    Perturbed balance 30.00 -> 70.00.
    `Expected: Tuple ("RPT-6.4", "Report route retired asset", Friday, 25 September 2026, 70.00) / Actual: Tuple ("RPT-6.4", "Report route retired asset", Friday, 25 September 2026, 30.00)`

16. ReportRoutes `REQ-RPT-6.4 REQ-RPT-3.1 the period activity rendered report header shows the report title and the begin and end dates of the range` (row #083)
    Perturbed title "Period Activity" -> "Period activity".
    `Assert.Equal() Failure: Strings differ / Expected: "Period activity" / Actual: "Period Activity"`

17. ReportRoutes `REQ-RPT-7.7 REQ-RPT-3.1 pre-posting review report mode writes an HTML file whose header shows the report title, ...` (row #083)
    Perturbed title "Pre-Posting Review" -> "Pre-posting Review".
    `Assert.Equal() Failure: Strings differ / Expected: "Pre-posting Review" / Actual: "Pre-Posting Review"`

18. SonOfLeoCli `REQ-NGUI-3.8 The domain argument is case sensitive` (row #066)
    Perturbed expected message to "Unknown command: Account FetchAll".
    `Assert.Equal() Failure: Strings differ / Expected: "Unknown command: Account FetchAll" / Actual: "Unknown command: account FetchAll"`

19. SonOfLeoCli `REQ-NGUI-3.8 The verb argument is case sensitive` (row #066)
    Perturbed expected message to "Unknown command: Account FetchAll".
    `Assert.Equal() Failure: Strings differ / Expected: "Unknown command: Account FetchAll" / Actual: "Unknown command: Account fetchAll"`

20. Reports CLI `REQ-NGUI-4.2 The name argument is case sensitive` (row #066)
    Perturbed expected message to "Unknown report: TrialBalance.".
    `Assert.Equal() Failure: Strings differ / Expected: "Unknown report: TrialBalance." / Actual: "Unknown report: trialbalance."`

21. Reports CLI `REQ-NGUI-4.4 System responds with the payload via stdout upon success` (row #067)
    Perturbed the oracle: route rows with the first dropped.
    `Expected: [Tuple ("F-1250", "Roth IRA", 50.00, 0), ...] / Actual: [Tuple ("F-1000", "Assets", 406521.38, 1411.38), Tuple ("F-1250", "Roth IRA", 50.00, 0), ...]`

##### Round 2 (setup and secondary assertions)

22. NetWorth `REQ-RPT-8.2 a Property linked to two asset accounts ...`
    Removed the setup: the Property created with no asset accounts.
    `Assert.DoesNotContain() Failure: Item found in collection / Collection: [···, "F-1275", "F-1280", "F-1290", "RPT-8.2A", "RPT-8.2B"] / Found: "RPT-8.2A"`

23. NetWorth `REQ-RPT-8.5 the owned-property mortgages total ... excludes the mortgage of a Property disposed before the date`
    Setup: the mortgaged Property not disposed (disposal None).
    `Assert.Equal() Failure: Values differ / Expected: 480000.00 / Actual: 530000.00`

24. NetWorth `REQ-RPT-8.5 REQ-RPT-8.2 the mortgage of a Property disposed of before the date is listed ...` (row #018)
    Same setup change (disposal None).
    `Assert.Equal() Failure: Collections differ / Expected: [Tuple ("RPT-8.5M", "Disposed property mortgage", 50000.00)] / Actual: []`

25. PositionsReportRoutes `REQ-RPT-8.5 REQ-RPT-8.6 the rendered net worth Totals block ...`
    Dropped the mortgages term from the rendered identity.
    `Assert.Equal() Failure: Values differ / Expected: 207945.00 / Actual: 687945.00`

26. PositionsReportRoutes `REQ-RPT-9.5 REQ-RPT-3.1 the rendered wealth history header shows the begin date, end date and grouping, and no as-of date`
    Perturbed expected grouping "TaxTreatment" -> "Region".
    `Assert.Contains() Failure: Sub-string not found / String: "From  \n2026-06-30\n  to  \n2026-08-31\n , by"··· / Not found: "Region"`

#### 8.8.3 Cash flow and classification

#### Watch log: CashFlow and Classification worker

##### Tests.Isolated CashFlowComponent: REQ-CF-5.20 REQ-SYS-1.1 an external invoice ID of only whitespace is rejected with a typed empty-ID error
Perturbed: expected raw "   " -> "  ".
  Assert.Equal() Failure: Strings differ / Expected: "  " / Actual: "   " (pos 2)

##### Tests.Isolated CashFlowComponent: REQ-CF-5.20 an external invoice ID of 101 characters is rejected ..., and one of exactly 100 characters is accepted ...
Perturbed: expected limit 100 -> 99.
  Assert.Equal() Failure: Values differ / Expected: 99 / Actual: 100

##### Tests.Isolated CashFlowComponent: REQ-SYS-1.1 for each CashFlow value an operator gives as text, every allowed value wrapped in whitespace parses to the same case as the bare value
Perturbed: expected flow directions [Income; Outgo] -> [Outgo; Income].
  Assert.Equal() Failure: Collections differ / Expected: [Outgo, Income] / Actual: [Income, Outgo]

##### Tests.Isolated ClassificationRuleComponent: REQ-SYS-1.1 for each classification rule value an operator gives as text, every allowed value wrapped in whitespace parses to the same case as the bare value
Perturbed: expected connectors [And; Or] -> [Or; And].
  Assert.Equal() Failure: Collections differ / Expected: [Or, And] / Actual: [And, Or]

##### DerivedStateRules: REQ-CF-9.10 raising a FullyPaid, PostedToLedger Invoice's amount above its Payments ...
Perturbed: expected after-states (PartiallyPaid, PartiallyPosted) -> (FullyPaid, PartiallyPosted).
  Assert.Equal() Failure: Values differ / Expected: Tuple (FullyPaid, PartiallyPosted) / Actual: Tuple (PartiallyPaid, PartiallyPosted)

##### DerivedStateRules: REQ-CF-9.10 adding a new unpaid Invoice to a fulfilled Instance makes the Instance no longer fulfilled ...
Perturbed: Assert.False(fulfilled) -> Assert.True(fulfilled).
  Assert.True() Failure / Expected: True / Actual: False

##### DerivedStateRules: REQ-CF-9.10 cancelling an Instance's one unpaid Invoice ... makes the Instance fulfilled ...
Perturbed: Assert.True(fulfilled) -> Assert.False(fulfilled).
  Assert.False() Failure / Expected: False / Actual: True

##### Cancellation: REQ-CF-4.12 cancelling an Instance one of whose Invoices is already cancelled leaves that Invoice's own reason note ...
Perturbed: expected Invoice notes [invoiceNote; instanceNote] -> [instanceNote; instanceNote].
  Assert.Equal() Failure: Collections differ / Expected: [Some(Agreement wound up early), Some(Agreement wound up early)] / Actual: [Some(Forgiven by the counterparty), Some(Agreement wound up early)]

##### ClassificationRuleCrud: REQ-CR-5.4 for each of account code ascending and descending, rules sharing an account code come back ordered by rule name
Perturbed: expected tie order under descending code sort, names ascending -> names descending.
  Assert.Equal() Failure: Collections differ / Expected: ["TestArchiveBank two-group rule then 5650", "Source = TestSplitBank && Credit then 5650", ...] / Actual: ["Acme Insurance to 5650", "Source = MixedOutcomeBank && Debit then 5650", ...]

##### ClassificationRuleCrud: REQ-CR-5.4 for each of priority ascending and descending, rules sharing a priority come back ordered by rule name
Perturbed: expected descending order (-priority, name) -> (priority desc, name desc).
  Assert.Equal() Failure: Collections differ / Expected: [..., "Source = TestBank then 5300", "Zed CR-5.4 at 500", "Acme Insurance to 5650", ...] / Actual: [..., "Source = TestBank then 5300", "Acme Assurance CR-5.4", "Acme Insurance to 5300", ...]

##### ClassificationRuleCrud (#145, tightened and renamed): REQ-CR-5.4 fetchRulesFiltered sorted by priority ascending returns the fixture's rules in exactly increasing priority, rules tied at the same priority ordered by rule name
Perturbed: expected order (priority asc, name asc) -> (priority asc, name desc).
  Assert.Equal() Failure: Collections differ / Expected: ["Source = TestCreditCardCo && Desc = Rei then 5650", "Source = TestSplitBank && Debit then 5350", "Source = TestSplitBank && Credit then 5650", ...] / Actual: ["Source = TestCreditCardCo && Desc = Rei then 5650", "Source = TestSplitBank && Credit then 5650", "Source = TestSplitBank && Debit then 5350", ...]

##### ClassificationRuleCrud (#038): REQ-CR-5.3 fetchRulesFiltered by source pattern fragment returns the rule with a Source match carrying it in chainOne and the rule with one in chainTwo, and not the rule carrying it only in a Description match
Perturbed: expected second rule "chainTwo Source" -> "Description only".
  Assert.Equal() Failure: Collections differ / Expected: ["CR-5.3 CR5325a0cfd557 chainOne Source", "CR-5.3 CR5325a0cfd557 Description only"] / Actual: ["CR-5.3 CR5325a0cfd557 chainOne Source", "CR-5.3 CR5325a0cfd557 chainTwo Source"]

##### CashFlowRoutes: REQ-CF-14.8 an UpdateAgreement payload naming a Payment Agreement of a different Master Agreement is rejected ...
Perturbed: expected (other leg id, agreement id) -> swapped.
  Assert.Equal() Failure: Values differ / Expected: Tuple (fc4462c4-..., 539be509-...) / Actual: Tuple (539be509-..., fc4462c4-...)

##### InvoiceStateByDirection (#033): REQ-CF-5.10 a new Instance on an Income agreement carrying an Invoice in an Outgo state is rejected naming the state (both rows)
Perturbed: expected direction "Income" -> "Outgo".
  (state: "InvoiceExpected") Assert.Equal() Failure: Strings differ / Expected: "Outgo" / Actual: "Income"
  (state: "InvoiceReceived") Assert.Equal() Failure: Strings differ / Expected: "Outgo" / Actual: "Income"

##### PaymentAgreementUpdate (#035): REQ-CF-14.8 REQ-CF-3.7 REQ-CF-3.9 for each of an expected amount of 0.00 and a name another Payment Agreement holds, an UpdateAgreement payload updating ... is refused, and both agreements are unchanged
Perturbed: zeroAmount expected (leg, 0.00) -> (leg, 0.01); duplicateName accepted error DalErrorDuringNonQueryExecution -> DalErrorDuringReaderQueryExecution.
  (zeroAmount) Assert.Equal() Failure: Values differ / Expected: Tuple (c959adb0-..., 0.01) / Actual: Tuple (c959adb0-..., 0.00)
  (duplicateName) Wrong error. DalError.DalErrorDuringNonQueryExecution: ... 23505: duplicate key value violates unique constraint "payment_agreement_payment_agreement_name_key"

##### PaymentAgreementUpdate (#035): REQ-CF-14.9 REQ-CF-3.7 REQ-CF-3.9 for each of ..., an UpdateAgreement payload adding a Payment Agreement with it is refused, and the Master Agreement keeps exactly its original legs
Perturbed: same as above (0.00 -> 0.01; NonQuery -> ReaderQuery).
  (zeroAmount) Assert.Equal() Failure: Values differ / Expected: 0.01 / Actual: 0.00
  (duplicateName) Wrong error. DalError.DalErrorDuringNonQueryExecution: ... 23505: duplicate key value violates unique constraint "payment_agreement_payment_agreement_name_key"

##### MaintenanceOperations (#037): REQ-CF-14.7 for every route whose payload names a Payment Agreement (CreateInstance, CreateInvoice, UpdateAgreement) ... (route: "UpdateAgreement")
Perturbed: the name check made to reject the UpdateAgreement row (n = name && route <> "UpdateAgreement").
  Assert.True() Failure / Expected: True / Actual: False

All perturbations restored (git checkout); the same subset re-ran green before perturbing (161 passed) and is re-run green below.

#### 8.8.4 Ingestion

#### Watch log — ingestion/staging worker (DB sonofleo_test_stg)

Every perturbation below was made, run, read, then reverted (`git checkout -- Tests`), and the suite re-run green.

##### StageEntryIngestion.fs

###### REQ-STG-1.18 a file in which two groups share a source and fi_reference is rejected ... no entry from the file is staged (new)
- Perturbed line numbers expected [1;2;5;6] -> [1;2;5;7]:
  `Assert.Equal() Failure: Collections differ  Expected: [1, 2, 5, 7]  Actual: [1, 2, 5, 6]`
- Perturbed named groups ["grp-key-a";"grp-key-b"] -> ["grp-key-a";"grp-fine"]:
  `Expected: ["grp-key-a", "grp-fine"]  Actual: ["grp-key-a", "grp-key-b"]`
- Flipped `Assert.Empty(staged)` to `Assert.NotEmpty(staged)` (shows the read-back really is empty):
  `Assert.NotEmpty() Failure: Collection was empty`

###### REQ-STG-7.2 entries sharing only source or only fi reference gain no Duplicate transition ... (changed: pk6 now in a second file; exact member flagged)
- Perturbed "Partial key both shared one" expected count 0 -> 1:
  `Assert.Equal() Failure: Values differ  Expected: 1  Actual: 0`

###### REQ-STG-7.3 dedup does not flag entry matching voided JE external reference (row #044)
- Perturbed duplicateTransitionCount expected 0 -> 1: `Expected: 1  Actual: 0`
- Perturbed latest status Classified -> NoMatch: `Expected: NoMatch  Actual: Classified`

###### REQ-STG-7.2 an entry repeating the key of a Posted entry whose journal entry has since been voided is flagged Duplicate ... (new, row #048)
- Perturbed newcomer duplicateTransitionCount 1 -> 0: `Expected: 0  Actual: 1`
- Perturbed precondition Posted -> Classified: `Expected: Classified  Actual: Posted`

###### REQ-STG-7.2 an entry repeating the key of an earlier entry the operator set to Duplicate is flagged Duplicate ... (new, row #048)
- Perturbed newcomer duplicateTransitionCount 1 -> 0: `Expected: 0  Actual: 1`
- Perturbed original's latest mechanism Operator -> Deduplicator: `Expected: Deduplicator  Actual: Operator`

##### StageEntryFetching.fs

###### REQ-STG-10.6 an entry returned by fetchFiltered carries its header fields, all of its lines, and all of its status transitions (row #045)
- Perturbed transition sequence (Some Ingested, Classified, Classifier) -> (Some Ingested, NoMatch, Classifier):
  `Expected: [Tuple (null, Ingested, StageIngestion), Tuple (Some(Ingested), NoMatch, Classifier)]  Actual: [... Tuple (Some(Ingested), Classified, Classifier)]`
- Perturbed source file -> "/tmp/stg-test-checking-second.jsonl":
  `Expected: "/tmp/stg-test-checking-second.jsonl"  Actual: "/tmp/stg-test-checking.jsonl"`
- Perturbed expected line memos to None:
  `Expected: [Tuple (187.50, "Credit", Some(AccountId ...), null), ...  Actual: [Tuple (187.50, "Credit", Some(AccountId ...), Some(LineMemo "State withholding")), ...`

##### StageEntryPosting.fs

###### REQ-STG-9.3 posted JE source is one fixed provenance label whatever institution the entry came from (row #043)
- Perturbed fromBank label -> "Data ingestion importX": `Expected: Some(Data ingestion importX)  Actual: Some(Data ingestion import)`
- Perturbed fromSavings label the same way: `Expected: Some(Data ingestion importX)  Actual: Some(Data ingestion import)`

##### StagingIngestionRules.fs

###### REQ-STG-1.17 for each of group_id, entry_date, line_type, fi_source and fi_reference, a file whose records spell that required property by its column name ... (row #050)
- Appended "X" to the expected missing-field detail; all five rows red, e.g.
  `Strings differ  Expected: ···"Contracts+BaseStageRawRowInput: fiSourceX"  Actual: ···"nContracts+BaseStageRawRowInput: fiSource"`
  (likewise entryTypeX/entryType, fiReferenceX/fiReference, baseStageEntryGroupIdX/baseStageEntryGroupId, entryDateX/entryDate)

##### IngestionRoutes.fs

###### REQ-STG-8.1 REQ-STG-8.3 REQ-STG-8.4 PostStageEntries shadow route ... leaves ledger and staging untouched (row #049)
- Inserted a committed operator status change (Classified -> Reviewed) on one entry between the before snapshot and the after snapshot:
  `Assert.Equal() Failure: Collections differ  Expected: [{ stageEntryHeader = { sourceFile = SourceFile "/tmp/sonofleo-route-tests/import/ingestion-route-shadow-post.jsonl" ...` (actual shows `currentStatus = Some Reviewed`, `toStatus = Reviewed`)

###### REQ-STG-10.1 REQ-STG-10.6 FetchStageEntryFiltered route returns the staged entry with its lines and status transitions intact (row #045)
- Perturbed second transition mechanism "Classifier" -> "Operator":
  `Expected: [Tuple (null, "Ingested", "StageIngestion"), Tuple (Some(Ingested), "Classified", "Operator")]  Actual: [..., "Classifier")]`
- Perturbed debit account F-5300 -> F-5301:
  `Expected: [Tuple (18.00, "Credit", Some(F-1270)), Tuple (18.00, "Debit", Some(F-5301))]  Actual: [..., Some(F-5300))]`
- Perturbed header status "Classified" -> "Reviewed": `Expected: Some(Reviewed)  Actual: Some(Classified)`

###### REQ-STG-10.2 FetchStageEntryFiltered route resolves an ingestion source name to the source whose entries it returns (new, A.3.11)
- Swapped TestSavings expected id to the TestBank entry's: `Collections differ  Expected: [5411578b-...]  Actual: [a21af977-...]`
- Swapped TestBank expected id to the TestSavings entry's: `Collections differ  Expected: [88eeceb3-...]  Actual: [95b8a9a2-...]`

###### REQ-STG-10.7 FetchStageEntryFiltered route rejects an ingestion source name matching no source with a typed error naming that name ... (new)
- Perturbed expected name -> "NoSuchIngestionBankX": `Strings differ  Expected: "NoSuchIngestionBankX"  Actual: "NoSuchIngestionBank"`

##### Tests.Isolated StageEntryStatusTransition.fs

###### REQ-STG-2.6 SourceFile.create rejects an empty or whitespace-only source file with the empty-source-file error (new, row #012)
- Changed expected case to IngestionSourceFileTooLong; both rows red:
  `Wrong error: Ingestion source file cannot be empty. Provided value is .` / `... Provided value is    .`

###### REQ-SYS-1.1 for each data ingestion value an operator gives as text, every allowed value wrapped in whitespace parses to the same case as the bare value (new)
- Mapped "Classified" -> NoMatch in the expected table: `(kind: "status", text: "Classified")  Expected: NoMatch  Actual: Classified`
- Mapped "Operator" -> LedgerPoster: `(kind: "mechanism", text: "Operator")  Expected: LedgerPoster  Actual: Operator`

#### 8.8.5 Ledger core, JE, DAL, Money, Person

#### Watch log — core worker (DB sonofleo_test_core)

Each perturbation was applied, the test run and its failure read, then the file restored with a checkout of the
committed version and the test re-run green.

##### Batch 1

Perturbations, one per test:
- REQ-JE-3.5 FI-only / reference-only (JournalEntryFetching): expected header-id set minus its smallest member
- REQ-JE-3.2 closed-period header (Model/JournalEntryHeader) and FetchById route: expected description + " PERTURBED"
- REQ-JE-2.13/2.11 orchestrator happy path: expected debit amount 86.04 -> 86.05
- REQ-JE-2.5/1.11 period derivation: expected period looked up for the day before the first day / after the last day
- REQ-AC-2.7 theory: expected parent = revenue4000 instead of the created parent
- REQ-AC-4.2 equal-to-begin: expected stored end = begin + 1 day
- REQ-AC-4.3 theory: expected error case AccountNonZeroBalanceBeforeDeactivation
- REQ-AC-3.12 unfiltered: expected rows minus the first line-less account
- REQ-AC-3.13 totals: expected debits of account 1 + 1.00
- REQ-AC-3.13 normal-balance: expected expense net = amount + 1.00
- REQ-DAL-2.2 two rows: expected count 3
- REQ-DAL-2.4 fresh caches: expected idle-in-transaction count 1
- REQ-FP-3.4: expected keys minus the first
- REQ-FP-4.1 / 4.2: expected id = the other fixture period
- REQ-PER-2.2 rename only: expected birthdate + 1 day; birthdate only: expected name + "x"
- REQ-PER-1.4 route: expected type name PersonReturn
- REQ-PER-1.1 Create route whitespace name: expected raw " \t" (trailing space dropped)
- REQ-QP-3.5 extreme row: expected ...000000000002

Failure output (Integrated, 24 failed = the 24 perturbed cases; Isolated QP row below):

##### Tests.Integrated.InterfaceBridge.JournalEntryRoutes+JournalEntryRouteTests.REQ-JE-3.2 FetchById route happy path
Assert.Equal() Failure: Strings differ
                              ↓ (pos 19)
Expected: "Basic journal entry PERTURBED"
Actual:   "Basic journal entry"

##### Tests.Integrated.CrossDomainOrchestration.JournalEntryCreationTests.REQ-JE-2.5 REQ-JE-1.11 an entry posted on the first or last day of an open fiscal period is assigned the fixture period whose start and end dates contain that day(day: "first")
Assert.Equal() Failure: Values differ
Expected: FiscalPeriodId 25d01bcc-bb84-459a-9043-f3bf4767925e
Actual:   FiscalPeriodId 1b95b17b-7c88-4625-b28b-2d5cf1eb0657

##### Tests.Integrated.InterfaceBridge.PersonRoutes+PersonRoutesTests.REQ-PER-2.1 REQ-PER-1.1 a Person Create payload with a whitespace-only name is rejected with a typed empty-name error, and no Person is created
Assert.Equal() Failure: Strings differ
Expected: " \t"
Actual:   " \t "
              ↑ (pos 2)

##### Tests.Integrated.CrossDomainOrchestration.AccountDeactivationTests.REQ-AC-4.3 deactivateAccount rejects a parent whose only child's active end is today or later, the child being active as of the current date(childEndOffsetDays: 0)
Wrong error type. Expected LedgerError.AccountNonZeroBalanceBeforeDeactivation. Got LedgerError.AccountActiveChildrenBeforeDeactivation: Account cb3569e2-4a7f-4167-bcfb-3d913926b546 deactivation failed because one or more child account records is active.

##### Tests.Integrated.CrossDomainOrchestration.AccountBalanceTests.REQ-AC-3.13 the net balances of a debit-normal and a credit-normal account each holding one side of an entry both equal its amount in normal-balance orientation
Assert.Equal() Failure: Values differ
Expected: 201.00
Actual:   200.00

##### Tests.Integrated.CrossDomainOrchestration.AccountActivityTests.REQ-AC-3.12 fetchFiltered with no filters set returns one row per fixture journal entry line and one line-less row per account without lines
Assert.Equal() Failure: Collections differ
                                                                                                                                                                                                                                                              ↓ (pos 27)
Expected: [···, Tuple (AccountId 2de0191c-8f97-42f9-84fb-ee2f6d842d17, Some(JournalEntryLineId f1b076b2-1c3c-4922-866e-e36eca642ffc)), Tuple (AccountId 2de0191c-8f97-42f9-84fb-ee2f6d842d17, So

##### Tests.Integrated.CrossDomainOrchestration.PersonMaintenance+PersonMaintenanceTests.REQ-PER-2.2 updating only a Person's name, its birthdate left unchanged, stores the new name and keeps the birthdate it had
Assert.Equal() Failure: Values differ
Expected: Tuple ("Jordan Renamed Only", Wednesday, 06 January 2010)
Actual:   Tuple ("Jordan Renamed Only", Tuesday, 05 January 2010)

##### Tests.Integrated.Model.Ledger.FiscalPeriodTests.REQ-FP-4.1 closeFiscalPeriod happy path
Assert.Equal() Failure: Values differ
Expected: FiscalPeriodId bc11f4f8-17a6-42ea-94da-cadd467834df
Actual:   FiscalPeriodId cc5a13b1-1cbe-4230-b5fb-b202c61f684d

##### Tests.Integrated.CrossDomainOrchestration.AccountDeactivationTests.REQ-AC-4.3 deactivateAccount rejects a parent whose only child's active end is today or later, the child being active as of the current date(childEndOffsetDays: 30)
Wrong error type. Expected LedgerError.AccountNonZeroBalanceBeforeDeactivation. Got LedgerError.AccountActiveChildrenBeforeDeactivation: Account 9069fc52-1c6f-4c60-8e6f-a83fc52fbecc deactivation failed because one or more child account records is active.

##### Tests.Integrated.Model.Ledger.FiscalPeriodTests.REQ-FP-4.2 reopenFiscalPeriod happy path
Assert.Equal() Failure: Values differ
Expected: FiscalPeriodId cc5a13b1-1cbe-4230-b5fb-b202c61f684d
Actual:   FiscalPeriodId bc11f4f8-17a6-42ea-94da-cadd467834df

##### Tests.Integrated.InterfaceBridge.PersonRoutes+PersonRoutesTests.REQ-PER-1.4 a Person Create payload with no birthdate is rejected with a typed error naming the missing birthdate, and no Person is created
Assert.Equal() Failure: Strings differ
                                                 ↓ (pos 60)
Expected: ···"aceContracts.PersonContracts+PersonReturn"
Actual:   ···"ntracts.PersonContracts+PersonCreateInput"

##### Tests.Integrated.CrossDomainOrchestration.JournalEntryFetchingTests.REQ-JE-3.5 fetchByReference with FI only returns exactly the entries carrying a reference from that FI
Assert.Equal() Failure: Collections differ
           ↓ (pos 0)
Expected: [JournalEntryHeaderId 2bdf0589-9951-4b0f-a66b-0d81b0d89a35, JournalEntryHeaderId 372f34f5-a434-409a-a7e5-8f7384625b46, JournalEntryHeaderId 74a302cd-7adf-48a1-95f0-ab6a5ae057ad, JournalEntryHeaderId 79131cb5-75bb-4e8f-8ebb-33599d79b185, JournalEntryHeaderId 7eccb778-1ba9-4326-9501-d49b82395029, ···]
Actual:   [JournalEntryHeaderId 05dbf32d-c52c-4f5b-bf91-cb7086c1241a, JournalEntryHeaderId 2bdf0589-9951-4b0f-a66b-0d81b0d89a

##### Tests.Integrated.DataAccessLayer.DalTests+ConnectionReleaseTests.REQ-DAL-2.4 fetching through every lookup cache leaves no session idle in a transaction and the pool's in-use count where it started
Assert.Equal() Failure: Values differ
Expected: 1
Actual:   0

##### Tests.Integrated.Model.Ledger.AccountTests.REQ-AC-2.7 creating a child under a parent whose active end is today or later succeeds and the stored child carries that parent(parentEndOffsetDays: 0)
Assert.Equal() Failure: Values differ
Expected: Some(AccountId 7e8185f5-6b22-493b-9185-10d5644c7a2c)
Actual:   Some(AccountId edefa824-72f3-4243-b338-df730908471f)

##### Tests.Integrated.DataAccessLayer.DalTests.REQ-DAL-2.2 an update requiring exactly one row that touches two returns DalResultantRowsDidntMatchExpectation carrying the expectation and the count
Assert.Equal() Failure: Values differ
Expected: 3
Actual:   2

##### Tests.Integrated.Model.Ledger.AccountTests.REQ-AC-2.7 creating a child under a parent whose active end is today or later succeeds and the stored child carries that parent(parentEndOffsetDays: 30)
Assert.Equal() Failure: Values differ
Expected: Some(AccountId 7e8185f5-6b22-493b-9185-10d5644c7a2c)
Actual:   Some(AccountId bbbc9813-0151-4584-bb37-eee8e01fbc40)

##### Tests.Integrated.Model.Ledger.FiscalPeriodTests.REQ-FP-3.4 fetchAll without filter returns exactly the fixture's periods, open and closed
Assert.Equal() Failure: Collections differ
Expected: ["2026-06", "2026-07", "2026-08", "2026-09", "2026-10", ···]
Actual:   ["2026-05", "2026-06", "2026-07", "2026-08", "2026-09", ···]

##### Tests.Integrated.CrossDomainOrchestration.AccountBalanceTests.REQ-AC-3.13 REQ-RPT-1.10 fetchByAccountIdList returns correct debit and credit totals
Assert.Equal() Failure: Values differ
Expected: 454.03
Actual:   453.03

##### Tests.Integrated.CrossDomainOrchestration.JournalEntryCreationTests.REQ-JE-2.5 REQ-JE-1.11 an entry posted on the first or last day of an open fiscal period is assigned the fixture period whose start and end dates contain that day(day: "last")
Assert.Equal() Failure: Values differ
Expected: FiscalPeriodId 3b84872a-6608-4f10-8cea-8b40951cb2b3
Actual:   FiscalPeriodId 1b95b17b-7c88-4625-b28b-2d5cf1eb0657

##### Tests.Integrated.Model.Ledger.JournalEntryHeaderTests.REQ-JE-3.2 fetchById returns a header whose entry date is in a closed fiscal period must succeed
Assert.Equal() Failure: Strings differ
                                      ↓ (pos 27)
Expected: "Fixture JE in closed period PERTURBED"
Actual:   "Fixture JE in closed period"

##### Tests.Integrated.CrossDomainOrchestration.PersonMaintenance+PersonMaintenanceTests.REQ-PER-2.2 updating only a Person's birthdate, its name left unchanged, stores the new birthdate and keeps the name it had
Assert.Equal() Failure: Values differ
Expected: Tuple ("Jordan Examplex", Friday, 21 September 1979)
Actual:   Tuple ("Jordan Example", Friday, 21 September 1979)

##### Tests.Integrated.CrossDomainOrchestration.AccountDeactivationTests.REQ-AC-4.2 deactivateAccount accepts end equal to begin
Assert.Equal() Failure: Values differ
Expected: Some(Monday, 06 October 2025)
Actual:   Some(Sunday, 05 October 2025)

##### Tests.Integrated.CrossDomainOrchestration.JournalEntryCreationTests.REQ-JE-2.13 REQ-JE-2.11 constructNewAndPersist posts a valid journal entry and returns it
Assert.Equal() Failure: Collections differ
                                                                                  ↓ (pos 1)
Expected: [Tuple (AccountId 09b6f82c-465f-48e5-87ec-419f44731ded, 86.04, Credit), Tuple (AccountId d79bd92c-6f2d-4590-894c-9f853d1aca90, 86.05, Debit)]
Actual:   [Tuple (AccountId 09b6f82c-465f-48e5-87ec-419f44731ded, 86.04, Credit), Tuple (AccountId d79bd92c-6f2d-4590-894c-9f853d1aca90, 86.04, Debit)]

##### Tests.Integrated.CrossDomainOrchestration.JournalEntryFetchingTests.REQ-JE-3.5 fetchByReference with reference text only returns exactly the entries carrying that reference text
Assert.Equal() Failure: Collections differ
           ↓ (pos 0)
Expected: [JournalEntryHeaderId 372f34f5-a434-409a-a7e5-8f7384625b46]
Actual:   [JournalEntryHeaderId 2bdf0589-9951-4b0f-a66b-0d81b0d89a35, JournalEntryHeaderId 372f34f5-a434-409a-a7e5-8f7384625b46]

##### Tests.Isolated.Model.QuantityAndPrice.REQ-QP-3.5 ... (q: "9999999999.999999", p: "999999.999999", expected: "9999999999989999.000000000002")
Assert.Equal() Failure: Values differ
Expected: 9999999999989999.000000000002
Actual:   9999999999989999.000000000001


##### Batch 2 (second assertions of tests above, the amount filter, and #006)

- REQ-AC-3.12.1/3.12.2 amount filter: expected line ids minus the first
- REQ-AC-3.13 normal-balance: expected revenue net = amount + 1.00
- REQ-FP-4.1 / 4.2: expected key = the other period key (id assertion left correct)
- REQ-PER-2.2 rename only / birthdate only: read-back assertion perturbed (returned assertion left correct)
- REQ-DAL-2.2 two rows: expected expectation "ExactlyZero"
- #006 route table: Src/Ui.OperatorCli/Program.fs commandRoutes with personDomainCommandRoutes removed (temporary, reverted). Every PersonRoutes test, which reaches routes through Tests.Helpers.RouteResolver, fails with CliUnknownCommand: the helper uses the shipped table.

##### Tests.Integrated.CrossDomainOrchestration.PersonMaintenance+PersonMaintenanceTests.REQ-PER-2.2 updating only a Person's birthdate, its name left unchanged, stores the new birthdate and keeps the name it had
Assert.Equal() Failure: Values differ
Expected: Tuple ("Jordan Examplex", Friday, 21 September 1979)
Actual:   Tuple ("Jordan Example", Friday, 21 September 1979)

##### Tests.Integrated.InterfaceBridge.PersonRoutes+PersonRoutesTests.REQ-PER-2.2 REQ-PER-1.1 a Person Update payload with a whitespace-only name is rejected with a typed empty-name error, and the stored name is unchanged
Unknown command: Person Create

##### Tests.Integrated.InterfaceBridge.PersonRoutes+PersonRoutesTests.REQ-PER-2.1 a Person Create payload creates the Person, and the return carries its name and birthdate
Unknown command: Person Create

##### Tests.Integrated.InterfaceBridge.PersonRoutes+PersonRoutesTests.REQ-PER-2.1 REQ-PER-1.1 a Person Create payload with a whitespace-only name is rejected with a typed empty-name error, and no Person is created
Wrong error. Unknown command: Person Create

##### Tests.Integrated.DataAccessLayer.DalTests.REQ-DAL-2.2 an update requiring exactly one row that touches two returns DalResultantRowsDidntMatchExpectation carrying the expectation and the count
Assert.Equal() Failure: Strings differ
                  ↓ (pos 7)
Expected: "ExactlyZero"
Actual:   "ExactlyOne"

##### Tests.Integrated.CrossDomainOrchestration.PersonMaintenance+PersonMaintenanceTests.REQ-PER-2.2 updating only a Person's name, its birthdate left unchanged, stores the new name and keeps the birthdate it had
Assert.Equal() Failure: Values differ
Expected: Tuple ("Jordan Renamed Only", Wednesday, 06 January 2010)
Actual:   Tuple ("Jordan Renamed Only", Tuesday, 05 January 2010)

##### Tests.Integrated.InterfaceBridge.PersonRoutes+PersonRoutesTests.REQ-PER-2.2 REQ-SYS-6.1 a Person Update payload changing the name and re-sending the stored birthdate succeeds, changing the name and leaving the birthdate as stored
Unknown command: Person Create

##### Tests.Integrated.InterfaceBridge.PersonRoutes+PersonRoutesTests.REQ-PER-1.4 a Person Create payload with no birthdate is rejected with a typed error naming the missing birthdate, and no Person is created
Wrong error. Unknown command: Person Create

##### Tests.Integrated.InterfaceBridge.PersonRoutes+PersonRoutesTests.REQ-PER-2.2 REQ-SYS-6.1 a Person Update payload naming no field is rejected with a typed no-change error, and the Person is unchanged
Unknown command: Person Create

##### Tests.Integrated.Model.Ledger.FiscalPeriodTests.REQ-FP-4.1 closeFiscalPeriod happy path
Assert.Equal() Failure: Values differ
Expected: FiscalPeriodKey "2026-05"
Actual:   FiscalPeriodKey "2026-06"

##### Tests.Integrated.CrossDomainOrchestration.AccountActivityTests.REQ-AC-3.12.1 REQ-AC-3.12.2 fetchFiltered by amount returns exactly the lines of that amount, voided entries' lines included
Assert.Equal() Failure: Collections differ
                                                                                                                                  ↓ (pos 7)
Expected: [···, JournalEntryLineId 4bd1f9f7-fd83-4f50-ad5d-0194ebbfaf06, JournalEntryLineId 563e54a3-6472-4d9c-ae63-2c69bd93d891, JournalEntryLineId 8da24846-8f10-474f-a072-c2dc28f55c28, JournalEntryLineId 8e594f41-d5f9-4b7a-aefb-f89def9c0df5, JournalEntryLineId b977c275-f03d-4ab3-a4aa-16ef92ee8618, ···]
Actual:   [

##### Tests.Integrated.Model.Ledger.FiscalPeriodTests.REQ-FP-4.2 reopenFiscalPeriod happy path
Assert.Equal() Failure: Values differ
Expected: FiscalPeriodKey "2026-06"
Actual:   FiscalPeriodKey "2026-05"

##### Tests.Integrated.InterfaceBridge.PersonRoutes+PersonRoutesTests.REQ-PER-2.2 a Person Update payload naming a new name and birthdate changes both, and the return carries the new values
Unknown command: Person Create

##### Tests.Integrated.CrossDomainOrchestration.AccountBalanceTests.REQ-AC-3.13 the net balances of a debit-normal and a credit-normal account each holding one side of an entry both equal its amount in normal-balance orientation
Assert.Equal() Failure: Values differ
Expected: 201.00
Actual:   200.00

##### Tests.Integrated.InterfaceBridge.PersonRoutes+PersonRoutesTests.REQ-PER-2.3 the Person List route returns every Person with its name and birthdate, ordered by name
Unknown command: Person List


##### Batch 3 (PersonRoutes with the route table restored)

- REQ-PER-1.4: expected message ends "birthdateX"
- REQ-PER-1.1 Create whitespace name: expected person list after = before minus its first

##### Tests.Integrated.InterfaceBridge.PersonRoutes+PersonRoutesTests.REQ-PER-2.1 REQ-PER-1.1 a Person Create payload with a whitespace-only name is rejected with a typed empty-name error, and no Person is created
Assert.Equal() Failure: Collections differ
Expected: ["Jordan Example", "Sam Example"]
Actual:   ["Alex Example", "Jordan Example", "Sam Example"]

##### Tests.Integrated.InterfaceBridge.PersonRoutes+PersonRoutesTests.REQ-PER-1.4 a Person Create payload with no birthdate is rejected with a typed error naming the missing birthdate, and no Person is created
Assert.Equal() Failure: Strings differ
                                                      ↓ (pos 112)
Expected: ···"onContracts+PersonCreateInput: birthdateX"
Actual:   ···"sonContracts+PersonCreateInput: birthdate"


##### #072 (done by the lead session in PositionsRoutes.fs): REQ-POS-4.7, REQ-POS-9.4, REQ-POS-9.5 missing-field route tests
Perturbed each expected field name with a trailing X:
    Expected: ···"cts+PropertyCreateInput: acquisitionDateX"   Actual: ···"acts+PropertyCreateInput: acquisitionDate"
    Expected: ···"racts+PropertyCreateInput: purchaseBasisX"   Actual: ···"tracts+PropertyCreateInput: purchaseBasis"
    Expected: ···"nvestmentAccountCreateInput: activeBeginX"   Actual: ···"InvestmentAccountCreateInput: activeBegin"
Restored: green.

