# Tests

The single home for test doctrine. `Skills/TestWriter/` is the *procedure* for writing
tests; this is the *standard* they are written to. Each test directory has its own README
saying what belongs in that layer — read the one for the layer you are working in.

## Three projects

| Project | What it is |
|---|---|
| `Tests.Helpers` | Not a test project. The shared library both suites reference: `Railroad.fs`, `Cleanup.fs`, `GenericTestProperties.fs`, `EntityFunctions.fs`, `TestDataStage.fs`, `RouteResolver.fs`. No tests live here. |
| `Tests.Isolated` | Tests that never touch the database. Run in parallel. |
| `Tests.Integrated` | Everything that touches the database or the CLI process. Run serially. |

`SharedTestDataCollection.fs` lives in `Tests.Integrated` and can never move to
`Tests.Helpers`. xUnit reflects over the *test assembly it is executing* to find types
carrying `[<CollectionDefinition>]`; a definition in a referenced library is never
discovered. The fixture itself is shared; the two-line collection declaration is not.

## Hierarchy of testing layers

The various tests are stratified into layers that roughly follow the trajectory of core Model type tests -> user interface tests. The idea is that you test every happy path at each layer, but test every failure point only once, and at the lowest possible layer. Example, if you've already tested that the ModelOrchestrator.AccountBalance.fetchByAccountIdList excludes voided journal entries in its calculations, you should not also test that same concept in the UI functions that call that fetch.

Listed highest to lowest:
1. Tests.Integrated.SonOfLeoCli
2. Tests.Integrated.InterfaceBridge
3. Tests.Integrated.ModelOrchestrator
4. Tests.Integrated.Model
5. Tests.Integrated.DataAccessLayer
6. Tests.Isolated.Model

## The five test forms

Every test in this solution is one of five forms. The form dictates how — and whether — the
test cleans up after itself. Getting this wrong is the most consequential test mistake
available to you, because forms 3 and 4 look alike and behave oppositely.

Walk the tree in order:

**1. Does it touch the database?** No → **Form 1**, `Tests.Isolated`.
Operating on types built from primitives: the construction rules and the operations on the
constructed type. `AccountName.create`, `Money.splitByN`, `AccountType.fromString`. Nothing
to clean up. Runs in parallel.

Everything below here lives in `Tests.Integrated` and runs serially.

**2. Does it write to the database?** No → **Form 2**.
Read-only model and orchestrator interactivity. Reads fixture data directly; no transaction
needed. Nothing to clean up.

**3. Does the function being tested manage its own transaction?** No → **Form 3**.
The *test* owns the transaction: open it, pass `Some transaction` into the function under
test, assert, then roll back and dispose in `finally`. Nothing survives the test.

**4.** Yes — the function being tested owns its own transaction → **Form 4**.
It has already committed by the time it returns to you, so rollback is not available. The
test deletes what it created, by hand, in `finally`, using the `Cleanup.fs` helpers.
Capture the ID into a `let mutable idToCleanUp = None` the instant the create succeeds —
including on the `| Ok _ -> Assert.Fail "expected failure"` arm of a sad-path test. Children
before parents; the helpers take options so they no-op when nothing was created.

**5. Does the test invoke the CLI as a separate OS process?** → **Form 5**.
Same cleanup obligation as form 4, and for the same reason: the write happened in another
process and this one cannot roll it back.

All five are legitimate. None of them is a workaround.

## The silent-pass hazard — read this before writing an assertion

A `[<Fact>]` whose body evaluates to `Result<_, _>` **passes unconditionally**. xUnit 2.9.3
discards a non-`unit`, non-`Task` return value without complaint, so the `Error` branch
never reaches an assertion and the test reports green having verified nothing. This is why
`railroadWrapper` exists.

Terminate every railroad with it:

```fsharp
result {
    let! account = someOperation ...
    Assert.Equal(expected, account |> Account.accountName |> AccountName.value)
}
|> railroadWrapper
```

`railroadWrapper` (`Tests.Helpers.Railroad`) takes the `Result`, returns `unit`, and fails
the test with `AppError.toMessage` on the error branch. Both suites reference
`Tests.Helpers`, so it is available everywhere — there is no second rule for isolated tests.

A trailing `match railroad with | Ok _ -> () | Error e -> Assert.Fail ...` does the same job
and still appears in the route tests. It is the older form, it is easy to leave off, and
leaving it off is invisible. New tests use the wrapper.

## Assertion shape

- **Happy path:** railroad containing the asserts, terminated by `railroadWrapper`. A leaked
  error fails with its message rather than passing silently.
- **Sad path:** match the **typed DU case** — `| Error (JournalEntryDebitCreditMismatch _) -> ()`
  — with both escape arms: a wrong error fails with `Assert.Fail $"Wrong error. {…}"`, and
  `| Ok _ -> Assert.Fail "Expected failure; got success"` (capturing any created ID for
  cleanup). Never `Result.isError`. Never string-matching on error text.
- Assert on domain **values** — names, amounts, dates round-tripped — and on membership.
  Counts only in addition to values, never instead of them.

## Test data strategy

The test database is populated at test execution start by `TestDataFixture`
(`Tests.Helpers/TestDataStage.fs`), whose first act is `TRUNCATE … CASCADE` across all
ledger tables. This happens whether you run an individual test or the entire test suite.

**The database is left populated when the run ends.** Truncation is setup, not teardown —
the fixture is not `IDisposable`. Cleanup inside a run exists for *within-run* consistency:
expected values are derived from fixture data, so an entity a test forgot to delete will
break some later test's count. It does not exist to leave a clean database behind.

### The fixture

- Reference accounts carry `F-` prefixed codes. Ad-hoc codes created by a test use the REQ
  ID (`"AC-4.8"`), so a failed cleanup names the test that leaked it.
- Fiscal periods span -4 to +4 months from today, plus a closed period at -5.
- **The +4 period is reserved-empty.** No test may post an entry dated in it —
  `REQ-JE-3.3 fetchByPeriod returns empty list for period with no entries` depends on it.
- Hard-coded period keys in tests must fall outside that range. Use a distant year
  (`"2050-01"`).
- Dates derive from `Calendar.today()`. There are no hard-wired dates in the fixture and none
  may be added — month-boundary runs are supposed to be able to expose real bugs.
  `Checks/check-hardwired-dates.sh` enforces this.

### Test data advisories
- Do not ever architect a test that relies on data persisted by a previous run.
- Test data fixtures are created with dates relative to the system date/time, so do not count on any statically dated test data.
- Do not ever write tests that assume the count of anything in the database is a constant (ex: number of accounts of type "Asset"). Always calculate expected counts (using a different means than what you're testing) before the assertion phase of the test.
- When testing a filter, derive the filter target from the fixture data (e.g., find the most common amount via list operations), not from a hardcoded value. This ensures the test adapts if fixtures change — and also proves you're testing against real data, not a value you hope exists.
- **Tests do not create their own setup entities.** If a test needs an entity to exist before it can exercise the behavior under test, that entity belongs in the fixture. The only entities a test should create are the ones whose creation *is* the behavior being tested.
- It is okay to create new fixture data, but your first thought should be to see if an existing fixture can suffice. Before adding one, ask what *archetype* is missing — a closed account, a child of a particular subtype, a period in a given state. Three well-chosen archetypes beat fourteen single-purpose snowflakes.
- Do not leave the database in a mutated state relative to how you found it. Roll back or clean up, per your form.
- All tests--other than Tests.Isolated tests--run in series. This is what makes forms 4 and 5 possible at all.

### Test data creation by test type

- **Pure read operation tests**: rely fully on test data fixtures and create new fixtures if needed to support your test.
- **Creation tests in the CLI** (form 5):
  - Generate new entities inside of your test, but only for the type of creation you're testing. Ex: To test an Account create with a specific parent account, you don't have to create the parent. You can use an existing fixture as the parent and only create the child entity.
  - Use manual clean-up methods to delete anything you added to the DB
  - Wrap it all in a try / **finally** to ensure clean-up no matter what. `with` catches an exception; only `finally` guarantees the cleanup runs.
- **Creation tests in the InterfaceBridge, ModelOrchestrator, or Model** (forms 3 and 4):
  - Generate new entities inside of your test, but only for the type of creation you're testing.
  - Use transactions to clean-up created data unless the function you're testing manages its own transactions
  - Use manual clean-up methods to delete anything where you couldn't use a transaction.
  - Wrap it all in a try / **finally** to ensure clean-up no matter what
- **Update tests**:
  - Use the same strategy that you would for creation tests regarding data clean-up
  - It is okay to update an existing fixture if you can roll-back with a transaction (eg closing a fiscal period).
  - It is not okay to update an existing fixture if the update function manages its own transaction (eg voiding a journal entry).

## Bullshit test practices (unacceptable and deserving of ridicule)
- Do not test the same thing twice. Ex: "Test that invalid input fails" by using an empty string in the "source" field and then "Test that empty source string fails" further down the file.
- Do not test all possible failure vectors at all levels. All vectors should be tested, but only once, at their lowest possible level.
- Do not assert failure without asserting an exact error. The wrong error code may be "only unhelpful" but it could also be masking a deeper problem.
- Do not assert imprecise counts (number on Asset accounts > 2). I want to know that you know you should have 6 and expect exactly 6.
- Do not write tests unless you have a behavioral REQ to cite. If the code you are testing does something uncited by the REQs, stop and point that out. Likely an REQ needs to be added.

Worked before-and-after examples of each: `Skills/TestWriter/references/bullshit-test-specimens.md`.

## Admirable test practices (encouraged and deserving of praise)
- Tests should strive to be atomic, testing only one behavior per test. There are times when it is reasonable to go against this guidance, but that decision should be weighed carefully. 
- Use XUnit's Theory construct to test multiple similar behaviors where you can.
- Test all public functions in a module (at least the happy path)
- Test all permutations of a function's parameters unless that parameter is a pure passthrough to a function tested at a lower-level. Ex, if a fetchByFiltered function allows you to filter on 4 different values, test all 4 independently.
- Asserting equality is preferred to asserting truth. You should know what you expect and you should assert the outcome is exactly what you expect.

## Naming

Every test name starts with the requirement ID(s) it verifies, then the behavior:
``REQ-JE-1.12 constructNewAndSaveToDb rejects entry with fewer than 2 lines``. Integrated
tests are class members in `[<Collection("SharedTestData")>]` classes; isolated tests are
module-level `let` bindings.
