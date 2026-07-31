---
name: SonOfLeo:TestWriter
description: >
  This skill should be used when planning, creating, or maintaining xUnit tests for SonOfLeo.
  It covers REQ-traceability enforcement, the waiver process, and the two-phase
  stub-then-implement workflow. Triggers on "write tests", "test coverage", "stub tests",
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

## Two-phase workflow

Never skip phase 1.

### Phase 1 — Stub

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
10. Present the stubs to Dan for review before proceeding — **and with them, any fixture
    archetype step 7 identified as missing.** A fixture addition is a change to shared
    state every other test reads; it gets reviewed before it gets written, not after.

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

### Phase 2 — Implement

After Dan approves the stubs, implement them. Read the production code for the module under
test first and match its style — Dan's naming and pipeline idiom, not generic F# convention.

## Choosing the layer

During phase 1, read the actual function signatures to decide where each test belongs. Three
rules govern it:

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
