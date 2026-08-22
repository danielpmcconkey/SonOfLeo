---
name: SonOfLeo:TestWriter
description: >
  This skill should be used when planning, creating, or maintaining xUnit tests for SonOfLeo.
  It covers REQ-traceability enforcement, the waiver process, and the three-phase
  name-then-read-then-implement workflow, including the obligation to prove each new
  assertion can fail. Triggers on "write tests", "test coverage", "stub tests",
  "add tests for", or any work involving SonOfLeo's Tests.Isolated or Tests.Integrated projects.
---

# SonOfLeo TestWriter

The procedure for writing tests. The **standard** they are written to lives in
`Tests/README.md` — the five test forms, the silent-pass hazard, assertion shape, fixture
rules, and what a worthless test looks like. Read it before you write anything; this skill
does not restate it.

Also read the README in the test directory you're working in. Each layer states what
belongs in it and what does not.

## Core contract

Every active REQ in `Specs/Behavioral/*.md` is in exactly one of two states: **tested** or
**waived**. There is no third state. Closing the gap between specs and tests is this
skill's whole job.

## Three-phase workflow

The phases exist to keep one thing true: **the spec decides what you assert; the Src decides
only how you reach it.** Reading the implementation before you have committed to a claim is
how a suite ends up describing what the code does instead of what it owes. Once names are
approved they are a contract — phase 2 may send you back to renegotiate one out loud, never
to soften one quietly.

Never skip phase 1.

### Phase 1 — Name, from the spec alone

**Do not read implementation during this phase.** The hand-off you receive describes the
change in business terms for exactly this reason. If you cannot tell what a requirement
demands without reading the code, that is a finding about the spec — raise it, do not
resolve it by peeking.

The boundary is behavior, not files. Signatures and module layout are fair game, because
choosing a layer (below) needs to know which functions exist and where they live — knowing
that `postStageEntry` takes a source argument is structural. Function *bodies* are not:
knowing what value `post` passes into that argument is behavior, and it is precisely the
knowledge that would have let a wrong spec pass unnoticed.

1. Read the relevant spec file(s) in `Specs/Behavioral/`.
2. Inventory every `REQ-` ID in the spec, including sub-dot IDs like `REQ-AC-1.48.1`.
3. Cross-reference against existing tests (grep for the REQ ID across both test projects).
4. Cross-reference against the spec's **Waived from testing** table.
5. For each untested, non-waived REQ, decide how many test cases it needs. One REQ may need
   several — happy path, boundary, error cases.
6. For each case, decide its **form** (`Tests/README.md`) before writing a line. The form
   determines the project, the class-vs-module shape, and the cleanup obligation.
7. **Decide what state each test needs to already exist.** If a test needs an entity in a
   particular state before it can exercise the behavior under test, that entity belongs in
   the fixture — not in the test. The only entity a test creates is the one whose creation
   *is* the behavior being tested. Ask what *archetype* is missing (a closed account, a
   comment already carrying a secondary link), not what row this one test wants.
8. Produce stubs: `[<Fact>]`, backtick name carrying the REQ tag, body of
   `Assert.Fail "not implemented"`.
9. For any REQ that appears untestable, propose a waiver with rationale. Flag it for Dan —
   never add to the waived table without explicit sign-off.
10. Check every name against the hollow-names table in
    `references/bullshit-test-specimens.md` before showing it to anyone. A name that
    describes the call rather than the outcome licenses a body that asserts nothing, and
    it will be approved because nothing looks wrong with it.
11. Present the stubs to Dan for approval before proceeding — **and with them, any fixture
    archetype step 7 identified as missing.** A fixture addition is a change to shared
    state every other test reads; it gets reviewed before it gets written, not after.
12. If a requirement cannot be named honestly — it describes an unreachable failure, or a
    behavior the system has no way to exhibit — stop and say so. Four of those surfaced in
    one August 2026 slice, and every one was a wrong spec rather than a hard test. This is
    the single most valuable thing this phase produces.

Integrated stubs are class members:
```fsharp
[<Fact>]
member _.``REQ-XX-N.N short description of what is being verified`` () =
    Assert.Fail "not implemented"
```

Isolated stubs are module-level:
```fsharp
[<Fact>]
let ``REQ-XX-N.N short description of what is being verified`` () =
    Assert.Fail "not implemented"
```

When one REQ needs several tests, keep the naming parallel:
```fsharp
member _.``REQ-XX-N.N description — happy path`` () =
member _.``REQ-XX-N.N description — empty input`` () =
member _.``REQ-XX-N.N description — exceeds max length`` () =
```

### Phase 2 — Read the Src

Only after the names are approved. Read the production code for the module under test and
ask one question: *does anything here suggest one of my approved tests is aimed wrong?*

Style is the other half of it — match Dan's naming and pipeline idiom, not generic F#
convention.

What you may **not** do is adjust an assertion to match what the code happens to do. If the
code and the spec disagree, that is the disagreement worth having, and it goes to Dan. A
test quietly rewritten to agree with the implementation is a spec bug made permanent.

### Phase 3 — Implement, then prove it can fail

Write the bodies. Then, for every new assertion, **watch it go red for the right reason.**
Perturb the expected value — double the amount, append to the string, drop an element,
remove the setup step that creates the condition — run the test, read the failure, put it
back. Record the actual output.

This is not optional and it is not a formality. A test you have never seen fail is a claim
you have never checked, and the assertion-shaped code around it proves nothing on its own.
An August 2026 review of a green suite found three tests with no assertion at all, one that
asserted the opposite of its requirement, and six hiding an unhandled error leak — all of
them written to this skill, all of them passing, none of them ever mutated.

Then ask the second question mutation cannot answer: **does the setup actually produce the
scenario the name claims?** A test aimed at the wrong fixture entity mutates red exactly like
a correct one. When the name states a branch — "exactly one rule matched", "the period was
already closed" — assert the branch itself, and grep for sibling tests using the same fixture
data that claim a different outcome for it. See
`CompoundedLearnings/articles/testing/mutation-proves-liveness-not-aim.md`.

Report the mutation and its output when you hand off. `Expected: 1600.00 / Actual: 800.00`
is evidence. "The test is correct" is not.

## Choosing the layer

During phase 1, read the function signatures — not their bodies — to decide where each test
belongs. Three rules govern it:

1. **Don't test what the type system enforces.** A `Guid` cannot be null; a DU cannot hold a
   case that doesn't exist; a validated wrapper cannot hold an invalid value. Propose a
   waiver instead of a test.
2. **Test happy paths at every layer.** Each layer gets its own happy-path test proving it
   works end to end at that layer. They compose upward, but the lower test still earns its
   place by proving the component in isolation.
3. **Test unhappy paths once, at the lowest layer where the failure can occur.** If
   `Description.create` rejects whitespace, that is an isolated component test and is not
   re-tested at orchestration. Orchestration-level sad paths cover only failures that
   *emerge* at orchestration — line count, debit/credit balance, fiscal period state,
   cross-entity checks.
4. **Count vectors as user interactions, not failure modes.** "The constructor rejects a bad
   value" and "the caller gets back an error naming the field they got wrong" are two
   vectors, not one — so rule 3 places them at two different lowest layers, isolated and
   InterfaceBridge. Read
   `CompoundedLearnings/articles/testing/failure-vector-is-a-user-interaction.md` before
   deleting or waiving a route-level validation case as redundant.

A REQ about optionality may turn out to be enforced at the boundary, or by the type system,
and need no test at all.

## Waiver criteria

Suggest a waiver only when one of these holds:

- The F# type system makes the invalid state unrepresentable.
- The requirement is a negative existence claim over the API surface ("no function exposes a
  delete") that no unit test can prove.
- The requirement describes behavior enforced entirely by the caller, not by the module
  under test.

Always give the rationale. Never add to the waived table without Dan's explicit approval.

## Timing and AuditEnvelope

`AuditEnvelope` carries one instant per user action, taken from `Clock.now()`
(`SystemClock.Instance.GetCurrentInstant()`, truncated to DB-storable precision). When a test
needs two genuinely distinct timestamps, put `System.Threading.Thread.Sleep(10)` between the
envelope creations — three orders of magnitude above the truncation boundary. Not
`Sleep(1000)`; one second is wasteful overkill.

## Code quality

Write tests to the same standard as production code:

- Pipeline style (`value |> function`) over nested calls, where Dan uses it
- No unnecessary mutability — `let` bindings threaded through `result { }`. The one
  sanctioned exception is the `let mutable idToCleanUp` of forms 4 and 5.
- Descriptive errors in `Result.mapError`; never a bare `failwith` to assert behavior
- `defaultWith failwith` is acceptable in test setup — a broken fixture means every test is
  broken — never for asserting user-facing behavior
- When matching a DU case inside a result CE, use `return! match` — not `Result.map`
  after the CE:

```fsharp
// correct — errors stay in the railway
result {
    let! returned = someOperation
    return!
        match returned with
        | TrialBalanceReturn.DataOnly rows ->
            Assert.Equal(expected, rows |> List.length)
            Ok ()
        | TrialBalanceReturn.Report _ ->
            Error (TestingError "Expected DataOnly but got Report")
}
|> railroadWrapper

// wrong — Assert.Fail throws outside the Result type
result { ... return returned }
|> Result.map(fun returned ->
    match returned with
    | TrialBalanceReturn.DataOnly rows -> Assert.Equal(expected, rows |> List.length)
    | TrialBalanceReturn.Report _ -> Assert.Fail "Expected DataOnly but got Report")
|> railroadWrapper
```

## Build and run

```bash
# Build
dotnet build --artifacts-path /tmp/sonofleo-build

# Run all tests
dotnet test --artifacts-path /tmp/sonofleo-build

# Run isolated only
dotnet test Tests/Tests.Isolated/Tests.Isolated.fsproj --artifacts-path /tmp/sonofleo-build

# Run integrated only
dotnet test Tests/Tests.Integrated/Tests.Integrated.fsproj --artifacts-path /tmp/sonofleo-build
```

## Iterating on this skill

This skill is the procedure; `Tests/README.md` is the standard. When a session shows the
*workflow* needs refinement, update this file. When it shows a *rule* is wrong or missing,
update `Tests/README.md` — do not restate it here.
