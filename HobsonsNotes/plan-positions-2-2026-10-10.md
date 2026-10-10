# Plan — Positions slice 2 (Src)

Written 2026-10-10 by Hobson for a Claude Code session that can see this
repository and nothing else. Everything you need is either in this document
or in the repo. Where this plan and `Specs/Behavioral/` disagree, the spec
wins — tell Dan.

## 0. Start here — one session does Src and tests, in this order

You are doing both halves of the slice loop: Src from this plan, Tests from
`HobsonsNotes/brief-test-agent-positions-2-2026-10-10.md` (the brief).
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

Slice 1 (`HobsonsNotes/plan-positions-2026-10-04.md`, merged) gave
SonOfLeo **Positions**: persons, investment accounts, securities, holdings,
weekly whole-account snapshots, properties and valuations, and the net worth
and investment wealth history reports.

The plan of record after slice 1 was a **holdings ledger** for the taxable
account: every purchase and sale recorded, lots derived from them, realised
gains posted to the general ledger. Measured against real data, that design
was dropped. This slice does four smaller things instead:

1. **Lots on snapshots.** A taxable holding's snapshot line may carry its
   open lots exactly as the institution reports them. Nothing derives lots.
2. **Activity.** Every investment account gets a record of what the
   institution reported happening in it (contributions by source,
   rollovers, dividends, fees, purchases, sales), plus a **unit
   roll-forward**: do the units that moved between two snapshots explain the
   difference between them?
3. **Pre-ledger balances.** Dated balances of ledger accounts for dates
   before the ledger's first fiscal period, so **net worth** can be
   computed for those dates.
4. **Net worth history.** Month-end net worth over a range, each point
   saying which components are absent (old records don't cover everything).

Realised gains stay off the general ledger. Nothing in this slice writes a
journal entry.

## 2. Before you touch anything, read

- `README.md` — the slice loop and the three load-bearing constraints.
- `Specs/README.md` — requirement grammar, the traceability gate, the rule
  that specs never name source files and source never carries REQ
  annotations.
- **The spec for this slice.** `git diff main -- Specs/` on this branch
  shows exactly what changed: `Specs/Behavioral/Positions.md` (design notes;
  §1; REQ-POS-5.6, 6.9–6.13; amended 7.2, 7.5, 8.3, 11.4, 11.10; new §12–§15),
  `Specs/Behavioral/Reporting.md` (§3 scope line; §8 amended and REQ-RPT-8.7,
  8.8; new §10), and `Specs/Definitions.md` (Pre-ledger date). Read all of
  Positions.md and Reporting.md §8–§10, not just the diff.
- `Src/README.md`, and `Tests/README.md` for what tests will hold you to.
- `Skills/SonOfLeoSrcDeveloper/SKILL.md` — how Src is shaped. Follow it.
  (Lines ~414–423 still say `Utilities.Clock` / `Utilities.Calendar` /
  `AppError.fs`; read them as `App.Utility.*`, `IAppError.fs` and the
  per-domain error files.)
- `Skills/ArchReviewer/SKILL.md` — run it over your own diffs before you
  report.
- `Skills/ArchiMate/SKILL.md` — you will extend the model (§4 Part E).
- In `Architecture/SonOfLeo.archimate`, the principles **"Domains Build
  Upward"** and **"Dependencies Build From the Base Up"**.
- `CompoundedLearnings/` — especially `orchestration-layer.md`,
  `type-placement-by-compile-tier.md`, `type-taxonomy.md`,
  `validation-layers.md`, `dal-errors-are-backstops.md`,
  `money-type-enforcement.md`, `numeric-type-taxonomy.md`,
  `du-case-collision-across-opens.md`.
- **Slice 1's code is your precedent for everything here.** In
  `Business.FinancialServices.Positions`: `AccountSnapshotHeader.fs`,
  `AccountSnapshotLine.fs`, `Holding.fs`, `Valuation.fs`. In
  `Business.CrossDomainOrchestration`: `AccountSnapshotOrchestration.fs`,
  `PositionsLedgerLinks.fs`, `HoldingsAsOf.fs`, `NetWorth.fs`,
  `InvestmentWealthHistory.fs`. In `Ui.InterfaceBridge`:
  `PositionsContracts.fs`, `PositionsFieldConverters.fs`,
  `PositionsRoutes.fs`, `NetWorthWriter.fs`,
  `InvestmentWealthHistoryWriter.fs`. Slice 1's §8 report in its plan says
  what was decided along the way.

## 3. Standing rules

- **You write `Src/`, `DbMigration/Scripts/`, `Architecture/` (Part E only)
  and `Tests/`; never `Specs/`.**
- **Branch `positions-slice-2`.** `git pull --rebase --autostash` before every
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
  unique key the application also checks is a backstop, not the rule.
- **Infallible create, orchestrator validates.** `create` is total;
  orchestrators validate. `reconstitute` validates everything that needs no
  database read.
- **Loud beats bubblewrap.** A failure that already aborts with an error
  needs no new guard and no new error case. Only silent failures earn code.
- **Build only what the spec asks for.** No route the spec has no "means to"
  for. In particular: no update for an Activity or a Lot (they are replaced
  by re-recording), no per-Activity delete (an empty range clears,
  REQ-POS-13.2), no update for a Pre-ledger Balance (re-record replaces it).
- **Never create a record to absorb a failed lookup.** An Activity naming a
  Security with no Holding fails (REQ-POS-12.5).
- **Never deduplicate.** Two Lots or two Activities may be identical in
  every field; both are kept, in the order supplied (REQ-POS-6.13, 12.9).
  Store an explicit ordinal; don't rely on insertion order or `unique_id`
  order to come back.
- **Code never breaks a tie** unless a requirement names the tie-break.
- **Tuple lists of domain primitives in construct functions are
  intentional.** Don't introduce "create input" records.
- **Fully qualify common DU case names.** The new Activity kind DU has
  `Contribution`, `Sale`, `Fee`, `Dividend`, `Interest`, `Withdrawal` —
  every one of them will collide with something. Qualify every use.
- **All currency arithmetic and comparison goes through `Money`**
  (REQ-MON-2.1); Quantity and Price through their own modules. The two
  places this slice needs arithmetic over Quantities are the lot-sum check
  (REQ-POS-6.12, an exact sum compared to the line's quantity) and the unit
  roll-forward (REQ-POS-14.2, whose expected and difference are plain exact
  decimals because they can go negative). Add whatever Quantity operation
  those need to `Quantity.fs` (a sum, a conversion to `decimal`) rather than
  unwrapping in the caller.
- **Every operation uses its context's initiation instant** for every
  timestamp and every derived "current date" (REQ-SYS-3.4) — REQ-POS-12.8
  and 13.1 included. `Checks/check-clock.sh` enforces it.
- **Migrations:** new scripts in `DbMigration/Scripts/` named
  `YYYYMMDDHHMM-PascalCaseDescription.sql`, with a header comment saying why,
  following slice 1's `202610041020-CreatePositionsTables.sql` (`{ENV}`
  placeholder, `sonofleo_migrator` owner, read-only grant to
  `leobloom_hobson`) and `…1030-TestRoleTruncateGeneralAndPositions.sql`
  for the test role's truncate grant on new tables. Never run anything
  against production; Dan applies migrations by hand.
- Commit subjects describe the behaviour, not the edit ("A lot dated after
  its snapshot is rejected"). Match the log.
- **Before every hand-off:** green build, `Tests.Isolated` green, and
  `bash Checks/run-all.sh` green, with the output in your report.
  `Tests.Integrated` needs the `sonofleo_test` database
  (`Tests/Tests.Integrated/setup-throwaway-test-db.sh` builds one from the
  migration scripts); if you can't get one, say so — don't skip or fake.

## 4. The work

Each item names the requirements it satisfies. Placement is decided by the
architecture principles; where this plan names a file, that is the expected
home, and `ArchReviewer` is the judge. Where it doesn't, decide by
`type-placement-by-compile-tier.md` and say what you chose in the report.

### A. Schema

1. **Migration: lots.** (REQ-POS-6.9–6.13, 7.2.) A child table of the
   snapshot line: line reference (cascade on delete, so replacing a
   snapshot replaces its lots), ordinal, acquired date, quantity
   `numeric(16,6)`, reported cost basis `numeric` nullable as the line's is.
   Unique on (line, ordinal) only — **not** on the lot's content.

2. **Migration: activity.** (REQ-POS-12.1–12.9.) Investment account
   reference, activity date, ordinal within (account, date), kind (text,
   checked by the application, not a Postgres enum — match how slice 1
   stored tax treatment and basis method), description `varchar(500)`,
   source `varchar(100)` null, holding reference null, quantity null, price
   null, amount. Unique on (account, date, ordinal). Index on (account,
   date).

3. **Migration: pre-ledger balances.** (REQ-POS-15.1.) Ledger account
   reference, balance date, balance. Unique on (account, date). It lives in
   schema `positions` (§6).

4. **Migration: test-role grants** for the three new tables, and add them
   to `Tests.Helpers/TestDataStage.fs` truncation (brief Part B).

### B. Positions domain (`Business.FinancialServices.Positions`)

5. **Lot.** (REQ-POS-6.9–6.13.) A component of the snapshot line, after
   `AccountSnapshotLine.fs` (e.g. `AccountSnapshotLot.fs`): acquired date,
   Quantity, optional Money cost basis, ordinal. The line now carries its
   lots. `reconstitute` validates what needs no read: quantity > 0, cost
   basis not negative, acquired date not after the snapshot date, and the
   exact sum (REQ-POS-6.12). The taxable-only rule (6.9) needs the
   account's tax treatment, so it is checked where slice 1 checks the
   contribution-basis-only-on-Roth rule (REQ-POS-6.4). Follow that
   precedent.

6. **Activity.** (REQ-POS-12.1–12.9.) An entity in its own file. Name the
   module and type something that cannot be confused with the ledger's
   `AccountActivity.fs` in orchestration (which is about ledger account
   activity and unrelated) — e.g. `InvestmentActivity.fs`. The kind is a
   DU of the fifteen cases in REQ-POS-12.2, with one function from kind to
   direction (units in, units out, none) that §14 and the 12.6 rule both
   use. Description and source are validated value types (trim, length).
   The 12.6 shape rule (which kinds must, may or must not name a Security
   and carry a quantity; price only with quantity) is pure — put it in
   `reconstitute`, one typed error naming account, date, kind and what is
   wrong.

7. **Pre-ledger Balance.** (REQ-POS-15.1.) An entity in its own file in this
   project: ledger `AccountId`, balance date, Money balance. Positions
   already references the Ledger project, so the account id is available.

8. **Errors and auditable actions.** New cases in `PositionsError.fs` and
   `PositionsAuditableAction.fs` for each new rejection and write.

### C. Orchestration (`Business.CrossDomainOrchestration`)

Positions-level orchestration compiles after cash flow and before
classification, as slice 1's does. Expected new or changed files are named;
report where you put each.

9. **Snapshots with lots.** (REQ-POS-6.9, 6.11, 7.2, 7.3, 7.5.) Extend
   `AccountSnapshotOrchestration.fs`: record writes each line's lots; fetch
   returns them; replacement replaces them. REQ-POS-6.9 rejects lots on a
   non-taxable account's line.

10. **Tax treatment and lots.** (REQ-POS-5.6.) Extend the investment
    account update the way REQ-POS-5.5 was done: changing tax treatment
    away from 'Taxable' is rejected while any of the account's lines
    carries a lot, naming the earliest and latest such snapshot dates.

11. **Holdings as of a date.** (REQ-POS-8.3.) `HoldingsAsOf.fs` returns each
    line's lots in the order supplied. **Net worth and investment wealth
    history do not use lots** — make sure loading them doesn't change those
    reports' results, and don't make those reports pay for loading lots if
    it's avoidable cheaply.

12. **Activity use cases.** (REQ-POS-12.5, 12.8, 13.1–13.5, and 11.4,
    11.10.) Record (atomic across accounts; per account a begin/end range;
    every activity dated in its range; ranges for one account in one
    operation must not overlap; replace everything of that account dated in
    the range; return removed/recorded counts per range). List by account
    and date range. Holding lookup per activity that names a Security —
    batch it, don't query per row. Extend the investment account
    active-period update (11.4) to reject when activities would fall
    outside, and holding delete (11.10) to refuse when an activity
    references the holding.

13. **Unit roll-forward.** (REQ-POS-14.1–14.3.) A read in its own file (e.g.
    `UnitRollForward.fs`). Both snapshot dates must have a snapshot of the
    account, first earlier than second. Window: activity dated **after** the
    first date and **on or before** the second. Rows for every Security on
    either snapshot or named by a units-moving activity in the window;
    in/out totals by the kind's direction; expected and difference as exact
    `decimal`. Ordered by Security name. A difference is data, never an
    error.

14. **Pre-ledger balance use cases.** (REQ-POS-15.1–15.6.) Record (atomic;
    no account+date twice per operation; replace on an existing
    account+date; returns each as stored with `replaced`), delete, list.
    Validations that need reads: the account exists and is Asset or
    Liability (15.1); balance date earlier than the start date of the
    earliest fiscal period, and fails when there is no fiscal period
    (15.2); the account is not linked to an Investment Account and not an
    asset Account of a Property (15.3). 15.3 reads both sub-domains' links —
    `PositionsLedgerLinks.fs` already does exactly that for slice 1's link
    rules; reuse it.

15. **Net worth on pre-ledger dates.** (REQ-RPT-8.1, 8.2, 8.3, 8.5, 8.7,
    8.8.) Change `NetWorth.fs`:
    - Accept a date in a fiscal period **or** a pre-ledger date (earlier
      than the start of the earliest fiscal period). Anything else still
      fails with the existing error.
    - Account balances come from one place, by date: the ledger for a
      fiscal-period date (today's behaviour, unchanged); the latest
      Pre-ledger Balance on or before the date for a pre-ledger date. An
      account with no pre-ledger balance on or before the date is **not
      listed** and contributes nothing — not listed at zero. Never mix the
      two sources for one date.
    - The mortgage balances behind a property's equity go through the same
      rule.
    - The result gains: whether the date is pre-ledger; per listed account
      on a pre-ledger date, the date its balance came from; and the absent
      components (REQ-RPT-8.8). On a fiscal-period date, ledger assets and
      liabilities are absent only if the chart of accounts has no account
      that would contribute.
    - Today's results for fiscal-period dates must not change. Slice 1's
      net worth tests are your regression net.

16. **Net worth history.** (REQ-RPT-10.1–10.3.) A new file (e.g.
    `NetWorthHistory.fs`) after `NetWorth.fs`. Month-end selection exactly
    as `InvestmentWealthHistory.fs` does it — reuse, don't re-derive. Each
    point is §15's computation on that date, reduced to the totals, the
    pre-ledger flag and the absent components. Fail naming the earliest
    month-end that is neither in a fiscal period nor pre-ledger
    (REQ-RPT-10.3). Don't run a full per-account net worth query per point
    if a range-wide read is straightforward; correctness first, and say in
    the report what you chose.

### D. Interface (`Ui.InterfaceBridge`, `Ui.OperatorCli`, `Ui.ReportCli`)

17. **Command routes.** One per "means to", and no others. Changed or new:

    | Domain | Verbs | REQs |
    |---|---|---|
    | `AccountSnapshot` | `Record`, `Fetch` (lots added to both payloads) | POS-6.9–6.13, 7.1, 7.5 |
    | `Holding` | `FetchAsOf` (lots added to the payload) | POS-8.3 |
    | `InvestmentActivity` | `Record`, `List` | POS-13.1–13.5 |
    | `InvestmentAccount` | `RollForward` | POS-14.1–14.3 |
    | `PreLedgerBalance` | `Record`, `Delete`, `List` | POS-15.4–15.6 |

    Name the activity and roll-forward domain/verb otherwise if the
    existing conventions argue for it, and say why. Accounts are addressed
    by name, ledger accounts by code, and every ledger account in a return
    payload carries its name beside its code (REQ-NGUI-1.4, 1.6). Lots and
    activities are ordered lists in the contract; the order sent is the
    order stored. Quantities and prices convert through
    `Quantity.fromDecimal` / `Price.fromDecimal`: no rounding at the
    boundary. The activity kind crosses as its case name, case-sensitive,
    trimmed, as slice 1's enums do.

18. **Report route and writer.** (REQ-RPT-8.6 unchanged; REQ-RPT-10.4.)
    `NetWorthHistory` in the Reports CLI, read-only
    (`Context.create NoTransaction FetchOnly`), both output modes,
    `-yyyy-MM-dd_yyyy-MM-dd` interpolation. Writer: one row per month-end,
    one column per total, an "absent" column (empty when nothing is
    absent), pre-ledger points marked. Header with the range; footer and
    print CSS from `ReportVisualizationAssets`. Extend `NetWorthWriter.fs`
    to show the pre-ledger flag, each account's balance date on a
    pre-ledger date, and the absent components.

### E. Architecture model

19. **Bring `Architecture/SonOfLeo.archimate` in step.** (Skill:
    `Skills/ArchiMate/SKILL.md`.) A component for every new `.fs`,
    composition under the right project folder, serving edges for new
    module `open`s, realization edges to the existing "Positions functions"
    group and the report capabilities. `python3 Skills/ArchiMate/validate.py`
    must say `VALID` and `python3 Skills/ArchiMate/model_drift.py` must
    report nothing. Dan owns view layout; don't edit views.

## 5. Out of scope

- Specs (Hobson).
- Parsers and imports. Lots, activity and pre-ledger balances arrive through
  the routes from parsers outside the repo; loading history is Dan and
  Hobson's job after this merges.
- Anything that derives lots, holding periods, cost basis or realised gains.
  The design note "lots are reported, not derived" is the rule.
- Any journal entry, and any change to Ledger, DataIngestion, CashFlow or
  Classification behaviour.
- Charts; market reference data; the simulator.

## 6. Open design notes you may hit

- **Pre-ledger Balance lives in the Positions project and schema.** It
  references ledger accounts, but it is a record of what was held on a
  date, which is Positions' job, and Positions already sits above the
  Ledger. Hobson's call; if you find a reason it can't, say so.
- **A fiscal period created later, earlier than an existing pre-ledger
  balance, is not prevented.** The write-time rule (REQ-POS-15.2) checks
  against the earliest period at the time; net worth's date rule
  (REQ-RPT-8.7) decides which source a date uses, so such a balance is
  simply never read for dates the ledger now covers. Don't add a check to
  fiscal period creation.
- **A link added later is not prevented either.** If an account gains an
  Investment Account or Property link after it carries pre-ledger balances,
  net worth excludes linked accounts anyway (REQ-RPT-8.2). Don't add a
  check to the link operations.
- **"Not listed" is not "listed at zero".** On a pre-ledger date an account
  with no balance on or before the date is absent from the result
  (REQ-RPT-8.7). An account with a recorded zero is listed at zero.
- **Lots are optional per line, every week.** A taxable line with no lots
  means "not supplied" (REQ-POS-6.10). Don't reject a taxable snapshot for
  missing lots, and don't carry lots forward from an earlier snapshot.
- **The roll-forward window is half-open on purpose:** after the first
  snapshot date, on or before the second (REQ-POS-14.2's why).
- **Activity replacement counts.** "Removed" counts what was dated in the
  range before the write; re-recording an identical range returns removed =
  recorded, and that is a success, not REQ-SYS-6.1's no-op error.

## 7. When you finish

Report to Dan, at the bottom of this file under `## 8. Report`, together
with the brief's final report: what you implemented (by item number and
REQs), what you didn't and why, every placement choice §4 left to you, the
migration scripts Dan must apply (in order), test status (which suites ran,
where), `run-all.sh` output, `validate.py` and `model_drift.py` output, and
any requirement you believe is wrong. Don't mark anything done that you
haven't verified.

## 8. Report
