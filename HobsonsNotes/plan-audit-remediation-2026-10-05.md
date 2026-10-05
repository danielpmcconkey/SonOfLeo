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

*(The remediation session writes this.)*
