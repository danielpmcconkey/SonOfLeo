# Test Patterns

Reference for test patterns in SonOfLeo. Read existing tests in the relevant area before
writing new ones — match the actual code, not this document, when they diverge.

## Isolated tests (pure functions, no DB)

Isolated tests use module-level functions. No fixture, no database, no setup/teardown.

```fsharp
namespace Tests.Isolated.Model.Ledger

open Xunit
open Model.Ledger.SomeModule

module SomeModule =

    // =============================================================================
    // Section name
    // =============================================================================

    [<Fact>]
    let ``REQ-XX-1.1 description of verification`` () =
        let result = functionUnderTest input
        Assert.True(Result.isOk result)
```

All data constructed inline. Use `result { }` computation expressions when chaining
multiple Result-returning calls.

## Integrated tests (class-based, fixture-aware)

All integrated tests are class members receiving `TestDataFixture` via constructor
injection, grouped under `[<Collection("SharedTestData")>]`.

### Reading fixture data (no transaction needed)

When a test only reads committed fixture data, no transaction is required:

```fsharp
namespace Tests.Integrated.Model.Ledger

open Xunit
open Tests.Integrated
open Utilities.ResultCE

[<Collection("SharedTestData")>]
type SomeTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-XX-3.5 fetch by parent ID returns children`` () =
        let railroad = result {
            let! fetched = SomeModule.fetchByParentId None fixture.Data.parentId
            Assert.Equal(3, List.length fetched)
            return ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e
```

### Mutating fixture data (transaction + rollback)

When a test modifies fixture data (updates, deactivation), wrap in a transaction:

```fsharp
    [<Fact>]
    member _.``REQ-XX-4.8 update name succeeds`` () =
        let envelope = AuditEnvelope.create SomeAction
        let transaction = DAL.createDbTransaction() |> Result.defaultWith failwith

        try
            let railroad = result {
                let! updated =
                    SomeModule.updateNameById fixture.Data.someEntityId "new name" envelope (Some transaction)
                Assert.Equal("new name", SomeModule.name updated)
                return ()
            }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail e
        finally
            DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore
```

### Creating test-specific data (transaction + rollback)

When a test needs entities the fixture doesn't provide (e.g., invalid states, edge cases):

```fsharp
    [<Fact>]
    member _.``REQ-XX-2.7 rejects inactive parent`` () =
        let transaction = DAL.createDbTransaction() |> Result.defaultWith failwith

        try
            let childResult =
                SomeModule.constructNewAndSaveToDb "test" ... (Some fixture.Data.closedEntityId)
                    ... (Some transaction)
            Assert.True(Result.isError childResult, "Should have rejected inactive parent")
        finally
            DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore
```

### CLI subprocess tests

CLI tests commit data via the subprocess and cannot use transaction rollback. Tests that
read can use fixture data directly. Tests that create or mutate need manual cleanup.

```fsharp
    [<Fact>]
    member _.``REQ-XX-3.4 FetchByCode happy path`` () =
        let args = ["Domain"; "FetchByCode"]
        let payload = { code = "F-1270" } |> toJson<FetchInput> |> Result.defaultWith failwith
        let code, _, e = runCli args payload
        match code with
        | 0 -> ()
        | _ -> Assert.Fail $"FetchByCode returned non-zero: {e}"
```

CLI tests that create committed data still need cleanup in `finally`:

```fsharp
    [<Fact>]
    member _.``REQ-XX-2.21 Create happy path`` () =
        let mutable idToCleanUp = None
        try
            let railroad = result {
                let! account = createInDb "TestCode"
                idToCleanUp <- Some (getId account)
                // ... invoke CLI, assert ...
                return ()
            }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail e
        finally
            match cleanUpId idToCleanUp with
            | Ok () -> ()
            | Error e -> failwith e
```

## Fixture reuse principle

Before creating setup data, check whether the fixture already provides what the test
needs. Common reuse patterns:

- **Need an active account?** Use `fixture.Data.moneyMarket1270Id` (or any non-closed
  fixture account).
- **Need an inactive account?** Use `fixture.Data.closedBank1290Id`.
- **Need a parent with children?** Use `fixture.Data.assets1000Id` (has 3 children).
- **Need to test uniqueness?** Try creating a duplicate of a fixture entity code (e.g.,
  `"F-1250"`).
- **Need to test type mismatch?** Create a child of wrong type under a fixture parent.
- **Need an account to update?** Update a fixture account inside a rolled-back transaction.

Do not assert exact counts on queries that return sets (`fetchAll`, `fetchByType`). The
fixture populates the database, so counts are unpredictable. Assert containment of
expected IDs instead.

## Assertion style

Use xUnit assertions throughout:
- `Assert.Equal(expected, actual)` — note: expected first
- `Assert.True(condition)` / `Assert.True(condition, "failure message")`
- `Assert.False(condition)`
- `Assert.Null(value)`
- `Assert.Single(collection)`
- `Assert.Contains(substring, string)`
- `Assert.Fail(message)` — for explicit failure in error branches

## Result railway in tests

When a test chains multiple Result-returning operations:
```fsharp
let railroad = result {
    let! a = operation1 ()
    let! b = operation2 a
    Assert.Equal(expected, b)
    return ()
}
match railroad with
| Ok _ -> ()
| Error e -> Assert.Fail e
```

Use `Result.mapError` with descriptive messages when the source of an error would
otherwise be ambiguous:
```fsharp
let! account =
    Account.fetchById id (Some transaction)
    |> Result.mapError (fun e -> $"Failed to fetch account for test setup: {e}")
```
