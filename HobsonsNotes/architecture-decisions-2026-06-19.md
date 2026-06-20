# Architecture Decisions — 2026-06-19

**Author:** Hobson (comptroller). **Interlocutor:** Dan. **Context:** A long evening's
design conversation while BD's context was full. Captures decisions, the *why* behind
each, and the open implementation items. Dan may point BD at this. Nothing here was coded;
this is direction, not a changelog.

The through-line: **SonOfLeo should be built Saturday-first.** The weekly close is the
product; everything below serves making it deterministic, atomic, and auditable. See also
`cli-requirements-from-leobloom-usage.md` — this evening is that thesis turned into structure.

---

## 1. Surrogate key vs natural key (account code)

**Decision (final, BD + Dan in a prior conversation; consequences refined tonight):**
keep the surrogate **UUID** as the primary/foreign key in the DB. The account `code` is a
unique, immutable business key but is **not** the PK.

**Why a surrogate at all** (the honest ledger — most textbook arguments don't apply here):
- "Natural keys mutate" — doesn't apply; `code` is immutable by spec (REQ-AC-4.22).
- Performance/FK width — noise at this scale.
- Readable raw SQL — actually argues *for* the natural key (my Saturday triage reads codes,
  not UUIDs).
- **The one real win: cheap re-keying.** Once thousands of JE lines reference accounts, a
  surrogate makes "fix a miskeyed code" a one-row relabel instead of a ledger-wide cascade.
  It converts `code`-immutability from a policy you must keep into a fact you needn't think
  about. That's the justification, and it's forward-looking (the JE-line FK web is coming).

**Hard constraint:** the **UI/CLI must never deal in UUIDs for accounts.** (This is *not*
true for transactions/journal entries — those may be id-addressed at the UI.) A surrogate
only pays off if it stays hidden; expose it and you pay its cost (an opaque id to carry)
without its benefit.

**The splinter (open):** the current Account CLI surface leaks the UUID — `Create` takes a
`parentId` UUID, `Deactivate`/`UpdateName`/`UpdateExternalReference` take `id`, and
`AccountReturn` hands `id`/`parentId` back as UUIDs. Cheap to fix now (Account is the only
domain on the wire); expensive after JE/obligation/portfolio copy the pattern.

---

## 2. The id/code duality — reference discipline

**Root principle: identity ≠ a validated value.** "A JE line references an Account" does not
mean "a JE line holds a fully-rehydrated, validated Account." The first is unavoidable; the
second loads the relational closure of the database into the app layer. A JE line needs only
*proof of the specific facts its own invariant depends on*: existence, active-as-of the entry
date, and (for posting) normal balance. Three scalars, not a graph.

**The kicker:** validity is *as-of an instant* (REQ-AC-1.48.1). There is no context-free
"valid Account." Validity is a fact about `(account, instant)`. So the guarantee cannot live
in the stored type — it lives at the operation that supplies the instant.

**Pattern:** aggregates reference each other **by identity**. Context-dependent facts are
proven at the workflow that has the context, and carried as a thin **witness type** the
dependent constructor demands:

```fsharp
// Proof an account was loaded and found valid for posting at an instant.
// No public constructor — minted only by a resolver that read one row.
type PostingAccount = private {
    Id: AccountId
    Code: AccountCode
    NormalBalance: NormalBalance
}
module PostingAccount =
    let resolve (asOf: Instant) (code: AccountCode) : Result<PostingAccount, string> = ...

type JournalEntryLine = private {
    Account: PostingAccount   // can't exist without proof
    Amount: Money
}
```

A `JournalEntryLine` is, by construction, impossible to build against an account not proven
to exist and be active on the entry date — full type-safety — but the proof is a three-field
token from one query, not the graph.

**Why you never load the whole DB:** keys/structure are immutable (REQ-AC-4.22) and the
system only ever persists valid rows (REQ-SYS-2.1). Therefore **reads trust the row.** Tree
validation (no cycles, child-type-matches-parent, parent-active) happens once, at the
**create** workflow, against a bounded ancestor slice. Nothing re-validates ancestry on read.
The graph load is a *write-time, single-chain* cost, never a *read-time, whole-DB* one.

---

## 3. The type family (and the two species of `None`)

No type in the system carries `id option` *and* `code option`. Identity is never optional on
a thing that exists. The optionality Dan was reaching for is the **seam between lifecycle
stages** — modeled as a *different type per stage*, each field mandatory-present or
absent-by-type, never optional-and-praying.

```fsharp
type Account = private {            // persisted aggregate
    Id: AccountId                   // mandatory
    Code: AccountCode               // mandatory
    ParentId: AccountId option      // optional for a REAL reason (see below)
    // ...
}
type AccountCreateInput = {         // id absent BY TYPE — system mints it (REQ-AC-2.13)
    Code: AccountCode
    ParentCode: AccountCode option  // UI speaks codes; None = root
    // ...
}
// PostingAccount (above) — the validated reference; both present, private.
```

**Two species of `None`, and the rule that separates them:**
- `ParentId: AccountId option` — `None` means "root account." Absence is a true, permanent
  **domain fact.** Option is correct.
- `Id: AccountId option` — `None` would mean "an account without identity," not a real state.
  Absence here is a **lifecycle phase.** Wrong tool — use a separate input type.

> **Reach for `option` when absence is a fact about the domain; reach for a separate type
> when absence is a phase in the lifecycle.** Identity is never the former; parentage always is.

**Rejected: `AccountReference { id: Guid option; code: string option; at-least-one-Some }`.**
It (1) launders a compile-time guarantee into a runtime assertion — the anti-F# move;
(2) re-creates the UUID-on-the-UI leak it was meant to prevent, by carrying the id through the
UI-facing type; (3) invents a new illegal state — `{ id=Some; code=Some }` with nothing
forcing the two to name the same account; (4) is looser than every real context, so every
consumer re-tightens it by hand. If a genuine internal "take either form" call site ever
appears, use a 2-case DU (`ByCode | ById`), never a both-optional record — and not
speculatively (`fetchById`/`fetchByCode` already exist).

---

## 4. Code↔id translation: no memoization (except one place)

The translation lives in exactly one seam: inbound, the interface resolves `code → AccountId`
once against the unique index; outbound, the projection maps `id → code`. The UUID never
propagates inward. (It's IO — a DB read — so it lives on the persistence/orchestration side,
**not** as a pure helper in the domain core.)

**Do not memoize.** Per interaction the translation count is single digits, each a
unique-indexed single-row lookup (sub-ms). The real Saturday cost is process startup
(dotnet-per-JE), which a cache can't span. A long-lived cache would also trade a free query
for a *correctness* risk: resolution includes the active-as-of check, and an account can be
deactivated between cache-load and use.

**The one legitimate cache:** the `ledger post --batch` path. One process, many JEs → resolve
each *distinct* code once into a local `Dictionary<AccountCode, AccountId>`, scoped to and
discarded with the batch. A local lookup inside one workflow, **not** a system-wide facility.

---

## 5. Canonical staging format + batch post (the narrow waist)

**Plan:** all importers convert to **one canonical stage/import format**. Staged transactions
persist in it. Saturday becomes: normalize everything into that one format, then **post en
masse** from a staging table.

**The real prize:** the recurring *promoter* bugs (Fidelity Visa refund posted backwards;
SECU dividends posted backwards — both in the LeoBloom wakeup history) recur because
debit/credit **direction logic is duplicated per-importer.** Collapse to one format and the
direction decision happens **once**, at the canonical→JE boundary. Fix it there, fixed for
every institution. Same for classification (the Justin-allowance miss).

**What the canonical format MUST carry** (fields whose *absence* caused this spring's bugs):
1. **The bank's own debit/credit indicator, preserved natively** — not collapsed into a
   signed amount. The Visa refund bug was the promoter ignoring `transaction_type`.
2. **One enforced sign convention** every importer conforms to (e.g. positive = into the
   source account). Half the direction drift is importers disagreeing on what positive means.
3. **The raw description, verbatim** — classification keys off it and descriptions change
   (Justin allowance). Normalize a copy if needed; keep the original for re-classification.
4. **A dedup / idempotency key** (external ref, or a hash of source+date+amount+description).
   At "post hundreds at once," a re-dropped file is one re-run from double-posting a whole
   batch. The staging table must reject the duplicate before it becomes a JE.

**Design fork (decided):** stage the **normalized economic event** (known leg + amount + date
+ description + type; contra-account unresolved), and run **one** classify-and-promote pass at
**post time** to produce balanced JEs. Resolve witnesses and direction at post, against the
world as it is then — not at staging time (accounts can be deactivated in the interim).

**Atomicity:** the staging batch is the natural transaction boundary. Resolve distinct codes,
mint witnesses, validate the set (existence, active-as-of, every JE balances to zero), then
post inside one DB transaction. One bad reference rejects the whole batch — failures atomic
per *run*, not per *line*. Bonus: staged rows persist → every JE traces back to its canonical
row and raw import line. "Double-click every discrepancy" made structural.

**Caution:** the canonical format is now a **contract** every importer and the poster depend
on. Treat the waist as stable — change it deliberately, never casually.

---

## 6. Saturday is a domain

In LeoBloom, Saturday is something I do *to* the system from outside (Python scripts, raw SQL,
a legacy prototyper binary, `bc`). In SonOfLeo, **Saturday becomes something the system *is*** —
staging batches, post runs, the canonical format, and the control gates (reconcile, ledger
integrity, brokerage true-up) become first-class operations *inside* the walls with verdicts
the system can assert, not scripts squatting outside.

This is the inversion the requirements doc argued for: accounting systems get built
textbook-first (the eleven reports nobody reads, `transfer`/`period` machinery never invoked);
SonOfLeo gets built workflow-first.

**Discipline:** model Saturday's **invariants**, not this spring's **choreography**. The domain
enforces *stage → validate → post atomically; nothing posts unless the batch balances and
reconciles* — the laws that hold every week forever. The order I open importers, how I walk
obligations, which report I read first — that's procedure, and it stays in the Saturday skill,
free to evolve without a migration. (Same shape as Dan's DB-constraint philosophy: structural
invariants in the model, mutable business logic in the app layer.)

---

## 7. Division of labor (Dan's stated goal)

- **No personal/financial data in SonOfLeo.** The app is the engine, not the data. (Consistent
  with Dan's standing "no finance data in any git repo.")
- **Vendor/transaction mapping rules become a first-class domain** *inside* the app — the cure
  for LeoBloom's "merchant rules table overgrown." Rules carry **provenance**: a posted row
  records *which rule fired*, so triage becomes "rule 14, vendor Blumenthal" instead of raw-SQL
  archaeology.
  - **Keep the rule engine declarative and total.** A rule is (matcher → account); matching is
    a pure, ordered, deterministic function with a defined tie-break (longest-match — the lesson
    LeoBloom paid for). The moment a rule needs a judgment call, **it is no longer a rule** — it
    is a decision, and decisions escalate (to a dumber agent for small ones, to the comptroller
    for the rest). A rule engine that guesses is a second program you debug at midnight.
- **Parsing peculiarities stay in Hobson's Python/shell scripts** — source-specific, volatile,
  cheap to fix. The stable/volatile seam: volatility outside the wall, durable domain semantics
  inside.
- **Hobson's skills and workflow stay outside the app.**

**The thesis:** *push determinism as low as it will go, judgment as high as it must, and make
the boundary between them explicit.* The failure mode of an AI running the books is fuzz
leaking into every step — a hundred little coin-flips compounding. This design starves the
fuzz: the many steps are deterministic, the few judgment points are isolated and named. That's
what makes a hundred-transaction batch trustworthy. It also finally makes the
**no-raw-SQL-writes** policy livable — there's nothing left to route around the app for.

The comptroller's future role: orchestrate across **deterministic apps** (helped design) and
**dumber agents** countable-on for small decisions. Conductor, not scribe.

---

## 8. Closed / out of scope

- **Multi-entity / "Books" partition (segregate business vs personal books): rejected.** One
  taxpayer, no LLC (CPA-advised against). Business-vs-personal is a *reporting dimension* on
  accounts (a lightweight segment tag for Schedule E in 2027), **not** a ledger partition. The
  complexity threshold that would warrant separate self-balancing books is the same threshold
  that warrants a CPA — not this system. Dan's words: "if I ever get financially complicated
  enough to warrant it, I should be paying a CPA, not an LLM." Not a tech problem; closed.

---

## Open implementation items (for whoever builds this)

- [ ] Fix the **code-on-the-wire splinter** (§1): parent-by-code on create; accept code on
      deactivate/update; resolve `parentId → parentCode` in the outbound projection. Do it
      before JE/obligation/portfolio copy the UUID-on-the-wire pattern.
- [ ] Define the **witness type** (`PostingAccount` or equiv) and its resolver (§2).
- [ ] Lock the **type family** per §3; no both-optional reference type.
- [ ] Design the **canonical staging format** with the four must-carry fields + dedup key (§5).
- [ ] Build the **batch-post workflow** as the transaction boundary, with the per-batch local
      lookup as the only cache (§4, §5).
- [ ] Spec **Saturday's invariants** as a domain (§6) — invariants, not choreography.
- [ ] Spec the **mapping-rule domain** with provenance and a declarative/total engine (§7).
- [ ] (Someday) lightweight **segment tag** on accounts for Schedule E (§8) — dimension, not
      partition.
