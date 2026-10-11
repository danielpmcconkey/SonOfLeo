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
