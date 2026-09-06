# Saturday Routine — State Machine Draft

Written 2026-09-04. Revised 2026-09-06 after the 09-05 Saturday run
exposed flow gaps and after a Hobson↔Dan architecture session that
restructured classification into three separate concerns: diagnostics,
account resolution, and PA linkage.

Mechanical nodes run scripts or CLI routes. Judgment nodes invoke
`claude -p` with a step-specific blueprint prompt and return structured
JSON with an outcome key. The state machine follows a transition table —
no LLM reasoning in the orchestrator.

Supersedes `cashflow-cli-routes-proposal-2026-08-28.md` for flow design.
The CashFlow spec (`Specs/Behavioral/CashFlow.md`) remains canonical for
requirements.

---

## Phase 1 — Dan's prep (manual, before the state machine)

| Step | Actor | Action | Effect | Success | Fail |
|---|---|---|---|---|---|
| 1.0.0.0 | Dan | Download from the internet | All FI data pulled and placed in correct locations | 2.0.0.0 | N/A |
| 1.1.0.0 | Dan | Download FI extracts | Transaction and position exports placed in `/LeoBloomImportData/` | 1.2.0.0 | N/A |
| 1.2.0.0 | Dan | Download invoices | Utility bills and any other tracked invoices to their folders | 2.0.0.0 | N/A |

---

## State machine boundary

**Everything below this line (steps 2.0 through 9.0) runs inside the
state machine.** Dan starts it once at step 2.0. The machine walks the
transition table through all phases — ingest, classify, resolve, shadow
post, real post, reconcile, cash flow, reports — and exits at END or on
an unrecoverable failure. Any node that fails routes to the global
failure handler (F.0). The phase headings below are logical groupings
for readability; they are not separate machines.

---

## Phase 2 — Ingest + classify + resolve (mechanical + judgment nodes)

| Step | Actor | Action | Effect | Success | Fail |
|---|---|---|---|---|---|
| 2.0.0.0 | Dan | Prompt Hobson to begin | Hobson launches the state machine | 2.1.0.0 | N/A |
| 2.1.0.0 | State machine | Begin controlled workflow | Sequential node execution with structured outcomes. Each node returns SUCCESS or FAIL. | 2.2.0.0 | F.0.0.0 |
| 2.2.0.0 | Script | Preflight — file validation | Validate filename dates against file content. Only transaction dates are checked — metadata dates (balance-as-of headers, download timestamps) are ignored. Verdicts per file: PASS, EMPTY (no rows — assume no activity), UNVERIFIED (snapshot), HARD STOP (content contradicts filename). Non-zero exit on any HARD STOP. | 2.2.1.0 | F.0.0.0 |
| 2.2.1.0 | Script | File transformation | Run each FI-specific parser. Produce standardized raw JSONL per source. Move originals to `Processed/`. Log applied windows per source. | 2.2.2.0 | F.0.0.0 |
| 2.2.2.0 | CLI | Ingest to stage | `Ingestion IngestRawFileToStage` — parse each raw file, load to stage, dedup by native ref. **No classification.** Entries land in `ingestion.staged_entry` / `staged_entry_line` with no account or PA assignments. | 2.2.3.0 | F.0.0.0 |
| 2.2.3.0 | CLI | ProjectionSweep | Walk every active agreement's cadence, create missing Instances through the horizon. For PAs with a fixed amount and `daysDueAfterInvoiceDate`: also create an Invoice on each new Instance (amount from the PA, due date = invoice date + `daysDueAfterInvoiceDate`). For variable-amount PAs: create Instances only — Invoices wait for the parsing step. Return instance composites for what was just created. | 2.2.4.0 | F.0.0.0 |
| 2.2.4.0 | Script | Parse external invoices | For each new bill PDF in tracked folders: extract counterparty, amount, invoice number, dates. Call CLI to create or update an Invoice on the appropriate Instance. If the sweep already created an Invoice (fixed-amount PA), overwrite amount and due date with actuals from the bill. If no sweep Invoice exists (variable PA), create one. Try multiple known filename patterns per source; fail loudly to F.0 on no match (format changes surface here, not silently). Skip if no new bills found (not a failure). | 2.2.5.0 | F.0.0.0 |
| 2.2.5.0 | CLI | Classify — all rules, one run ID | Run the classification engine twice internally: once with account-claimant rules, once with PA-claimant rules. Both passes use the same candidates (unposted stage entry lines). Persist all match diagnostics to the classification diagnostic table under a single run ID. The classifier does **not** write `accountId` or create any PA linkage — it records what matched and returns the run ID. Two internal passes avoid false conflicts from the priority-based resolution logic. | 2.2.6.0 | F.0.0.0 |
| 2.2.6.0 | CLI | Account resolution | Read the account-claimant diagnostics for this run ID. For each stage entry line: OneMatch or ClearWinner → write `accountId` on the line. Tied → flag for operator. NoMatch → leave unassigned. Update header status based on results (Classified, NoMatch, Conflict). This is `StageEntryOrchestration`'s job. | 2.2.7.0 | F.0.0.0 |
| 2.2.7.0 | CLI | PA resolution — pivot and create linkage | Read the PA-claimant diagnostics for this run ID. Pivot from row-focused to rule-focused: for each PA, how many stage entries matched it? Single-claimant → create a linkage record in the CashFlow domain's PA linkage table (PA × entry). Multi-claimant → create no record, flag entire cluster for operator. Return structured result: `{ linked, multiClaimant, unmatched }` with `containsUnwrittenTies` flag per cluster. | 2.2.7.1 | F.0.0.0 |
| 2.2.7.1 | `claude -p` | Operator review of PA linkage | Blueprint: "Review the PA linkage table and unresolved clusters from step 2.2.7.0. For linked records: verify the match looks correct (right PA, plausible description/amount). For multi-claimant clusters: pick the correct stage entry for each PA and create its linkage record. For clusters with unwritten ties: add the correct linkage. For anything suspicious: remove or reassign linkage records. Return `{outcome: SUCCESS, corrections: N}` when satisfied." | 2.2.7.2 | F.0.0.0 |
| 2.2.7.2 | `claude -p` | Operator sign-off on PA mapping | Blueprint: "Confirm that all PA linkage records are correct and the mapping is ready for payment matching. This is the point of no return — after sign-off, step 2.2.8.0 creates Payment records. Return `{outcome: APPROVED}`." | 2.2.8.0 | F.0.0.0 |
| 2.2.8.0 | CLI | ProcessObligations — match linked entries to invoices | Read the PA linkage table. For each PA with linked entries: pull open invoices and pair by date window (grace period derived from cadence). Resolution is **invoice-focused**: for each open invoice, how many linked entries could satisfy it? Exactly one candidate → auto-create Payment (linking entry to invoice), re-derive invoice paymentState. Zero candidates → unfulfilled (expected if bill not yet paid). Multiple candidates → do not pick, send cluster to step 3.0. An entry claimed by one invoice is removed from candidacy for all others — no greedy matching. If sum of payments exceeds invoice amount, flag as overpayment exception. Return structured result: `{ autoMatched, unfulfilled, multiCandidate, overpaymentExceptions }`. | 2.2.9.0 | F.0.0.0 |
| 2.2.9.0 | Script | Portfolio imports | Run position importers for each tracked portfolio source. Load snapshots to portfolio tables. Positions posted every week regardless of transaction activity. | 3.0.0.0 | F.0.0.0 |

## Phase 3 — Categorize unknowns (judgment node)

| Step | Actor | Action | Effect | Success | Fail |
|---|---|---|---|---|---|
| 3.0.0.0 | `claude -p` | Categorize uncategorized stage entries + resolve obligation ambiguities | Blueprint: "Two jobs. (1) Query all `new`/`parked` stage entries across all stage tables. Use `category-crosswalk.md`. For each entry: if confident → set `proposed_account_code`, promote, log in review stack. If uncertain but best-guess available → promote but flag in 'Judgment calls for Dan'. If genuinely unknown → leave `parked`. (2) Resolve the `multiCandidate` and `overpaymentExceptions` lists from step 2.2.8.0. For multi-candidate clusters: use dates, amounts, and descriptions to pick the correct stage entry for each invoice, or flag for Dan if genuinely ambiguous. For overpayment exceptions: surface to Dan with the excess amount and likely cause. Return `{outcome: SUCCESS, classified: N, flagged: N, parked: N, obligationsResolved: N, obligationsEscalated: N, reviewStack: [...], judgmentCalls: [...]}`." | 4.0.0.0 | F.0.0.0 |

## Phase 4 — Shadow post + recon (loop until clean)

The shadow post validates that every JE leg is correct before anything
becomes indelible. Obligation linkage (PA linkage records, invoice
links, payment records) is mutable and correctable after the fact — it
does not need shadow validation. The shadow post can optionally include
a read-only cash flow snapshot showing what the batch does to PA-linked
account balances, but it does not run or roll back any cash flow
operations.

| Step | Actor | Action | Effect | Success | Fail |
|---|---|---|---|---|---|
| 4.0.0.0 | CLI | Shadow post | `Ingestion PostStageEntries` with `isShadow: true`. Posts all promoted stage entries inside a transaction, captures before/after trial balance, then rolls back. Returns structured JSON with account-level balances. JEs are indelible once posted — this is the gate that prevents voids. | 4.1.0.0 | F.0.0.0 |
| 4.1.0.0 | `claude -p` | Shadow recon | Blueprint: "Compare the shadow trial balance to `recon_balances` CSV (the point-in-time truth Dan captured at extract time). For each account: compute delta. For each non-zero delta: identify the exact stage entries composing it — name the specific transactions and their amounts, not the category. Known deltas with transaction-level causes (e.g. IRA SPAXX core-cash, identified pending items) may be carried. If a delta is caused by a classification error you can fix: fix the staged entry classification and return `{outcome: RETRY}`. If a delta is unexplained: return `{outcome: MISMATCH, account: ..., delta: ..., candidates: [...]}`. If all accounts reconcile: return `{outcome: PASS}`. Max retries: 3." | On PASS: 5.0.0.0 | On RETRY: 4.0.0.0 |
| 4.1.1.0 | `claude -p` | Shadow recon — mismatch triage | Entered on MISMATCH or max retries exceeded. Blueprint: "Reconciliation failed. These accounts don't match: {list}. Open the relevant triage statements (`Procedures/Hobson/Reconciliation/triage-*.md`) to find the offending transactions. If you can identify and fix the root cause: fix and return `{outcome: FIXED}`. If you need Dan: return `{outcome: ESCALATE, reason: ...}`." | On FIXED: 4.0.0.0 | On ESCALATE: F.2.0.0 |

## Phase 5 — Post + reconcile

| Step | Actor | Action | Effect | Success | Fail |
|---|---|---|---|---|---|
| 5.0.0.0 | CLI | Full post | `Ingestion PostStageEntries` with `isShadow: false`. Rubber stamp — the shadow recon loop already validated the data. Stage entries become journal entries. | 5.1.0.0 | F.0.0.0 |
| 5.1.0.0 | Script | Brokerage true-up | Run `brokerage_trueup.py` against `recon_balances` CSV. If drift ≥ $0.01: post the drift JE (Dr/Cr brokerage cash ↔ equity) via CLI. Surface the drift amount and JE id (or "no drift") for the summary. | 5.2.0.0 | F.0.0.0 |
| 5.2.0.0 | Script | Balance-sheet integrity | Run `ledger_integrity.py --as-of {period-end}`. GATE: `Balanced: YES` → PASS. `Balanced: NO` → HARD STOP. An unbalanced ledger means a one-legged JE exists. Nothing else is trustworthy until it's found and fixed. | 5.2.1.0 | F.0.0.0 (HARD STOP) |
| 5.2.1.0 | `claude -p` | Ledger recon | Blueprint: "Run `reconcile_csv.py` against `recon_balances`. For each account delta: name the exact transactions composing it. Never rubber-stamp a repeating delta — a recurring mismatch is an unreconciled error, not a stable offset. Known deltas with written, transaction-level causes (IRA SPAXX core-cash) may be carried. Unexplained deltas go in 'Problems encountered'. Return `{outcome: SUCCESS, reconResults: [...], problems: [...]}`." | 5.2.2.0 | F.0.0.0 |
| 5.2.2.0 | `claude -p` | Position recon | Blueprint: "Compare imported positions to expected values for each portfolio source. Verify share counts and cost basis match the import source. Flag drift > $1. Return `{outcome: SUCCESS, positionResults: [...]}`." | 6.0.0.0 | F.0.0.0 |

## Phase 6 — Cash flow transitions

| Step | Actor | Action | Effect | Success | Fail |
|---|---|---|---|---|---|
| 6.0.0.0 | CLI | TransitionPaymentsToPosted | For each Payment whose pointer is Staged and whose stage entry was just posted: set the JE header ID on the Payment (retain stage header ID as provenance). Derive postedState on affected Invoices. Check isFulfilled on affected Instances. Return list of transitions: agreement name, invoice amount, JE ID. | 7.0.0.0 | F.0.0.0 |

## Phase 7 — Tenant invoice generation

| Step | Actor | Action | Effect | Success | Fail |
|---|---|---|---|---|---|
| 7.0.0.0 | `claude -p` | Generate outgoing tenant invoices | Blueprint: "Run `generate_bills.py` for the **prior month** (tenant agreements bill in arrears — the bill generated today covers last month's charges). The script reads utility amounts from the W&E and gas Invoices on the prior month's Instances (created by the sweep or parsing step). If utility Invoices haven't arrived yet (no Invoice on the W&E or gas Instance for the billing period), skip tenant invoice generation and flag as 'bills to chase' in the summary. Return `{outcome: SUCCESS, generated: [...], skipped: [...]}`." | 7.1.0.0 | F.0.0.0 |
| 7.1.0.0 | CLI | Update invoice states | Set generated tenant invoices to `InvoiceSent` if already delivered. Update any other invoice lifecycle states that changed during this phase. | 8.0.0.0 | F.0.0.0 |

## Phase 8 — Projection

| Step | Actor | Action | Effect | Success | Fail |
|---|---|---|---|---|---|
| 8.0.0.0 | CLI | Cash flow projection | `CashFlow Projection` with horizon N days. Per managed cash account: compute `projected_low = current_balance + known_inflows − known_outflows`. Surface shortfall alerts (projected_low < $500 cushion), transfer recommendations, bills to chase (Instances with no Invoice), and discretionary contribution affordability. | 9.0.0.0 | F.0.0.0 |

## Phase 9 — Reports + summary

| Step | Actor | Action | Effect | Success | Fail |
|---|---|---|---|---|---|
| 9.0.0.0 | `claude -p` | Generate reports and Saturday summary | Blueprint: "Run `render_networth.py` and `render_transactions.py`. Sync COA markdown to prod state. Then write the Saturday summary MD to `Reports/Saturday/saturday-YYYY-MM-DD.md` using the canonical template. Include ALL control gate results from prior steps, ALL review stack entries from step 3.0, ALL recon results from 5.2.1–5.2.2, cash flow projection from 8.0, and candidate learnings tagged by destination (merchant rule / importer quirk / process rule / memory / continuity). The spending report path must be prominent — Dan reads it every week, no exceptions." | END | F.0.0.0 |

---

## Global failure handler

Any node that fails routes here. This is not Phase 2-specific — it
handles failures from any step in the state machine.

| Step | Actor | Action | Effect | Success | Fail |
|---|---|---|---|---|---|
| F.0.0.0 | State machine | Failure exit | Exit with failure code, failed step number, and structured error reason. Log full context. | F.1.0.0 | N/A |
| F.1.0.0 | `claude -p` | Investigate failure | Blueprint: "The state machine failed at step {N} with error {E}. Read the error, read the relevant source files, diagnose the root cause. If fixable (parser bug, data formatting issue, classification error): fix it and return `{outcome: FIXED}`. If not fixable (missing file, schema mismatch, policy question): return `{outcome: ESCALATE, reason: ...}`." | F.4.0.0 | F.2.0.0 |
| F.2.0.0 | `claude -p` | Escalate to Dan | Surface the problem, the diagnosis from F.1.0.0, and what Dan needs to do (re-download a file, approve a migration, make a policy call). Wait for Dan's input. | F.3.0.0 | N/A |
| F.3.0.0 | Dan | Fix the issue | Dan resolves whatever was escalated | F.4.0.0 | N/A |
| F.4.0.0 | State machine | Restart at failed step | Resume the state machine from the step that failed. Prior successful steps are not re-run. | Various | F.0.0.0 |

---

## Design notes

**Pattern.** Modeled after the EtlReverseEngineering engine
(`/media/dan/fdrive/ai-sandbox/workspace/EtlReverseEngineering/`). The
orchestrator is dumb — a deterministic state machine that follows a
transition table. All intelligence lives in the `claude -p` agents,
which get a fresh context on every invocation.

**Three tables, three concerns.** Classification produces three separate
data stores, each owned by a different domain:

1. **Diagnostic table** (owned by StageDataClassification) — "rule X
   matched entry Z." Immutable after the classification run. The
   operator reads it during review. Never the authority for what was
   decided — only what was observed.
2. **PA linkage table** (owned by CashFlow) — "PA Y claims entry Z."
   Created by CashFlowOps after the operator confirms the match at
   2.2.7.2. This is the SOR for the match decision. ProcessObligations
   reads from here.
3. **Payment records** (owned by CashFlow) — "Invoice I on PA Y is
   satisfied by entry Z." Created by ProcessObligations when it matches
   a confirmed linkage to a specific invoice.

`paymentAgreementId` on `StageEntryLine` is eliminated. The PA-to-entry
linkage lives in the CashFlow domain's own table, not as a tag on the
stage entry.

**One classification step, two internal passes.** The classifier runs
twice — once with account-claimant rules, once with PA-claimant rules —
under a single run ID (2.2.5.0). Two passes avoid false conflicts: the
classifier's priority-based resolution doesn't distinguish claimant
types, so mixing account and PA rules would produce spurious ties. From
the state machine's perspective this is one step; the internal split is
an implementation detail.

**Ingest and classify are separated.** Step 2.2.2.0 loads stage entries
with no classification. Step 2.2.5.0 classifies them. This separation
enables the architectural split: the classifier records diagnostics
without writing to any domain, and each consumer (account resolution at
2.2.6.0, PA resolution at 2.2.7.0) acts on those diagnostics
independently.

**Account resolution is its own step.** Previously baked into ingest
(2.2.2.0), now 2.2.6.0. Reads account-claimant diagnostics and writes
`accountId` on matched lines. Updates header status. Owned by
`StageEntryOrchestration`.

**PA resolution is rule-focused, not row-focused.** The dangerous
failure mode is a single rule (i.e., a single PA) matching multiple
stage entries, not a single stage entry matching multiple rules. Two
$150 checking-account lines both claiming the same water bill is a
catastrophe; one $150 line ambiguously matching the water bill or the
ISP bill is merely inconvenient. Step 2.2.7.0 pivots the diagnostics
by rule and flags multi-claimant clusters.

**Operator gate before payment linkage.** Diagnostic matches are
advisory. PA linkage records and Payment records are the decisions.
The operator sign-off at 2.2.7.2 is the point of no return — after
that, 2.2.8.0 creates linkage records and Payments.

**No backstop creation — hard stop.** If ProcessObligations finds a
linked entry with no open instance or invoice to match against, it
surfaces an error and stops. Any open transactions roll back. It does
not create instances or invoices on the fly — that masks upstream
problems (sweep didn't run far enough, cadence is wrong, bill hasn't
arrived). This is a requirement, not a preference.

**Overpayment is flagged, not automated.** If the sum of payments on an
invoice exceeds the invoice amount, the system surfaces it as an
exception. Under GAAP the excess is a credit (liability or revenue),
which requires a ledger-level split we don't model yet. No `Overpaid`
payment state for now — park it for the operator.

**Sweep creates invoices for fixed-amount PAs.** ProjectionSweep
(2.2.3.0) creates Instances for all PAs, and also creates Invoices for
PAs with a known fixed amount when both `expectedAmount` and
`daysDueAfterInvoiceDate` are `Some`. Variable-amount PAs get Instances
only — their Invoices come from the parsing step (2.2.4.0). If an
actual bill arrives for a fixed-amount PA, the parsing step overwrites
the sweep-generated Invoice's amount and due date with actuals.

**Shadow recon validates JE legs, not obligation linkage.** JEs are
indelible — once posted, correction requires a void. Obligation linkage
(PA linkage records, invoice links, payment records) is mutable and
correctable in place. The shadow post (Phase 4) exists to prevent
indelible errors.

**Tenant invoices bill in arrears.** The bill generated on Saturday
covers the prior month's charges. The billing period is derived from the
agreement's cadence and arrears terms, not from the calendar. This is
deterministic — the LLM does not choose which month to bill.

**paymentState and postedState are derived, not operator-set.** Creating
a Payment auto-derives paymentState from the sum of payments vs invoice
amount. TransitionPaymentsToPosted auto-derives postedState from Payment
pointer states. The operator sets invoiceState (bill arrived/sent) and
blocker (why something is stuck).

**Shadow recon is a loop.** Shadow post → shadow recon → fix → shadow
post, with a max retry count. Mismatches are caught and fixed before
they reach the real ledger. The post-post recon (5.2.1) is the final
check — should confirm zero new deltas since the shadow already passed.

**Open questions for future design:**
- What does the state machine implementation look like? Python script
  with a transition dict (like EtlReverseEngineering) or a COYS-style
  cron + agent setup?
- How does the state machine pass structured output from one `claude -p`
  node to the next? Process artifacts (JSON files per step, like
  EtlReverseEngineering's `process/` dir)?
- Max retry counts for the shadow recon loop and failure handler.
- The invoice parsing script (2.2.4) doesn't exist yet — needs design.
  Must handle FI format changes gracefully (Enbridge changed filename
  format on 2026-08-24; the parser should try multiple known patterns
  and fail loudly on no match).
- ProcessObligations (2.2.8) matching by date window needs spec work
  beyond what's in CashFlow §11. Grace period by cadence is implemented
  in CashFlowOps but the matching logic is placeholder.
- Schema for the classification diagnostic table (owned by
  StageDataClassification) and the PA linkage table (owned by CashFlow)
  — neither exists yet.
- `classifyMatchCandidatesAndUpdateLines` currently writes tags. Under
  the new architecture, it records diagnostics only. The write-side
  logic moves to the consumers (StageEntryOrchestration for accounts,
  CashFlowOps for PA linkage). This is the refactor Simian identified
  on 2026-09-06.
