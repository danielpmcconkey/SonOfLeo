# Test Fixture Design

Shared reference data for integrated tests, implemented via xUnit's
`ICollectionFixture<TestDataFixture>` in `Tests/Tests.Integrated/_TestDataStage.fs`.

## Architecture

```
TestDataFixture : IDisposable
├── Constructor: creates reference data (committed, no transaction)
├── Data: FixtureData record exposing all entity IDs
├── Dispose(): TRUNCATE CASCADE on all ledger tables
│
├── FixtureData record fields:
│   ├── Account IDs (assets1000Id, moneyMarket1270Id, closedBank1290Id, etc.)
│   └── fiscalPeriodIds: Guid list
│
[<CollectionDefinition("SharedTestData")>]
SharedTestDataCollection : ICollectionFixture<TestDataFixture>
│
[<Collection("SharedTestData")>]
type SomeTests(fixture: TestDataFixture) =
    [<Fact>]
    member _.``test name`` () =
        fixture.Data.someEntityId |> ...
```

## What the fixture provides

### Accounts (14 total, all with `F-` prefix codes)

Top-level (5):
- `F-1000` Assets (Asset)
- `F-2000` Liabilities (Liability)
- `F-3000` Equity (Equity)
- `F-4000` Revenue (Revenue)
- `F-5000` Expenses (Expense)

Children (9):
- `F-1250` Roth IRA (Asset/Investment, child of F-1000)
- `F-1270` Money Market (Asset/Cash, child of F-1000)
- `F-1290` Closed Bank (Asset/Cash, child of F-1000, **deactivated** 2 months ago)
- `F-2210` Mortgage Payable (Liability/LongTermLiability, child of F-2000)
- `F-2220` Credit Card (Liability/CurrentLiability, child of F-2000)
- `F-3030` Retirement Contributions (Equity, child of F-3000)
- `F-4290` Personal Revenue (Revenue/OperatingRevenue, child of F-4000)
- `F-5350` Food (Expense/OperatingExpense, child of F-5000)
- `F-5650` Entertainment (Expense/OperatingExpense, child of F-5000)

All accounts have `activeBegin` set to one year ago. Only `F-1290` has an `activeEnd`
(two months ago).

### Fiscal periods (10 total)

Created for months -4 through +4 relative to `Calendar.today()`, all open, plus one
closed period at -5 months (closed after its fixture JE was created). Keys are derived
dynamically (e.g., if today is June 2026: `"2026-02"` through `"2026-10"`).

**The +4-month period is reserved-empty.** No test may post an entry dated in it — the
`fetchByPeriod` empty-list test depends on it staying empty.

Tests that create their own fiscal periods must use keys outside this range — distant
years like `"2050-01"` are safe. The `genericFiscalPeriodKey` in `GenericTestProperties`
is set to `"2050-01"` for this reason.

### Journal entries

Archetypes (all dated today unless noted):
- `basicJeId` — 2 lines (Mortgage debit / Food credit), one committed fixture comment
  (`fixtureCommentId`), and the consumable CLI update-victim ext ref
  (`cliUpdateVictimExtRefId`, "CliUpdateVictimBank"/"CLI-UPDVIC-001")
- `jeWithRefId` — 2 lines, ext ref `jeWithRefExtRefId` ("TestBank"/"TXN-001").
  **This ref must survive every run** — the fetchByReference tests read it. Mutate it
  only inside a rolled-back transaction.
- `sharedRefJe1Id` / `sharedRefJe2Id` — two entries sharing one ext ref
  ("SharedBank"/"F-SHARED-001") for shared-reference fetch tests
- `voidedJeId` — pre-voided (direct UPDATE) with a voiding-reason comment
- `jeInClosedPeriodId` — dated in the closed -5 month period
- `voidVictim1Id`/`voidVictim2Id`/`voidVictim3Id` — consumable victims, one per voiding
  happy-path test; their voided end-state after a run is by design

### Consumable victims

Irreversible committed operations (voiding, CLI-layer updates that cannot roll back
across a subprocess) each get a dedicated victim entity — one per test, never shared.
End-state after consumption doesn't matter; the next fixture build starts fresh. When a
new irreversible-op test appears, add a new victim rather than reusing one.

## Rules

1. **Reuse aggressively.** Do not create setup entities when the fixture already has what
   the test needs. The fixture provides active accounts, an inactive account, parent-child
   relationships, and fiscal periods.

2. **Read-only by convention.** Tests may read fixture data directly (no transaction). Tests
   may mutate fixture data within a transaction that rolls back. Tests must never commit
   mutations to fixture data — except consumable victims, which exist to be consumed.

3. **Tests own their mutations.** Any entity a test creates, updates, or voids is either
   inside a rolled-back transaction (model/component tests), manually cleaned up in
   `finally` via `_Cleanup.fs` (orchestrator-committing and CLI tests), or a consumable
   fixture victim (irreversible ops).

4. **Temporal anchoring.** All dates are computed relative to `Calendar.today()` at fixture
   creation time. A fixture created today and one created next month produce equivalent
   test conditions.

5. **No exact count assertions.** Queries like `fetchAll` and `fetchByAccountType` return
   fixture data plus any test-created data. Assert containment of expected IDs, not exact
   row counts.

6. **Known identifiers.** The fixture exposes UUIDs via `fixture.Data.<fieldName>`. Account
   codes are the `F-` prefixed strings used during creation and can be used directly in
   CLI tests.

## Cleanup

`Dispose()` runs `TRUNCATE ... CASCADE` on all six ledger tables:
- `ledger.journal_entry_comment`
- `ledger.journal_entry_ext_reference`
- `ledger.journal_entry_line`
- `ledger.journal_entry`
- `ledger.account`
- `ledger.fiscal_period`

No per-entity tracking or dependency ordering needed. If the fixture creation partially
fails, the constructor throws and xUnit skips the test collection. The truncate on the
next successful run cleans up any orphaned data.

## Adding new fixture entities

To add a new fixture entity:
1. Add the creation call in `TestDataFixture`'s constructor `result { }` block
2. Add a field to the `FixtureData` record type
3. Set the field in the `return { ... }` block
