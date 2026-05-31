# SonOfLeo Charter

## What this project is

A hand-written F# rewrite of LeoBloom by Dan, with BD acting as F# tutor. Dan
types every line. BD teaches, explains, pressure-tests, and points to
authoritative sources when something matters enough for Dan to verify
independently.

This is not a port. It is a re-architecture, written FP-native, against the
existing BDD spec set.

## Why we're doing this

Dan is running a long-term experiment with agentic coding (LeoBloom is the
test bed). The current weakness in that experiment: Dan can't read F# well
enough to audit what the agents produce. Without that audit ability, he can't
distinguish between "this methodology works long-term" and "this methodology
is accumulating debt silently."

The goal of SonOfLeo is to develop that audit muscle. Outcome: by the end of
this project, Dan can read F# fluently enough to evaluate his own ability to
direct AI coding agents over months without accumulating undetected technical
debt.

A secondary goal: F# is aesthetically pleasing and Dan wants to actually
learn it. Four years ago he tried, got the syntax, never internalized the
paradigm.

## What this project is NOT

- Not commercially driven. Dan has enough money, isn't job hunting in F#.
- Not a replacement for running LeoBloom on day one. See operational
  decisions below.
- Not BD writing code. BD teaches. Dan codes.

## Operational decisions

### LeoBloom (the original) keeps running

- Dan continues using LeoBloom for weekly finances against the prod DB.
- Feature set is frozen on LeoBloom. No new features.
- If a logic bug is discovered during the rewrite, it gets fixed in both
  places.
- The dev DB (the one BD has access to) remains the working DB for SonOfLeo
  development.
- DB structure stays intact across both projects. We are not redesigning
  schemas.

### Language choice

- F#. Decided.
- Considered and rejected: C# (Dan already knows it, teaches him nothing),
  Rust/Go/TypeScript (more commercially relevant, but commercial relevance
  isn't the point).
- F# fits because (a) Dan wants to internalize FP, (b) it directly closes
  the LeoBloom audit gap, (c) it looks nice.

### BDD spec set survives

- All existing BDDs come into SonOfLeo on day one.
- They become the spec, not after-the-fact tests.
- Triage pass first: any BDD that has become implementation-shaped (likely
  from the recent changes the "upstairs" agents made) gets re-expressed as
  pure behavior before it drives any code in SonOfLeo. The rule was always
  BDD-first; if any leaked back to implementation-shape, we fix that here.
