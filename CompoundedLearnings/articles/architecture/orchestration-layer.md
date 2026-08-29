# Orchestration Layer

**Source:** Specs/Archive/Decisions.md, 2026-06-11; Dan's clarification 2026-07-11; read-only
join clarification added 2026-08-28 from the `CashFlow.Payment` CRUD session; Dan's full
definition (five reasons) added 2026-08-28 from a `Src/ModelOrchestrator` pattern-review session.

The orchestration layer (`ModelOrchestrator/`) is for any function that coordinates multiple distinct activities. Domain modules own single-concern operations on their own types; orchestration composes.

## Dan's five reasons `ModelOrchestrator` exists (in his words)

1. **Where domains combine, in the direction the model can't.** `Model/` modules can depend on
   each other one-way when the relationship is intrinsic — `JournalEntryLine` depends on
   `Account` because "you can't have a line without an account." But you can't close an account
   without ensuring its balance is zero, and checking that requires reading `JournalEntry` data
   — the reverse dependency, which would be circular if handled in the model. That's why
   `deactivateAccount` lives in `ModelOrchestrator/AccountDeactivation.fs`, not in `Account.fs`.
2. **Composite validation.** A `JournalEntryHeader` isn't valid unless at least 2 lines
   reference it with equal debits and credits — but you can't validate that in the model,
   because the lines need a `JournalEntryHeaderId` to exist in the first place, and that id
   doesn't exist until DB insertion. (Yes, you could pre-generate the id — that's a mess Dan
   deliberately avoids.) Create, edit, and validate composite qualities in the orchestrator.
3. **Orchestrated events** — a multi-step process triggered by one UI action that succeeds or
   fails as a group. JE creation is header + lines + comments + references, one trigger.
   Ingesting staged entries is read raw → transform → deduplicate → classify, with composite
   validation woven through — one trigger, one outcome.
4. **"Fetch++"** — complex or cross-domain fetch and reporting. `FetchFilterAndSort.fs` and
   `TrialBalance.fs` live here because they reference multiple domains; trial balance in
   particular is "really just an elaborate fetch that operates across multiple domains."
5. **The odd sock drawer.** Some functions don't *strictly* need to be here —
   `AccountCreation.constructNewAndSaveToDb` could theoretically live closer to `Account.fs` —
   but they do three things (validate, create, persist), and that's the general shape Dan
   routes through the orchestrator regardless of whether every individual case is forced there
   by a hard constraint.

**On non-negotiable rules forced up a layer:** Dan wants DDD type constraints to hold
unconditionally wherever possible — "they ALWAYS have to resolve to true (or not an error),
non-negotiable under any circumstance." Composite validation (reason 2) is the one place a
non-negotiable rule is *forced* up to the orchestrator anyway, purely because of the id-doesn't-
exist-yet problem — not because the orchestrator is a looser place to put validation. The
orchestrator is *also* where you have real leeway to decide which *additional*, use-case-specific
validations apply on top of the non-negotiable ones (closing an account, voiding a JE, and some
update functions are the examples Dan gave) — but that leeway is a separate thing from the
composite-validation escape hatch, and shouldn't be used as an excuse to weaken a rule that
could have lived in the model.

## When a function belongs in orchestration

- It needs data from more than one domain module (e.g., deactivating an account requires checking journal entries)
- It coordinates multiple distinct steps even within one domain (e.g., constructing a new entity AND saving it to the database — two activities)

The test is not "does it cross domain boundaries?" but "does it orchestrate?" If a function does more than one thing, it belongs here — even if both things live in the same domain module.

## F# compile order

F# compile order makes cross-domain composition structural rather than optional. A module cannot reference another module that appears later in the build. This means cross-domain functions physically cannot live in a single domain module — they must live above both.

## Example

`deactivateAccount` started in the Account module. When it needed JournalEntry data for its checks (REQ-AC-4.4, REQ-AC-4.6), it moved to `ModelOrchestrator/AccountDeactivation.fs`. The `constructNewAndSaveToDb` functions follow the same principle — they orchestrate construction + persistence.

*Post-refactor note (2026-07-25): ModelOrchestrator also owns cross-entity business validation and read-model types; `InterfaceBridge` now sits above it as the boundary layer.*

## A read-only join across domains is not, by itself, orchestration

"Needs data from more than one domain module" means needing another domain module's *code* —
its F# construction/validation functions, or composing its type into a result. It does not mean
"the SQL touches a table another domain module owns." A single read query with `LEFT JOIN`s into
other schemas, still returning exactly one row per row of the entity's own table, is one
activity — it can live in that entity's own `fetchGenericRead`, no different in kind from a
predicate or `CTE`.

The test that matters is the same as everywhere else in this article: does the *function* do more
than one thing? A join that computes a scalar for the caller's own entity, deterministically and
without needing a second F# module's logic to interpret the result, is still one thing.

**Example:** `CashFlow.Payment.amount` and `.postedToLedgerDate` are marked "not separately
tracked in the database" — they don't exist as `cashflow.payment` columns. `Payment.fs`'s
`fetchGenericRead` joins to `ledger.journal_entry`/`journal_entry_line` and
`ingestion.staged_entry`/`staged_entry_line`, pinned by `payment_agreement`'s debit/credit
account and `master_agreement`'s flow direction so exactly one line matches per payment (no
aggregation). `reconstitute` never sees any of that — it only reads the already-computed
`amount`/`posted_to_ledger_date` columns the query produced, same as any other column. This
stayed inside `Model/CashFlow/Payment.fs`; it did not move to `ModelOrchestrator`, because the
whole thing is still exactly one read, scoped to `Payment`'s own rows.

Contrast with `Flow.paymentAgreements` (type-taxonomy.md, Component types): that case needed a
*child list*, which a single-table `reconstitute` cannot honestly populate no matter how the
query is written — that's what actually forces a move to the orchestrator (an `Obligation`-style
composite), not the mere fact that the data crosses a schema boundary.
