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

## Not done

The name-quality check (loop step 6) is meant to run as an independent grader agent. This
session could not spawn one, so the nine draft names were graded against the rubric by their
own author — the exact failure mode that skill exists to prevent. Four names were revised as
a result; the grading still wants an independent pass.
