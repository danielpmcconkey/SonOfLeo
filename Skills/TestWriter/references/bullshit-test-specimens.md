# Bullshit-Test Specimens

Negative exemplars harvested from the July 2026 test-suite rewrite (commits
`bba1c17..5cb26c4`), where these were purged from the real codebase. Each specimen is
a pattern that **passed CI while verifying nothing** — the test equivalent of a lock
that opens for any key. If a test you are writing resembles a "before", stop and write
the "after".

The authority for the rules is `Tests/README.md` — assertion shape and the
bullshit-practice list. These are the labeled corpses.

---

## Specimen 1 — the hard-wired count

**Before (purged in `12bea81`):**
```fsharp
let! payload = { parentCode = "F-1000" } |> toJson<AccountFetchByParentCodeInput>
// ...
Assert.Equal(3, fetchedChildren |> List.length)
```

**Why it's worthless:** `3` encodes what the fixture happened to contain the day the
test was written. Add a fixture account and this test fails for the wrong reason;
worse, if the fetch under test silently drops a child *and* someone removes a fixture
account, it passes while broken. The magic string `"F-1000"` has the same disease.

**After:**
```fsharp
let parentId = fixture.Data.assets1000Id
let parentAccount = fixture.Data.accounts |> List.filter(fun a -> a |> Account.accountId = parentId) |> List.head
let parentCode = parentAccount |> Account.code |> AccountCode.value
let expected =
    fixture.Data.accounts
    |> List.filter(fun a -> a |> Account.parentId = (Some parentId))
    |> List.length
// ...
Assert.Equal(expected, fetchedChildren |> List.length)
```
The expected value is *derived from the same fixture data the code under test reads*. The test now states the relationship, not a snapshot.

## Specimen 2 — the cowardly inequality

**Before (purged in `12bea81`):**
```fsharp
Assert.True(fetchedAccounts |> List.length >= 14)
```

**Why it's worthless:** `>= 14` is an assertion that gave up. It tolerates duplicates,
tolerates rows leaking in from other filters, and encodes a stale floor. A fetch
returning every row in the table passes this test.

**After:**
```fsharp
let expected = fixture.Data.totalAccounts
Assert.Equal(expected, fetchedAccounts |> List.length)
```

## Specimen 3 — the count that never looks inside

**Before (purged in `94cf9dc`):**
```fsharp
Assert.Equal(1, balances |> List.length)
Assert.True(bal.totalDebits |> Money.amount > 0M)
```

**Why it's worthless:** it checks that *a* row came back and that *some* money exists.
The actual business logic under test — that debits, credits, and net balance are
computed correctly per account type — is never examined. A sign-flipped balance
calculation passes.

**After:**
```fsharp
let expectedDebits1 = fixture.Data.journalEntryLines |> sumJournalEntryLinesByAccountIdAndType false id1 Debit
let expectedCredits1 = fixture.Data.journalEntryLines |> sumJournalEntryLinesByAccountIdAndType false id1 Credit
let expectedBal1 = expectedCredits1 - expectedDebits1 // liability: credit-normal
// ...
Assert.Equal(expectedDebits1, row1.totalDebits |> Money.amount)
Assert.Equal(expectedCredits1, row1.totalCredits |> Money.amount)
Assert.Equal(expectedBal1, row1.netBalance |> Money.amount)
```
Counts are allowed *in addition to* value assertions, never instead of them.

## Specimen 4 — the untyped failure

**Before (purged in `bba1c17`):**
```fsharp
Assert.True(Result.isError result)
// — or its string-matching sibling —
Assert.Contains(expectedError, e)
```

**Why it's worthless:** `isError` passes for *any* failure — the wrong validation
firing, a broken DB connection, a typo'd column name. String `Contains` is the same
bug plus brittleness: reword the message and the test breaks; produce the wrong error
with similar wording and it passes.

**After (the sad-path canon):**
```fsharp
match railroad with
| Error (JournalEntryDebitCreditMismatch _) -> ()
| Error e -> Assert.Fail $"Wrong error. {AppError.toMessage e}"
| Ok _ -> Assert.Fail "Expected failure; got success"
```
The typed case is the assertion. Both escape arms are mandatory — without the
`Ok _` arm a silently-succeeding operation passes a failure test.

## Specimen 5 — the exit-code-only CLI test

**Before (purged in `bba1c17`):**
```fsharp
Assert.Equal(expectedReturnCode, code)
```
…as the *only* assertion on a CLI operation's behavior.

**Why it's worthless:** exit code 1 means "something failed somewhere." As a lone
assertion it verifies plumbing, not behavior. This is why route-level
tests are split from process-level tests: the route tests make real assertions on
deserialized returns and typed errors; the thin `ProgramTests` class checks the process
boundary. Exit codes belong only to the latter, and those tests are deliberately few.

**After:** test the behavior at the route level via `routeUiCommandForTesting` with
typed assertions; keep process-level tests to plumbing (exit codes, stdout/stderr,
case-sensitivity) and nothing else.

---

## The smell test

Before presenting any test, ask: **"if the function under test returned garbage of
the right shape, would this test fail?"** If the answer is no — count-only,
inequality, `isError`, exit-code-only — the test is one of these specimens wearing
a new name.
