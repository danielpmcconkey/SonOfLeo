---
name: SonOfLeo:TestWriter
description: >
  This skill should be used when planning, creating, or maintaining xUnit tests for SonOfLeo.
  It covers REQ-traceability enforcement, test fixture design, and the two-phase
  stub-then-implement workflow. Triggers on "write tests", "test coverage", "stub tests",
  "add tests for", or any work involving SonOfLeo's Tests.Isolated or Tests.Integrated projects.
---

# SonOfLeo TestWriter

Plan, create, and maintain xUnit tests for SonOfLeo with full REQ traceability against the
behavioral specs.

## Core contract

Every active REQ in `Specs/Behavioral/*.md` must be in exactly one of two states: **tested**
or **waived**. There is no third state. The TestWriter's primary job is closing gaps between
specs and tests.

## Two-phase workflow

All test creation follows a two-phase process. Never skip phase 1.

### Phase 1 — Stub

1. Read the relevant spec file(s) in `Specs/Behavioral/`.
2. Inventory all `REQ-` IDs in the spec (including sub-dot IDs like `REQ-AC-1.48.1`).
3. Cross-reference against existing tests (grep for the REQ ID in test files).
4. Cross-reference against the spec's **Waived from testing** table.
5. For each untested, non-waived REQ, determine how many test cases it needs. One REQ may
   require multiple tests (happy path, boundary, error cases).
6. Produce stub test functions: `[<Fact>]` attribute, backtick name with REQ tag, body of
   `Assert.Fail "not implemented"`.
7. For any REQ that appears untestable, propose a waiver with rationale. Flag it for Dan's
   approval — never add to the waived table without explicit sign-off.
8. Present the stubs to Dan for review before proceeding.

Stub naming format:
```fsharp
[<Fact>]
let ``REQ-XX-N.N short description of what is being verified`` () =
    Assert.Fail "not implemented"
```

When a single REQ needs multiple tests, use consistent naming:
```fsharp
let ``REQ-XX-N.N description — happy path`` () =
let ``REQ-XX-N.N description — empty input`` () =
let ``REQ-XX-N.N description — exceeds max length`` () =
```

### Phase 2 — Implement

After Dan approves the stubs, implement them. Follow the patterns in
`references/test-patterns.md`. Match the code quality and style of the production codebase —
read production code in the relevant module before writing tests to absorb naming, pipeline
style, and structure.

## Deciding isolated vs integrated

- **Isolated** (`Tests.Isolated`): Pure functions with no database interaction. Validation,
  parsing, construction, arithmetic. These run in parallel freely.
- **Integrated** (`Tests.Integrated`): Anything that touches the database or invokes the CLI
  subprocess. These run serially (`parallelizeTestCollections: false`).

The dividing line is database access, not complexity.

## Waiver criteria

Suggest a waiver only when one of these applies:

- The F# type system makes the invalid state unrepresentable (e.g., non-nullable types
  prevent null, DU cases prevent invalid enum values).
- The requirement is a negative existence claim over the API surface ("no function exposes
  a delete") that cannot be proven by a unit test.
- The requirement describes behavior enforced entirely by the caller, not by the module
  under test.

Always include rationale. Never add to the waived table without Dan's explicit approval.

## Test fixture — shared reference data

Read `references/test-fixture-design.md` for the fixture architecture.

Key rules:
- The fixture provides read-only reference data (accounts, fiscal periods, journal entries).
- Tests may read fixture data and may operate on it within a transaction that rolls back.
- Tests must never commit mutations to fixture data.
- Tests that need entities beyond what the fixture provides create them inside their own
  transaction.

## Timing and AuditEnvelope

`AuditEnvelope.create` captures `Clock.now()` which uses real `DateTimeOffset.UtcNow`,
truncated to microsecond precision. When a test needs two distinct timestamps, insert
`System.Threading.Thread.Sleep(10)` between envelope creations. Ten milliseconds is
three orders of magnitude above the microsecond truncation boundary.

Do not use `Thread.Sleep(1000)`. One second is wasteful overkill.

## Code quality

Write tests to the same standard as production code. Before writing tests for a module,
read the production code for that module and match Dan's style — not generic F# convention.

Specific expectations:
- Pipeline style (`value |> function`) over nested calls where Dan uses it
- Respect Dan's naming and legibility preferences even when they diverge from F# norms
- No unnecessary mutability — use `let` bindings threaded through `result { }` blocks
- Descriptive error messages in `Result.mapError` — never rely on generic `failwith`
- `defaultWith failwith` is acceptable only in test setup where failure means the test
  infrastructure is broken, not for asserting user-facing behavior

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

This skill is a living document. After each test-writing session, evaluate what worked and
what didn't. If the workflow, patterns, or fixture design need refinement, update this skill
and its references. Dan expects this process to improve over time.
