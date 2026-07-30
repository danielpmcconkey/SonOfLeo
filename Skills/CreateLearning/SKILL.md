---
name: create-learning
description: Record a new compounded learning or update an existing one in SonOfLeo's CompoundedLearnings system. Use whenever a session produces a reusable insight — an overruled audit finding, a settled design decision, a naming convention, a process rule, a domain concept that agents keep getting wrong. Also use when asked to "compound this," "remember this for next time," or "add this to the learnings."
---

# Create Learning

Record operational knowledge into the CompoundedLearnings system so future agents benefit from what this session discovered.

## Before you begin

Read `CompoundedLearnings/README.md` to understand the system's structure and authority model.

## Step 1 — Identify the learning

A learning is a reusable insight that will help a future agent do better work. It is NOT:
- A task or action item (those go in action-items or task tracking)
- A behavioral requirement (those go in Specs/Behavioral/)
- A one-time fact about the current session

Good learnings answer: "what should a future agent know before encountering this situation?"

## Step 2 — Find the right domain

| Domain | Use for... |
|---|---|
| audit-conduct | How to behave as an auditor — judgment standards, what not to flag |
| gaap-domain | Accounting concepts agents must understand |
| architecture | Settled structural decisions that should not be re-litigated |
| coding | F# idioms, naming, temporal handling, constructor discipline |
| testing | Test writing doctrine, fixture rules, coverage accounting |
| process | How audits run, how migrations get reviewed, traceability |

If the learning doesn't fit any domain, propose a new one to Dan before creating it.

## Step 3 — Check for existing articles

Read the domain's catalog at `CompoundedLearnings/catalogs/<domain>.md`. If an existing article covers the same concept, update it rather than creating a duplicate. A learning that refines or extends an existing one should be folded in with the new context added.

## Step 4 — Write the article

Create a new file at `CompoundedLearnings/articles/<domain>/<concept-slug>.md`.

Structure:

```markdown
# Concept Name

**Source:** Where this learning came from (audit ID, session date, Dan's directive, etc.)

One or two sentences stating the learning as a rule or principle.

## What works
- Concrete guidance on the right approach

## What doesn't
- Specific anti-patterns this learning corrects

## Example
A real instance from the codebase or a session where this learning applies.
The example should be specific enough that a future agent recognizes the pattern.
```

Keep articles atomic. If you're writing more than ~40 lines, you may be combining two concepts. Split them.

## Step 5 — Add the catalog entry

Add a row to `CompoundedLearnings/catalogs/<domain>.md`:

```
| Concept name | `articles/<domain>/<slug>.md` | When to read this — be specific about the trigger |
```

The "when to read" column is the most important field. It should describe the moment during a task when this learning becomes relevant. Not "when working with accounts" but "when writing a query that filters by voided_at" or "when evaluating whether a spec term is ambiguous."

## Step 6 — Verify

- The article file exists at the path the catalog points to
- The catalog entry's "when to read" trigger is specific enough to fire at the right moment
- The article doesn't duplicate an existing one
- The article doesn't contradict a behavioral spec (if it appears to, the article is wrong)

## Step 7 — Guardrail triage

A learning that corrects a *violation* (not just records knowledge) should also strengthen
the enforcement layer. Route it:

1. **Mechanically detectable?** → propose a new `Checks/check-*.sh` script (grep-grade,
   ≤30 lines, header citing the pattern ID). The article stays as the why; the script
   becomes the gate.
2. **Judgment call that recurs?** → the article you just wrote, plus one checklist line in
   `Skills/CodeReviewer/SKILL.md` or `Skills/TestWriter/SKILL.md` citing it.
3. **House-style question?** → raise it with Dan for disposition first. If he blesses it
   and it names a piece of infrastructure or a silent convention, it belongs in
   `Src/README.md`; then apply 1 or 2 as appropriate.

If none apply (pure domain knowledge, audit conduct), stop at Step 6.
