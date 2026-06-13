# Solution Conventions — Card Catalog

Code and naming conventions for the SonOfLeo solution. These are developer-facing rules
enforced by review, not behavioral requirements verified by tests — that's why they live
here and not in a Behavioral spec with REQ- IDs.

This README is the index. Read it, then open only the file your task touches — no agent
should need every convention in context for every prompt.

- [Naming](Naming.md) — smart-constructor naming: `create` vs `fromString`, and why they
  don't unify
- [Requirement traceability](Traceability.md) — requirement IDs, code annotations, test
  annotations, and audit expectations
- [Temporal values](Temporal.md) — NodaTime, instants and dates as separate algebras,
  `AuditEnvelope` for temporal coherence, and Postgres `timestamptz`/`date` discipline
- [Money](Money.md) — the Money type, exact decimal end to end, 2dp USD ledger amounts,
  half-up rounding, exact allocation, and arithmetic boundaries
- [Build & environment](BuildAndEnvironment.md) — environment separation, debug/release
  access rules, and building in the BD container without breaking Rider on the host
