# Resolved Audit Findings

Prior rulings from requirements audits. Agents must read this before flagging findings.
If a finding matches a resolved entry's scope, skip it — Dan already ruled on it.

## How to read this file

- **overruled**: Dan reviewed and explicitly rejected the finding. Do not re-flag.
- **deferred**: Dan acknowledged the gap but chose not to act yet. Do not re-flag unless
  the "revisit when" trigger has been met.

---

## CV-2: Money.fromDecimal Rounding Mode
- **Status:** overruled
- **Date:** 2026-06-13
- **Scope:** Money.fromDecimal using Math.Round for precision validation
- **Ruling:** The rounding in fromDecimal is a precision gate (reject values with more than 2dp), not an arithmetic rounding operation. The raw value is used when constructing the Money record. The explicit MidpointRounding.AwayFromZero was added to silence future auditors, but the behavioral concern was never real.

## CV-4: Money.fromDecimal Naming
- **Status:** overruled
- **Date:** 2026-06-13
- **Scope:** Money.fromDecimal naming vs create convention
- **Ruling:** fromDecimal is intentional — it's the clearest name for the caller. The naming convention's spirit is "don't confuse your reader," and this name doesn't. The private `create` already exists and does the wrapping. Adversarial validator confirmed this is correct.

## AMB-4: REQ-DAL-2.1 vs REQ-DAL-2.3 Overlapping Scope
- **Status:** overruled
- **Date:** 2026-06-13
- **Scope:** Whether DAL-2.1 and DAL-2.3 should be consolidated
- **Ruling:** These are two separate concepts. DAL-2.1 covers data inserted into the database (parameterized values). DAL-2.3 covers user-originated input specifically. The distinction exists to avoid requiring parameterization of structural query elements like LIMIT clauses in the flexible multipurpose read pattern (Account.fs readRowsFromDb).

## AMB-5: REQ-DAL-2.2 Missing Failure Behavior
- **Status:** overruled
- **Date:** 2026-06-13
- **Scope:** Whether REQ-DAL-2.2 needs explicit failure mode specification
- **Ruling:** "Verify" is self-explanatory in this codebase. Dozens of requirements say "must validate" without spelling out the failure mode. Not setting a precedent that forces a useless sentence on every validation requirement.

## AMB-6: REQ-SYS-5.1 "Perfectly Reconstituted" Overpromises
- **Status:** overruled
- **Date:** 2026-06-13
- **Scope:** Whether "perfectly reconstituted" is too strong given precision constraints
- **Ruling:** Any application of the system that writes a value to the database must do so such that any subsequent read returns a byte-perfect entity. The temporal precision and money 2dp constraints are deliberately planned for and coded against — they aren't edge cases, they're the design. "Perfectly" is accurate.

## AMB-11: REQ-DAL-3.2.1 Escape Valve Unbounded
- **Status:** overruled
- **Date:** 2026-06-13
- **Scope:** Whether the non-ANSI SQL exception needs tighter guidance
- **Ruling:** The system architecture requires client modules to pass query strings and parameters into DAL functions. They must have the discretion to determine when Postgres-specific SQL is appropriate. No useful wording exists to constrain this further.

## AMB-13: Money Multiplication Prohibition Boundary
- **Status:** overruled
- **Date:** 2026-06-13
- **Scope:** Whether Money.md needs to clarify operator vs behavioral prohibition
- **Ruling:** The code already doesn't define * and / operators on Money. The convention says "can't do it." There's nothing more to do here.

## IE-1: Temporal.md Missing Application-Layer Date Type
- **Status:** deferred
- **Date:** 2026-06-13
- **Revisit when:** Calendar date values are needed in the application layer (likely when fiscal periods or journal entry dates arrive)
- **Ruling:** No calendar date needs exist in the system today. Not deliberating storage and app-layer representation for a value we neither store nor represent yet.

## IE-3: Temporal.md Missing US Eastern Anchoring Rule
- **Status:** overruled
- **Date:** 2026-06-13
- **Scope:** Whether US Eastern anchoring belongs in Temporal.md
- **Ruling:** The anchoring assumption applies to importers, at their creation time, by their creators. It's not a system-wide temporal convention. The Decisions.md entry was deleted — it was context from a conversation, not a standing rule.

## IE-4: Equity Subtypes Not Future-Proofed
- **Status:** deferred
- **Date:** 2026-06-13
- **Revisit when:** Period closure is designed
- **Ruling:** The subtype isn't the only or obvious way to identify retained earnings. Could use code, name, or a flag. Speculating on the mechanism before knowing what period closure needs just cements a guess.

## DEC-1: Convention "Must" vs Requirement "Must"
- **Status:** deferred
- **Date:** 2026-06-13
- **Revisit when:** Next spec refactoring conversation (affects MR-3, MR-5, MR-6, MR-7)
- **Ruling:** The question is valid and the traceability gap is acknowledged. But the answer requires a broader conversation about how convention docs relate to behavioral specs. Punting until that conversation happens.

## IE-2: REQ-DAL-3.6 Mixes Requirement and DBA Advisory
- **Status:** deferred
- **Date:** 2026-06-13
- **Revisit when:** Next spec refactoring conversation
- **Ruling:** There's a meta conversation needed about where the line is between requirement and advisory. Not enough energy for it today.

## MR-3: Money Rounding and Allocation Rules Have No REQ- IDs
- **Status:** deferred
- **Date:** 2026-06-13
- **Revisit when:** DEC-1 is resolved
- **Ruling:** Blocked on the convention-must vs requirement-must meta-decision.

## SS-3: SystemWide.md todo Comment
- **Status:** overruled
- **Date:** 2026-06-13
- **Scope:** Whether the bare `todo` on SystemWide.md line 27 should become a REQ- ID
- **Ruling:** Dan uses Rider's todo function. The comment stays as-is.

## DEC-3: REQ-AC-1.39 Self-Parent Enforcement
- **Status:** overruled
- **Date:** 2026-06-13
- **Scope:** Whether an explicit self-parent check is needed beyond UUID generation
- **Ruling:** Dan added the explicit check anyway (as a middle finger to the auditors), but the probabilistic argument was never a real concern. The finding was horseshit at medium severity.
