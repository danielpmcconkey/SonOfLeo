# Audit Conduct

How to behave as an auditor of SonOfLeo specs, code, and tests. Read this catalog before any audit task.

| Concept | Article | Read when... |
|---|---|---|
| Reasonable-person standard | `articles/audit-conduct/reasonable-person-standard.md` | Evaluating whether a spec ambiguity is a real finding or a pathological reading |
| Domain terminology is precise | `articles/audit-conduct/domain-terminology-is-precise.md` | Tempted to flag a GAAP term (balance, posting, void, close, debit, credit) as ambiguous |
| Specs define the what not the how | `articles/audit-conduct/specs-define-what-not-how.md` | A finding questions whether a spec should prescribe the detection heuristic or implementation approach |
| Verify before claiming missing | `articles/audit-conduct/verify-before-claiming-missing.md` | About to report that a requirement's enforcement or verification is missing from the codebase |
| Don't assume implementations | `articles/audit-conduct/dont-assume-implementations.md` | A finding depends on how code is implemented rather than what the spec says |
| Stay within the statement of position | `articles/audit-conduct/stay-within-statement-of-position.md` | Evaluating capabilities against future plans or domains that don't exist yet |
| Check the schema before questioning waivers | `articles/audit-conduct/check-schema-before-questioning-waivers.md` | A waiver's soundness seems questionable based on the spec prose alone |
| Not every rule has a REQ ID | `articles/audit-conduct/conventions-without-reqs.md` | A learning or operational rule describes a testable behavior but has no REQ ID in any behavioral spec |
| Requirements may be stricter than learnings | `articles/audit-conduct/requirements-stricter-than-conventions.md` | A behavioral requirement appears to narrow or contradict a general principle stated in a learning |
| Entity identification by primary key is obvious | `articles/audit-conduct/entity-identification-by-pk.md` | A finding questions how a target entity is identified for an update or delete operation |
| Don't prescribe structure for external data | `articles/audit-conduct/dont-question-intentional-type-choices.md` | A finding suggests more structure or a domain type for a field that holds data from external systems (FI references, merchant strings, external IDs) |
| Schema-guaranteed values are not partial | `articles/audit-conduct/schema-guaranteed-values.md` | About to flag Option.get or similar partial operations on values sourced from NOT NULL columns |
