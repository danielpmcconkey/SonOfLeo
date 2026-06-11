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
