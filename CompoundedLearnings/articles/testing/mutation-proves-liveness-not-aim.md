# Mutation proves liveness, not aim

**Source:** BD, 2026-08-22 — audit finding BC-STG-1, discovered after the offending commit
(`428ea4d`) had already been pushed.

Perturbing an expected value proves the assertion runs and can fail. It says nothing about
whether the setup produces the scenario the test's name claims. A test aimed at the wrong
fixture data mutates red exactly like a correct one.

## What works

- After the mutation pass, ask a second question: **does the setup actually produce the
  situation the name describes?** Liveness and aim are separate properties and mutation only
  covers the first.
- When a name states a branch — *exactly one rule matched*, *the period was closed*, *the
  list was empty* — assert the branch itself, not only the values carried inside it. A `match`
  on the outcome DU case pins the scenario; an `Assert.Equal` on a field inside it does not.
- Grep the suite for other tests using the same fixture entity. A sibling asserting a
  *different* outcome for the same data is proof that one of the two names is lying.

## What doesn't

- Reporting "every assertion perturbed, run, failure read, reverted" as evidence a test
  verifies its requirement. It is evidence of half of that.
- Sharpening a vague name into a specific one without re-checking the fixture. A precise
  false claim is worse than a vague true one: it reads as verified, and the traceability grep
  counts it as covered.

## Example

`REQ-STG-5.4` says *"when exactly one rule matches and the line's account is null..."*. Its
test used grp-001's DoorDash debit line and was renamed to
``a line that arrives with no account takes the account of the single rule that matched it``.
Every assertion in it was mutated and seen red.

Two rules match that line. `Tests/Tests.Integrated/ModelOrchestrator/StageEntryClassification.fs`
proves it one screen below, where the `REQ-STG-5.5` test asserts `ManyMatchesClearWinner` on
the same line. The requirement was cited, counted as covered by the audit, and never
exercised — and the mutation evidence was real the whole time.
