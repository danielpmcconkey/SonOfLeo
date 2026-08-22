# CashFlow Domain Brief — 2026-08-22

Forward-looking note from Dan's direction. No spec work yet — this
captures intent so the next session has it cold.

## What Dan said

The next major domain after the data-ingestion audit closes is
**CashFlow** — a unified domain covering:

1. **Obligations** (the existing LeoBloom concept, both directions:
   payables and receivables)
2. **Budgeting** — not gamified, not an iPhone app. The system's job is
   to produce a defensible answer to "can we afford this?" backed by
   data, not vibes.

Dan's framing: *"It's me having data to tell my wife and kids why I
can't buy them the thing they want."*

## Sequencing

Obligations come first regardless — they're the Saturday cutover
blocker. Budgeting layers on top once obligations are migrated.

## Data advantage

LeoBloom has nearly a year of actuals by now. Budgets don't need to
start as aspirational guesses — they can start as "this is what we
actually spent." The spending report and category crosswalk already
produce the raw material.

## Open questions (for a future design session, P7-style)

- **Reporting surface:** Does budgeting live inside the Saturday summary
  ("Money you need to move" already exists) or become its own report?
- **Granularity:** Budget by account? By category grouping? By
  obligation? By some new concept?
- **Variance tracking:** Period-over-period? Budget-vs-actual? Both?
- **Relationship to obligations:** An obligation is a committed outflow.
  A budget target is a planned ceiling. How do committed outflows count
  against the budget — automatically, or explicitly?
- **Receivables in the budget model:** Tenant income is predictable and
  known. Does it appear as a budget inflow, or is that handled by
  obligations alone?

None of these need answers now. Obligations first, then a design session.

## Dependencies

- Data-ingestion audit remediation (in progress, `audit-2026-0821-remediation`)
- Obligation migration into SonOfLeo (not started)
- Saturday cutover test (see memory: `project_sonofleo_roadmap.md`)

---

*This is a note to self, not a spec. No work should start from this
document without a design session with Dan.*
