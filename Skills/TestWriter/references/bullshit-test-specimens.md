# Bullshit-Test Specimens

Negative exemplars harvested from two sweeps of the real codebase: the July 2026
test-suite rewrite (commits `bba1c17..5cb26c4`), and the August 2026 data-ingestion
review that produced specimens 7 through 9. Each specimen is
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
Counts are allowed *in addition to* value assertions, never instead of them. When the
thing being counted is a finite enumeration, see Specimen 10 — the fix there is a truth
table, not a bigger count.

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

**The same disease in the idiom this codebase writes (August 2026):**
```fsharp
return!
    match someOperation context with
    | Error _ -> Ok ()
    | Ok _ -> Error (TestingError "Expected failure; got success")
```
`| Error _ -> Ok ()` is `Result.isError` wearing a match expression. It was
present in six ingestion tests. Replacing each bare arm with one that *reported*
what it had actually caught revealed that four of them were passing on a raw data
access layer error — `Resultant rows didn't match expectation. Expected ExactlyOne.
Actual 0.` — which is what a user got when they named an unknown bank. Nobody knew,
because nobody had ever asked the test what it was catching. Four leaks, one grep.

If you cannot name the error case, you do not know what the code does, and the test
does not either. Find out before you write the arm: replace it with
`| Error e -> Error (TestingError $"DISCOVERED[{AppError.toMessage e}]")`, run it once,
and read the answer off the failure.

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

## Specimen 6 — the fox guarding the hen house

**Before (caught in review, August 2026):**
```fsharp
// Testing fetchTrialBalanceData, which internally calls fetchByAccountIdList
let! balances = AccountBalance.fetchByAccountIdList context (Some [leafId]) None
let expectedDebits = (balances |> List.head).totalDebits |> Money.amount
let expectedCredits = (balances |> List.head).totalCredits |> Money.amount
let! rows = fetchTrialBalanceData context nextMonth
let leafRow = rows |> List.find(fun r -> r.accountCode = leafCode)
Assert.Equal(expectedDebits, leafRow.totalDebits |> Money.amount)
```

**Why it's worthless:** `fetchTrialBalanceData` calls `fetchByAccountIdList` internally.
Both sides of the assertion run the same code. If the balance computation is wrong —
sign-flipped, off-by-one in the as-of filter, miscounting voided entries — both the
expected and actual values are wrong in the same way, and the test passes. You have
tested that a function agrees with itself.

**After:**
```fsharp
let unvoidedLines =
    fixture.Data.journalEntries
    |> List.filter(fun je ->
        je |> header |> JournalEntryHeader.voidedAt |> Option.isNone)
    |> List.collect lines
let expectedDebits =
    unvoidedLines
    |> List.filter(fun l ->
        l |> JournalEntryLine.accountId = leafId
        && l |> JournalEntryLine.lineType = Debit)
    |> List.sumBy(fun l -> l |> JournalEntryLine.amount |> Money.amount)
// ...
Assert.Equal(expectedDebits, leafRow.totalDebits |> Money.amount)
```
Expected values are derived from the raw fixture data — the journal entry lines that
were staged into the database — using only list operations, not any function in the
system under test's call chain. If the balance computation is wrong, the expected value
is right and the test fails.

The rule: **no function in the call chain of the function under test may appear in the
derivation of the expected value.** This applies to all expected values — amounts,
counts, codes, dates — not just counts.


## Specimen 7 — the test that never asserts

**Before (purged August 2026, and it held three requirements at once):**
```fsharp
[<Fact>]
member _.``REQ-STG-9.3 posted JE has description and entry_date from staged entry`` () =
    runCommandRouteAndAutoRollback IngestPostStageEntries (fun context ->
        result {
            let! fullResult = StageTestData.runPipeline context
            let entry = fullResult.stagedEntries |> StageTestData.findByDescription "HARRIS TEETER 0381 ANYTOWN US"
            let! jeSource =
                Some "Data ingestion import"
                |> convertOptionToDesiredTypeWithFallibleConverter JournalEntrySource.create
            do! postStageEntry context jeSource entry
        })
    |> railroadWrapper
```

**Why it's worthless:** there is no assertion in it. It proves the call returned
`Ok`. Every mapping the requirement describes — description, entry date, account
resolution, external reference — is unexamined. Break any of them and this stays
green. Two sibling tests carried nearly the same body under different REQ IDs, so
one absent assertion was doing nothing three times.

This is the cheapest specimen to detect and the easiest to miss in review, because
the body *looks* like work: setup, a pipeline, a railroad, a wrapper. Everything a
real test has except the part that decides anything.

**After:** post, read the entity back, and assert its values against the input that
produced them. See Specimen 8 for how to read it back without cheating.

**Detection:** a `[<Fact>]` whose body contains no `Assert.` and no typed `| Error`
match. Worth a grep before every hand-off.

## Specimen 8 — the tautological locator

**Before (caught in self-review, August 2026):**
```fsharp
// verifying that the posted JE carries the staged entry's fi and reference
let! posted = fetchByReference context (Some fi) (Some fiReference)
Assert.Equal(
    fiReference |> JournalExternalReferenceText.value,
    posted |> externalReferences |> List.head |> referenceText |> JournalExternalReferenceText.value)
```

**Why it's worthless:** the row was *selected* by the value the assertion then
checks. `fetchByReference` filters on exactly that field, so the assertion can only
fail if the fetch returns rows that do not match its own filter. It tests the
`WHERE` clause. The mapping it claims to verify — that posting copies the staged
reference onto the journal entry — is never exercised at all.

This is Specimen 6's disease moved from the expected value to the lookup. Both sides
of the comparison trace back to the same input.

**After:** locate by a field this test does not assert on, and assert on a field it
did not use to locate.
```fsharp
// this test owns the external reference, so it finds its entry by date and description
let! onThatDate = fetchByDateRange context entryDate entryDate
let posted = onThatDate |> List.find (fun je -> je |> header |> description = staged.description)
```
Where two requirements each own one of the two, they swap locators: the
header-mapping test finds by external reference, the external-reference test finds
by date and description. Neither may look itself up.

**The rule:** the locator and the assertion must not overlap. If the only way to
find the row is by the thing under test, the behavior needs a different layer or a
different anchor — not a shrug.

## Specimen 9 — observing from inside the mechanism under test

**Before (deleted August 2026):**
```fsharp
[<Fact>]
member _.``REQ-STG-8.1 REQ-STG-8.4 shadow post does not create journal entries or change staging statuses`` () =
    runCommandRouteAndAutoRollback IngestShadowPostStageEntries (fun context ->
        result {
            let! _ = StageTestData.runPipeline context
            do! StageEntryOrchestration.post contextForPost
            let! postablesAfterPost = fetchAllForPosting contextForPost
            Assert.Equal(0, postablesAfterPost |> List.length)
        })
    |> railroadWrapper
```

**Why it's worthless:** the feature under test *is* the rollback. Every
orchestrator test already runs inside a transaction that is rolled back whatever
happens, so a shadow post and a real post leave identical evidence from in there —
the test cannot tell them apart even in principle. Worse, the assertion states the
opposite of the requirement: 8.4 says staging is untouched, and this asserts that
nothing remains postable. It passed by reading its own uncommitted writes.

It had been copied from the batch-post happy path with the audit action swapped, so
it also tested the same thing twice.

**After:** observe from the layer where the mechanism has already resolved — for a
transaction, that is outside it, after the route returned and committed or discarded.
```fsharp
let! postResult = postThroughRoute true          // the route owns the transaction
let context = Context.create NoTransaction FetchOnly   // a fresh, separate read
let! posted = fetchByReference context (Some fi) (Some reference)
Assert.Empty(posted)                              // nothing survived the rollback
```

**The rule:** when the behavior is isolation, atomicity, rollback, or commit, no test
running inside that boundary can see it. Pick the layer at which the boundary has
already closed. If no such layer exists in your suite, that is the finding — say so
rather than writing a test that cannot fail.

---

## Specimen 10 — counting a truth table instead of reading it

**Before (purged in the 2026-08-19a audit remediation):**
```fsharp
[<Theory>]
[<InlineData("Ingested", 5)>]
[<InlineData("Classified", 4)>]
[<InlineData("NoMatch", 3)>]
// ... one row per status
let ``... validTransitions returns correct count for each status`` (statusStr: string, expectedCount: int) =
    let transitions = validTransitions (Some status)
    Assert.Equal(expectedCount, transitions |> List.length)
```

**Why it's worthless:** the function returns a finite list of DU cases and the test never
looks at one of them. Had `validTransitions Ingested` returned `[ Ingested; Ingested;
Ingested; Ingested; Ingested ]` the test would have passed — five of the wrong transition is
still five. The transition table decides which status changes the system permits, so a
member that is wrong while the cardinality is right is a silent gate: it either blocks a
legitimate workflow or admits an illegal one, and nothing goes red.

This is Specimen 3 in its most seductive form, because the counts *look* derived — they were
transcribed from the spec's own transition table, so the author felt they were checking
against the spec. They were checking the table's cardinality against itself.

**After:** enumerate every pair the enumeration can form and give each an explicit verdict.
Nine sources (eight statuses plus `None`) against eight targets is seventy-two rows: twenty
one permitted, fifty one denied.

```fsharp
[<Theory>]
[<InlineData("None", "Ingested", true)>]
[<InlineData("None", "Classified", false)>]
// ... seventy-two rows, transcribed from the spec's transition table
let ``... validTransitions permits exactly the pairs the spec's transition table lists``
    (fromStr: string, toStr: string, expectedPermitted: bool) =
    let permitted = validTransitions fromStatus |> List.contains toStatus
    Assert.Equal(expectedPermitted, permitted)
```

The denials are the half that carries the weight. A count test cannot express "and nothing
else"; a truth table is nothing but that. Note also that the assertion uses `List.contains`
because that is precisely how the production caller consults the list — the test asserts the
property the system actually depends on, not a property that merely correlates with it.

**When a truth table is the right shape:** the input space is a product of two finite
enumerations, and the function's whole job is to say yes or no to each cell. Do not reach for
one when the space is open-ended — you will end up enumerating the implementation.

## Hollow names

A name is the claim a test makes. Written badly it describes the *machinery* the test
touches; written well it states the *property* that must hold. The body can only be
as honest as the name it is trying to satisfy — a hollow name licenses a hollow body,
and reviewers approve it because there is nothing visibly wrong.

| Hollow | Real |
|---|---|
| `shadow post returns trial balance before and after` | `the difference between the two trial balances is the staged amount` |
| `batch post creates journal entries through domain model` | `a four-record group posts as a single journal entry carrying all four lines` |
| `fetchAllForPosting returns only Classified and Reviewed entries` | `fetchAllForPosting returns every Classified or Reviewed entry and nothing else` |
| `batch post fails when account code does not resolve` | `batch post fails loudly when a postable entry carries an uncoded line` |

The tells:

- **It names the call, not the outcome.** "returns a trial balance" is satisfied by
  any trial balance, including an unchanged one.
- **It says "correctly", "properly", "as expected", or "through the domain model".**
  These are placeholders for the property the author had not yet decided on.
- **It is satisfiable by a stub.** If a function returning well-shaped garbage would
  honour the name, the name has not asked for anything.
- **It states only one side of a filter.** "returns only X" forbids over-returning
  and permits returning nothing.
- **It describes a failure without naming the trigger.** "fails when the code does
  not resolve" — under what reachable circumstance? Ours turned out to be impossible;
  the reachable case was a *null* code, and the name had been hiding that for weeks.

Write the name so that a reader who never sees the body knows what would have to
break for the test to go red.

---

## The smell test

Before presenting any test, ask all three:

1. **"If the function under test returned garbage of the right shape, would this
   test fail?"** If no — count-only, inequality, `isError`, exit-code-only — the test
   is one of these specimens wearing a new name.
2. **"If I deleted the operation and asserted against the untouched state, would this
   test still pass?"** If yes, it is measuring nothing that the operation caused.
3. **"Did I find the row by the value I am about to assert?"** If yes, see Specimen 8.

None of the three is a substitute for actually watching the test fail. Perturb the
expected value, run it, read the failure, put it back. A test you have never seen
red is a claim you have never checked.
