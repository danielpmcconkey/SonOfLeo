---
name: SonOfLeo:CodeReviewer
description: >
  Mandatory self-review before presenting any SonOfLeo code change to Dan. Use as the
  final step of every task that created or modified code in Src/ or Tests/ — after the
  work feels done, before writing the hand-off message. Triggers on "review my changes",
  "ready to present", finishing any implementation task, or preparing work for Dan's review.
---

# SonOfLeo CodeReviewer

You are the first-pass reviewer of your own diff. Dan personally reviews every line you
present; this skill exists so the worst offenses never reach him. Review the **diff** —
every file you created or changed this session — not the whole repo.

This skill tells you *where to look*. What right looks like lives elsewhere:

| For | Read |
|---|---|
| What infrastructure already exists | `Src/README.md` |
| Test standards | `Tests/README.md`, plus the README in the test directory you touched |
| Behavioral requirements | `Specs/Behavioral/` |
| Judgment — layering, validation location, naming, temporal, money | `CompoundedLearnings/` catalogs, following their "read when" triggers |

## Gate 0 — mechanical (hard gate)

Run, in order:

```bash
bash Checks/run-all.sh
dotnet build --artifacts-path /tmp/sonofleo-build
dotnet test --artifacts-path /tmp/sonofleo-build
```

Any failure: stop, fix, rerun. **Never present red.** Do not rationalize a failure as
pre-existing without verifying it fails on a clean checkout of your starting commit.

Equally, do not act on a red you have not confirmed. Reproduce the individual failing check
before you run a formatter, edit the files it names, or reach for `--no-verify` —
`check-format` has produced both a false PASS and a false FAIL, and the files it names may
not be yours to touch. See
`CompoundedLearnings/articles/process/a-check-verdict-is-evidence-not-truth.md`.

## Pass 1 — "there's already a function for that"

Check each new or changed function against the inventory in `Src/README.md`. Reinventing any
of it is a review rejection. Symptoms to hunt in your diff:

| If your diff contains… | You should have used… |
|---|---|
| A string being built to describe an error | An `AppError` case and its `toMessage` arm |
| A hand-rolled fold or loop over `Result` values | `ResultHelper` |
| `NpgsqlCommand`, `NpgsqlConnection`, raw SQL execution | `DataAccessLayer` |
| `DateTime.Now`, `DateTimeOffset.UtcNow`, `SystemClock` | `Clock.now()` / `Calendar.today()` |
| An option or flag meaning "don't update this field" | `FieldUpdate` and its converters |
| A query looking up an account code or period key by string | `LookupCache` boundary converters |
| A fresh `Clock.now()` inside a mutating operation | The route handler's `AuditEnvelope` instant |
| Arithmetic on raw `decimal` money values | `Model.Money` |
| A `JsonSerializerOptions` or a direct serializer call | `InterfaceBridge.Json` |

## Pass 2 — altitude and layering

- **Dependency direction:** does each changed file reference only layers below it?
- **Validation altitude:** per-field validation in component smart constructors; cross-entity
  rules in the orchestrator; shape-only checks at the boundary. F# versus SQL is a real
  question with a real test — `CompoundedLearnings/articles/coding/validation-location.md`.
- **Error translation altitude:** infrastructure errors are re-branded to domain errors only
  at the caller that knows the domain meaning; everything else passes through unchanged.
- **Transaction ownership:** route handlers own transactions; model and orchestrator
  functions are participants taking `DbTransaction option`.
- **Compile order:** every new file inserted at its correct position in the `.fsproj`, never
  appended blindly.

## Pass 3 — the single-author test

Read your diff as if Dan wrote it. **If a reviewer could tell your hunk from Dan's code, name
the difference and eliminate it.** In particular:

- Naming: `fetchById` / `fetchByX` / `fetchAll`, `insertNewToDb`, `updateXById`,
  `constructNewAndSaveToDb`, `mapRawForDbRead` / `reconstitute` / `readRowsFromDb`, and
  `confirmX` for unit-returning checks — `validateX` is retired
  (`Checks/check-confirm-naming.sh`).
- Boundary converters are named in prose with square brackets around each side:
  ``` ``convert [Account code string option list] to [AccountId option list]`` ```. Scan for
  an existing converter before writing one; that is what the convention is for.
- Parameter order: context first, subject last (`Src/README.md`).
- Visibility: private by default; a function whose analogs elsewhere are private must be
  private (`Src/README.md`).
- Comments: REQ tags, `///` contracts, `(* *)` rationale. No narration.
- No `match` expression as a direct item in a list literal — it is the one shape Fantomas
  formats unstably. Use a pipeline expression instead.

## Pass 4 — test quality

`Tests/README.md` owns test standards; do not restate them. Spot-check the three most
violated before presenting:

- Every railroad is terminated by `railroadWrapper`. A `[<Fact>]` returning a bare `Result`
  passes unconditionally.
- Every new test asserts domain **values**, not just counts, and sad-path tests match the
  typed error case with both escape arms.
- Every entity a test creates carries a unique, test-identifying name or code and is disposed
  of by the cleanup obligation of its form — rollback for form 3, `_Cleanup` helpers in
  `finally` for forms 4 and 5.

## Output contract

Present the work with a short self-review note containing:

1. The tail of `Checks/run-all.sh`, build, and test output (the pass/fail summary lines).
2. What each pass flagged and how you resolved it — "nothing" is an acceptable finding,
   silence is not.
3. Anything knowingly nonconforming, with why, flagged for Dan's ruling.

Honest flags are cheap; violations Dan discovers himself are expensive.

## Iterating on this skill

When Dan's review catches something this skill should have caught, that is a guardrail gap:
record it via the CreateLearning skill's triage (mechanical → new `Checks/` script; judgment
→ CompoundedLearnings article plus one checklist line here).
