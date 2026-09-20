---
name: SonOfLeo:ArchReviewer
description: >
  Architectural code review for SonOfLeo — the "What Would Dan Do" pass. Use after the
  existing CodeReviewer self-review, as the second gate before code is presented or merged.
  This skill checks domain ownership, layer discipline, type placement, authority hierarchy,
  compile-order soundness, naming, comment hygiene, return-type minimalism, scope discipline,
  and flow architecture — the architectural patterns Dan enforces in every review. Triggers on
  "arch review", "Dan review", "what would Dan say", "architectural review", "pipeline code
  review", "review this diff for architecture", or any code review step in the agentic
  pipeline (Layer 5.3.5). Also use when reviewing a commit or PR before presenting to Dan, or
  when the CodeReviewer self-review passes but you want a second opinion on placement and
  domain boundaries.
---

# SonOfLeo ArchReviewer

Dan reviews every line of code that enters this repo. This skill exists to catch the things
he catches — not syntax, not formatting, not whether it compiles (the CodeReviewer and
`Checks/` handle those). This catches architectural misplacement: code in the wrong domain,
types at the wrong altitude, responsibilities shipped across boundaries for convenience,
naming that obscures instead of clarifies, and scope creep beyond what was asked.

The patterns below were extracted from Dan's actual review history. They are ordered by how
frequently and how forcefully he enforces them.

## Prerequisites

Gate 0 must already be green before this skill runs. If it isn't, stop — architectural
review on code that doesn't compile is wasted motion.

- `bash Checks/run-all.sh` — all pass
- `dotnet build --artifacts-path /tmp/sonofleo-build` — clean

If you are running as a pipeline agent (Layer 5.3.5), the engine has already verified Gate 0.
If you are running interactively, verify it yourself or confirm the CodeReviewer already did.

## How to use this skill

Read the diff under review. For each changed or new file, walk through the checks below in
order. The checks are cumulative — a file that fails Check 1 will almost certainly also fail
Checks 2 and 5, but name each violation separately so the builder knows exactly what to fix.

**Reference material.** This skill tells you *what Dan looks for*. The detailed rules for
*how it should be* live in:

| For | Read |
|---|---|
| What right looks like — every paradigm with code examples | `Skills/ArchReviewer/references/paradigms.md` |
| Type taxonomy (entity/component/composite/primitive/contract) | `CompoundedLearnings/articles/architecture/type-taxonomy.md` |
| Type placement by compile tier | `CompoundedLearnings/articles/architecture/type-placement-by-compile-tier.md` |
| Why a function belongs in orchestration | `CompoundedLearnings/articles/architecture/orchestration-layer.md` |
| Naming conventions | `CompoundedLearnings/articles/coding/descriptive-naming.md` |
| Validation location | `CompoundedLearnings/articles/coding/validation-location.md` |
| What infrastructure already exists | `Src/README.md` |
| Behavioral requirements | `Specs/Behavioral/` |

Start with `references/paradigms.md` — it shows the canonical shape for every structural
pattern in the codebase (entity, CRUD, composite, orchestration, bridge, etc.). The other
references drill into specific decisions. Read the relevant reference when a check is
ambiguous. Don't guess — cite.

---

## Check 1 — Domain ownership

**The question:** Does every piece of code live in the domain that owns the responsibility,
or has something been shipped to another domain because the data happens to be there?

Dan's rule: **a domain records its own bookkeeping on its own table.** If classification
needs to know which rule matched which line, classification owns that diagnostic — it doesn't
piggyback a column onto a data-ingestion table. If cash flow needs to record which payment
agreement matched a staged entry, cash flow owns that linkage.

**How to check:**
1. For each new column, table, or type field in the diff: which domain is this table/type
   declared in? Which domain's *responsibility* is the data?
2. If the answer to those two questions is different domains, that's a finding.
3. For each function that writes to another domain's table: why? If it's writing bookkeeping
   that the other domain should own, that's a finding.

**What Dan says when this fails:**
> "the heart of the problem: cash flow is shipping its responsibility onto data ingestion
> out of convenience rather than an encapsulated responsibility"

**Severity:** This is the most common architectural error and the one Dan catches with the
most force. It indicates a structural problem, not a style preference.

---

## Check 2 — Layer discipline

**The question:** Is each type and function at the right altitude in the dependency chain?

The layers, bottom to top:
- **Model** — entity CRUD, domain primitives, component types. Single-domain, single-concern.
- **ModelOrchestrator** — composite types, cross-domain composition, orchestrated multi-step
  operations. See the five reasons in `orchestration-layer.md`.
- **InterfaceBridge** — boundary contracts, converters, routes. Highest error-interpretation
  authority.

**What goes wrong:**
- Simple types (value objects, enums, wrapper IDs) defined in an orchestration file.
  "You're defining basic bitch types inside of CashFlowOps. These should be in
  `CashFlowComponent.fs`."
- Composite types defined in Model. A type that assembles children from multiple tables
  belongs in orchestration, next to the function that builds it.
- Business logic in InterfaceBridge. The bridge converts and routes — it doesn't orchestrate.
- A function in Model that needs data from another domain module. That's orchestration
  (reason 1 from `orchestration-layer.md`).

**How to check:**
1. For each new type: classify it (domain primitive? entity? component? composite? contract?)
   using `type-taxonomy.md`. Is it in the right project?
2. For each new function: does it do more than one thing? Does it cross domain boundaries?
   If yes to either, it belongs in orchestration.
3. For types in orchestration files: is it a composite? If not, it probably shouldn't be
   there. A clean build proves nothing — the top of an orchestrator file sees everything,
   which is how basic types accumulate there.

**Exceptions:**
- Compile-order constraints sometimes force a type up a tier. When this happens,
  the type goes at the lowest tier that can see all its dependencies, not at the most
  convenient tier. Check `type-placement-by-compile-tier.md` for the decision process.
- `constructNewAndPersist` functions take child collections as **tuple lists of domain
  primitives**, not named input-record types. This is deliberate — it avoids a third type
  between the composite and the interface contract. A 5- or 9-element tuple of
  already-validated domain types in a construct function signature is the established
  pattern, not a smell. See `paradigms.md` §6.

---

## Check 3 — Authority hierarchy

**The question:** Does this code respect the authority chain?

```
Dan              ← highest
Hobson           ← second
Import script    ← deterministic, writes only what it KNOWS
Classifier       ← lowest, probabilistic
```

A value set by a higher authority is never re-evaluated or overridden by a lower authority.
If an import script sets an account code (because it knows with certainty), the classifier
does not get to re-classify that line. If Dan manually corrected a value, no automated
process touches it.

**How to check:**
1. Any fetch query that feeds a classification or matching process: does it exclude
   already-settled rows?
2. Any update path: can it overwrite a value that was set by a higher authority?
3. The exclusion must happen in the fetch filter (architectural), not in the result processor
   (behavioral) — settled rows should never enter the candidate set.

**What Dan says when this fails:**
> "dude. stop. listen. there is not a tie on a settled line. you are not allowed to run
> already settled lines through the classifier"

---

## Check 4 — Compile order and dependency DAG

**The question:** Does the diff introduce a circular dependency or violate the compile-order
DAG?

F# compiles files in the order they appear in the `.fsproj`. This is structural, not
optional. A type in file A cannot reference a type in file B if B compiles after A.

**How to check:**
1. For each new type reference across files: does the referencing file compile after the
   referenced file? (Check the `<Compile Include>` order in the `.fsproj`.)
2. For each new file: is it inserted at the correct position, not appended?
3. If a type can't be placed where it conceptually belongs because of compile order, is the
   dependency graph wrong? Compile-order problems are symptoms, not the disease.
4. If a pragmatic compromise is needed (type stays in a suboptimal location due to real
   constraints), define it at the top of the file and flag the compromise explicitly.

---

## Check 5 — Type placement

**The question:** Is each type in the right file within its correct layer?

This is more granular than Check 2 (which catches wrong-layer placement). This catches
wrong-file placement within the correct layer.

**Rules:**
- Domain primitives and component types go in the domain's `*Component.fs` file.
- Entity types get their own file: `Account.fs`, `MasterAgreement.fs`, etc.
- Composite types go next to the orchestration function that builds them. Not in a separate
  "types" file, not at the bottom of a random orchestration module.
- Interface contracts go in `InterfaceBridge`, never in `Model/`.
- If a component file can't see a dependency, check the tier below before concluding Model
  is closed. `ClassificationComponent.fs` exists for exactly this reason.

---

## Check 6 — Naming

**The question:** Is every name unambiguous, self-documenting, and consistent with
established conventions?

**Convention inventory:**
- CRUD operations: `persist` (not `insertNewToDb`), `fetch` / `fetchById` / `fetchByX` /
  `fetchAll` (not `readRowsFromDb`), `reconstitute` / `mapRawForDbRead`,
  `constructNewAndSaveToDb`, `updateXById`
- Confirmation checks: `confirmX` (not `validateX` — retired;
  `Checks/check-confirm-naming.sh` enforces this)
- Boundary converters: `convert [SourceType] to [TargetType]` with square brackets
- Table aliases in SQL: globally unique per table, so queries can be copy-pasted without
  collision

**What Dan catches:**
- Word play or double meanings: "instantiate" meaning both "create an OOP instance" and
  "create an Instance type" — use `createUpcomingInstances`
- Ambiguous short names: `daysOut` → `daysPastInvDateForDueDate`. Verbose-but-clear beats
  short-but-vague.
- Naked type names in converters: `convert [Id] to [X]` → `convert [PaymentAgreementId] to [X]`
- Inconsistent names for the same operation across domains

Read `CompoundedLearnings/articles/coding/descriptive-naming.md` for the full conventions.

---

## Check 7 — Comment discipline

**The question:** Does every comment earn its place, and are there comments that should exist
but don't?

Dan's bar: **would a reasonable reader, scanning cold, look at this code and suspect it's
wrong when it isn't?** That is the only thing that earns a comment.

**Reject on sight:**
1. Restatements of what the code says (`// fetch the account by id`)
2. Explanations of established conventions (`/// reconstitute constructs from primitives`)
3. F# language tutorials (`// required because AccountCode is a private string`)
4. External references — agent names, "the Saturday routine," personal process details
5. Anything that can go stale — descriptions of behavior that will change

**Missing comments (also a finding):**
- Code that looks wrong at first glance but is correct for a hidden domain reason — this
  *needs* a comment and its absence is a defect

See the Commenting section of `SonOfLeoSrcDeveloper/SKILL.md` for the full rationale and
the two canonical survivors.

---

## Check 8 — Return type minimalism

**The question:** Does each function return the minimum the caller needs?

**What Dan catches:**
- Elaborate composite return types when a tuple of already-defined types works
- Diagnostic chaff the operator won't look at ("here are all the lines we ignored")
- Data that's derivable from what's already returned, duplicated in the return shape
- Custom wrapper types for things that are already adequately represented

> "stop trying to glue them together. rule Id * classification results * decision log *
> open instances."

---

## Check 9 — Scope discipline

**The question:** Did this diff change only what was asked, and nothing else?

**Violations:**
- Renaming things tangentially related to the requested change
- Adding functionality that belongs to a different card or task
- Leaving vestigial code from a prior design sitting alongside the new design
- Modifying files outside the card's remit without a stated, necessary reason
- "Cleanup" edits that weren't requested

If something adjacent *should* change but wasn't asked for, the correct action is to note
it in the findings and stop — not to change it.

---

## Check 10 — Flow architecture

**The question:** For multi-step flows, was the skeleton visible before implementation?

If the diff introduces a new multi-step flow (multiple functions calling each other in
sequence, an orchestration function coordinating several operations), check:

1. Are stub signatures present for the full flow, or did implementation start before the
   shape was clear?
2. Are there vestigial stubs from a prior design mixed in with the new flow? Kill them.
3. Is the flow's structure readable from the orchestration function alone, or do you need to
   chase through implementation to understand the sequence?

> "the reason we're inside of a refactor that is, in itself inside of a refactor is because
> these workflow problems don't show up until I see how you're connecting the dots."

---

## Check 11 — Create/validate separation

**The question:** Do create functions stay infallible, with validation at the composite level?

`create` functions on entity and component types construct from primitives. They don't
validate cross-entity rules. Validation of composite invariants (balanced debits/credits,
minimum line counts, status transitions) happens in the orchestrator after reconstitution.

The update pattern is: persist changes → reconstitute the full composite → validate the
composite → if invalid, the orchestrator handles the error. The caller doesn't send the
full composite for pre-validation because it doesn't have it.

---

## Check 12 — Reinvention

**The question:** Does the diff reinvent something that already exists in `Src/README.md`?

This overlaps with the CodeReviewer's Pass 1, but architectural review catches subtler
reinvention: not just "you wrote a `NpgsqlCommand`" but "you wrote a validation function
that `confirmX` already handles" or "you built a lookup that `LookupCache` already provides."

Scan the infrastructure inventory in `Src/README.md` and check each new function against it.

---

## Check 13 — Once-only semantics

**The question:** Do classification, matching, and linkage flows exclude already-processed
rows?

If the diff touches a flow that processes staged entries, classifies transactions, or matches
payment agreements: does the candidate query exclude rows that have already been processed?
An already-linked line must not appear in the match candidates list. An already-classified
line must not re-enter the classifier.

This is related to Check 3 (authority hierarchy) but is specifically about the fetch filter:
settled rows are excluded architecturally, not filtered after retrieval.

---

## Output format

For each finding, report:

```
**[CHECK N — check name]** file:line
What's wrong: [one sentence]
What it should be: [one sentence]
```

**Verdicts:**
- **APPROVED** — no findings. The diff is architecturally sound.
- **CONDITIONAL** — findings exist but are resolvable without architectural rethinking.
  The builder can fix them in a revision pass.
- **REJECTED** — at least one finding indicates a structural problem (wrong domain
  ownership, wrong layer, authority violation) that requires rethinking the approach, not
  just moving code around.

When in doubt between CONDITIONAL and REJECTED: if the fix is "move this type/function to
file X," that's CONDITIONAL. If the fix is "this responsibility doesn't belong in this
domain and needs its own table/module," that's REJECTED.

**Conciseness matters.** Dan's own words: "write that again in 1/2 the words." State what's
wrong, where it is, and what the fix is. No essays. No hedging. No tangents about what might
theoretically be a problem.
