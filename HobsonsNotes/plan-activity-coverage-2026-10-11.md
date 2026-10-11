# Plan — Activity coverage, and two follow-ups from the audit fixes (Src and tests)

Written 2026-10-11 by Hobson for a Claude Code session that can see this
repository and nothing else. Branch `audit-fixes-2026-10-11a`, building on the
audit-fixes slice already there. Where this plan and `Specs/Behavioral/`
disagree, the spec wins — tell Dan.

## 0. Start here

1. Read `plan-audit-fixes-2026-10-11.md` §1 (reading list) and §2 (standing
   rules). They apply unchanged, except that this slice **does** add a
   migration (§3 Part B). Read that plan's §5 report too: it is the state of
   the branch you start from.
2. Read the spec: `Specs/Behavioral/Positions.md` §1 (Activity coverage) and
   §12–§14 in full, and `Specs/Behavioral/CashFlow.md` REQ-CF-4.9, 9.10 and
   14.9 (amended in commits `657d9d2` and `41ffb49`).
3. **Name first.** Before opening any Src, draft test names for REQ-POS-13.6,
   REQ-POS-13.7 and the amended REQ-CF-4.9 / 14.9 from the spec alone, run
   the name-quality check, and commit them as failing placeholders. Then do
   Parts A–C in order, committing as you go.
4. Finish with §4 and write your report into §5.

## 1. What this is for

Activity is recorded by replacing date ranges (REQ-POS-13.2). Nothing records
which ranges were supplied, so a stretch never supplied looks exactly like a
stretch where nothing happened. Contributions-versus-growth then comes out
silently wrong. Activity coverage records the supplied ranges, merged, and a
read returns the gaps. It must exist before Dan back-loads activity history,
so the back-load records what it covered.

## 2. Standing rules for this slice (in addition to the audit-fixes plan's §2)

- **You write `Src/`, `DbMigration/Scripts/`, `Checks/`, `Architecture/` and
  `Tests/`; never `Specs/`.**
- **Migrations:** new scripts in `DbMigration/Scripts/` named
  `YYYYMMDDHHMM-PascalCaseDescription.sql` with a header comment saying why.
  Follow `202610101010-CreateInvestmentActivityTable.sql` (`{ENV}`,
  `sonofleo_migrator` owner, read grant as the other Positions tables have it)
  and `202610101030-TestRoleTruncateLotsActivityPreLedger.sql` for the test
  role's truncate grant. Never run anything against production; Hobson
  reviews and Dan applies by hand.
- **Structural integrity in the schema; every business rule in the
  application** (REQ-DAL-3.6). The schema gets a primary key, the foreign
  key to the Investment Account, and at most a unique key on
  (account, begin date). No check constraint for date order or overlap.

## 3. The work

### A. Follow-ups from the audit-fixes review

1. **The REQ-DAL-2.2 test uses no table of its own.** Commit `a0cd4f3` made
   the two-row update test create a temporary table. Tests create no database
   objects (`Checks/check-test-ddl.sh`, Tests/README); it passed the check
   only because the pattern does not allow `temp`/`temporary` between
   `create` and `table`.
   - Rewrite the test to insert two rows into a real table inside the
     rolled-back transaction and update exactly those two. Pick the table
     with the fewest required columns and foreign keys; say which in the
     report.
   - Widen the check to catch `create temp table`, `create temporary table`
     and `create unlogged table`. Show it failing on `a0cd4f3`'s version of
     the test.
2. **Legs added later apply forward only.** (Dan, 2026-10-11; REQ-CF-4.9,
   9.10, 14.9.) `deriveIsFulfilled` (`InstanceOrchestration.fs`) counts a
   Payment Agreement only if its created-at is no later than the Instance's
   created-at. Tests:
   - a fulfilled Instance stays fulfilled after a leg is added to its Master
     Agreement, and stays fulfilled after a later re-derivation (any
     REQ-CF-9.10 trigger on that Instance that leaves each of its Invoices
     FullyPaid or cancelled);
   - an Instance created after the leg was added needs an Invoice on it;
   - a leg created in the same operation as an Instance counts for it, if
     any operation does that today; if none does, say so and write no test.

### B. Activity coverage (REQ-POS-13.6, 13.7)

3. **Table.** `positions.investment_activity_coverage`: `unique_id`,
   `investment_account_id`, `begin_date`, `end_date`, `created_at`,
   `modified_at`. It is an entity (REQ-SYS-3.1), not a component row: it is
   written by activity recording, not with its Investment Account. The
   migration also grants the test role truncate on it, and adds it to the
   test fixture's truncation.
   - Existing rows: there are none to carry forward. Activity already
     recorded before this migration has no coverage; the header says so.
4. **Entity module** in `Business.FinancialServices.Positions`, shaped like
   `InvestmentActivity.fs` (persist, reconstitute, a private `query`, fetch
   by account and date range, delete). Add it to `investment=` in
   `Checks/check-peer-guard.sh`; the guard fails until you do.
5. **Recording adds coverage** (REQ-POS-13.6), inside the recording
   transaction in `InvestmentActivityOrchestration.recordActivity`. For each
   supplied range, after the existing checks:
   - read the account's coverage rows that overlap the range or adjoin it
     (end date the day before its begin, or begin date the day after its
     end);
   - delete them, and insert one row from the earliest begin to the latest
     end. No update in place: an absorbed row is gone, and the merged row is
     new.
   - Two ranges for one account in one operation may adjoin (REQ-POS-13.3
     forbids only overlap). They must end up as one row. Process them in date
     order and say how in the report.
   - A range with no Activities is covered.
   - The REQ-POS-13.4 return is unchanged.
6. **The gap read** (REQ-POS-13.7). A Business function and an operator route
   beside the activity list route: Investment Account by name, begin and end
   dates. It returns the stretches between the two dates, inclusive, that no
   coverage row covers, in date order, each as a begin and end date. A
   reversed range fails with a typed error naming both dates, as the
   activity list's does (REQ-POS-13.5); follow its precedent for the error
   case. Fully covered returns an empty list.

### C. Tests and the model

7. Tests for 13.6 and 13.7 from the names you committed. At least:
   - coverage after one range; after a second range that overlaps it; after
     one that adjoins it; after one that bridges two existing rows into one;
     after one disjoint from the rest (two rows); after two adjoining ranges
     in one operation;
   - an empty range is covered;
   - a failed recording (any loud rejection, e.g. REQ-POS-13.3 overlap)
     leaves coverage unchanged;
   - gaps: none covered, fully covered, gaps at the start, the middle and the
     end, a gap of exactly one day, and both boundaries inclusive;
   - assert the stored rows by value (account, begin, end), not by count.
8. Add the coverage entity to the REQ-SYS-3.2 create theory.
9. Extend the ArchiMate model for the new entity, function and route
   (`Skills/ArchiMate/SKILL.md`). `validate.py` VALID and `model_drift.py`
   NO DRIFT.

### Out of scope

- Any read of coverage itself (only the gaps are specified).
- Deleting coverage. There is no delete for an Investment Account, and
  re-supplying a range never shrinks coverage.
- REQ-POS-11.4 (active-period updates) does not look at coverage.
- The 2026-10-11b refactors.

## 4. When you finish

- Build: 0 warnings, 0 errors.
- `Tests.Isolated` and `Tests.Integrated` pass, the latter against a
  `sonofleo_test` rebuilt from scratch with the new migration
  (`Tests/Tests.Integrated/setup-throwaway-test-db.sh`).
- `bash Checks/run-all.sh` green. `traceability-audit.sh .` Invariants 1 and 2
  both **clean**: this slice closes the last two untested requirements.
- Every new or changed assertion seen failing before it passed.
- No placeholder left.
- Push to `audit-fixes-2026-10-11a` and write §5: what changed per item, the
  migration Dan must apply, test counts before and after, fail-then-pass
  evidence, the outputs above, every name written after reading Src, and
  anything you disagreed with.

## 5. Report

Written 2026-10-11 by the Claude Code session that did this plan. Everything is on
`audit-fixes-2026-10-11a`, pushed after each item; nothing was merged.

### 5.1 Outcome

- Build: 0 warnings, 0 errors (`dotnet build SonOfLeo.slnx --no-incremental`).
- `Tests.Isolated`: 530 before, 530 after, all passing.
- `Tests.Integrated`, against a `sonofleo_test` rebuilt from scratch with the new migration
  (`setup-throwaway-test-db.sh`): 1751 before (measured on 8461d9e), 1778 after, all passing.
  The 27 new tests are 21 coverage tests, 1 route test, 1 create-theory row and 4 cash-flow tests.
- `Checks/run-all.sh`: 12 passed, 0 failed, 1 skipped. The skip is check-traceability, which only runs on main.
- `traceability-audit.sh .`: Invariant 1 **clean**, Invariant 2 **clean**.
- `validate.py`: VALID. `model_drift.py`: NO DRIFT.
- No `Not yet implemented` placeholder remains.
- ArchReviewer was run over `git diff 8461d9e HEAD -- Src Checks DbMigration` (5.5).

### 5.2 The migration Dan must apply

`DbMigration/Scripts/202610111000-CreateInvestmentActivityCoverageTable.sql`. It creates
`positions.investment_activity_coverage` with a primary key, the foreign key to the Investment
Account (on delete restrict) and a unique key on (account, begin date). It gives the read grant
the other Positions tables have, and in the test environment only, a truncate grant to
`sonofleo_test`. It was applied only to the throwaway `sonofleo_test`. The header says that
activity recorded before this migration has no coverage and that there are no rows to carry
forward.

### 5.3 Per item

**Names first** (ab5558f). 26 names were drafted from the spec alone, graded once (5.7), and
committed as failing placeholders before any file under `Src/` was opened.

**A1. The REQ-DAL-2.2 test uses no table of its own** (f7642e7).
- The two-row update test now inserts two rows into `ingestion.source` inside the rolled-back
  transaction and updates exactly those two. `ingestion.source` was chosen because it has the
  fewest required columns (4) and no foreign key.
- `check-test-ddl.sh` now also catches `create [global|local] temp|temporary|unlogged table`.
  On a0cd4f3's version of DalTests.fs, the widened check exits 1 and names the
  `create temporary table` line; the old check exits 0. Planted lines for `create temp table`,
  `create temporary table`, `create unlogged table` and `create global temporary table` each exit 1.

**A2. Legs added later apply forward only** (c4e031a; query form in 1080bb0).
- The plan says `deriveIsFulfilled` already counted a leg only when it was created no later than
  the Instance. It did not: it counted every Payment Agreement of the Master Agreement. Now each
  of the three re-derivations (composite update, Instance create, Invoice cancel) passes only the
  Payment Agreements created no later than the Instance. After review this became a query,
  `PaymentAgreement.fetchByMasterAgreementIdCreatedNoLaterThan`
  (`created_at <= @created_no_later_than`), so later legs never enter the candidate set.
- Tests, in `DerivedStateRules.fs`. Each step that stands for a separate operation runs under a
  later initiation instant (`TestContext.updateInitiationInstant`).
  - Adding a leg leaves a fulfilled Instance fulfilled, with Invoices only on its two original legs.
  - Adding a leg leaves an unfulfilled Instance unfulfilled.
  - After a leg is added, removing the Payment on one FullyPaid Invoice and replacing it with
    another that pays it in full re-derives the Instance, and it stays fulfilled.
  - An Instance created after the add stays unfulfilled until the added leg has a FullyPaid
    Invoice, and is then fulfilled.
- **Leg and Instance in one operation: no operation does this today.** CreateAgreement makes no
  Instance; the sweep and CreateInstance make no Payment Agreement; UpdateAgreement makes no
  Instance. So no test was written and that placeholder was deleted. The "no later than" boundary
  is still pinned: the existing REQ-CF-4.9 theory builds its agreement and Instance under one
  instant, and fails under `<` (5.4).

**B3. Table** (951f12a). Migration as in 5.2. `positions.investment_activity_coverage` is added
to the fixture's TRUNCATE list.

**B4. Entity module** (740c3a4). `Business.FinancialServices.Positions/InvestmentActivityCoverage.fs`,
shaped like `InvestmentActivity.fs`:
- `persist`, a private `reconstitute`, `mapRawForDbRead`, a private `query` and `fetchAny`;
- `fetchByInvestmentAccountBetween`, which returns the rows sharing a date with the range, in
  begin-date order;
- `delete` by ID.

Other changes in this commit:
- New ID type `InvestmentActivityCoverageId` in PositionsComponent.
- Table alias `invcov`.
- The module is added to `investment=` in `check-peer-guard.sh`. The guard failed until it was
  added, naming the file.

**B5. Recording adds coverage** (f135c10). `recordActivity`, after every existing check and
after recording each range's activity, does the following inside the same transaction, for each
supplied range:
- reads the account's rows that overlap or adjoin the range, by fetching for the begin date − 1
  to the end date + 1;
- deletes those rows;
- inserts one new row from the earliest begin to the latest end, stamped with the initiation instant.

Coverage is added for a range supplied with no Activities, and the REQ-POS-13.4 return is unchanged.

Two adjoining ranges in one operation: the ranges are processed sorted by begin date. That sort
is not what makes them merge. Each range merges against the rows already written in the same
transaction, so the second range of a pair sees the first one's row whatever the order. The test
supplies the later range first.

**B6. The gap read** (12ec0eb; contract renamed in 6299695).
- `InvestmentActivityOrchestration.listActivityCoverageGaps` returns, in date order, the
  stretches between two dates (both included) that no coverage row covers.
- The route is `InvestmentActivity ListCoverageGaps`, beside `InvestmentActivity List`. It takes
  `InvestmentActivityCoverageGapsInput` (account name, begin date, end date) and returns a list
  of `InvestmentActivityCoverageGapReturn` (begin date, end date). An empty list means fully covered.
- A reversed range fails with the new error `PositionsActivityCoverageGapsEndBeforeBegin of
  LocalDate * LocalDate`, which names both dates. This follows the precedent of
  `PositionsActivityListEndBeforeBegin`: its own case, with the same message.
- The model type's fields are `firstUncoveredDate`/`lastUncoveredDate` rather than
  `beginDate`/`endDate`, so that F# record-label inference cannot retarget existing code that
  reads `RecordedRange.beginDate`.

**C7. Tests** (774e507), in `CrossDomainOrchestration/InvestmentActivityCoverage.fs`,
plus the route test in `PositionsSliceTwoRoutes.fs`.
- Each test records through `recordActivity` in a rolled-back transaction. It reads coverage back
  with its own SQL, not through any function recording uses, and asserts (account, begin, end)
  by value.
- Covered cases:
  - one range;
  - an overlapping range;
  - adjoining on each side (a theory);
  - a range bridging two rows;
  - a range one uncovered day clear of the rest;
  - a range wholly inside a row;
  - two adjoining ranges in one operation;
  - an empty range;
  - two accounts in one operation;
  - another account's overlapping range;
  - a rejected recording, after which the whole table equals the fixture's two rows.
- Gaps are tested for:
  - no coverage;
  - fully covered;
  - start, middle and end;
  - one day;
  - both boundaries inclusive (two tests);
  - begin equal to end;
  - another account's coverage ignored;
  - a reversed range.
- The route test makes its own account, records two ranges through the Record route (later
  range first), and asserts the three gaps. `Cleanup.cleanUpInvestmentAccountByName` now deletes
  the account's coverage rows too; without that, the foreign key would refuse the account delete.

**C8.** (9bf500b) "Activity Coverage" is a row of the REQ-SYS-3.2 create theory, and the theory's name now
lists it.

**C9. Model** (87fe4e4).
- New component InvestmentActivityCoverage, composed by the Positions project.
- New function "List activity coverage gaps" under Positions functions. It is realized by the
  entity and by InvestmentActivityOrchestration.
- The entity also realizes "Record investment activity", whose documentation now says recording
  adds merged coverage.
- Seven serving edges were added from drift. Drift also found an unused `open App.Utility.Result`
  in the entity, which was removed.

### 5.4 Fail-then-pass evidence

Every new or changed assertion was seen failing under a Src perturbation (or a perturbed
expectation), then passing once restored. Counts are failures out of the filtered run.

| Perturbation | Failed |
|---|---|
| A1: expected count 2 → 3 | 1 |
| A1: the update touches one row | 1 |
| A2: no created-at filter (both the list form and, after review, the query form) | 1, the replace test |
| A2: `<` for `<=` (both forms) | 3, the existing REQ-CF-4.9 theory rows |
| A2: `>=` for `<=` (both forms) | 2, the replace and created-after tests |
| A2: each new assertion's expected value flipped, ten runs | 1 each |
| No coverage added | 16 |
| Adjoining rows not merged | 3 |
| A one-day gap treated as adjoining | 3, incl. the one-day-clear test |
| Merge ignores the account | 18 |
| Merged row keeps the new range's begin | 5 |
| Merged row keeps the new range's end | 3 |
| Empty ranges not covered | 1 |
| Coverage written before the overlap check | 1, the rejection test |
| Gaps ignore coverage | 4 |
| Trailing gap excludes the end date | 1 |
| Trailing gap stops a day early | 6, incl. no-coverage and the route |
| A gap stops two days before the next row | 3 |
| A gap starts on a row's last day | 3 |
| Gaps read another account's rows | 6 |
| Reversed range accepted | 1 |
| Route returns gaps reversed | 1 |
| Coverage stamped 1 ms after the initiation instant | 1, the create theory row |

Every one of the 21 coverage tests and the route test failed under at least one of these.
A 1-tick perturbation of the stamp did not fail: the column stores microseconds, so it truncates
away. The 1 ms run is the one that counts.

### 5.5 ArchReviewer

The verdict was conditional, with no structural problem. Acted on:
- **Filter in the query, not on the results.** Done: `fetchByMasterAgreementIdCreatedNoLaterThan`.
  All three A2 perturbations were re-run against it, with the same failures.
- **"Leg" vocabulary in Src.** Fixed: the helper is now `fetchPaymentAgreementIdsOfInstance`, and
  the doc comment says Payment Agreement.
- **Contract name.** Fixed: `InvestmentActivityCoverageGapReturn`.
- **Two comments that restated their types.** Deleted.

Kept, with reasons:
- **The DDL check widening is out of scope.** Plan item A1 asks for it.
- **The truncate grant is inline, not in a separate TestRoleTruncate migration.** Plan item B3
  says "the migration also grants", and the brief says this slice adds one migration.
- **Two vocabularies for a gap.** The model's field names avoid the record-label collision
  (5.3 B6). The contract keeps begin and end, as the plan asks.

### 5.6 Disagreements, spec notes and things to look at

- **Plan vs. code, A2.** The plan describes the forward-only filter as already present. It was not
  there, so it was written (5.3 A2). No disagreement with the spec.
- **Plan vs. code, A2 re-derivation.** I first chose cancelling one of two FullyPaid Invoices as the
  trigger. Cancel refuses an Invoice that has a Payment ("Delete the Payment first"), so the test
  instead removes and replaces that Invoice's Payment in one update. That is also a REQ-CF-9.10
  trigger, and it leaves every Invoice FullyPaid.
- **Spec gap, REQ-CF-14.9 "There is no means to remove one".** This is a negative existence claim
  over the whole API, like REQ-POS-11.12–14. Those are waived; this one is not. Traceability is
  clean because other REQ-CF-14.9 tests cite it. Dan to decide whether it needs a waiver row.
- **REQ-POS-13.7 "read-only".** No test. The gap read makes no write, and a test that the table is
  unchanged afterwards would be bubblewrap.
- **No disagreement between the plan and `Specs/Behavioral/`** was found in this slice, so the spec
  did not have to win anywhere. Nothing under `Specs/` was edited.
- **Environment.** The cloud container had no .NET SDK. `dotnet-sdk-10.0` came from Ubuntu's archive.

### 5.7 Names, grading, and names written after reading Src

The grader scored most names 85–92. Changes made from its pass, before the commit:
- The boundary pair is split into two parallel names.
- The cross-account names are made strong. For recording, the other account's range is
  overlapped. For the gap read, the other account covers the dates in full.
- "Exactly" is added to the route and empty-range names.
- New names: two accounts in one operation; begin equal to end; adding a leg leaves an
  unfulfilled Instance unfulfilled.
- Wording is parallel across the 13.6, 13.7 and CF names.

Not taken:
- the read-only clause (5.6);
- older REQ-CF-4.9 and 9.10 clauses that existing tests already cover.

Changes to names after Src was read:
- **Renamed:** "… stays fulfilled when one of its two FullyPaid Invoices is then cancelled …" →
  "… stays fulfilled when the Payment on one of its two FullyPaid Invoices is then removed and
  replaced by another paying it in full …" (5.6).
- **Deleted:** "REQ-CF-4.9 a Payment Agreement created in the same operation as an Instance
  counts for it …", because no operation does that (5.3 A2).
- **Renamed:** the REQ-SYS-3.2 create theory ("… Investment Activity and activity coverage …").
- **Moved, names unchanged:** the four cash-flow placeholders, from `PaymentAgreementUpdate.fs` to
  `DerivedStateRules.fs`, whose scenario builds Instances with Invoices and Payments.
