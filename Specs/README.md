# The SonOfLeo Documentation System

`Specs/` is the system of record for **what the system must do**. It exists for four jobs:
preserving the "why" behind non-obvious decisions, pinning terms whose meaning changes which
requirements apply, feeding auditor agents something objective to audit, and driving a test
suite where every requirement is either verified or explicitly waived.

## The rule that governs this whole corpus

**Prefer the executable form of a rule. Where a rule cannot be executable, write it once, as
close as possible to the thing it governs.**

Everything that rotted in this repo was the material nothing executes. A check that goes
stale fails; a paragraph that goes stale just sits there being wrong. Before writing a
document, ask whether it could be a `Checks/` script instead. Before adding a rule to an
existing document, ask whether it is already stated somewhere closer to the code.

Corollaries, learned the hard way:

- **One word, one meaning.** This codebase has overloaded *context*, *transaction*,
  *create*, *check* and *fixture*. A human reader who hits an ambiguity stops and asks; an
  LLM resolves it to the statistically likely meaning and proceeds confidently. Pick the
  word and keep it.
- **Warnings before the step, never after.** An agent reads forward and commits.
- **More documentation is not the cure for bad documentation.** In July 2026 this repo held
  more lines of markdown than lines of F#.

## Where things live

| Kind | Home | Enforced by |
|---|---|---|
| **Requirements** — testable statements about behavior, with stable IDs | `Specs/Behavioral/` | Tests + the audit skill |
| **Definitions** — terms whose meaning changes which requirements apply | `Specs/Definitions.md` | Citation; every spec uses these terms as defined |
| **Infrastructure inventory and silent conventions** | `Src/README.md` | Code review |
| **Test standard** | `Tests/README.md`, plus a README per test directory | Code review |
| **Judgment and interpretation** | `CompoundedLearnings/` | Nothing; it is guidance |
| **Mechanical rules** | `Checks/*.sh` | Themselves — that is the point |
| **Procedure** | `Skills/` | Whoever runs the skill |
| **History** | `Specs/Archive/`, `Audit/`, `Skills/SonOfLeoRequirementsAudit/Runs/`, `HobsonsNotes/`, `BdsNotes/` | Nothing. Never read as authority. |

Authority runs `Specs/Behavioral/` > everything else. A learning, a skill or a README that
contradicts a requirement is wrong and gets fixed.

## Requirement ID grammar

`REQ-<DOMAIN>-<section>.<n>` — e.g., `REQ-AC-1.5`. The domain is an all-caps identifier of
no more than five characters: `AC` (Account), `JE` (Journal Entry), `DAL` (data access),
`MON` (Money), `SYS` (system-wide), more as entities arrive.

Numbers increment like software versions — the one after `2.9` is `2.10`, not `3.0`. A
sub-dot number applies only within the context of its parent:

- **REQ-AC-1.48** An Account is "deactivated" when its active-end date is non-null and
  earlier than a given reference date.
- **REQ-AC-1.48.1** The reference point is context-dependent — the system clock, or a date
  specific to the operation.

**IDs are permanent.** Never renumbered, never reused. A dead requirement moves to its
document's **Withdrawn** table with a reason — it does not vanish. Gaps in numbering are
normal and meaningless.

## Requirement anatomy

```markdown
- **REQ-AC-1.5** Account code is case sensitive. "ACCT-100" and "acct-100" are distinct.
  - *Why:* <rationale, dated> (2026-06-11)
```

The *Why* line is optional and reserved for requirements where a reasonable reviewer would
ask "wait, why?" — annotating the obvious is noise. Withdrawn-table rows carry their why in
the Reason column.

A requirement that cannot or should not be verified by a test is in exactly one of two
non-tested states:

- **Waived from testing** — something enforces it (the type system, schema constraints,
  construction patterns, code review, or periodic audit), but a test either cannot or need
  not verify it. Waiver table: ID, reason, Dan's approval date.
- **Unenforceable** — nothing in the system enforces it; it binds humans, not code. These
  are requirements that state policy, convention, or responsibility assignments that the
  system cannot mechanically verify. Unenforceable table: ID, why it cannot be enforced,
  Dan's approval date.

Every active requirement is therefore in exactly one of three states: **tested**, **waived**,
or **unenforceable**.

## Linkage rules (the star chart)

The map never lists coordinates. Spec documents **never** name source files, functions, or
tests. All linkage lives at the destination, and the destination is the test suite:

- **Test names begin with the requirement IDs they verify.** This is the only linkage there
  is.
- **Source code and migrations carry no REQ annotations.** Retired 2026-07-31 — a comment
  cannot be verified, and "every site, not just the first" was never checkable. Rationale
  comments explaining *why* code is shaped a certain way may still cite a requirement; it is
  the traceability tag that is gone, not the ability to name a rule. See
  `CompoundedLearnings/articles/architecture/no-req-annotations-in-source.md`. Settled; do
  not re-litigate.
- The requirement→test map is generated by tooling
  (`Skills/SonOfLeoRequirementsAudit/traceability-audit.sh`), never hand-maintained.

To answer "how does X work": find the requirement for X here, grep `Tests/` for its ID, and
read what the test exercises.

**Exclusion:** `BdsNotes/` is an archaeological record, never scanned and never updated. It
still contains the pre-2026-06-11 `FT-` prefix by design.
