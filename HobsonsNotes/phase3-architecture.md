# Phase 3 — Guardrail Architecture (DRAFT for Dan's review)

**Status:** Draft 2026-07-25. Annotate inline with `[Dan]...[/Dan]`; I'll condense
dispositions and produce build specs for Opus 4.6 once you've ruled.

Everything here references `PATTERNS.md` (the canonical house-style doc)
rather than restating it. Binding constraints from Phase 0 are assumed, not re-argued:
portable plain files, CI-phase not real-time, Fantomas + thin scripts + prose skills,
no bureaucracy expansion, Fable specs / Opus builds.

---

## 1. The three-tier model

Every guardrail lands in exactly one tier, chosen by one question: **can a script
detect the violation with zero judgment?**

| Tier | Mechanism | Catches | Runs |
|---|---|---|---|
| **1 — Deterministic** | `Checks/` shell scripts + Fantomas | Mechanical violations (banned APIs, layering breaches, naming bans, compile-order integrity) | Pre-commit + on demand |
| **2 — Judgment** | Prose skills (`Skills/CodeReviewer`, `Skills/TestWriter`) | Bullshit tests, rolling-his-own, stylistic variance, wrong-altitude decisions | BD self-applies before presenting work |
| **3 — Extensibility** | CompoundedLearnings + a triage rule | New problems discovered after go-live | When a problem recurs or Dan overrules |

Portability: Tiers 1 and 3 are plain files any LLM (or human) can consume. Tier 2's
*content* is plain markdown in `Skills/`; only the frontmatter is Claude wiring. If you
move to Gemini, you rewrite ~8 lines of frontmatter per skill, nothing else.

## 2. Tier 1 — `Checks/`

A new top-level `Checks/` directory: one script per rule, one runner.

```
Checks/
  run-all.sh          # runs every check-*, prints pass/fail table, exit 1 on any fail
  check-format.sh     # fantomas --check across Src/ and Tests/
  check-clock.sh      # DateTime.Now/UtcNow/SystemClock outside Clock.fs/Calendar.fs
  check-npgsql.sh     # "Npgsql" outside Src/Utilities/DAL.fs
  check-testingerror.sh   # TestingError constructed anywhere in Src/
  check-tomessage-wildcard.sh  # wildcard arm inside AppError.toMessage
  check-compile-order.sh  # every .fs on disk is in its .fsproj; every Compile Include exists on disk
  check-confirm-naming.sh # new `validateX` function definitions (P5.1 — confirmX canon)
  check-hardwired-dates.sh # date literals in Tests/ (P7.3 — fixtures move through time)
  check-apperror-coverage.sh # every AppError case name appears in ≥1 test file (#125a)
```

Design rules for the scripts:

- **Grep-grade, not parser-grade.** Each is ≤30 lines of bash/grep/awk. When a rule
  needs real parsing to be trustworthy, it's not Tier 1 — it goes in the CodeReviewer
  checklist instead. No AST tooling, no dependency beyond coreutils + dotnet + fantomas.
- **Allowlist escape hatch in-file.** Each script hard-codes its blessed exceptions at
  the top (e.g. `FiscalPeriod.fetchIdByKey` for anything that ever needs one) with a
  comment pointing at the pattern ID. No config files — the script *is* the config.
- **Every script header cites its pattern ID** (`# Enforces P2.4 — see PATTERNS.md`).
- `check-compile-order.sh` verifies **membership**, not order — order requires a build,
  and `dotnet build` is already the order check. Its job is catching the file BD created
  on disk but forgot to add, or appended blindly to the wrong project.
- `check-apperror-coverage.sh` starts as a **report, not a gate** (current code won't
  pass until #125a lands). Flip it to gating once the suite exists.

**Where they run.** Two hooks, both thin:

1. **Git pre-commit hook** — runs `run-all.sh` minus the slow ones (format + greps;
   skip apperror-coverage). Installed by `Checks/install-hooks.sh` into `.git/hooks/`
   — opt-in, one command, and BD's container setup runs it. Not versioned magic;
   the hook is 3 lines calling the runner.
2. **The review contract** — BD's process (CodeReviewer skill, §3) requires a clean
   `run-all.sh` *and* `dotnet build` *and* `dotnet test` before work is presented to
   you. The transcript shows the output; you never review code that hasn't passed.

No GitHub Actions for now — there's no remote CI habit in this repo and the pre-commit
hook + review contract cover the same ground without new moving parts. If the repo
gains a shared remote later, `run-all.sh` drops into any CI verbatim.

**Fantomas caveat — needs a pilot before we commit.** Fantomas enforces *its* style,
configured via `.editorconfig` `fsharp_*` settings. It will match 4-space indent and
most of the house look, but P6.3's signature format (one param per line, indent 8,
annotated return on its own line) and P6.4's record style may not survive a
format pass byte-for-byte. Proposed pilot: run `fantomas --check` on `Src/Utilities/`,
review the diff together, then decide: (a) adopt Fantomas and let it own formatting
wholesale (amending P6.3/P6.4 to "whatever Fantomas emits with our .editorconfig"),
or (b) drop `check-format.sh` and leave formatting to the CodeReviewer skill's eyes.
Half-adopting (Fantomas but hand-fixing its output) is the one option I'd refuse to
serve — it makes every commit a fight.
[Dan]yes. we can commit and push, try out the pilot run, and revert if I get sick when I see the results[/Dan]

## 3. Tier 2 — the skills

### 3a. `Skills/CodeReviewer` (new)

BD's mandatory last step before presenting any work. Structure:

- **Gate 0 — mechanical.** Run `Checks/run-all.sh`, `dotnet build`, `dotnet test`.
  Any failure: stop, fix, rerun. Never present red.
- **Pass 1 — "there's already a function for that."** For each new/changed function,
  check against the P2 catalog (P2.1–P2.9, cited not restated): did you hand-roll
  error strings, result plumbing, SQL access, time, field updates, money arithmetic,
  JSON, or lookups? The skill lists the *symptoms* (e.g. "you wrote `new NpgsqlCommand`",
  "you wrote a match producing an error message string") and the P2 module that
  should have been used.
- **Pass 2 — altitude and layering.** P1.1's one-way dependency check per changed file;
  validation in the right layer (P3 vs P4.3/P4.4); error translation altitude (P5.3);
  transaction ownership at the route handler (P4.6/#118a).
- **Pass 3 — single-author test.** Read your diff as if Dan wrote it. Naming canon
  (P6.1), converter dialect (P6.2), parameter order (P4.8), visibility default (P6.8),
  comment philosophy (P6.6). The skill's framing: *"if a reviewer could tell this
  hunk from Dan's code, name the difference and eliminate it."*
- **Pass 4 — test quality.** Defer to the TestWriter skill's standards (don't
  duplicate); spot-check that every new test asserts domain values, not just counts
  (P7.4/P7.6), and that every created entity carries a test-identifying code (P7.5).
- **Output contract.** A short self-review note presented with the work: checks output,
  what Pass 1–4 flagged and how it was resolved, anything knowingly nonconforming
  with why. Honest flags are cheap; discovered violations are expensive.

### 3b. `Skills/TestWriter` (reconcile, don't rewrite)

The existing skill is good and largely consistent with patterns.md §7. Changes:

- Add a preamble: **patterns.md §7 is the authority**; on conflict, patterns.md wins
  and the skill needs a PR.
- Fold in the P7.6 assertion canon verbatim-by-reference (railroad + typed-DU-case
  sad paths with both escape arms) — the current skill is silent on assertion *shape*.
- Add a **negative-exemplar section: "what a bullshit test looks like"** — 3–4
  before/after specimens harvested from `bba1c17..5cb26c4` (hard-wired counts,
  count-without-values, missing sad-path escape arms). Each specimen: the bad test,
  why it's worthless, the rewrite. This is the single highest-leverage artifact
  against guard target #1 — Opus learns far more from a labeled bad example than
  from a rule.
- Keep the two-phase stub→approve→implement workflow — it already matches your
  small-chunks review preference.
  [Dan]My idea, right before the refactor, was to keep the skills lightweight and have them reference specific articles in the CompoundedLearnings catalog. That way we get reusability and progressive disclosure together. Is my idea still valid?[/Dan]

### 3c. Skill installation

Skills stay in the repo's `Skills/`; BD symlinks per current practice. Each SKILL.md
body is plain markdown with zero Claude-isms below the frontmatter — that's the
portability line.

## 4. Tier 3 — extensibility (CompoundedLearnings verdict)

**The framework has merit; keep it.** Catalog + atomic articles + "when to read"
triggers is exactly the right shape for LLM consumption, and CreateLearning's
workflow (identify → domain → dedupe → article → catalog row) is sound. Verdict:
retain the framework, fix the content. Three problems:

1. **Missing catalogs.** README promises six; `testing.md` and `process.md` don't
   exist and their article dirs are empty. Create both (empty catalogs with headers
   are fine — Phase 3 and the first BD sessions will feed them).[Dan]those original 6 weren't well vetted by me. I'm not sure they're the right organizational structure. Just keep in mind that the catalog is supposed to work for coding agents as well as you doing my finances[/Dan]
2. **Stale content.** Several `architecture/` and `coding/` articles predate the July
   refactor and patterns.md. One-time triage pass: each article is either
   (a) still true → add a "consistent with patterns.md PX.Y" line, (b) superseded →
   delete (patterns.md is the survivor), or (c) true but now redundant with
   patterns.md → delete. Redundancy is a defect here: two sources of truth will
   drift, and patterns.md has the amendment process.[Dan]I want to know what you're deleting before you hard delete[/Dan]
3. **Authority chain needs one sentence added** to the README: patterns.md sits
   between Specs and CompoundedLearnings — *Specs > patterns.md > learnings*.

**The moving-forward triage rule** (added to CreateLearning as a final step) — when a
new problem surfaces:

1. Mechanically detectable? → new `Checks/check-*.sh` (+ a line in the relevant
   skill only if context helps).
2. Judgment call that recurs? → CompoundedLearnings article + one checklist line in
   CodeReviewer or TestWriter citing it.
3. House-style question? → it's a patterns.md amendment (Dan dispositions), and then
   possibly 1 or 2.

That's the whole extension process. No new machinery.

## 5. What I'm deliberately not building [Dan]agreed with all[/Dan]

- **Directory-ownership enforcement, BD's CLAUDE.md/settings hardening** — parked
  per Phase 0, until load-bearing.
- **Real-time hooks** (post-edit checks) — your database-trigger analogy governs.
- **A patterns-linter DSL / config-driven rule engine** — the 10 scripts above don't
  justify a framework. Boring but right.
- **Test-file restructuring (1 code : N test files)** — deferred per patterns.md.
  Nothing above depends on current test-file layout, so the rethink stays unblocked.

## 6. Open questions for Dan

1. **Fantomas pilot** — §2. Run the pilot diff on Utilities before deciding? [Dan]Src/Model/Ledger/Account.fs is my baseline. Run it on that[/Dan]
2. **Where does patterns.md live?** Currently `HobsonsNotes/`. BD can read it there,
   but "canonical doc all guardrails cite" arguably belongs somewhere less
   Hobson-flavored — e.g. `Conventions/patterns.md` or repo root. I mildly favor
   moving it; your call. `[Dan]Not hobsons notes. Where would a real dev team put it? It should span both Src and Tests so it's somewhere off the root. It's kind of like .gitignore or SonOfLeo.slnx in that regard. I need guidance[/Dan]`
3. **Pre-commit hook** — comfortable with the opt-in `.git/hooks` install, or prefer
   review-contract-only (BD runs `run-all.sh` manually, you see the output)? `[Dan]I'm not sure what you're asking me[/Dan]`
4. **Acceptance dry-run timing** — after the skills land, I spec a small, bounded task;
   a subagent plays BD with skills installed; we audit what leaks. Do it immediately
   after build, or wait for BD's first real assignment and treat that as the dry run
   (cheaper, riskier)? `[Dan]I can give him a small assignment. I have plenty to pick from[/Dan]`
5. **CompoundedLearnings triage** — do the staleness pass myself as part of Phase 3,
   or spec it for Opus? It requires judgment against patterns.md; I'd keep it. `[Dan]you do it[/Dan]`

## 7. Build plan (once you've ruled)

Handoff to Opus 4.6, in order, each a small reviewable chunk:

1. `Checks/` scripts + runner + hook installer (specs from me, per-script: rule,
   pattern ID, allowlist, test cases including a deliberate violation).
2. Fantomas `.editorconfig` (only if pilot passes).
3. `Skills/CodeReviewer/SKILL.md` (I draft the checklist content; Opus formats and
   wires — or I just write this one myself, it's prose not construction).
4. TestWriter reconciliation + specimen harvest from `bba1c17..5cb26c4` (specimen
   selection is judgment — me; formatting into the skill — Opus).
5. CompoundedLearnings: missing catalogs, README authority line, CreateLearning
   triage step, staleness pass (per Q5).
6. Acceptance dry-run (per Q4).

Items 3 and 4's judgment halves are Fable work; I'd fold them into my own output
rather than paying Opus to guess. The scripts and mechanical edits are Opus's. [Dan]I'm fine with you keeping any of these you want.I'm nowhere near any of my token limits[/Dan]
