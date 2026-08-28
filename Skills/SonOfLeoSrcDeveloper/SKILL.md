---
name: SonOfLeo:SrcDeveloper
description: >
  This skill should be used when writing or modifying F# source under SonOfLeo's Src/
  directory — new entity CRUD, new domain primitives, AppError cases, or any Src
  implementation task Dan hands off. Covers the entity/component/composite type taxonomy,
  the CRUD function shape (insertNewToDb / reconstitute / mapRawForDbRead / readRowsFromDb /
  fetchGenericRead / fetchById / updateDb), AppError and FieldUpdate conventions, the
  mechanical checks in Checks/, and the working relationship with Dan. Triggers on
  "build out X's CRUD", "write the Src for", "add a domain type", "finish insertNewToDb",
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
   need yet.
6. **`fetchById`** (public) — the one predicate every entity gets for free:
   `alias.unique_id = @unique_id`, `ExactlyOne`, `|> Result.map List.head`. Add more
   `fetchByX` functions the same way as the task calls for them.
7. **`updateDb`** (public) — takes an `<Entity>FieldUpdates` record (see FieldUpdate below),
   builds `(setClause, QueryParameter) list option` per field via
   `FieldUpdate.mapNoChangeToOptionWithConversion`, flattens with
   `List.choose id |> List.collect id` (a field that maps to more than one column — see
   `Cadence` below — just returns a longer list from its conversion function; the flatten step
   doesn't care). No-op guard: if the flattened list is empty, `Error(Cashflow<Entity>UpdateNoOp)`
   (see AppError below) before touching the DB. On success, write then re-fetch via
   `fetchById` and return the current row — never trust the caller's view of what changed.
   Only include fields in `<Entity>FieldUpdates` that should actually be settable after
   creation; an entity's own identity/FK-to-parent field is typically excluded the same way
   `headerIdToUpdate`/`agreementIDToUpdate` themselves aren't update targets.

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

## Orchestration — when a function leaves `Model/`

(`CompoundedLearnings/articles/architecture/orchestration-layer.md`)

A function belongs in `ModelOrchestrator/`, not a domain module, when it needs data from more
than one domain module, or when it coordinates more than one distinct step even within a
single domain (construct + persist is two activities). F# compile order makes this structural,
not stylistic — a module can't reference one that compiles after it, so genuinely cross-domain
composition has nowhere else to live. This is where `Obligation` (the composite that will
assemble a `MasterAgreement` with its `PaymentAgreement` children, once `PaymentAgreement`
fetch-by-parent exists) belongs when it's built — not as a field on `MasterAgreement` itself.

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
