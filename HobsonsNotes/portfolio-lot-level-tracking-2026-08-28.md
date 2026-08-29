# Portfolio — Lot-Level Tracking for Monte Carlo

**Written:** 2026-08-28. Conversation during CashFlow domain design.
Not actionable now — migration roadmap step 3 (portfolio moves into
SonOfLeo). Recording so the portfolio schema isn't designed around
aggregate positions when we get there.

## Decision

Track investment positions at the **lot level**, not the aggregate.
The Monte Carlo sim needs cost basis per lot to model tax on
liquidations — specifically, the long-term vs short-term capital
gains distinction (>1 year holding = 15% rate vs ordinary income).
An aggregate view loses acquisition dates, making it impossible to
determine which portion of a position qualifies for favorable rates.

Aggregate views are always derivable from lots. Lots are not
recoverable from the aggregate. Store the detail, derive the summary.

## What this means per account type

**Taxable brokerage (Fidelity, FNILX):**
- Track at lot level: acquisition date, shares, cost basis per lot
- Needed for: long-term vs short-term distinction, selling strategy
  modeling (FIFO, specific ID, highest-cost-first), tax-loss harvesting
- FNILX uses average cost method for mutual funds by default (IRS
  expectation unless specific ID is elected) — but the sim may want
  to model specific ID for optimization
- Fidelity has a "Cost Basis" / "Tax Lots" export per position (CSV).
  This is a different export from the existing `FidelityTransactionHistory`
  import — needs its own parser

**401(k) — T. Rowe Price:**
- Aggregate is sufficient. No lot tracking needed.
- Traditional: entire distribution taxed as ordinary income. Cost basis
  is effectively $0 (deducted on the way in). Sim models: withdraw $X,
  pay ordinary income tax on full amount.
- Roth: qualified distributions are tax-free. Cost basis irrelevant.
- One "position" per account type (Traditional/Roth) with total balance
  and a tax treatment flag is enough.

**IRAs (Fidelity — Roth, Traditional, Rollover):**
- Same logic as 401(k): tax treatment is per-account, not per-lot.
- Already excluded from the ledger per standing decision. Portfolio
  schema only.

## Schema implications

The portfolio schema needs (at minimum):
- A `lot` table: symbol, account, acquisition date, shares, cost basis
- Applicable to taxable accounts only
- Retirement accounts carry balance + tax treatment, no lots
- Account-level metadata: tax treatment (taxable / traditional-pretax /
  roth-posttax), cost basis method (average / specific-id / fifo)

## Parser implications

- **New parser needed:** Fidelity lot/cost-basis CSV export → lot records
- **Existing parsers unchanged:** `fidelity_history.py` (transaction
  history) and `portfolio-fidelity-csv.md` (aggregate positions) serve
  different purposes and continue to work as-is
- **T. Rowe:** existing `portfolio-trowe.md` captures aggregate positions,
  which is sufficient for retirement accounts

## Reference

- Monte Carlo constraints doc: `HobsonsNotes/montecarlo-constraints-from-personalfinance.md`
  - C7 flags this exact gap: "the simulator consumes investment positions
    and cost basis to compute capital gains"
- Migration roadmap: memory `project_sonofleo_roadmap.md` — step 3
  (migrate portfolio into SonOfLeo)
- IRA exclusion: memory `project_leobloom_ira_no_tracking.md`
