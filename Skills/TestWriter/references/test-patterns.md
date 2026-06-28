# Test Patterns

Reference for the two test execution patterns in SonOfLeo. Read existing tests in the
relevant area before writing new ones — match the actual code, not this document, when
they diverge.

## Isolated tests (pure functions, no DB)

Structure:
```fsharp
namespace Tests.Isolated.Model.Ledger

open Xunit
open Model.Ledger.SomeModule
open Utilities

module SomeModule =

    // Section headers for logical grouping
    // =============================================================================
    // Section name
    // =============================================================================

    [<Fact>]
    let ``REQ-XX-1.1 description of verification`` () =
        let input = constructInput ()
        let result = functionUnderTest input
        match result with
        | Ok value -> Assert.Equal(expected, value)
        | Error e -> Assert.Fail e
```

No setup, no teardown. All data constructed inline. Use `result { }` computation
expressions when chaining multiple Result-returning calls.

## Integrated tests (database, transactions)

Structure:
```fsharp
namespace Tests.Integrated.Model.Ledger

open Xunit
open Model.Ledger.SomeModule
open Model.Audit
open Utilities
open Tests.Integrated

module SomeModule =

    [<Fact>]
    let ``REQ-XX-2.1 description of verification`` () =
        let transaction = DAL.createDbTransaction() |> Result.defaultWith failwith

        try
            let railroad = result {
                let envelope = AuditEnvelope.create SomeAction
                let! entity =
                    constructAndSave ... (Some transaction)

                // assertions here
                Assert.Equal(expected, actual)
                return ()
            }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail e
        finally
            DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore
```

Key points:
- `defaultWith failwith` is acceptable for `createDbTransaction` — failure here means
  infrastructure is broken.
- Every database call passes `(Some transaction)`.
- The `finally` block always rolls back. No exceptions.
- Assertions go inside the `result { }` block after the operations they verify.
- The outer `match` catches any Error that propagated through the railway.

## CLI subprocess tests

Structure:
```fsharp
namespace Tests.Integrated.SonOfLeoCli

open Xunit
open Tests.Integrated
open Tests.Integrated.SonOfLeoCli.CliExecutor
open Model.UI.Json

module SomeRoutes =

    [<Fact>]
    let ``REQ-XX-3.1 description of verification`` () =
        // Setup: create required entities via model layer (committed, not transactional)
        let setupResult = result {
            let envelope = AuditEnvelope.create SomeAction
            let! entity = SomeModule.constructNewAndSaveToDb ... None
            return entity
        }
        let entity = setupResult |> Result.defaultWith failwith

        try
            // Act: invoke CLI
            let args = ["route"; "subcommand"]
            let payload = toJson someInput
            let exitCode, stdout, stderr = runCli args payload

            // Assert
            Assert.Equal(0, exitCode)
            let! returned = fromJson<SomeReturnType> stdout
            Assert.Equal(expected, returned.someField)
        finally
            // CLI commits data, so manual cleanup is required
            cleanUpEntityId (Some entity.id) |> ignore
```

CLI tests cannot use transaction rollback because the subprocess commits its own
transactions. Manual cleanup in the `finally` block is the necessary pattern here.

## Assertion style

Use xUnit assertions throughout:
- `Assert.Equal(expected, actual)` — note: expected first
- `Assert.True(condition)` / `Assert.False(condition)`
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
