# Memory cleanup — 2026-07-31

**Do this before you read `wakeup-2026-07-31a.md`.** Your memory currently contradicts that
document in six places. If you read them in the wrong order you will silently reconcile the
conflict yourself and nobody will see which way you went.

This is a self-contained work order. Everything you need is here — you do not need to
re-derive what is stale, because being unable to tell is precisely the problem.

## The principle

> An agent's persistent memory holds **who and how**. The wakeup holds **what and where we
> are**. Code facts belong in the repo, where they are versioned and a reviewer can catch
> them being wrong.

`project_sonofleo.md` is currently doing all three jobs. The rules half is largely sound.
The session-state half is between four and eight weeks stale, and the code-facts half is
simply wrong — the codebase was substantially refactored on 2026-07-25 and the documentation
corpus was cut roughly in half on 2026-07-30.

## Step 0 — preserve before you delete

Your memory directory is not in git. Before deleting anything, copy the blocks marked
DELETE below, verbatim, into a new file:

```
/workspace/SonOfLeo/BdsNotes/memory-archive-2026-07-31.md
```

Head it with one line saying it is the historical content stripped from
`project_sonofleo.md` on 2026-07-31 and is archaeology, never authority. That file lands in
git, so nothing is actually lost.

## Step 1 — delete these blocks entirely

From `/home/sandbox/.claude/projects/-workspace/memory/project_sonofleo.md`:

| Block | Why it goes |
|---|---|
| `## Session record 2026-06-12` (the whole thing, including the nerd-fight verdict) | Episodic. Its durable outcomes now live in `Specs/Definitions.md` and `CompoundedLearnings/articles/coding/temporal-*.md` |
| `## Current state (2026-06-06)` | Claims `main @ a753679` and Account CRUD §1–2. Reality: `main @ c0a7c4b`, 339 tests, the journal-entry slice is built |
| `## Concepts Dan has learned this session` | From a session that ended in early June |
| `## What's next` | Every item is done or superseded |
| `## JE test coverage — deferred by Dan (2026-07-03)` | Dan wrote that Src. REQ-JE-3.6/3.7/3.8/3.9 are implemented and tested. The REQ-JE-3.4 retag question is closed |
| The single line `- Comment-only annotation edits in .fs files: BD may make them (code untouched).` | **Directly contradicts `fsharp-guard`**, which blocks every `.fs` write under `Src/` including comment-only ones. The hook is the current intent. See below |

## Step 2 — correct these facts

| Currently says | Should say |
|---|---|
| `Src/Utilities/DAL.fs` — Data Access Layer | `Src/DataAccessLayer/` — its own project since 2026-07-25. `Checks/check-npgsql.sh` enforces that it is the only place Npgsql is touched |
| `Src/Utilities/ResultCE.fs` | `Src/Utilities/ResultHelper.fs` |
| `Specs/Behavioral/DataAccessLayer.feature` | `Specs/Behavioral/DataAccessLayer.md`. **There are no `.feature` files.** All seven behavioral specs are markdown |
| `Specs/Behavioral/AccountCrud.feature` | `Specs/Behavioral/AccountCrud.md` |
| **Dan owns Conventions/ (all but README), Decisions.md, and Definitions.md** | `Specs/Conventions/` was **deleted** 2026-07-30. `Decisions.md` moved to `Specs/Archive/Decisions.md` and is labelled history — never cite it as a current rule. `Specs/Definitions.md` is still Dan's |
| `LEOBLOOM_ENV=Development`, `LEOBLOOM_DB_PASSWORD=…` | The connection strings come from `SONOFLEO_DEV_CONNSTR`, `SONOFLEO_TEST_CONNSTR` and `SONOFLEO_PROD_CONNSTR`, named in the relevant `appsettings*.json` via `ConnectionStringEnvVar` |

Also add, under Structure or Key rules:

- `Src/**/*.fs` is Dan's alone and `fsharp-guard` enforces it on `Edit`/`Write`. The hook
  does not cover `Bash`; the rule holds anyway. Propose F# changes in prose and let Dan
  type them.
- Three test projects: `Tests.Helpers` (shared library, no tests), `Tests.Isolated` (no
  database), `Tests.Integrated` (database and CLI). `Tests/` is **not** blocked — you may
  edit it.

And delete these from your Key files list, because they no longer exist:
`PATTERNS.md`, anything under `Specs/Conventions/`, `Specs/Decisions.md`.

## Step 3 — leave these alone

These are correct and load-bearing. Do not touch them:

- F# code is Dan's alone. Agents never write it. Table stakes.
- Dan types. You teach, pressure-test, and point to sources.
- SQL and migrations: you may propose, but announce and get Dan's approval first. Migrations
  are reviewed by Dan and Hobson before anything is applied.
- Dan reads specs just-in-time, so **every recommendation you make must cite the spec text
  that justifies it** — that citation is his reading hook.
- Flag load-bearing claims with `📖 Research:`; distinguish high confidence, moderate
  confidence, and speculation.
- Repo at `/workspace/SonOfLeo`, .NET 10, F#, dev database `sonofleo_dev`, schema `ledger`,
  UUID primary keys, Rider on Dan's host.
- `/workspace/LeoBloom` is the reference codebase — agent-written, not gospel. It still
  exists.
- The `MEMORY.md` line `**SonOfLeo** (PRIVATE): /workspace/SonOfLeo — … F# rewrite of
  LeoBloom, Dan types, BD tutors` is accurate. Leave it.

`/workspace/CLAUDE.md` has no SonOfLeo content. Nothing to do there.

## Step 4 — one question for Dan, do not decide it yourself

Your memory says *"Specs/ folder: BD writes freely, encouraged. No pre-approval needed."*
That predates Hobson taking ownership of specs and infrastructure. It may still hold, it may
not. **Ask Dan and record his answer** — do not delete it and do not act on it in the
meantime.

## Step 5 — report

Show Dan the resulting `project_sonofleo.md` in full, not a summary of what you changed. He
is reviewing the artifact, not your description of it.

Then read `BdsNotes/wakeup-2026-07-31a.md` and stop.
