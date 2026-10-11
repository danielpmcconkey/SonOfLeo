# test-efficacy-SystemWide

## EFF-SYS-3.2-SLICE2-1 — test-gap
- **Location:** REQ-SYS-3.2, REQ-SYS-3.3. Tests/Tests.Integrated/CrossDomainOrchestration/OperationInstantAndAtomicity.fs:491-528 (the two Person/Positions theories). Untested sites: Src/Business.CrossDomainOrchestration/InvestmentActivityOrchestration.fs:109 (insert instant), Src/Business.CrossDomainOrchestration/PreLedgerBalanceOrchestration.fs:78-92 (insert), Src/Business.FinancialServices.Positions/PreLedgerBalance.fs:194-205 (replaceBalance writes modified_at = @modified)
- **Summary:** No test checks created-at or modified-at for either of the two new timestamped Positions records, Investment Activity and Pre-ledger Balance. The in-place balance replace that Pre-ledger Balance recording performs (an update that must move modified-at and keep created-at) is also unverified.
- **Resolution:** fix-test
- **Prior ruling:** Audit 2026-10-04a #085 (EFF-SYS-3.2-POS-1), accepted: 'Add Person and every Positions entity to the REQ-SYS-3.3 theory... assert created = modified = creating instant in one create test per entity.' That ruling covered the slice-1 entities. The slice-2 entities did not exist then, so this is the ruling's scope extended to them, not a re-flag.

Migrations 202610101010 and 202610101020 give positions.investment_activity and positions.pre_ledger_balance NOT NULL created_at/modified_at columns. Both domain types expose createdAt/modifiedAt (InvestmentActivity.fs:43-44, PreLedgerBalance.fs:29-30), and the InvestmentActivityReturn converter even returns them (PositionsFieldConverters.fs, `createdAt = activity |> InvestmentActivity.createdAt`). Recording a Pre-ledger Balance for an account and date that already has one runs PreLedgerBalance.replaceBalance, a real UPDATE that sets `modified_at = @modified` from Context.getInitiationInstant and returns `{ existing with balance = balance; modifiedAt = modifiedAt }`. Under REQ-SYS-3.3 that path must move modified-at to the updating instant and leave created-at alone.

The REQ-SYS-3.2 and REQ-SYS-3.3 theories in OperationInstantAndAtomicity.fs list their record kinds in InlineData rows: "Person", "Dimension Value", "Security", "Investment Account", "Holding", "Account Snapshot", "Property", "Valuation". Neither new kind is among them. A grep of Tests/ for `InvestmentActivity.createdAt`, `InvestmentActivity.modifiedAt`, `PreLedgerBalance.createdAt` and `PreLedgerBalance.modifiedAt` returns nothing. The new suites (InvestmentActivityRecording, PreLedgerBalances, PreLedgerBalanceGuards, PositionsSliceTwoRoutes) use the initiation instant only to derive a current date. The REQ-POS-15.4 replace test, PreLedgerBalances.fs:103, asserts the new balance and the replaced flag but never a timestamp.

Smell test: suppose replaceBalance dropped `modified_at = @modified`, or an activity insert stamped a stale or fresh-clock instant. Every test in the suite would still pass.

This is the same gap audit 2026-10-04a raised for slice 1 (#085, accepted). Slice 2 reopens it for the two entities it adds. It also contradicts Dan's statement that 'every active requirement is tested': REQ-SYS-3.2 and 3.3 apply system-wide, and nothing covers them for the new records.

**Action:** Add "Investment Activity" and "Pre-ledger Balance" cases to createdRecord in OperationInstantAndAtomicity.fs and as InlineData rows of the REQ-SYS-3.2 theory. Add a "Pre-ledger Balance (re-recorded)" row to the REQ-SYS-3.3 theory: record under the creating context, re-record the same account and date with a different balance under TestContext.updateInitiationInstant, then read back and assert (createdAt, modifiedAt) = (creating instant, updating instant).

**Why:** REQ-SYS-3.2/3.3 are system-wide. Each table has its own INSERT and UPDATE statements, so evidence about one table says nothing about another's. The previous CashFlow audit found five UPDATEs that silently never wrote modified_at, and a missing theory row is how that kind of defect survives.

---

## EFF-SYS-3.3-UNCOVERED-UPDATES — test-gap
- **Location:** REQ-SYS-3.3. Tests/Tests.Integrated/CrossDomainOrchestration/OperationInstantAndAtomicity.fs:237-242 (theory rows); Tests/Tests.Integrated/Model/Ledger/FiscalPeriod.fs (closeFiscalPeriod and reopenFiscalPeriod happy-path tests). Untested sites: Src/Business.FinancialServices.CashFlow/Instance.fs:204 (cancel) and :238 (update), Invoice.fs:421 (update) and :442 (cancel), Payment.fs:326 (update), Src/Business.FinancialServices.Ledger/FiscalPeriod.fs:155-170 (toggle open/closed)
- **Summary:** Seven UPDATE statements that set modified_at have no test asserting that modified-at becomes the updating operation's instant: Instance update and cancel, Invoice update and cancel, Payment update, and Fiscal Period close and reopen. Audit 10-03a accepted adding the CashFlow ones to the REQ-SYS-3.3 theory, and they are not there.
- **Resolution:** fix-test
- **Prior ruling:** Audit 2026-10-03a #128 (EFF-SYS-3.3-1), accepted: 'test-agent adds those five [MasterAgreement, PaymentAgreement, Instance, Invoice, Payment] as rows of the REQ-SYS-3.3 theory.' Master Agreement and Payment Agreement are covered by other tests. Instance, Invoice and Payment rows are absent from the theory today, so this re-raises the unimplemented remainder of an accepted ruling. Fiscal Period close and reopen were not in that ruling's scope.

A grep of Tests/ for `Instance.modifiedAt`, `Invoice.modifiedAt` and `Payment.modifiedAt` finds only:
- AgreementCreation.fs:159-161, the create path (created = modified = now);
- PaymentsToPosted.fs:256 and :380, which assert modified-at is UNCHANGED by an idempotent transition.
No test performs an Instance, Invoice or Payment update or cancel and then checks modified-at.

The REQ-SYS-3.3 theory in OperationInstantAndAtomicity.fs has four rows: "amending a comment's text", "re-pointing a comment's secondary journal entry", "voiding a journal entry", "re-pointing a payment agreement link". Master Agreement (SameValueUpdates.fs:93) and Payment Agreement (PaymentAgreementUpdate.fs:207/327) are covered elsewhere. Instance, Invoice and Payment are not.

For Fiscal Period, FiscalPeriod.toggle writes `modified_at = @modified`. The close and reopen happy-path tests in Model/Ledger/FiscalPeriod.fs assert only isOpen, id and periodKey. The only modified-at assertions in that file (lines 185-195 and 221-231) check that a rejected no-op leaves modified-at unchanged. None checks that a successful close or reopen moves it.

Smell test: delete `modified_at = @modified` from any of these seven statements and no test goes red. That is the defect audit 10-03a found live in CashFlow.

**Action:** Add InlineData rows to the REQ-SYS-3.3 theory in OperationInstantAndAtomicity.fs for 'updating an Instance', 'cancelling an Instance', 'updating an Invoice', 'cancelling an Invoice', 'updating a Payment', 'closing a fiscal period' and 'reopening a fiscal period'. Each row reads the record back and asserts (createdAt, modifiedAt) = (creating instant, updating instant). For the fiscal period rows, use an open fixture period, with the creating instant taken from the stored period read before the toggle.

**Why:** REQ-SYS-3.3 says every successful update sets modified-at. Each UPDATE statement is independent code, and the theory row is the only thing that would catch one losing its modified_at clause. That happened to five CashFlow UPDATEs before.

---

## EFF-SYS-6.1-EMPTY-LISTS — test-gap
- **Location:** REQ-SYS-6.1 (with REQ-POS-13.1 and REQ-POS-15.4 'one or more'). Src/Business.CrossDomainOrchestration/InvestmentActivityOrchestration.fs:139 (PositionsActivityRangeListIsEmpty); Src/Business.CrossDomainOrchestration/PreLedgerBalanceOrchestration.fs:101 (PositionsPreLedgerBalanceListIsEmpty). Sibling test: Tests/Tests.Integrated/InterfaceBridge/PositionsRoutes.fs:1151
- **Summary:** The guards that stop slice 2's two new batch-record operations from succeeding as silent no-ops on an empty list have no test.
- **Resolution:** fix-test
- **Prior ruling:** Audit 2026-10-04a #020 (CDO-COV-POS-7), accepted, required the same guard plus a route test for AccountSnapshot Record. These are the two new operations of the same shape, so the precedent supports the finding rather than suppressing it.

recordActivity begins with `do! if ranges |> List.isEmpty then error PositionsActivityRangeListIsEmpty else Ok()`. recordPreLedgerBalances begins with `do! if inputs |> List.isEmpty then error PositionsPreLedgerBalanceListIsEmpty else Ok()`. A grep of Tests/ for either error case, or for an empty ranges or balances payload, returns nothing.

The slice-1 sibling has a test: PositionsRoutes.fs:1151, `REQ-POS-7.1 REQ-SYS-6.1 an AccountSnapshot Record payload with an empty list of snapshots is rejected with a typed no-snapshots error`, which uses `expectError (function AsError PositionsSnapshotListIsEmpty -> Some() | _ -> None)`. It exists because audit 10-04a #020 found that operation silently succeeding on [] and required both the guard and a test.

These two guards do not fall under Dan's 'error cases that only reject loudly are untested by design'. Without them the operation does not reject loudly; it returns Ok with nothing written. That is exactly the silent no-op REQ-SYS-6.1 forbids. Delete either guard and no test fails.

Note that REQ-POS-13.2's empty activity list within one range is the documented REQ-SYS-6.1.1 exception. An empty list of ranges is not.

**Action:** Add two route tests in PositionsSliceTwoRoutes.fs, citing REQ-POS-13.1 REQ-SYS-6.1 and REQ-POS-15.4 REQ-SYS-6.1. Send an InvestmentActivity Record payload with `ranges: []` and a PreLedgerBalance Record payload with an empty balance list. Assert the typed PositionsActivityRangeListIsEmpty and PositionsPreLedgerBalanceListIsEmpty errors, each with both escape arms.

**Why:** REQ-SYS-6.1 exists to surface a caller who believes something happened when nothing did. A guard whose only job is to stop a silent success has to be pinned by a test, because removing it produces no loud failure that some other test would catch.

---

## CON-SYS-3.1-LOT — contradiction
- **Location:** REQ-SYS-3.1; Specs/Definitions.md:54-55 ('Component rows'); DbMigration/Scripts/202610101000-CreateAccountSnapshotLotTable.sql; Src/Business.FinancialServices.Positions/AccountSnapshotLot.fs
- **Summary:** positions.account_snapshot_lot has no created_at or modified_at. The Definitions.md 'Component rows' exemption lists its tables by name, and the lot table is not among them, so under the higher authority REQ-SYS-3.1 applies to lots and the schema does not meet it.
- **Resolution:** fix-spec
- **Prior ruling:** Audit 2026-10-04a #097 (SYS31-POS-1), accepted: Hobson added the Definitions 'Component rows' entry for the four slice-1 tables, with #126, #169, #180 and #194 as duplicates. The lot table postdates that ruling and is not named in it, so matching it requires squinting. Re-raised for an explicit addition.

The lot migration's comment says: 'A lot is a component of its line, like the line is of its snapshot: no timestamps.' Definitions.md:55 defines Component rows by enumeration: 'an account snapshot line (`positions.account_snapshot_line`, part of an account snapshot), an owner set (`positions.investment_account_owner`, `positions.property_owner`) and a property's ledger-account sets (`positions.property_asset_account`, `positions.property_mortgage_account`, part of a property)... A component row is **not an entity**... REQ-SYS-3.1 does not apply.' `positions.account_snapshot_lot` is not on that list, and the 2026-10-10 Definitions change added only 'Pre-ledger date'.

A lot has its own UUID primary key, is inserted by a user operation (recording a snapshot), and is deleted by code before its line (commit 4f72476). By Definitions' own terms it is an entity unless exempted, so REQ-SYS-3.1 (waived from testing, so no test can catch this) requires the timestamps.

The code's intent, a component written and removed only with its parent, matches the exemption's rationale. Only the authority is missing. Per the authority hierarchy, Definitions (2) outranks code and config (5).

**Action:** Add `positions.account_snapshot_lot` (part of an account snapshot line) to the Component rows enumeration in Specs/Definitions.md. Alternatively, if Dan wants lots to be entities, add created_at and modified_at to the table and type.

**Why:** The exemption was written as a closed list precisely so that each new timestampless table has to be justified explicitly. Leaving the lot table off it means the spec says an entity is missing its audit columns, and the next auditor or agent will either re-flag it or 'fix' the schema against the design intent.

---

