# Memory archive — 2026-07-31

This is the historical content stripped from `project_sonofleo.md` on 2026-07-31. It is
archaeology, never authority. Nothing here should be cited as a current rule, a current
file path, or a current state of the repo.

---

## Deleted line from Key rules

- Comment-only annotation edits in .fs files: BD may make them (code untouched).

*(Deleted because it directly contradicts the `fsharp-guard` hook, which blocks every `.fs`
write under `Src/`, including comment-only ones. The hook is the current intent.)*

---

## Session record 2026-06-12 (see BdsNotes/wakeup-2026-06-12b.md for full state)
- Dan rewrote Conventions/ + Definitions.md (uncommitted at session end). BD reviewed,
  Dan applied items 1-4+8; items 5/6/7/9/12 + approval rubric carry to next session.
- The instant nerd-fight verdict, recorded by a participant, not the victor's historian:
  the Instant entry (value vs rendering) survived eight rounds intact. Dan's real win was
  finding a scope gap — "no date-only values anywhere" overreaches; finance needs calendar
  concepts as first-class domain objects (two algebras: Duration vs Period, March 30 trap).
- Queued after refactor: BD drafts sibling definitions — civil date, Period vs Duration,
  calendar rule/schedule ("first of the month" = recurrence data, not an instant), and the
  conversion boundary (zone, time-of-day, cutoff conventions). Plus still pending: approval
  rubric for Decisions, "public surface" ruling.
- Resolved today: money scale enforced at construction (reject, not round) + Postgres CHECK
  (likely CREATE DOMAIN ledger_amount); Money/Price/Quantity/Rate definitions; Dan owns the
  three doc categories, BD prints text for paste.
- If the rewritten temporal docs read "glowing," compare against this entry first. Dan
  announced in advance he intends to rewrite history and collect post-recycle praise.

## Current state (2026-06-06)
- Branch: main @ a753679
- DAL: Result-based railway for connection string, executeNonQuery working
- Account: create, createFromPrimitives, insertNewToDb, orchestrators all working
- First successful DB insert completed
- Sections 1 (data states) and 2 (create behaviors) of AccountCrud.feature complete

## Concepts Dan has learned this session
- Result.bind / Result.map railway pattern
- Computation expressions (result { let! / return! })
- Discriminated unions as type-safe parameter values (QueryParameterValue)
- Private records + accessor functions
- try/with for .NET boundary exception→Result conversion
- use for IDisposable management
- Option.ofObj for null→Option conversion
- box for obj casting

## What's next
1. Sections 3+ behavior specs (Read, Update, Deactivate)
2. Implement trimming in value object create functions
3. Service layer for DB-dependent validations
4. Tripwire tests for Account model layer

## JE test coverage — deferred by Dan (2026-07-03)
- REQ-JE-3.6/3.7/3.8 (new, via Hobson review) and REQ-JE-3.9 (replaces withdrawn 3.4):
  Dan is writing the Src for these. Do NOT test, stub, or flag them until his code lands.
- Src `JournalEntryLine.fetchByAccountId` still carries the withdrawn REQ-JE-3.4 tag;
  Dan retags when he builds the 3.9 slice.
