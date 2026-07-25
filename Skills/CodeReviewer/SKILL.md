---
name: SonOfLeo:CodeReviewer
description: >
  Mandatory self-review before presenting any SonOfLeo code change to Dan. Use as the
  final step of every task that created or modified code in Src/ or Tests/ — after the
  work feels done, before writing the hand-off message. Triggers on "review my changes",
  "ready to present", finishing any implementation task, or preparing work for Dan's review.
---

# SonOfLeo CodeReviewer

You are the first-pass reviewer of your own diff. Dan personally reviews every line
you present; this skill exists so the worst offenses never reach him. Review the
**diff** — every file you created or changed this session — not the whole repo.

The authority for every rule cited here is `PATTERNS.md` at the repo root (pattern IDs
like P2.4, P6.1). This skill tells you *where to look*; PATTERNS.md tells you *what
right looks like*. On any conflict, PATTERNS.md wins and this skill needs updating.

## Before you begin

- Read `PATTERNS.md` in full if you have not already this session.
- Read the CompoundedLearnings catalogs relevant to the change (`CompoundedLearnings/README.md`
  explains the system; follow "when to read" triggers into articles).

## Gate 0 — mechanical (hard gate)

Run, in order:

```bash
bash Checks/run-all.sh
dotnet build --artifacts-path /tmp/sonofleo-build
dotnet test --artifacts-path /tmp/sonofleo-build
```

Any failure: stop, fix, rerun. **Never present red.** Do not rationalize a failure as
pre-existing without verifying it fails on a clean checkout of your starting commit.

## Pass 1 — "there's already a function for that"

For each new or changed function, check the P2 infrastructure catalog (P2.1–P2.9).
Reinventing any of it is a review rejection. Symptoms to hunt in your diff:

| If your diff contains… | You should have used… |
|---|---|
| A string being built to describe an error | `AppError` case + its `toMessage` arm (P2.1) |
| A hand-rolled fold/loop over `Result` values | `ResultHelper` (P2.2) |
| `NpgsqlCommand`, `NpgsqlConnection`, raw SQL execution | `Utilities.DAL` (P2.3) |
| `DateTime.Now`, `DateTimeOffset.UtcNow`, `SystemClock` | `Clock.now()` / `Calendar.today()` (P2.4) |
| An option/flag meaning "don't update this field" | `FieldUpdate` (P2.5) |
| A query looking up an account code or period key by string | `LookupCache` boundary converters (P2.6) |
| A fresh `Clock.now()` inside a mutating operation | The route handler's `AuditEnvelope` instant (P2.7) |
| Arithmetic on raw `decimal` money values | `Model.Money` (P2.8) |
| A `JsonSerializerOptions` or direct serializer call | `InterfaceBridge.Json` (P2.9) |

## Pass 2 — altitude and layering

- **Dependency direction** (P1.1): does each changed file reference only layers below it?
- **Validation altitude** (P3, P4.3/P4.4): per-field validation in component smart
  constructors; cross-entity rules in the orchestrator; shape-only checks at the boundary.
- **Error translation altitude** (P5.3): infrastructure errors re-branded to domain
  errors only at the caller that knows the domain meaning; all else passes through.
- **Transaction ownership** (P4.6): route handlers own transactions; model/orchestrator
  functions are participants taking `DbTransaction option`.
- **Compile order** (P1.2): every new file inserted at the correct position in its
  `.fsproj`, never appended blindly.

## Pass 3 — the single-author test

Read your diff as if Dan wrote it. **If a reviewer could tell your hunk from Dan's
code, name the difference and eliminate it.** Check in particular:

- Naming canon (P6.1) — including `confirmX` not `validateX` (P5.1).
- Boundary converter names in the square-bracket dialect (P6.2) — and scan for an
  existing converter before writing one.
- Parameter order: context first, subject last (P4.8).
- Visibility: private by default; a function whose analogs elsewhere are private must
  be private (P6.8).
- Comment philosophy (P6.6): REQ tags, `///` contracts, `(* *)` rationale — no narration.
- No `match` expression as a direct list item (P2.5/P4.7).

## Pass 4 — test quality

The TestWriter skill owns test standards; do not restate them. Spot-check its two most
violated rules before presenting:

- Every new test asserts domain **values**, not just counts (P7.4, P7.6), and sad-path
  tests match the typed error case with both escape arms (P7.6).
- Every entity a test creates carries a unique, test-identifying name/code and is
  cleaned up by rollback or `_Cleanup` helpers (P7.5).

## Output contract

Present the work with a short self-review note containing:

1. The tail of `Checks/run-all.sh`, build, and test output (the pass/fail summary lines).
2. What each pass flagged and how you resolved it — "nothing" is an acceptable finding,
   silence is not.
3. Anything knowingly nonconforming, with why, flagged for Dan's ruling.

Honest flags are cheap; violations Dan discovers himself are expensive.

## Iterating on this skill

When Dan's review catches something this skill should have caught, that is a guardrail
gap: record it via the CreateLearning skill's triage (mechanical → new `Checks/` script;
judgment → CompoundedLearnings article + one checklist line here).
