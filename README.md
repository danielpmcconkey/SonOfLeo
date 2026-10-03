# SonOfLeo

A personal-finance double-entry ledger in F#. This file is the playbook: who does what, in
what order, and why the awkward parts are awkward on purpose.

It is a playbook, not a law codex. Players improvise. What follows is the shape the game
takes when nobody has a reason to deviate — and three constraints that are load-bearing,
called out below so that a future improvisation doesn't quietly remove one.

## Who does what

Dan is the human. He has the idea, creates the branch, rules on what agents and audits
raise, merges to `main`, and runs the audit. He no longer writes code here, and reviews it
only when there is a strong reason to.

Hobson is a Claude instance on Dan's desktop. He writes the specs and the plans agents work
from, makes product-level calls on agents' findings (Dan overrides), reviews code as needed,
and keeps the architecture model in step with the code.

Agents are Claude Code sessions, usually in the cloud, each on its own clone of the repo.
One implements Src from a plan; another writes tests from the spec. Several can work on one
branch at once, so each pulls with `--rebase` before every commit and keeps to the files its
brief names.

## The slice loop

A *slice* is one coherent piece of behavior: a spec section, the code that satisfies it, and
the tests that hold it to account.

| # | Step | Who |
|---|---|---|
| 0 | Create the branch | Dan |
| 1 | Write the spec, fleshing out Dan's idea | Hobson |
| 2 | Read enough of the spec to confirm its general shape | Dan |
| 3 | Write the plan: work items, each naming the requirements it satisfies | Hobson |
| 4 | Write the Src | Implementing agent |
| 5 | Brief the test agent with a **business** description of what changed and the shape of the spec — not the implementation mechanisms | Hobson |
| 6 | Read the spec only, not the new code, and draft the test names | Test agent |
| 7 | Run the name-quality check over the draft names | Test agent |
| 8 | Commit the test names as failing placeholders | Test agent |
| 9 | *Now* read the Src, and raise any concern that a committed test is aimed wrong; Hobson rules. Return to 7 or continue to 10 | Test agent + Hobson |
| 10 | Write the tests, seeing every assertion fail (constraint 3) | Test agent |
| 11 | A test fails: the test agent records whether the bug is in the Src or the spec. Hobson rules; the implementing agent fixes Src, Hobson fixes specs. Dan overrides | Agents + Hobson |
| 12 | All tests pass, `bash Checks/run-all.sh` passes, and the plan's final report is in → merge to `main` | Dan |
| 13 | Run the traceability script on `main` | Dan or Hobson |
| 14 | Run the audit process; it names the gaps we missed | Dan |

Step 13 is manual on purpose. `Checks/check-traceability.sh` exits 0 on any branch that is
not `main`, because the invariant it enforces — every active requirement tested or waived —
*cannot* hold mid-slice: the spec lands before the tests exist. Gating every commit on it
once produced a chicken-and-egg where nothing could be committed until dummy tests were
written first.

## The three load-bearing constraints

Everything else in the loop is convenience. These three are the reason it works, and each
one has already paid for itself.

**1. The test agent names tests from the spec alone (step 6), before seeing Src (step 9).**

The friction is the detector. When a spec is wrong, the symptom is that a test cannot be
written honestly against it — and that only surfaces if the person writing the test is
working from the spec rather than from the code. In one August 2026 slice, four requirements
turned out to be wrong (`REQ-STG-4.4`, `9.3`, `9.4`, and a thin `8.3`), and every one
surfaced this way. Had the tests been written from the implementation, all four would have
gone green and stayed wrong.

This is also why step 5 is a *business* description. A hand-off that explains how the code
works reintroduces exactly the bias the step is there to prevent.

**2. Committed names are a contract (step 8 precedes step 9).**

Because the claims are fixed before the test agent reads the implementation, Src knowledge can only
inform *how* a test reaches a behavior — never *what* it asserts. Step 9 can send a name back
to step 7 to be renegotiated out loud; it can never quietly soften one. This turns a rule
that used to depend on discipline into something the order of operations enforces. The commit
is the contract: Dan no longer approves names (retired 2026-09-27); the name-quality check
(step 7) and the rule that names are committed before Src is read carry it.

**3. No test is done until it has been seen to fail.**

For every new assertion: perturb the expected value, run it, read the failure, put it back.
Report the output. A suite can be entirely green and prove nothing — an August 2026 review
found three tests with no assertion at all, one asserting the opposite of its requirement,
and six concealing an unhandled error leak, all of them passing, all of them written by
someone who believed they were fine.

Steps 7 and 11 do not cover this. Step 7 checks names; step 11 fires when a test *fails*. A
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
