# CashFlow CLI Routes — Proposal for Saturday Automation

Written 2026-08-28. Design rationale for the CashFlow orchestration
routes. The behavioral spec in `Specs/Behavioral/CashFlow.md` is
canonical for requirements; this document preserves the *why* and the
division-of-labor reasoning.

## Design principle

The CLI is Hobson's control surface. Every [DET] phase is a single
command that does the work and reports what it did. Hobson's tokens go
to the [JUDGE] phases — matching staged entries to obligations,
classifying unknowns, and writing the summary. Everything else is
lever-pull-and-read-the-output.

Every mutating route returns the complete state of what it touched —
agreement name, amounts, lifecycle states — so Hobson never needs a
follow-up query.

## Tier 1 — Saturday blockers (can't cut over without these)

| Route | Tag | What it does | Replaces |
|---|---|---|---|
| `CashFlow ProjectionSweep` | [DET] | Takes a horizon (days). Walks every active agreement's cadence, creates missing Instances, creates Invoices for fixed-amount PAs. Returns what it created and what already existed. | Manual one-at-a-time instance spawning |
| `CashFlow Projection` | [DET] | Takes a horizon. Reads ledger balances + open invoices. Returns per-account `{ currentBalance, knownInflows, knownOutflows, projectedLow }` + `billsToChase` (instances with no invoice). | Hobson doing `bc` arithmetic across hand-queried balances |
| `CashFlow ObligationStatus` | [DET] | Optional agreement filter. Returns every active agreement with its current-period instance, invoices, payments, lifecycle states, unfulfilled amounts. The "brief me" query. | Multiple ad-hoc DB queries chained together |
| `CashFlow CreatePayment` | [DET] | Takes invoice ID + staged entry header ID + amount. Creates Payment, validates against invoice amount, returns payment + updated invoice state. | `ledger post` + manual obligation linking + state transition |
| `CashFlow CreateInvoice` | [DET] | Takes instance ID + PA ID + amount + dates + state. Diamond-relation validated. Returns the invoice. | Manual instance confirmation + amount setting |
| `CashFlow UpdateInvoice` | [DET] | FieldUpdate pattern. Transitions invoice/payment/posted states, blocker. Returns updated invoice. | Manual state transitions |
| `CashFlow TransitionPaymentsToPosted` | [DET] | No input. For every Payment pointing at a staged entry that now has a JE, transitions pointer to Posted + updates invoice posted state. Returns the list. | Phase 7 — querying, matching, updating each one |

## Tier 2 — Quality of life

| Route | Tag | What it does |
|---|---|---|
| `CashFlow StagedEntryMatchCandidates` | [DET] | Given an invoice or agreement, returns unlinked staged entries matching the PA's account pattern within the instance's date window. Candidates only — Hobson decides. |
| `CashFlow GenerateTenantInvoices` | [DET] | Takes month + utility amounts. Creates Invoices for each Income agreement using PA expected amounts (rent) + provided utility figures. |
| `CashFlow AgreementSummary` | [DET] | Full tree view: master → PAs → recent instances → invoices → payments. |

## What stays Hobson (irreducible [JUDGE] work)

1. **Matching staged entries to obligations** — "This $1,866.58 CMA
   draft on the 1st is the mortgage." The CLI surfaces candidates;
   Hobson makes the call.
2. **Classifying unknowns** — merchants the rules engine doesn't catch.
3. **Variable-amount invoice amounts** — reading the utility PDF,
   extracting the number.
4. **Blocker assignment** — why something is stuck.
5. **Summary synthesis** — assembling the narrative.
6. **Reclassification decisions** — the exception conversation.

## Saturday flow with these routes

```
Phase 0:  preflight.py                                    [DET] exists
Phase 1:  importers → Ingestion IngestRawFileToStage       [DET] exists
Phase 2:  dedup + classify (inside ingest)                 [DET] exists
Phase 3:  CashFlow ProjectionSweep                         [DET] NEW
          CashFlow ObligationStatus                        [DET] NEW → Hobson reads
Phase 4:  HOBSON THINKS                                    [JUDGE]
          → calls CreatePayment, CreateInvoice, UpdateInvoice
Phase 5:  HOBSON CLASSIFIES UNKNOWNS                       [JUDGE]
Phase 6:  Ingestion PostStageEntries                       [DET] exists
Phase 7:  CashFlow TransitionPaymentsToPosted              [DET] NEW
Phase 8:  reconcile scripts                                [DET] exists
Phase 9:  CashFlow Projection                              [DET] NEW
Phase 10: report renderers                                 [DET] exists
Phase 11: HOBSON WRITES SUMMARY                            [JUDGE]
```

## Output contract

Every mutating route returns the full state of what it touched so
Hobson can log it in the summary without re-querying:

- **ProjectionSweep** → list of (agreement name, instance date,
  invoices created with amounts) + already-existed counts. Feeds
  "Upcoming bills."
- **Projection** → per-account cash-coverage table + `billsToChase`
  with agreement names. Feeds "Money you need to move."
- **ObligationStatus** → per-agreement: name, direction, current
  instance date, invoice amounts, payment state, posted state,
  blockers. Hobson's briefing sheet before Phase 4.
- **CreatePayment / CreateInvoice / UpdateInvoice** → the full updated
  invoice (with all payments) so Hobson can confirm lifecycle states.
- **TransitionPaymentsToPosted** → count + list of (agreement name,
  invoice amount, JE ID). Feeds the review stack.
