# Decisions

Append-only log of structural decisions that don't attach to any single requirement ID.
One line per decision, dated, with a one-sentence why. If an entry wants to grow past that,
it's hiding a requirement — extract it. If it maps to a requirement ID, it doesn't belong
here; put the *Why* under the ID.

- **2026-06-06** — Two-layer architecture, not the C# three-layer split: domain modules own
  their entity end-to-end (type + validation + persistence); orchestration composes across
  modules. *Why: F# smart constructors eliminate the dumb-model layer that ORMs force on C#.*
- **2026-06-08** — `FieldUpdate<'a>` is `NoChange | SetTo of 'a`, with no `Clear` case.
  *Why: nullability lives in the type parameter (`SetTo None`), making "clear a NOT NULL
  field" unrepresentable rather than merely invalid.*
- **2026-06-11** — `deactivateAccount` graduates from the Account module to the orchestration
  layer when its journal-dependent checks (REQ-AC-4.4, REQ-AC-4.6) are implemented;
  single-domain CRUD stays in its entity module. *Why: a function that needs another domain's
  data is cross-domain composition, and F# compile order makes that structural rather than
  optional.*
- **2026-06-11** — Requirement ID prefix renamed `FT-` → `REQ-` repo-wide, except `BdsNotes/`,
  which is preserved as an archaeological record and excluded from all audits. *Why: the
  BDD-fossil prefix would never be cheaper to fix, and the wakeup notes are history, not index.*
- **2026-06-11** — Deletion policy is per-entity, not system-wide: REQ-SYS-4.1 withdrawn,
  REQ-AC-5.1 reinstated. *Why: whether an entity's records may be hard-deleted is a domain
  decision; a blanket prohibition presumes the answer for entities not yet specced.*
- **2026-06-11** — Definitions added as a fourth document species (`Definitions.md`), above
  the domains. *Why: "entity" changed the scope of REQ-SYS twice in one session; a term
  that does scope arithmetic must be pinned once, where every domain can cite it.*
- **2026-06-11** — Temporal model: all temporal values are instants; no date-only values
  anywhere in the system; persistence stores the instant and deliberately discards the
  original local offset. *Why: date-only values are clumsy at best and wildly inaccurate at
  worst, and the system has no business knowing where the viewer was standing.* Note: the prohibition against date-only has been overturned. See Definitions.md

- **2026-06-11** — NodaTime adopted as the temporal library, mapped through `Npgsql.NodaTime`.
  *Why: it makes the instants-only model compiler-enforced rather than review-enforced, and
  its injectable `IClock` is what makes the audit-timestamp requirements testable.* Note: we rejected the IClock in favor of the AuditEnvelope type.
- **2026-06-11** — Leap seconds are deliberately ignored. *Why: NTP smearing hides them, the
  stack (.NET, Postgres) cannot represent them, no domain operation can observe a one-second
  window, and the CGPM abolishes them by 2035 anyway.*
- **2026-06-11** — The balance invariant is exact and tolerance-free: numbers this system
  computes must agree to the penny, and a journal entry's lines will be required to sum to
  zero exactly. *Why: an epsilon between numbers the same system wrote is a bug amnesty,
  not a materiality judgment.*
- **2026-06-11** — Reconciliation tolerance is a domain rule, not an arithmetic one:
  thresholds are specced per account class, and an accepted discrepancy is posted as an
  explicit adjustment entry, never silently absorbed. *Why: our books vs an external
  statement is two bookkeepers legitimately disagreeing; accepting a difference must leave
  an audit trail, and the books stay exactly balanced either way.*
- **2026-06-11** — Ledger amounts are USD at two decimal places; sub-cent precision lives in
  price/quantity types in their own domains. *Why: the ledger records cash that moved, and
  cash moves in cents; brokerage precision is a property of positions, not of money.*
- **2026-06-11** — Rounding is half-up (away from zero), chosen by BD under Dan's delegation.
  *Why: GAAP mandates no mode; half-up matches IRS and FI statement convention, and banker's
  bias-correction only pays at aggregation volumes this system will never see.*
- **2026-06-11** — USD only, cash basis: foreign transactions enter the ledger as the USD
  amount the financial institution actually settled. *Why: cash-basis personal books record
  money that moved, not FX exposure; no revaluation, no multi-currency machinery.*
- **2026-06-19** — No negative-amount tests for `splitByN`. *Why: the underlying operations
  (decimal division, multiplication, subtraction, `MidpointRounding.AwayFromZero`) are all
  sign-symmetric; positive-amount tests prove the arithmetic, and negative amounts traverse
  identical code paths with no sign-dependent branching.*
- **2026-06-20* — Account UUIDs are internal-only; the UI boundary speaks codes exclusively. Parent references, deactivation, and updates all accept codes and resolve to IDs at the boundary. Why: a surrogate key only pays off if it stays hidden (architecture-decisions-2026-06-19.md §1); exposing it costs the UI an opaque token with no benefit.
- **2026-06-22** — Journal entry references split into two concepts: external references (an external transaction id + its source FI) and comments (free text, optionally linking a second entry). Deduplication of imported rows is the importer's concern, not the ledger's. *Why: prod LeoBloom's one `reference` string was overloaded across three roles — real external ids, synthetic composite dedup keys, and stringly-typed JE→JE pointers; separating them removes the dedup key from the ledger entirely and gives the JE→JE link a real foreign key.*
- **2026-06-22** — Reversal is not a distinct mechanism: a reversal is an ordinary offsetting entry plus a comment linking it to the original. *Why: prod LeoBloom never used a formal reversal — every closed-period-respecting correction was already an offsetting entry plus a note (no `reverses` reference type exists in the live ledger).*
- **2026-06-22** — Account `active_begin` / `active_end` are Calendar Dates (inclusive on both ends), not Instants. *Why: they are business boundaries compared only against day-granular entry dates and never observed at sub-day precision (same logic that retired leap seconds); making them dates removes all date↔instant conversion from posting validation and unifies them with the fiscal-period and entry-date types. The instant a (de)activation occurred remains captured by `modified_at`.*
