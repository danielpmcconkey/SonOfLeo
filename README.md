# SonOfLeo

A personal-finance double-entry ledger in F#. This file is the playbook: who does what, in
what order, and why the awkward parts are awkward on purpose.

It is a playbook, not a law codex. Players improvise. What follows is the shape the game
takes when nobody has a reason to deviate — and three constraints that are load-bearing,
called out below so that a future improvisation doesn't quietly remove one.

## Who does what

Three of us work on this repo. Dan is the human; Hobson and BD are Claude instances, Hobson
on the host and BD in a container. Hobson and Dan share a working tree. BD does not — BD's
clone is separate, and everything reaches it through `origin`.

| Area | Owner | Notes |
|---|---|---|
| `Specs/Behavioral/` | Dan | Hobson drafts from Dan's input; Dan rules on content |
| `Src/` | Dan | `fsharp-guard` refuses BD's edits to `.fs` under `Src/`; BD proposes diffs instead |
| `Tests/` | BD | BD is the **only** reviewer of test bodies |
| `Checks/`, `DbMigrations/` | Dan | |
| `Audit/` | Dan | owns the audit process end to end |
| `Skills/`, `CompoundedLearnings/` | shared | whoever learns the lesson writes it down |
| `BdsNotes/`, `HobsonsNotes/` | respective author | session hand-offs |

Branch creation and merging to `main` are Dan's. `git-guard` refuses BD the working-tree
destroying verbs — the restore family, the branch-switching family, `clean`, `stash`, hard
reset.

## The slice loop

A *slice* is one coherent piece of behavior: a spec section, the code that satisfies it, and
the tests that hold it to account.

| # | Step | Who |
|---|---|---|
| 0 | Create the branch | Dan |
| 1 | Write the spec, fleshing out Dan's idea | Hobson |
| 2 | Read enough of the spec to confirm its general shape and pick up specifics that Src must match | Dan |
| 3 | Write the Src, checking edge cases and copy/paste slips with Hobson along the way | Dan |
| 4 | Hand BD a **business** description of what changed and the shape of the spec — not the implementation mechanisms | Hobson |
| 5 | Read the spec only, not the new code, and draft the test names | BD |
| 6 | Run the name-quality check over the draft names | BD |
| 7 | Review and approve the test names | Dan |
| 8 | *Now* read the Src, and raise any concern that an approved test is aimed wrong. Discuss; return to 6 or continue to 9 | BD + Dan |
| 9 | Write the tests | BD |
| 10 | A test fails: work out together whether it's a bug in the Src or a spec that steered the test wrong. Hobson joins when useful. This is usually the first careful read of the spec. Dan dispositions | Dan + BD |
| 11 | All tests pass and all three agree the slice is complete → merge to `main` | Dan |
| 12 | Run the traceability script | Dan or Hobson |
| 13 | Run the audit process; it names the gaps we missed | Dan |

Step 12 is manual on purpose. `Checks/check-traceability.sh` exits 0 on any branch that is
not `main`, because the invariant it enforces — every active requirement tested or waived —
*cannot* hold mid-slice: the spec lands before the tests exist. Gating every commit on it
once produced a chicken-and-egg where nothing could be committed until dummy tests were
written first.

## The three load-bearing constraints

Everything else in the loop is convenience. These three are the reason it works, and each
one has already paid for itself.

**1. BD names tests from the spec alone (step 5), before seeing Src (step 8).**

The friction is the detector. When a spec is wrong, the symptom is that a test cannot be
written honestly against it — and that only surfaces if the person writing the test is
working from the spec rather than from the code. In one August 2026 slice, four requirements
turned out to be wrong (`REQ-STG-4.4`, `9.3`, `9.4`, and a thin `8.3`), and every one
surfaced this way. Had the tests been written from the implementation, all four would have
gone green and stayed wrong.

This is also why step 4 is a *business* description. A hand-off that explains how the code
works reintroduces exactly the bias the step is there to prevent.

**2. Approved names are a contract (step 7 precedes step 8).**

Because the claims are fixed before BD reads the implementation, Src knowledge can only
inform *how* a test reaches a behavior — never *what* it asserts. Step 8 can send a name back
to step 6 to be renegotiated out loud; it can never quietly soften one. This turns a rule
that used to depend on discipline into something the order of operations enforces.

Step 7 is also Dan's one unskimmable step. It is the only place in the loop where his intent
is checked against the claims being made, and roughly thirty names is a five-minute read.

**3. No test is done until it has been seen to fail.**

For every new assertion: perturb the expected value, run it, read the failure, put it back.
Report the output. A suite can be entirely green and prove nothing — an August 2026 review
found three tests with no assertion at all, one asserting the opposite of its requirement,
and six concealing an unhandled error leak, all of them passing, all of them written by
someone who believed they were fine.

Steps 6 and 10 do not cover this. Step 6 checks names; step 10 fires when a test *fails*. A
hollow body under a good name passes both.

## Where the rules live

| For | Read |
|---|---|
| What the system must do | `Specs/Behavioral/`, `Specs/README.md`, `Specs/Definitions.md` |
| What infrastructure already exists | `Src/README.md` |
| The standard tests are written to | `Tests/README.md`, plus the README in the test directory you're in |
| The procedure for writing them | `Skills/TestWriter/` |
| Tests that pass while proving nothing | `Skills/TestWriter/references/bullshit-test-specimens.md` |
| Settled judgment calls, and why | `CompoundedLearnings/` |
| Mechanical rules with teeth | `Checks/` |
