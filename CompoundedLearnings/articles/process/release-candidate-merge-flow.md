# Release-Candidate Merge Flow

**Source:** Dan's directive, session 2026-07-31, after BD verified three task branches
individually and then noticed their combination had never been built.

One task, one branch. At the end of a session BD assembles the task branches into a
release-candidate branch, verifies *that*, and pushes it. Dan verifies the RC and merges it
to `main`. BD never touches `main`.

## What works

- **One task, one branch, one hand-off.** BD pushes and stops; Dan reviews the narrow diff
  in Rider against `main`. A second worthwhile finding becomes its own branch, never a
  second commit on this one.
- **BD verifies every branch before handing it over** — `Checks/run-all.sh`, build, test.
  That is BD's "never present red" obligation, not a substitute for Dan's final gate.
- **Assemble the RC from `main`, then re-run everything on it.** Name it `rc-<date>`. The
  task branches stay pushed, so Dan can still read each one on its own.
- **Announce before the final verification run.** BD's container and Dan's host both point
  at `Host=172.18.0.1 / sonofleo_test`, and `TestDataFixture` opens with
  `TRUNCATE ... CASCADE`. Two simultaneous integrated runs corrupt each other; whoever is
  not running, stops.

## What doesn't

- **Treating per-branch green as combination green.** Branches verified independently have
  never been built together. A clean `git merge` proves only that no two hunks touched the
  same lines — it says nothing about behavior.
- **Verifying a scratch merge and then deleting it.** The commit graph Dan merges must be
  the one the suite actually ran against. A throwaway merge verifies an artifact that no
  longer exists.
- **Merging task branches into `main` one at a time.** That produces a graph nobody
  verified, and merging is Dan's regardless.

## Example

2026-07-31, three branches off `main@1958b55`: `je-1.56-tests` (REQ-JE-1.56 coverage),
`fix-rollback-on-test-exception` (a transaction leaked when a test raised instead of
returning `Error`), and `testwriter-fixture-step` (a skill change). Each was green alone.
The rollback fix changed the exact helper the new tests run through, so the interaction was
real and unverified until all three were merged into `rc-2026-07-31` and the suite was run
once more — 8 checks, clean build, 341 tests.
