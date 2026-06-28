# Test Fixture Design

Shared reference data for integrated tests, created once per test suite run via xUnit's
`ICollectionFixture<T>` mechanism.

## Rationale

Many integrated tests need accounts, fiscal periods, and journal entries to already exist
before the test-specific operation can run. Creating this graph from scratch in every test
is wasteful and obscures the actual behavior under test. A shared fixture provides rich,
realistic reference data that tests can operate on within rolled-back transactions.

## Architecture

```
TestDataFixture : IDisposable
├── Creates reference data in the test DB (committed, not transactional)
├── Anchors temporal data relative to Calendar.today()
├── Exposes IDs and codes as public properties
└── Dispose() tears down all created data

[CollectionDefinition("SharedTestData")]
public class SharedTestDataCollection : ICollectionFixture<TestDataFixture> { }

[Collection("SharedTestData")]
module SomeTests =
    // Tests in this collection receive the fixture via constructor injection
```

## What the fixture provides

### Accounts (relative to today)
- Multiple account types: Revenue, Expense, Asset, Liability
- At least one parent-child relationship
- At least one deactivated account (active_end in the past)
- All with known, stable codes for easy reference in tests

### Fiscal periods (relative to today)
- Current month: open
- Prior month: closed
- At least one future period: open
- Keys derived from `Calendar.today()` so they never go stale

### Journal entries
- A handful of posted JEs in the current and prior periods
- Lines spanning multiple accounts
- At least one JE with external references
- At least one JE with comments
- At least one voided JE

## Rules

1. **Fixture data is read-only by convention.** Tests never commit mutations to fixture
   entities. A test may modify fixture data inside a transaction — the rollback restores
   the original state.

2. **Tests own their mutations.** Any entity a test creates, updates, or voids must be
   either inside a rolled-back transaction (model/orchestrator tests) or manually cleaned
   up (CLI tests).

3. **Fixture data is realistic.** Use plausible account names, descriptions, and amounts.
   The fixture should resemble a small but real chart of accounts with real-looking
   activity, not a collection of "test1", "test2" placeholders.

4. **Temporal anchoring.** All dates are computed relative to `Calendar.today()` at fixture
   creation time. A fixture created today and a fixture created next month produce
   equivalent test conditions.

5. **Known identifiers.** The fixture exposes both UUIDs and codes/keys for all reference
   entities, so tests can use whichever the function under test requires.

## Implementation notes

- The fixture class lives in `Tests.Integrated` alongside `GenericTestProperties.fs`.
- F# xUnit collection fixtures require a class (not a module) for the fixture itself and
  for the collection definition. Test modules use `[<Collection("SharedTestData")>]`.
- Disposal order: JE comments, JE external references, JE lines, JE headers, accounts,
  fiscal periods (reverse dependency order).
- If a fixture entity fails to create, the fixture should fail loudly — `failwith` with a
  clear message. Broken fixture = broken test suite, not silent skips.

## Status

This fixture does not exist yet. It will be implemented when the first test-writing session
using this skill needs it. This document captures the design so that implementation is
straightforward.
