---
name: SonOfLeo:SrcDeveloper
description: >
  This skill should be used when writing or modifying F# source under SonOfLeo's Src/
  directory — new entity CRUD, new domain primitives, AppError cases, ModelOrchestrator
  functions (constructNewAndSaveToDb, composite create/read, orchestrated updates),
  InterfaceBridge work (interface contracts, boundary converters, use case routes), a DbMigration
  script that a Src change depends on, or any Src implementation task Dan hands off — including
  reviewing a commit before fixing what it broke. Covers the entity/component/composite type
  taxonomy, the Model/ CRUD function shape (insertNewToDb / reconstitute / mapRawForDbRead /
  readRowsFromDb / fetchGenericRead / fetchById / updateDb), the ModelOrchestrator function
  shape and the five reasons a function belongs there, what InterfaceBridge is for and how its
  contracts and converters are named, AppError and FieldUpdate conventions, the mechanical checks
  in Checks/, and the working relationship with Dan. Triggers on "build out X's CRUD", "write the
  Src for", "add a domain type", "finish insertNewToDb", "build the orchestrator for", "write an
  orchestration function", "fix the interface bridge", "add a route for", "wire up the contracts",
  or any task that edits a `.fs` file under `Src/`.
---

# SonOfLeo SrcDeveloper

Dan writes the spec-level thinking and the hard queries. This skill is for the mortar: the
CRUD, the value objects, the error cases, the plumbing that follows an established shape once
the shape is known. `Src/` was Dan's alone until this skill existed — `README.md`'s ownership
table still shows it that way, and BD (the test-writing agent) is refused edits to `.fs` under
`Src/` by design. This skill does not change that boundary; it operates inside it, on Dan's
explicit invitation, one task at a time.

## The operating rules

- **Never `git commit`, never `git push`, never stage anything.** Dan reviews and commits
  everything himself. Hand back a diff, not a commit.
- **One small, scoped task at a time.** Don't sweep, don't refactor beyond what was asked,
  don't chain unrequested follow-on work. When a task naturally reveals the next one (a CRUD
  function that will obviously need a sibling), say so and stop — don't just keep going.
- **No `.md` or agent files in this repo**, except a skill file Dan explicitly asks for (this
  file is that exception, not a precedent for writing docs unprompted).
- **Build after every change.** There is no REPL feedback loop here — `dotnet build` on the
  touched project, plus at least one downstream consumer project (`ModelOrchestrator`,
  `Tests.Isolated`, `Tests.Integrated`) when the change touches a widely-shared file like
  `AppError.fs`, is the only verification available before handing back.
- **Surface a blocker instead of guessing past it.** If finishing a task needs a change
  outside the file you were pointed at (a missing accessor, a missing `AppError` case, a type
  that can't represent what the DB row needs), stop and say exactly what's missing and why,
  propose the minimal fix, and wait — unless it falls under a standing permission below.
- **Comment only what would otherwise get flagged as a bug.** The test isn't "is this
  non-obvious" — it's "would a reasonable reader, or an auditor agent with no session context,
  look at this code cold and suspect it's wrong." `Payment.transactionPointerFromColumns`'s
  `Some journalEntryHeaderUuid, _ ->` arm ignores whether `stageEntryHeaderUuid` is also set —
  that looks like a missed validation case unless you know a posted payment is *supposed* to
  carry both ids as its normal terminal state, not a corrupted one. That gets a comment
  explaining the lifecycle. Most other rationale (a design tradeoff, why one approach was
  chosen over another) belongs in the hand-off message, not the file — Dan reads that for the
  "why," and it doesn't need to live in both places. Never restate what a type definition's own
  comment already says (e.g. `Payment.amount`'s `// not separately tracked in the database`
  covers that field everywhere it's used).

## Standing permissions (act, then report — don't ask first)

- **Adding `AppError` cases.** Use the existing naming convention (see below) and always
  tell Dan what was added and why, in the same message as the rest of the work. Don't ask
  permission first — this specific type of small addition is pre-approved.
- **Matching an established structural convention** even where the immediate task didn't spell
  it out — e.g. making a bare public record `private` with accessors + `create` when adding
  CRUD to it, because every other entity type in `Model/` already works that way. Flag the
  choice in the hand-off; don't silently deviate from it.

## Everything else: propose, don't decide

Anything that changes a type's shape across files it doesn't own, collapses or introduces an
abstraction (like retiring the `Flow` bundle type), or resolves a genuine architectural
ambiguity (how should `Obligation`-style composition work, should a field be updatable) is
Dan's call. Lay out the options and the tradeoff in a sentence or two; don't pick for him.

## Type taxonomy — decide what you're building before you write it

(`CompoundedLearnings/articles/architecture/type-taxonomy.md`)

- **Domain primitive** — single-value validated wrapper or fixed-case enum DU. `AccountId`,
  `AgreementName`, `Cadence`, `WeekDay`. Constructed via `create` (wraps input) or `fromString`
  (parses one of a fixed set of case labels) — never unify the two names.
- **Entity type** — private record + same-named companion module (accessors, `create`,
  persistence). Independently persisted. `MasterAgreement`, `PaymentAgreement`,
  `ClassificationRule`.
- **Component type** — a part of a future or existing composite, shaped exactly like an entity
  type, but meant to be assembled with siblings above the `Model/` layer. `StageEntryHeader`,
  `StageEntryLine`, `StageEntryStatusTransition` compose into `StageEntry` in
  `ModelOrchestrator`. This is why `Flow.paymentAgreements` came out of `MasterAgreement` —
  a single-table `reconstitute` cannot honestly populate a child list, so that composition
  belongs in an orchestrator-level type (`Obligation`, mirroring `StageEntry`), not embedded in
  the entity.
- **Composite type** — multi-part, built and validated at the orchestrator (`JournalEntry`,
  `StageEntry`). Collection-level rules (≥2 lines, debits = credits) live there, not in any one
  component.
- **Interface contract** — DTOs at the CLI boundary, owned by `InterfaceBridge`. Not a `Model/`
  concern.

## The Component-file convention

Every domain slice with more than one sibling entity gets exactly one `*Component.fs` file
holding every shared ID, enum, and bounded-string value object for the whole slice —
`AccountComponent.fs`, `JournalEntryComponent.fs`, `StageEntryComponent.fs`,
`CashFlowComponent.fs`. It is a flat file, not sub-organized by category, though related types
(all the `*Memo` types, a `Cadence`-and-its-parts cluster) should sit contiguously. Don't split
it further preemptively — `AccountComponent.fs` exists because `Account.fs` itself was getting
huge, not because the component file was. Splitting is worth reconsidering only once a
component file roughly doubles from a healthy size (~150–350 lines is normal for 1–5 sibling
entities) or starts accumulating real logic unrelated to value-object construction.

**In a file that spans multiple domains, prefer not opening a component module at all — fully
qualify every reference.** `open` resolves a bare case name to whichever module was opened
*last* — silently, with no compiler warning — so two unrelated DUs that happen to share a case
name (`Posted`, `Staged`, `Active`) will shadow each other, and the resulting type error won't
mention the DU you actually meant. The fix isn't "open it and remember which names collide" —
it's "don't open it." Rider flags unnecessary qualifiers as a warning; that warning is
*signal*, not noise — it tells you the module is open and collision is possible. Qualify
everything (`CashFlowComponent.MasterAgreementId`, `CashFlowComponent.Posted`,
`CashFlowComponent.DebitAccount`) and the collision class disappears entirely. Full writeup:
`CompoundedLearnings/articles/coding/du-case-collision-across-opens.md`.

## Entity shape

```fsharp
type Foo = private {
    fooId: FooId
    // ...
}

let fooId f = f.fooId
// one pipe-friendly accessor per field
let create (fooId: FooId) (...) : Foo = { fooId = fooId; ... }
```

Private by default. A type whose analogs elsewhere in `Model/` are private must be private too,
unless a documented, Dan-approved rationale sits at the definition site
(`Src/README.md`, "Two conventions the code follows silently"). If you're adding accessors to
a type that's still a bare public record, make it private as part of that change — check first
that nothing else in the repo constructs it via record-literal syntax (`grep` for
`TypeName = {` / `{ fieldName =`); if something does, flag it before changing privacy rather
than breaking callers silently.

## CRUD function shape

One file per entity, mirroring `StageEntryHeader.fs`'s shape (the most complete reference —
also read `ClassificationRule.fs` for a simpler single-table example without a
status-transition side table):

1. **`insertNewToDb (context) (entity) : Result<unit, AppError>`** — parameter order is
   context first, subject last (`Src/README.md`), so it pipelines:
   `entity |> insertNewToDb context` at the call site, `entity` as the last positional param
   here. Builds the parameterized insert, never string-interpolates a *value* into SQL
   (structural fragments — table/column names, `SET` clauses — are fine; values are always
   `@param`s via `DataAccessLayer.QueryParameters`).
2. **`reconstitute raw`** (private) — raw tuple → `Result<Entity, AppError>`, calling each
   component's `create`/`fromString`. Runs inside an open DB reader: **no DB calls from
   here** — that's a validation-layering rule, not a style preference
   (`CompoundedLearnings/articles/coding/validation-layers.md`). This is exactly why a
   composite's child list can't be reconstituted alongside its parent in one query.
3. **`mapRawForDbRead (row: RowReader)`** (private) — one `RowReader.getX "column"` line per
   column, tupled, in the same order `reconstitute` destructures them.
4. **`readRowsFromDb`** (public) — the generic query executor: `cteList`/`select`/`joinList`/
   `predicate`/`limit`/`groupBy`/`orderBy`/`parameters`/`expectedRows`, fixed `from`, wired to
   `mapRawForDbRead` and `reconstitute` via `executeReaderQuery`. Keep the full parameter list
   even when nothing today needs CTEs/joins — it's what every sibling fetch function (and any
   future one) calls into.
5. **`fetchGenericRead`** (private) — fixes the `select` column list (table-aliased, e.g.
   `ma.unique_id, ...`), calls `readRowsFromDb` with `None` for whatever this slice doesn't
   need yet. If a field is marked "not separately tracked in the database" (a scalar derived
   from another domain's tables — see `Payment.amount`), it's fine for `fetchGenericRead` to
   bake in the `joinList` that computes it, even when that means joining across schemas — that's
   still one read, not orchestration. See "A read-only join across domains is not, by itself,
   orchestration" in `CompoundedLearnings/articles/architecture/orchestration-layer.md`.
   `reconstitute` then just reads the already-computed column like any other. This is different
   from a *child list*, which genuinely can't be reconstituted honestly from one query — see
   Component type below.
6. **`fetchById`** (public) — the one predicate every entity gets for free:
   `alias.unique_id = @unique_id`, `ExactlyOne`, `|> Result.map List.head`. Add more
   `fetchByX` functions the same way as the task calls for them.

   **`fetchByXIdList`** — the bulk-fetch-by-parent-id variant, needed by the composite read
   pattern (never N+1). Shape: empty-list guard up front (`if ids |> List.isEmpty then
   Error ...`) because `in ()` is invalid SQL; ordinal-numbered params (`@xId1, @xId2, ...`)
   zipped against the list, joined into an `in (...)` predicate; built on top of the entity's
   own `fetchGenericRead`, same as `fetchById`. The empty-list `AppError` case is keyed to
   the *id type being listed*, not the target table — e.g.
   `CashflowMasterAgreementIdListCannotBeEmpty` is shared by both
   `PaymentAgreement.fetchByMasterAgreementIdList` and
   `Instance.fetchByMasterAgreementIdList`, since both are listing the same parent id type.
   Precedent: `StageEntryLine.fetchByHeaderIdList`, `Instance.fetchByMasterAgreementIdList`.
7. **`updateDb`** (public) — takes an `<Entity>FieldUpdates` record (see FieldUpdate below),
   builds one `(setClause, QueryParameter) option` per field via
   `FieldUpdate.mapNoChangeToOptionWithConversion`, and flattens with `List.choose id`.
   No-op guard: if the flattened list is empty, `Error(Cashflow<Entity>UpdateNoOp)`
   (see AppError below) before touching the DB. On success, write then re-fetch via
   `fetchById` and return the current row — never trust the caller's view of what changed.
   Only include fields in `<Entity>FieldUpdates` that should actually be settable after
   creation; an entity's own identity/FK-to-parent field is typically excluded the same way
   `headerIdToUpdate`/`agreementIDToUpdate` themselves aren't update targets.

   **When a value spans multiple columns, decide deliberately whether `<Entity>FieldUpdates`
   bundles it as one `FieldUpdate<TheWholeThing>` or splits it into independent
   `FieldUpdate<...>` fields per sub-part** — the read shape and the update shape don't have to
   match (`Payment.transactionPointer` is one field for reading, but
   `journalEntryHeaderIdUpdate`/`stageEntryHeaderIdUpdate` are two independent update targets).
   Split when the sub-parts get set or cleared at different times for different reasons in
   actual use (`Payment`'s two ids; `Invoice`'s `invoiceState`/`paymentState`/`postedState`/
   `blocker`). Keep it bundled when the sub-parts are only meaningful together (`Cadence`;
   `Invoice.Blocker`'s state+note pair). Full writeup and more examples:
   `CompoundedLearnings/articles/coding/field-update-pattern.md`.

   **A field that writes several columns shouldn't reshape the fields that write one.** Give it
   a converter that takes the `FieldUpdate` itself, so the `NoChange` case lives next to the
   column logic rather than in a `match` above the list. Two or three columns: return a tuple of
   `(setClause, QueryParameter) option`s, destructure above the list, and drop the pieces in
   beside the single-column entries — `ClassificationOrchestration.classificationClaimantToJointUpdates`
   is the reference. More than that: return a `(setClause, QueryParameter) list`, empty on
   `NoChange`, and `List.append` it — `Cadence`'s five columns are the case that earns this.
   Wrapping every single-column conversion in a list so one `List.collect` can flatten them all
   makes four fields pay for one; Dan rejected that shape on sight.

**Encode/decode symmetry for a DU that spans several columns.** When one field decomposes into
multiple DB columns (`Cadence` → `cadence`, `cadence_week_day`, `cadence_date_in_month`,
`cadence_week_in_month`, `cadence_month`), write the encode direction
(`cadenceToColumns`) and decode direction (`cadenceFromColumns`) as private module-level
functions — not inline in `insertNewToDb`, and not duplicated between `insertNewToDb` and
`updateDb`. Both directions belong in the entity's own file unless/until the same shape needs
reuse from a second file. A case with no natural column value (`MonthDay.Last` needs none of
the three `MonthDay` columns) should decode unambiguously from "all relevant columns null" —
if two DU cases would produce the same all-null combination, that's a sign the schema is
underspecified, not a case to paper over.

**When the columns are mutually exclusive, every write touches all of them.** A DU whose cases
map to different nullable columns (`ClassificationClaimant` → `account_at_match` /
`payment_agreement_at_match`) has to null the column it moved off of, or a row ends up with both
set and `reconstitute` rejects it on the next read. Encode the whole set every time —
`NullableUniqueId None` for the ones this case doesn't use — rather than emitting only the
column being set.

## Table aliases

Dan wants every table to own one unique alias, usable in any query anywhere in the codebase
without risk of collision when copy-pasting across `from`/`join` clauses — not just unique
within one file. Before picking one for a new entity, check this list and
`grep -rn 'let from = "' Src` for anything added since:

| Table | Alias |
|---|---|
| `cashflow.master_agreement` | `ma` |
| `cashflow.payment_agreement` | `pa` |
| `cashflow.instance` | `ins` |
| `cashflow.invoice` | `inv` |
| `cashflow.payment` | `pmt` |
| `ingestion.classification_rule` | `cr` |
| `ingestion.source` | `src` |
| `ingestion.staged_entry` | `se` |
| `ingestion.staged_entry_line` | `sel` |
| `ingestion.staged_entry_audit` | `sea` |
| `ledger.account` | `a` |
| `ledger.fiscal_period` | `fp` |
| `ledger.journal_entry` | `je` |
| `ledger.journal_entry_line` | `jel` |
| `ledger.journal_entry_comment` | `jec` |
| `ledger.journal_entry_ext_reference` | `jer` |

Pick something short, mnemonic, and not an SQL reserved word (`in`, `on`, `as` — avoid these
even though most are technically usable quoted; it isn't worth the risk). Add the new row to
this table in the same change that adds the entity file.

## AppError — the one error DU

`Src/Utilities/AppError.fs` is the only place error strings live
(`AppError.toMessage`) — never build an error string anywhere else, and never touch
`toMessage`'s match with a wildcard arm (`Checks/check-tomessage-wildcard.sh` enforces this;
its exhaustiveness is the compile-time guarantee every case has a message). `TestingError`
exists for test plumbing only and is banned in `Src/` (`Checks/check-testingerror.sh`).

Adding a case (standing permission, see above):

- Name it `<DomainPrefix><Concept><Failure>` — `CashflowInvalidCadenceRow`,
  `CashflowMasterAgreementUpdateNoOp`, `AccountUpdateNoOp`. The no-op-guard convention across
  every entity's `updateDb` is `<DomainPrefix><Entity>UpdateNoOp`, no payload, message
  `"Updating the <Entity> record failed because at least one updatable parameter must be
  set."`
- Slot it alphabetically within its domain-prefix group in both the case list and the
  `toMessage` match — the file is organized as blocks of one prefix each, blank-line
  separated.
- Write the `toMessage` arm in the same style as its neighbors — usually
  `$"<Concept> cannot be empty. Provided ... is {x}."` /
  `$"<Concept> cannot exceed {max} characters. ..."` for bounded strings, `$"Invalid
  <Concept> of \"{x}\"."` for parse failures.
- Report what you added in the hand-off, every time — this is the one rule that has no
  exception, per Dan's explicit instruction.

**AppError instantiation style: bind first, construct second.** Never inline pipeline chains
as constructor arguments. Extract every derived value into a named `let` binding, then pass
plain names to the `Error(...)` call. The constructor should read as a list of names, not a
list of computations.

```fsharp
// correct — bind first, construct second
let uuid = instance |> Instance.instanceId |> InstanceId.value
let agreementUuid = agreementId |> MasterAgreementId.value
Error (CashflowInstanceNotUnderMasterAgreement(uuid, agreementUuid))

// wrong — inlined derivation as constructor arguments
Error (CashflowInstanceNotUnderMasterAgreement(
    instance |> Instance.instanceId |> InstanceId.value,
    agreementId |> MasterAgreementId.value))
```

The inlined form may be more idiomatic F#, but Dan finds it ugly to read — and readability at
the error site is the priority, not concision.

## Infrastructure inventory — read before you write a helper

`Src/README.md` is the authoritative "does this already exist" table. The highlights that are
easy to violate by accident:

- `Utilities.FieldUpdate` — `NoChange | SetTo`. There is no `Clear` case — nullability lives in
  the type parameter, so `SetTo None` clears a nullable field and `SetTo someValue` sets it.
  Every entity `updateDb` uses this, even for entities with no nullable fields — it's about
  explicit caller intent, not just null-disambiguation. Never write update plumbing by hand,
  never use a bare `option` to mean "don't touch this field."
- `Utilities.ResultHelper` — `result { }`, `convertListOfResultsToResultsList`,
  `convertOptionToDesiredTypeWithFallibleConverter` (the one for turning a raw-tuple `'a
  option` into a `Result<'b option, AppError>` via a fallible converter — used constantly in
  `reconstitute` for optional memo/name fields). Never hand-roll a fold over `Result` values.
- `Utilities.Clock` / `Utilities.Calendar` — the only source of "now"/"today".
  `DateTime.Now`, `DateTimeOffset.UtcNow`, `SystemClock` anywhere outside those two files is a
  hard failure (`Checks/check-clock.sh`).
- `DataAccessLayer` — the only project allowed to touch Npgsql
  (`Checks/check-npgsql.sh`). `QueryParameterValue`, `RowReader`, `buildReadQuery`,
  `executeReaderQuery`/`executeNonQuery` are the whole surface a domain module needs.
- `Model.Money` — all money arithmetic goes through it (`add`, `subtractVal1FromVal2`,
  `sumList`, `splitByN`); never arithmetic on a raw `decimal` money value.
- `Model.LookupCache` — account code ↔ ID, fiscal period key ↔ ID. Don't hand-write that
  lookup.

## Validation — four layers, and where a check may live in SQL

(`CompoundedLearnings/articles/coding/validation-layers.md`,
`.../validation-location.md`)

1. Type definitions — unbypassable compiler-level constraints (a validated wrapper can't hold
   an invalid value).
2. Smart constructors — per-field validation in `create`/`fromString`; cross-field constraints
   within one record, in the entity's construction path.
3. Composite validation — relationships *between* components of a composite (line count,
   balance invariant) — lives in the orchestrator, ordering is domain-determined.
4. Operation functions — state-dependent constraints that need external state (account is
   active, fiscal period is open) — never inside a constructor; `reconstitute` in particular
   runs inside an open reader and cannot make DB calls at all.

Validation logic is F#, not SQL, by default. It may go directly to SQL only when the check is
a **pure data question** (no validated types involved — dates, counts, existence) **and** doing
it in F# would need real new infrastructure or cost real performance. "It's more convenient in
SQL" alone doesn't qualify.

A unit-returning validation function is named `confirmX`, never `validateX` — the earlier
convention is retired and `Checks/check-confirm-naming.sh` fails on a new `validateX`.

## Naming canon

`create`, `fetchByX`, `insertNewToDb`, `updateDb`, `reconstitute`, `confirmX` are used
verbatim — don't invent a synonym. Everything else gets a fully descriptive name; verbosity is
a feature (`subtractVal1FromVal2`, `mapNoChangeToOptionWithConversion`) because clarity at the
call site beats brevity at the definition. Variables are never single-letter or abbreviated
outside a short, fully-graspable lambda (`fun x -> x + 1` is fine; `let ca = ...` for
`creditAccount` is not).

## Orchestration — what `ModelOrchestrator/` is for and how it's shaped

(`CompoundedLearnings/articles/architecture/orchestration-layer.md` has the full reasoning and
examples behind every point below — read it before building a new composite or orchestrated
function, not just when something feels ambiguous.)

**Five reasons a function lives here, not in `Model/`** (Dan's own framing):
1. Cross-domain composition in the direction `Model/` can't take without a circular reference
   — `JournalEntryLine` depends on `Account` (intrinsic, one-way), but closing an account needs
   to check `JournalEntry` balances, which would be the reverse dependency. That check lives in
   `AccountDeactivation.fs`, not `Account.fs`.
2. Composite validation — a composite invariant (JE needs ≥2 lines, debits = credits) can't be
   checked in `Model/` because the components need something (a header id) that doesn't exist
   until DB insertion. This is the one place a non-negotiable rule is *forced* up a layer —
   Model-level type constraints must always resolve to true/no-error, no exceptions, and this
   is the deliberate escape hatch for the one case where that's structurally impossible. It is
   not a general license to move validation up because it's more convenient there.
3. Orchestrated events — a multi-step process triggered by one UI action that succeeds or fails
   as a group (JE creation: header + lines + comments + references; stage ingestion: read raw →
   transform → deduplicate → classify).
4. "Fetch++" — complex or cross-domain fetch/reporting (`FetchFilterAndSort.fs`,
   `TrialBalance.fs`). Whether a new domain's filter/sort types (e.g. a `CashFlow` fetch filter)
   go into the existing shared `FetchFilterAndSort.fs` or get their own file is genuinely
   unsettled — Dan hasn't landed on a pattern. Default to the shared file; ask before deviating.
5. The "odd sock drawer" — a function that does three things (validate, create, persist) and
   therefore gets routed here by convention, even when nothing structurally forces it up (e.g.
   `AccountCreation.constructNewAndSaveToDb` could live nearer `Account.fs`, but doesn't).

**File granularity has no settled pattern yet.** Some orchestrator files own an entire domain's
worth of functions (`StageEntryOrchestration.fs`); others are single-purpose
(`AccountDeactivation.fs`, `JournalEntryVoiding.fs`). Dan: "I've not yet landed on a pattern.
Start by assuming one orchestrator will work. Pivot as needed." For a new domain, start with one
file and split later if it gets unwieldy — same size-based judgment as the Component-file
convention, not a naming taxonomy to reverse-engineer.

**`constructNewAndSaveToDb`** — the orchestrator's validate+create+persist verb (`Model/`'s
equivalent is `insertNewToDb`, persistence only). Always fallible, returning `Result` up to
`InterfaceBridge`, which commits or rolls back the transaction based on the outcome. The
standard doc comment, reused near-verbatim across the codebase — use it on the "route everything
through here" constructor for any new entity/composite:
```fsharp
/// constructNewAndSaveToDb validates that the components work together to
/// form a valid whole before adding it to the persistence layer. All new
/// <Entity> creation should route through here before being sent to the
/// persistence layer. Internal model functions may construct through other
/// means if they're operating on known good data.
```

**Its parameters are primitives and tuples of primitives, deliberately — don't propose replacing
them with a record.** The construct half of the verb is what builds the model types, so it can't
take them as input, and a list of children arrives as a tuple list
(`AgreementOrchestration.constructNewAndSaveToDb`'s payment agreements;
`JournalEntryOrchestration.constructNewAndSaveToDb`'s lines). Named primitive-collection types
used to exist for this and were retired: the domain type, the interface contract, and the
collection type all spelled out the same structure, and only the first two earned their
maintenance. These signatures aren't meant to be pleasant for a general consumer — exactly one
`InterfaceBridge` route calls each.

**Composite create pattern**: construct + persist each child through *that child's own*
orchestration function (never inline SQL for a child), collect with
`List.map(...) |> convertListOfResultsToResultsList`, then run the composite-level invariant
check as the final step — after every child is already individually valid and persisted, not
before.

**Composite read pattern**: fetch parents first; if empty, `return []` before touching a child
table; bulk-fetch children by an id list (`headerIds |> Entity.fetchByXIdList`, never N+1); a
pure compose function groups children by parent id and zips them together. Composite validation
helpers (line count, balance) are duplicated per composite rather than factored into a shared
generic — each composite owns its own copy.

**DAL errors are backstops, not operator-facing errors — and translating one is a matter of
which layer first has enough use-case context, not always the orchestrator's job specifically.**
`Model/` < `ModelOrchestrator/` < `InterfaceBridge` in how much use-case context each has;
translate at whichever layer first knows what the error should actually mean, and it's fine to
pass a DAL error through untranslated if this layer genuinely doesn't have that context yet —
that's `InterfaceBridge`'s job then, not a bug here. When you do translate here: only
`actual = 0` becomes a domain "doesn't exist" error, any other mismatch re-raises unchanged
(that's corruption, not absence). Full reasoning:
`CompoundedLearnings/articles/architecture/dal-errors-are-backstops.md`.

**`Context.getInitiationInstant` is the only source of "now" here — never create a timestamp
any other way.** `Context.updateInitiationInstant` exists for re-stamping between major phases
of a multi-step pipeline, but treat calling it as an always-ask, never standing-permission
decision: Dan's words, "you better be prepared with a bulletproof rationale... I will revert
that change on sight."

**Multi-phase pipelines stay one flat `result { }` block with inline phase comments**, not
split into named helper functions per phase, and not built to aggregate every failure across a
batch — "fix what broke, try again" is the deliberate policy (Hobson is the only user; this
isn't a consumer app). Contrast with a create function that has a few cleanly-named
sub-concerns (header, lines, references, comments), which *does* get split into named private
helpers — both shapes coexist on purpose; don't force one pipeline's shape onto the other.

**Reusing an `AppError` case across orchestrator functions is a judgment call, not a mechanical
one** — fine when two call sites truly mean the same thing to the operator, but whether to
collapse several similar no-op errors into one shared case or keep them granular (so it's clear
exactly which orchestrated sub-update failed) is Dan's call.
`StageEntryOrchestration.updateStageEntry`'s two-flag no-op guard
(`isThereAHeaderUpdate`/`isThereALineUpdate`) is the example: composing two independently-guarded
child updates needs its own composite-level guard, or the header-only/line-only cases produce
invalid SQL.

**Naming: update functions aren't called `updateDb` here.** `Model/`'s update verb is always
`updateDb`; the orchestrator layer names an update for what it updates
(`updateComment`, `updateFiAndReferenceText`, `updateClassificationRule`, `updateStageEntry`),
since an orchestrator update is often composing more than one `Model/`-level update.

**Comment voice**: the "comment only what would otherwise get flagged as a bug" rule (Operating
rules, above) exists specifically for the post-slice audit gauntlet Dan and Hobson run (~35
agents). Matching Dan's own blunt, direct register in these comments is fine and expected — the
point is telling an auditor agent "this was deliberate, move on" efficiently, not being polite
about it.

**`///` triple-slash comments are caller-facing API notes**, not general explanation —
`ModelOrchestrator.fsproj` has `GenerateDocumentationFile=true`, so they compile into
IDE-visible XML docs. Reserve them for a caveat about safely calling the function (a transaction
risk, a "this isn't set-based" warning), not a restatement of what it does. This isn't strict
correct XML-doc usage by .NET convention, but it's the established pattern here — follow it
rather than "fixing" it.

This is where `Obligation` (the composite that will assemble a `MasterAgreement` with its
`PaymentAgreement` children, once `PaymentAgreement` fetch-by-parent exists) belongs when it's
built — not as a field on `MasterAgreement` itself.

## `InterfaceBridge/` — where use cases come together

Dan's own list of what this layer does: define the interface contracts; define the use case
routes; turn user input (primitives) into DDD-valid types; turn model types back into
primitives; set the context (audit type, database context); manage commit/rollback; and call
the `Model/` and `ModelOrchestrator/` functions the use case needs. Nothing else belongs here,
and none of those belong anywhere else.

Three directories, and a change to a model type usually ripples through all three:
`InterfaceContracts/` (the DTOs), `BoundaryConverters/` (one file per domain —
`AccountFieldConverters.fs`, `IngestionFieldConverters.fs`, `CashFlowFieldConverters.fs`), and
`Routes/` (one file per domain, each ending in a `CommandRoute list`).

**Converter names are the index.** They read
`` convert [SourceType] to [TargetType] ``, brackets on both sides, backtick-quoted. Dan scans
these to see whether a converter already exists before asking for a new one, so the shape of the
name matters more than its elegance — match the existing entries rather than inventing a tidier
scheme. Fallible ones return `Result`; the private lookup-and-validate helpers behind them are
named `fallibleConverterXToY` instead, since they aren't part of that index.

**An entity crosses the boundary under a human-typed key, not a guid.** Accounts go out and come
back as codes, fiscal periods as period keys, payment agreements as names — each backed by a
`LookupCache` pair (`Model.LookupCache`, never a hand-written lookup) and a
`<Concept>DoesntMatch<Id>` `AppError` for the miss. When a new entity needs to be addressable
from the CLI, it needs such a key: a validated, DB-unique string column, not its uuid. The
exception is input a machine authors rather than a person — `BaseStageRawRowInput` carries a raw
guid because the import scripts produce those rows — and that exception is worth a comment at the
contract, because it reads as an oversight otherwise.

**Mirror the model's shape in the contract; don't invent invariants it doesn't hold.** A model
DU becomes a contract DU (`ClassificationClaimant` → `ClassificationClaimantInput` /
`ClassificationClaimantReturn`), because the either/or is real and the compiler should enforce it
at the edge too. A model record holding two independent options stays two options in the contract
(`PrioritizedMatch`'s account and payment agreement ids), even when a DU would look neater —
tightening there would force an error path for states the model permits.

## No REQ annotations in source

(`CompoundedLearnings/articles/architecture/no-req-annotations-in-source.md`) — settled,
don't re-litigate. Never add a `// REQ-XX-N.N` traceability tag to `.fs` or `.sql`; nothing
executes a comment, so it's an unverifiable claim about coverage. Test names carry REQ IDs;
that's the entire traceability mechanism. The one thing that *is* fine: a `(* *)` rationale
comment that cites a REQ ID to explain *why* code looks the way it does (a workaround, a
deliberately-dropped check) — that's an explanation for the next reader, not a coverage claim.
Don't strip an existing one of these because it superficially matches a REQ-annotation sweep.

## Mechanical checks

`bash Checks/run-all.sh --quick` runs the fast subset of everything above as one command —
worth running before calling a task done, even though this skill never commits and therefore
never triggers the pre-commit hook that runs it automatically. The one check with a gotcha for
new files rather than edits to existing ones: **`check-compile-order.sh`** verifies every `.fs`
on disk has a matching hand-maintained `<Compile Include>` entry in its `.fsproj`, in both
directions. The moment a task creates a brand-new file (not just edits an existing one), it
must be added to the project file at the correct dependency position — `dotnet build` verifies
compile *order* is valid, but won't catch a file that exists on disk and was simply never
declared.

## Working relationship

Dan reads every hand-off. State what changed and why, flag the judgment calls made under
standing permission, and stop at blockers rather than routing around them with an assumption.
When a task is genuinely just "do the next obvious thing" (the CRUD shape repeats identically
across `MasterAgreement`, `PaymentAgreement`, and whatever comes next), moving fast through
the mechanical parts is the job — the value this skill adds is in recognizing which parts
*aren't* mechanical: a schema that doesn't decompose cleanly, a type that can't be built
honestly from one table, an update field that shouldn't exist.

**Work the lines you were pointed at.** Dan often hands off a task by naming a file and a line
range, mid-refactor, with plenty of half-finished thinking elsewhere in the tree. Reading around
for context is fine; treating what you find as settled intent is not. His words: "don't go
looking too far afield while you're doing it or you'll just confuse yourself." Anything broken
outside the assigned lines gets reported with file and line number, not fixed.

**A review of a commit is a list, not an essay.** When a hand-off includes findings alongside the
work, order them by what breaks first at runtime, one or two sentences each, always with file and
line. Dan decides which ones become tasks.
