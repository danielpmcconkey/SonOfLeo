# Saturday Routine — SonOfLeo Future State (Draft)

Working draft of what the Saturday routine looks like once SonOfLeo's
CashFlow domain is operational. Not a spec — a planning document for
the cutover. Refine as the domain builds out.

Replaces the current LeoBloom Saturday routine
(`~/.claude/skills/leobloom:saturday/SKILL.md`) once the cutover test
passes: can Hobson run the full Saturday — pre-flight through summary —
without touching LeoBloom's CLI for any ledger or obligation operation?

## The seam: [DET] vs [JUDGE]

Carried forward from the current routine. The line between deterministic
and judgment work is how the mechanical phases eventually get offloaded
(to cron, to a dumber model) without touching the judgment ones.

- **[DET]** — deterministic: pre-flight, parse, ingest, dedup, classify
  (rules), sweep, project, reconcile, render. No taste required.
- **[JUDGE]** — judgment: obligation matching, manual classification,
  invoice creation for variable amounts, summary synthesis.

## Classification authority hierarchy

From most authoritative to least:

0. **Dan** — override authority, rarely exercised
1. **Hobson** — judgment authority when operating as comptroller
   (obligation-informed or not). Tier 1 for everything below.
2. **Parser** — hard-wired account assignments in import scripts
3. **Classification rule** — pattern matching

Hobson outranks the parser. If the obligation tells Hobson a different
account than what the parser hard-wired, Hobson changes it.

## Phase 0 — Pre-flight  [DET]

Same as current. Dan downloads FI exports to the import data folder.
Hobson runs the pre-flight script to validate dates and content.

Verdicts: PASS, EMPTY (assume no activity), UNVERIFIED (snapshot),
INFO (statement/recon target), HARD STOP (content contradicts filename).

## Phase 1 — Transform + Ingest  [DET]

Hobson runs import scripts that transform FI exports into the base
staging JSONL format (bespoke parsers, outside SonOfLeo). Then runs
the SonOfLeo ingest route to load staged entries.

Data enters `ingestion.staged_entry` / `ingestion.staged_entry_line`.

## Phase 2 — Dedup + Classify (rules)  [DET]

Run the dedup and classification rule routes. Staged entries get
classified by the rules engine where pattern matches exist. Data
**stays in stage** — nothing is posted yet.

## Phase 3 — Projection sweep  [DET]

Run the projection sweep with a horizon of N days (30–60 TBD).

For every active Master Agreement:
- Walk the cadence forward through the horizon window
- Create missing Instances
- For fixed-amount Payment Agreements, create Invoices with the
  expected amount
- Variable-amount Payment Agreements get Instances only — Invoices
  wait for bills

After this step, the system has a complete picture of every obligation
due in the next N days.

## Phase 4 — Obligation routine  [JUDGE]

This is where Hobson's judgment lives. For each agreement:

### 4.a — Match staged entries to obligations

Review staged entries against open Invoices and Instances. Identify
which staged entries correspond to which obligations. This is judgment
— the system doesn't auto-match.

Surface:
- Staged entries that should be linked to an obligation
- Staged entries that need manual classification (obligation tells
  Hobson the right accounts)
- Instances with no bill yet (variable amounts — flag for "bills to
  chase")

### 4.b — Create Payments

For matched staged entries, create Payment records via CLI:
- Transaction pointer = Staged (staged entry header ID)
- Amount = the staged entry amount
- Link to the appropriate Invoice

### 4.c — Update staged entry classification

Where the obligation informs better account assignment than what the
parser or classifier produced, Hobson updates the staged entry lines
with the correct accounts.

### 4.d — Create Invoices for variable amounts

When a bill has arrived (utility PDF in the Concord folder, etc.),
create the Invoice on the appropriate Instance with the actual amount.
Update invoice state to InvoiceReceived (Outgo) or InvoiceSent (Income).

### 4.e — Generate tenant invoices

For Income agreements (tenant rent + utilities), generate invoices
using the utility bill amounts from 4.d. This replaces
`generate_bills.py` or wraps it.

### 4.f — Update Invoice lifecycle states

Transition invoice states, payment states, and blocker states as
appropriate based on what was found.

## Phase 5 — Classify remaining unknowns  [JUDGE]

Staged entries not matched to any obligation go through the current
categorization flow — Hobson best-guesses accounts for unknowns,
surfaces ambiguous merchants.

## Phase 6 — Shadow post + Post  [DET]

Shadow post validates everything. Then batch post promotes staged
entries to journal entries in the ledger.

## Phase 7 — Update Payments (Staged → Posted)  [DET]

For every Payment whose transaction pointer is Staged and whose
staged entry was just posted, transition the pointer to Posted
with the new Journal Entry Header ID.

Update Invoice posted states to PostedToLedger.

## Phase 8 — Reconcile  [DET]

Same as current:
- 8.0 — Brokerage cash true-up (run first)
- 8.a — Reconcile against `recon_balances` CSV (the outside world)
- 8.b — Balance-sheet integrity (debits == credits)

## Phase 9 — Cash-flow projection  [DET]

Run the projection:

```
Per managed account:
  projected_low = current_balance + known_inflows − known_outflows
```

Produce:
- Per-account projected low balance
- Shortfall alerts (projected_low < $500 cushion)
- Transfer recommendations
- "Bills to chase" — Instances with no Invoice (known obligation,
  unknown amount)
- Brokerage $500 contribution — affordable? y/n

## Phase 10 — Reports  [DET]

Render net-worth and spending reports. Same as current.

## Phase 11 — Write summary  [JUDGE]

Same shape as current, with CashFlow-informed sections:

```
# Saturday Summary — YYYY-MM-DD
## 🟢/🔴 Reconciliation
## 🟢/🔴 Balance-sheet integrity
## ⚠️ Spending report — please read
## Problems encountered
## Judgment calls I need from you
## Reports
## Cash-flow projection (next N days)
   per-account projected low, shortfall alerts, transfer recs
## Upcoming bills
   table + 🚩 known-cadence with NO bill in hand
## Money you need to move
   transfers, standing-transfer recs, brokerage contribution
## Review stack
## Candidate learnings
```

---

## Open questions

- **Horizon N**: 30 days? 60? Configurable per run?
- **Invoice date / due date defaults**: When the sweep creates an
  Invoice from a fixed-amount Payment Agreement, what are the invoice
  date and due date? The instance date for both? Instance date for
  invoice date, instance date + payment terms for due date?
- **Tenant invoice generation**: Does this stay as `generate_bills.py`
  or become a SonOfLeo CLI route?
- **Portfolio**: Still reading from `leobloom_prod` during transition
  per the migration roadmap. Net worth report bridges both databases.
- **Cutover test**: What's the minimum set of CLI routes needed before
  Hobson can run this end-to-end without LeoBloom?
