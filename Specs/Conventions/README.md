# Solution Conventions — Card Catalog

Code and naming conventions for the SonOfLeo solution. These are developer-facing rules
enforced by review, not behavioral requirements verified by tests — that's why they live
here and not in a Behavioral spec with REQ- IDs.

This README is the index. Read it, then open only the file your task touches — no agent
should need every convention in context for every prompt.

- [Naming](naming.md) — smart-constructor naming: `create` vs `fromString`, and why they
  don't unify
- [Requirement traceability](traceability.md) — code annotations, test naming, and the
  one-direction linkage rule (destination → ID)
- [Temporal values](temporal.md) — NodaTime, instants only, the one injected clock, and
  Postgres `timestamptz` discipline
- [Money](money.md) — exact decimal end to end, 2dp USD ledger amounts, half-up rounding,
  exact allocation, no code-level tolerances
- [Build & environment](build.md) — building in the BD container without breaking Rider
  on the host
