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

*(The build session writes this.)*
