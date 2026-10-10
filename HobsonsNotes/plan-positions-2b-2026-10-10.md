# Plan — Positions slice 2, review follow-up (Src and tests)

Written 2026-10-10 by Hobson after reviewing the slice 2 build on branch
`positions-slice-2` (`plan-positions-2-2026-10-10.md` §8). Same branch, same
session shape. Where this plan and `Specs/Behavioral/` disagree, the spec
wins — tell Dan.

## 0. Start here

1. Read `plan-positions-2-2026-10-10.md` §2 (reading list) and §3 (standing
   rules). They apply unchanged. Then read this whole file.
2. Read the spec changes in commit "Positions spec: review follow-up" on this
   branch (`git show` it): REQ-POS-5.6 withdrawn, REQ-POS-12.8 amended,
   REQ-POS-15.7 and REQ-POS-15.8 new.
3. **Name first.** Before opening any Src for Part B, draft test names for
   REQ-POS-15.7 and 15.8 from the spec alone, run the name-quality check, and
   commit them as failing placeholders. Then do Parts A–F in order, committing
   as you go.
4. Finish with §2 of this file and write your report into §3.

The migrations in this slice have been applied to `sonofleo_test` only, never
to prod. Editing `202610101000` in place is therefore allowed here; rebuild
the test database from scratch afterwards.

## 1. The work

### A. Lots are deleted by the code, not by the database

`202610101000-CreateAccountSnapshotLotTable.sql` gives
`account_snapshot_lot_account_snapshot_line_id_fkey` `ON DELETE CASCADE`.
It is the only cascade in the schema. Every other foreign key is
`ON DELETE RESTRICT` and the code deletes child rows itself; the schema
carries structure, not lifecycle.

1. Change it to `ON DELETE RESTRICT`.
2. Wherever snapshot lines are deleted (snapshot replacement in
   `AccountSnapshotOrchestration.recordOne`; snapshot deletion), delete their
   lots first, explicitly, in the same transaction. Give
   `AccountSnapshotLot` the delete function, shaped like the existing entity
   deletes.
3. The existing replacement and deletion tests must pass against `RESTRICT`.
   Confirm one of them fails with the delete removed (constraint 3), then
   restore it.

### B. REQ-POS-15.7 and REQ-POS-15.8

1. **15.7:** refuse creating a fiscal period that starts on or before any
   Pre-ledger Balance date. Fiscal periods are created through orchestration
   (`FiscalPeriodCreation.fs`); the guard goes there.
2. **15.8:** refuse linking a ledger Account that carries any Pre-ledger
   Balance to an Investment Account, or adding it to a Property's asset
   Accounts — on create and on update. Mortgage Accounts are unaffected.
3. New `PositionsError` cases (or the owning domain's, if the guard's home
   dictates), each naming what the spec says it names.

### C. Withdrawn and amended requirements

1. REQ-POS-5.6 is withdrawn (redundant with 5.3). Remove its guard in
   `InvestmentAccountOrchestration.fs`, its error case, and its test(s).
2. REQ-POS-12.8 no longer has a future-date clause. If the code checks it
   separately from REQ-POS-13.1's range check, remove that check, its error
   case if unused, and its test(s).

### D. One definition of "before the ledger began"

It is computed twice: `NetWorth.classifyDate` (NetWorth.fs ~89) and
`earliestFiscalPeriodStart` (PreLedgerBalanceOrchestration.fs ~74) with
`PreLedgerBalance.confirmBeforeLedger`. The second is a ledger fiscal-period
read living in a Positions orchestrator. Add one function in the Ledger
domain (e.g. `FiscalPeriod.earliestStart : FiscalPeriod list -> LocalDate
option`) and have NetWorth, the pre-ledger orchestration and Part B's 15.7
guard all use it.

### E. Shapes that say something false, and an entity API that leaks SQL

1. `fetchHoldingValuesAsOf` (HoldingsAsOf.fs ~113, ~228) returns lines whose
   lots are `[]`. Under REQ-POS-6.10 an empty list means "the institution
   supplied none"; here it means "not read". Delete it; NetWorth and
   InvestmentWealthHistory call `fetchHoldingsAsOf`.
2. `AccountSnapshotLot.fetchBySnapshotsOf` (AccountSnapshotLot.fs ~171)
   takes a caller's CTE string and name. Replace it with
   `fetchByAccountSnapshotLineIdList` (the house `fetchByXIdList` pattern in
   the SrcDeveloper skill) and pass the line ids HoldingsAsOf already holds.
   Remove the extracted `latestCte` (HoldingsAsOf.fs ~145) if nothing else
   needs it.

### F. Small items

1. Table aliases: one per table. `account_snapshot_line` is `snapl` and
   `account_snapshot` is `snap` already; replace `snapln`, `snplotln`,
   `snplothd`. Add the Positions tables to the alias table in
   `Skills/SonOfLeoSrcDeveloper/SKILL.md`.
2. `NetWorth.isPreLedger: bool` (~70) duplicates `NetWorthDate`; carry the
   case.
3. `ActivityShapeProblem` sits in `PositionsError.fs` because of compile
   order. Mark it as that compromise the way the codebase marks others, or
   move it if a clean home exists.
4. ReportsContracts: the comment "Declared ahead of the period activity
   input, which has the same fields" explains an F# rule. Remove or reword
   it to say what a reader of the contract needs.
5. `monthEndsBetween` is borrowed from InvestmentWealthHistory by
   NetWorthHistory. Move it to `App.Utility.Calendar`.
6. `UnitRollForward` uses `List.find` (~87, ~89). Return a typed error
   instead of throwing.

### Out of scope

- `computeNetWorthHistory` re-reading reference data each month: fine at
  this data size.
- The two empty-request guards (`PositionsActivityRangeListIsEmpty`,
  `PositionsPreLedgerBalanceListIsEmpty`): stay as they are, untested, no
  requirement. A loud rejection earns no spec and no test (Dan's ruling).
- The REQ-DAL-2.2 run-order failure on a fresh database: pre-existing on
  `main`, its own fix later.

## 2. When you finish

- Build: 0 warnings, 0 errors.
- `Tests.Isolated` and `Tests.Integrated` pass, the latter against a test
  database rebuilt from scratch (Part A edited a migration).
- `bash Checks/run-all.sh`, `validate.py`, `model_drift.py` (update the
  architecture model if Part D, E or F moved a function).
- Every new or changed test seen failing before it passed.
- Push to `positions-slice-2`. Write the report in §3: what changed per
  part, test counts, anything you disagreed with or couldn't do.

## 3. Report

Written by the build session on 2026-10-10. Everything below was checked at `6d2ee8e`
before this report was committed.

### What Dan needs to do

- **Migration `202610101000`.** Apply it as it now stands. It was edited in place (Part A),
  so its lot foreign key restricts rather than cascades.
- **Migration order.** Unchanged: `202610101000`, `202610101010`, `202610101020`,
  `202610101030`.
- **After merging.** Run `bash Checks/run-all.sh` again, because `check-traceability` only
  runs on main.

### Order of work

1. I drafted thirteen names for REQ-POS-15.7 and 15.8 from the spec alone.
2. One name-quality pass graded them. Its dispositions are at the end of this report.
3. I committed the names as failing placeholders before opening any Src. The new file is
   `Tests.Integrated/CrossDomainOrchestration/PreLedgerBalanceGuards.fs`.
4. Then I did Parts A to F, committing as I went.

### What changed, by part

**A. The database no longer cascades lot deletes; the code deletes lots itself.**
- In `202610101000`, the lot-to-line foreign key is now `ON DELETE RESTRICT`.
- `AccountSnapshotLot.deleteByAccountSnapshot` is shaped like the line's delete.
- Both callers delete lots before lines:
  - snapshot replacement in `recordOne`
  - `deleteSnapshot`
- The test clean-up helpers in `Tests.Helpers/Cleanup.fs` also delete lots first.
- I rebuilt the test database from scratch and confirmed the foreign key restricts (`r`).
- I removed the lot delete from `recordOne`. Both REQ-POS-7.2 lot-replacement tests then
  failed with Postgres 23503, a foreign key violation.
- I also removed the lot delete from `deleteSnapshot`. No test failed: nothing deleted a
  snapshot whose lines carry lots.
  - I added one: "REQ-POS-7.4 deleting the fixture's Taxable snapshot whose line carries
    lots returns it with those lots, after which neither the snapshot nor any of its lots
    can be fetched".
  - It failed with 23503 while the delete was missing.
  - I wrote this name after reading Src, so I'm saying so here.

**B. REQ-POS-15.7 and REQ-POS-15.8.**
- **15.7:** the guard is in `FiscalPeriodCreation.constructNewAndPersist`.
  - It takes the ledger's start as it would be once the new period exists, using
    `FiscalPeriod.earliestStart` over the existing periods plus the new one.
  - It then refuses the period if any pre-ledger balance is dated on or after that start.
  - REQ-POS-15.2 keeps every balance before the current earliest start. So this is exactly
    "starts on or before any balance date".
  - The error is `PositionsPreLedgerBalancesBlockFiscalPeriod(period, earliest, latest)`.
- **`ensureFiscalPeriods`:** it now creates months in order and stops at the first one
  refused.
  - Before, it mapped over every missing month eagerly. On a refusal, later months were
    already written inside the transaction, and only the rollback undid them.
  - The behaviour seen through the route is unchanged.
- **15.8:** the guard is `PositionsLedgerLinks.confirmCarriesNoPreLedgerBalance`.
  - It is called from the Investment Account link check and from the Property asset-account
    check, on both create and update. Mortgage accounts are not checked.
  - The error is `PositionsPreLedgerBalancesBlockLink(code, earliest, latest)`.
  - `PreLedgerBalance` gained `fetchByAccount`, `fetchOnOrAfter` and `dateRange`.

**C. Withdrawn and amended requirements.**
- **REQ-POS-5.6:** removed:
  - `confirmNoLotsStranded` and its call
  - the comment about checking lots before holdings
  - `PositionsTaxTreatmentChangeStrandsLots`
  - `AccountSnapshotHeader.fetchByInvestmentAccountCarryingLots`, which nothing else used
  - the 5.6 test in `InvestmentAccountMaintenance.fs`
- **REQ-POS-12.8:** the code never checked the future-date clause separately, so there was
  nothing to remove. The active-period test still covers 12.8.

**D. One definition of "before the ledger began".**
- `FiscalPeriod.earliestStart : FiscalPeriod list -> LocalDate option` lives in the Ledger
  domain.
- Three places use it:
  - `NetWorth.classifyDate`
  - the pre-ledger recording, which no longer has its own `earliestFiscalPeriodStart`
  - the 15.7 guard

**E. Removed the shapes that implied lots were absent when they had not been read.**
- `fetchHoldingValuesAsOf` is gone, and NetWorth and InvestmentWealthHistory call
  `fetchHoldingsAsOf`.
- A line read from the database is a function waiting for its lots. No `lots = []` stands in
  for "not read".
- `AccountSnapshotLot.fetchBySnapshotsOf` became `fetchByAccountSnapshotLineIdList`.
  - It follows the house `fetchByXIdList` shape.
  - It adds a new empty-list case, `PositionsAccountSnapshotLineIdListCannotBeEmpty`.
    HoldingsAsOf never calls it with an empty list.
- `latestCte` is gone. The CTE is inline again and now selects only from
  `account_snapshot`. The active-period filter moved to the outer query, so the CTE no
  longer needs a second alias for `investment_account`. The result is the same.

**F. Small items.**
- **F.1, aliases.** Each table now has one alias, and all 16 Positions and General tables
  are in the skill's alias table:
  - lot `snaplot`; `snapl` and `snap` replace `snplotln` and `snplothd`
  - the line fetch's `snaplh` became `snap`
  - the Property `paaf` and `pmaf` became `paa` and `pma`, which are separate subquery scopes
  - HoldingsAsOf's `la` for `ledger.account` became `a`
  - Not touched: `jex` for `ledger.journal_entry_ext_reference` in ingestion's
    `StageEntryHeader.fs`. The table lists that alias as `jer`, and the file is outside this
    slice.
- **F.2, the pre-ledger flag.**
  - `NetWorth` and `NetWorthPoint` carry `netWorthDate: NetWorthDate` instead of
    `isPreLedger`.
  - The writers match on the case.
  - The JSON contracts keep `isPreLedger` at the boundary.
- **F.3, `ActivityShapeProblem`.** It is marked as a compromise with a `todo:` comment, as
  `Cadence.fs` marks its compromise. There is no clean home for it: an error case carries
  it, and `PositionsError` compiles first.
- **F.4, the contract comment.** I removed the sentence that explained an F# rule.
- **F.5, `monthEndsBetween`.** It moved to `App.Utility.Calendar`.
- **F.6, `UnitRollForward`.** It returns `PositionsHoldingIdDoesntExist` or
  `PositionsSecurityIdDoesntExist` instead of throwing from `List.find`.
- **Architecture model.**
  - Updated four function descriptions:
    - creating a fiscal period
    - ensuring fiscal periods
    - maintaining investment accounts
    - maintaining properties
  - Added seven serving edges and removed one, as the drift output asked.

### Slice 1 and other tests changed, and why

REQ-POS-15.7 now refuses any fiscal period that starts on or before the fixture's pre-ledger
balances, which run to the end of month -7. Three older tests created such periods:

| Test file | Was | Now |
|---|---|---|
| `FiscalPeriodCreation.fs`, the month-length theory | `1974-07`, `1974-06` | `2074-07`, `2074-06` |
| `FiscalPeriodRoutes.fs`, Create happy path | `1993-06` | `2093-06` |
| `FiscalPeriodRoutes.fs`, Close happy path | `1992-05` | `2092-05` |
| `JournalEntryVoiding.fs`, REQ-JE-4.5 | month -7 | month -6 |

- The new keys use the 2040-and-later sentinel years that `check-hardwired-dates` allows.
- The voiding test moved to month -6, the only month before the ledger that a new period
  may still start in.
- Any future test that creates a period before the ledger has the same constraint.

These tests changed to follow Parts C and F.2:
- The REQ-POS-5.6 test was removed.
- `NetWorthHistory.fs` (`pointSummary`), `NetWorthPreLedger.fs` (two assertions) and
  `PositionsReportRoutes.fs` (the domain-side map) now read `netWorthDate`.

### Test status

Throwaway `sonofleo_test`, rebuilt from scratch with `setup-throwaway-test-db.sh` (exit 0),
and a clean build into an empty artifacts path:

```
    0 Warning(s)
    0 Error(s)

Passed!  - Failed:     0, Passed:   530, Skipped:     0, Total:   530, Duration: 450 ms - Tests.Isolated.dll (net10.0)
Passed!  - Failed:     0, Passed:  1715, Skipped:     0, Total:  1715, Duration: 1 m 2 s - Tests.Integrated.dll (net10.0)
```

- Integrated went from 1702 to 1715: thirteen 15.7 and 15.8 tests and the 7.4 deletion test
  were added, and the 5.6 test was removed.
- The REQ-DAL-2.2 run-order failure did not occur on this run against a fresh database. It
  depends on test order.

### Fail-then-pass

- **Placeholders.** All 13 failed first, with `Assert.Fail`.
- **Each guard removed from Src, in turn:**
  - without the 15.7 guard, its 4 rejection tests failed
  - without the Investment Account 15.8 guard, its 2 tests failed
  - without the Property 15.8 guard, its 2 tests failed
- **Every assertion in the new file, perturbed one at a time in three rounds:**
  - 13 primary assertions, 13 failed
  - 11 secondary assertions ("no period created", "link unchanged", "others still carry
    balances"), 11 failed
  - the "every balance unchanged" assertion, 1 failed
- **Part A.**
  - With the lot delete removed, the 2 replacement tests and the new deletion test failed
    with 23503.
  - The deletion test's 3 assertions were each perturbed, and 3 failed.
- **Older tests changed for 15.7.** Perturbed, 4 failed:
  - the 2074-07 month-end length
  - the 2093 end date
  - the Close test's open flag
  - the voiding test with the close skipped
- **F.2 tests.** The 4 changed assertions were perturbed, and 4 failed.
- **Part E.** In Src, I attached no lots to holdings lines. The REQ-POS-8.3 lots test failed.

Every file was restored after its run, and the suites above are green.

### Things I'd flag

1. **Untested error cases.**
   - `PositionsAccountSnapshotLineIdListCannotBeEmpty` is new. It is an empty-list guard
     that no route reaches, like the two empty-request guards you ruled on.
   - `UnitRollForward` now returns `PositionsHoldingIdDoesntExist` and
     `PositionsSecurityIdDoesntExist`. Both were already untested, and the foreign keys
     make them unreachable.
2. **The 15.7 guard reads every fiscal period on each creation.** `ensureFiscalPeriods`
   repeats that read for each missing month. That's fine at this data size.
3. **The voiding test's month.** Moving it to month -6 ties it to the fixture's pre-ledger
   dates. The comment there says so.

I didn't disagree with anything in the plan.

### `bash Checks/run-all.sh` (exit 0)

The full "untested" list has 79 cases; only the Positions ones are shown here.

```
PASS  check-apperror-coverage
untested: PositionsAccountSnapshotIdDoesntExist
untested: PositionsAccountSnapshotLineIdListCannotBeEmpty
untested: PositionsAccountSnapshotUpdateNoOp
untested: PositionsActivityRangeListIsEmpty
untested: PositionsDimensionValueIdDoesntExist
untested: PositionsDimensionValueUpdateNoOp
untested: PositionsHoldingIdDoesntExist
untested: PositionsHoldingUpdateNoOp
untested: PositionsInvestmentAccountIdDoesntExist
untested: PositionsPreLedgerBalanceListIsEmpty
untested: PositionsPropertyIdDoesntExist
untested: PositionsSecurityIdDoesntExist
untested: PositionsValuationIdDoesntExist
untested: PositionsValuationUpdateNoOp
untested: … (65 more from other domains)
AppError coverage: 312/391 cases referenced in Tests across 11 error types (report-only)
PASS  check-clock
PASS  check-compile-order
PASS  check-confirm-naming
PASS  check-hardwired-dates
PASS  check-npgsql
PASS  check-open-order
PASS  check-peer-guard
PASS  check-result-iserror
PASS  check-test-ddl
PASS  check-testingerror
PASS  check-tomessage-wildcard
SKIP  check-traceability
on branch 'positions-slice-2', not main: traceability is enforced on main after merge (README step 13)
----
12 passed, 0 failed, 1 skipped
```

### Traceability audit, Invariants 1 and 2

```
=== Invariant 1: phantom references (tests -> nonexistent or withdrawn requirement) ===
clean

=== Invariant 2: active requirements with no test, no waiver, and not unenforceable ===
clean
```

No active REQ-POS or REQ-RPT requirement is untested. Annotation counts: REQ-POS-15.7 has
5, REQ-POS-15.8 has 8, REQ-POS-7.4 has 4 and REQ-POS-12.8 has 1.

### ArchiMate

```
$ python3 Skills/ArchiMate/validate.py
Elements:      669
Relationships: 2852
Findings:      0

VALID — no issues found.
$ python3 Skills/ArchiMate/model_drift.py
NO DRIFT — the model and Src agree.
```

### Name-quality check

I ran one independent grader pass. It saw only the spec text of REQ-POS-15.7 and 15.8, the
waived and withdrawn tables, and the names.

**Weak names, and what I changed:**
- **The mortgage name (72).** It covered both create and update, and only checked
  "accepted". I split it in two, and each now asserts what was stored.
- **"Accepted" outcomes (78, 84).** Each now says "succeeds and" names the stored effect.
- **The ensure-range name (80).** It now names the first missing month's period and the
  dates.
- **The mixed-set name (82).** It gained "naming the period" and "no fiscal period is
  created".
- **The two update names (86).** Each now uses balances on two dates, so earliest and latest
  differ, and spells out the error.
- **The equality-boundary name (86).** I kept it as it is. The mixed-set and before-every
  names cover earliest and latest being different.

**Clauses no name covered, and what I added:**
- **Balances on more than one account.** The before-every name now places the earliest and
  latest balances on different ledger accounts.
- **A period starting before every balance.** I added a name for it, which also asserts
  every balance is unchanged.
- **Balances on another account don't block a link.** Both "once deleted" names now say they
  succeed while other accounts still carry balances.
- **Releasing a Property asset account.** I added the Property twin of the "once deleted"
  name.

**Ignored on purpose:**
- the "why" paragraphs
- the cross-referenced rules, which are tested under their own IDs
- the update case where an account is already linked and still carries balances, which
  REQ-POS-15.3 makes impossible

