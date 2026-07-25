# SonOfLeo — Architecture Extraction and Guardrail Design

## What this repo is

SonOfLeo is a work in progress cash-basis GAAP personal finance CLI app. F# on .NET 10,
PostgreSQL, xUnit. I wrote every line by hand over the last two months — partly
to learn F#, partly to set an architectural foundation for LLM developers to
build on top of.

**Repo:** `/media/dan/fdrive/ai-sandbox/workspace/SonOfLeo/`

## What I need from you

Three phases, checkpointing with me between each.

### Phase 1 — Extract my patterns

Read `Src/` and `Tests/` thoroughly. These are the sole source of truth.
Non-code artifacts (`Specs/`, `CompoundedLearnings/`, `Skills/`, `Conventions/`,
`Doctrines.md`, etc.) tell the story of how we got here — they contain useful
context but much is stale after a major refactor. **When a non-code artifact
contradicts what the code does, the code wins.** The only exception: if a single
function appears "wrong" relative to the rest of the codebase, evaluate it
against the codebase's own internal consistency, not against a sentence in a
spec doc.

What to extract:

- **Architectural patterns.** How I layer projects (Utilities, Model,
  ModelOrchestrator, InterfaceBridge, CLI). Where validation lives vs
  construction vs orchestration vs persistence. How errors flow. How
  transactions work. The entity ID wrapper pattern. The FieldUpdate pattern.
  The split-query fetch pattern. Anything structural that a new developer
  would need to replicate correctly.

- **Centralized infrastructure.** Functions and modules I built once and reuse
  everywhere — `ResultHelper`, `AppError`, `DAL`, `Clock`, `LookupCache`,
  `FieldUpdate`, boundary converters. These exist so that nobody reinvents
  them. I need you to catalog them and describe what each one is for, so that
  a downstream developer knows "there's already a function for that."

- **Style and naming conventions.** How I name modules, functions, types, DU
  cases, test classes, test methods. How I format record literals, pipeline
  operators, match expressions. Indentation patterns. Where I use backtick
  names. Comment philosophy (or lack thereof). Anything a developer would need
  to match in order to write code that looks like it belongs.

- **Test patterns.** How I structure test fixtures, what I assert and at what
  granularity, how I handle test data setup/teardown, how I separate isolated
  vs integrated tests, what the route resolver is for. Also note what I
  explicitly *don't* test (and why, if the code makes that clear).

### Phase 2 — Present and challenge

Walk me through what you found, **one pattern at a time.** For each:

1. State the pattern you observed.
2. If the F# community would do it differently, say so — and explain why they
   would. I want to know where my choices are idiomatic, where they're
   unconventional-but-defensible, and where they're genuinely fighting the
   language. I'm relatively new to thinking in F# and I want honest feedback,
   not validation.
3. Wait for my reaction before moving to the next one. I may agree, disagree
   with reasoning, or ask you to elaborate.

The goal of this phase is a **confirmed list of patterns** — the things I've
explicitly blessed as "yes, this is how we do it here" — plus any adjustments
I make based on your F# feedback.

### Phase 3 — Design the guardrails

I want to hand this code base off to "BD" — a Claude Code instance (Opus 4.6)
running in a Docker sandbox at `/media/dan/fdrive/ai-sandbox/workspace/SonOfLeo/`.
He installs skills via symlinks from the repo's `Skills/` directory.

I don't want to regret that decision so I want you to using the confirmed pattern list to design the enforcement layer for my LLM development team. I plan to personally review every line of code he writes but I want your work here to make my future code reviews easier because what you build today should've already weeded out the worst offenses.

The guardrails need to be extensible. As we find new "problems" I need a way to add their remediations into the moving-forward process. See the Skills/CreateLearning/SKILL.md and the CompoundedLearnings/README.md for the framework I started creating for this before the refactor. Much of the specific learnings are now dead, but there's probably merit in the framework. You tell me.

That said, there are some specific things I want to guard against. This isn't the full scope, but it's important that these not be left out:

1. **Bullshit tests.** BD wrote tests that asserted trivial or redundant
   things while missing the actual business logic under test (e.g., checking
   row counts but never checking the values in the rows). I need a reviewer
   skill — or a test-writing skill with strict guidance — that prevents this.

2. **Rolling his own.** I built centralized infrastructure for error handling,
   database interaction, date/time, result composition, field updates, and
   boundary conversion. BD must use these, not reinvent them. I need
   guardrails that make the existing infrastructure discoverable and
   violations detectable.

3. **Stylistic variance.** This code base needs to appear to have been written by one author. I shouldn't be able to look back in 6 months and know at a glance which code was written by me and which by BD.

The output should be concrete artifacts I can put in the repo:

- **Skills** (`.claude/` or `Skills/` — BD loads these via frontmatter). A
  first-pass code reviewer skill that catches the most common violations
  before work reaches me. A test-writing skill that enforces test quality
  standards.
- **Hooks** (if appropriate — pre-commit or post-edit checks that catch
  mechanical violations).
- **Whatever else you think is warranted.** If there's a guardrail mechanism
  I haven't thought of, propose it.

Design these for Opus 4.6 as the consumer. They don't need to be clever —
they need to be clear, explicit, and hard to misinterpret.

## Important context

- I am the PO and code reviewer. BD writes code; I approve or reject it.
- Dan (me) owns F# in Rider. BD has only ever written tests, though I've re-written most of his work by now.
- Work happens in small chunks so I can review without being overwhelmed.
- F# compile order is load-bearing. Don't let BD break it.
- Migration review is always my job (Hobson's — the host-side Claude).
