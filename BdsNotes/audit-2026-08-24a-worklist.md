# Audit 2026-08-24a — BD worklist

Branch: `audit-2026-08-24a-tests` off `main` @ 905bbbf

## Efficacy fixes (existing tests)
- [x] 004 SPEC8-AC-3.3 — Tests.Integrated/Model/Ledger/Account.fs:314 — assert a non-locator property
- [x] 005 AQ-AC-4.1 — Tests.Integrated/ModelOrchestrator/AccountDeactivation.fs:20 — assert activeEnd == provided date
- [x] 006 STG-AQ-1 — Tests.Integrated/ModelOrchestrator/StageEntryIngestion.fs:146 — `>= 2` becomes `Equal(2, …)`
- [x] 008 FP-LEAP-1 — Tests.Integrated/ModelOrchestrator/FiscalPeriodCreation.fs — Theory over 31/30/Feb-28/Feb-29
- [x] 009 IDIOM-JE-2 — Tests.Isolated/Model/Ledger/JournalEntryComponent.fs:121-146 — assert the Credit DU case
- [x] 010 COVER-JE-1 — Tests.Integrated/ModelOrchestrator/AccountActivity.fs — assert source enrichment
- [x] 011 RPT-EFF-1 — Tests.Integrated/InterfaceBridge/ReportRoutes.fs:108 — full expected path, Equal not Contains
- [x] 012 STALE-SYS-1 — Tests.Isolated/Model/Ledger/JournalEntryComponent.fs:153,158 — cite 1.3 not 1.2
- [x] 015 TRACE-1 — Tests.Integrated/ModelOrchestrator/StageEntryIngestion.fs:135 — assert the derived status

## New tests
- [x] 002 TG-ROUTE-CR-1 — five classification rule routes, no route-level coverage
- [x] 019 CUST-STG-DESC-1 — description filter is now case-sensitive partial match (REQ-STG-10.2)
- [x] 031 DEDUP-1 — Reviewed entries excluded from dedup (REQ-STG-7.2)

## Rules in force
- Mutation-prove every changed or added assertion; record the output.
- Names first, from the spec, for the three new areas.
- Build with `--artifacts-path /tmp/sonofleo-build`.

## Closed out 2026-08-25

All twelve items done. Suite 324 isolated / 456 integrated, green.
`Checks/run-all.sh` 9 passed 0 failed. `traceability-audit.sh .` invariants 1 and 2 clean.

Nineteen mutations run, each reddening exactly one test. Two mutations were bad and were
redone rather than believed:

- Swapping StatusAsc/StatusDesc in the sort test is symmetric — that test asserts the two
  directions are exact reverses, so relabelling them cannot break it. Re-run by pointing the
  projection at the wrong quantity.
- Changing which fixture rule the fetch-by-name test targets changes the request and the
  expectation together. Re-run by asserting the sibling rule's id with the request left alone.

## Picked up along the way, unassigned

REQ-CR-1.22 (rule name uniqueness) landed in this audit cycle from item #026 — Dan added the
constraint, Hobson added the requirement, nobody was assigned the test. It was the only
invariant-2 violation on this branch. Covered at the orchestrator layer.

## Name grading — ran after all

Dan authorised the grader agent, so step 6 got its independent pass after the names were
already written. Read the transcript, not the notification: it returned an idle notification
with no report, `stop_reason: end_turn`, and the full report on disk at
`~/.claude/projects/-workspace/<session>/subagents/agent-*.jsonl`. Second time this has
happened; the harness note on it is right.

Acted on three findings:

- **REQ-CR-5.1 scored 72.** Its name never denied returning the wrong rule. The test always
  did, but the name licensed a weaker body and read as deliberately asymmetric beside
  REQ-CR-5.2, which closes exactly that hole. Renamed.
- **REQ-CR-4.1 scored 82** — enumerated three of the five fields the create route stores
  while the test asserted all five. Renamed.
- **REQ-STG-10.2 case name scored 80** — "when lower-cased" is satisfied vacuously by a
  fragment with no upper case. Renamed.

And one real gap: **REQ-CR-1.22 covered only create.** Uniqueness is "across all
classification rules" and REQ-CR-6.1 makes the name settable, so rename-onto-a-taken-name was
untested. Added.

Most of the grader's "Uncovered" section is answered by tests it could not see — it grades the
batch, not the suite. REQ-CR-5.4, REQ-CR-6.2, REQ-CR-6.1's independence clause, all five
REQ-CR-5.3 criteria and REQ-STG-10.3 all have dedicated orchestrator tests. Check before
acting on that section next time; its parallelism note is the part worth keeping.

## Uniqueness tests, second pass

The grader wanted "and stores no second rule" on REQ-CR-1.22. Not assertable where those
tests lived — a failed statement aborts the open transaction, so nothing can be read back
after the refusal (`current transaction is aborted, commands ignored until end of transaction
block`; proven in psql, not assumed). Near-vacuous anyway: these are single-row inserts and
cannot half-land.

The claim with teeth is that the row already holding the key is untouched. An implementation
that errored *and* clobbered the incumbent passed every earlier version of these tests.
REQ-CR-1.22 create, REQ-CR-1.22 update, and REQ-AC-1.4 now run on a NoTransaction context so
the post-refusal state is readable, and each asserts exactly one row holds the key, that it is
the fixture row, and that its other fields survived.

**REQ-AC-1.4 changed form 3 to form 4 as a result.** It is the only account test that writes
outside a transaction. Its safety is the `finally`; if that ever fails to run it leaves a
duplicate `F-1250` and the account suite stays red until someone deletes it.

## Open for Dan — flagged, not ruled on

- **REQ-STG-7.2 has three gaps beyond the one assigned.** Nothing covers the `Duplicate` or
  `Posted` exclusions, and nothing covers the AND: sharing only source id, or only fi
  reference, must not flag. Item 031 was scoped to `Reviewed`, so BD stopped there. Three
  cheap tests when he wants them.
- **`accountNameAtMatch` has no requirement behind it.** The create and fetch classification
  rule routes return the resolved account *name* beside the code; REQ-CR-4.1 does not mention
  it and the new route test asserts it. Hobson's call: missing REQ, or a return field nobody
  meant to promise.

## State at hand-off

Branch `audit-2026-08-24a-tests`, four commits, pushed. 324 isolated / 457 integrated green.
`Checks/run-all.sh` 9 passed 0 failed. `traceability-audit.sh .` invariants 1 and 2 clean.
Database checked after a full run: no duplicate rule names, no duplicate account codes, no
rows left behind. Not merged — that is Dan's.
