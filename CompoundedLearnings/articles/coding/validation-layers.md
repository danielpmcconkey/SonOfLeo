# Validation Layers

**Source:** the retired Type Validation Doctrine. Updated 2026-07-25: the `validateThenConstruct` function it named never existed in this codebase. Rewritten 2026-10-03 (audit 2026-10-03a, #185/#200/#311) to match Dan's principle "Infallible Create, Orchestrator Validates" (`Architecture/SonOfLeo.archimate`), which the previous version contradicted.

Validation is layered. Each layer builds on the one below it. The layers are not optional — all of them apply, in combination, before persistence.

## The layers

1. **Type definitions** — single-value constraints at the compiler level (e.g., `Money` can't have sub-cent precision). Unbypassable by design.
2. **Component smart constructors** — per-value validation in a component's `create`/`fromString`, returning a `Result` (e.g., an account code's length, a memo's length and blankness). A value that exists has passed.
3. **Entity `create` is infallible.** An entity-level `create` accepts all inputs and never fails: no `Result`, no exception. It assembles already-validated components; it does not check how they relate to each other.
4. **The orchestrator validates** — everything about a whole entity or composite on the write path: cross-field constraints within one record (e.g., type/subtype combinations, activeEnd >= activeBegin), composite rules (a journal entry's minimum line count and balance), and state-dependent rules (the account is active, the fiscal period is open). Ordering of component vs composite validation is domain-determined, not doctrine-determined.
5. **`reconstitute` re-checks what needs no DB read.** On the read path (REQ-SYS-2.1), `reconstitute` validates the cadence and every cross-field constraint that can be decided from the row alone, and returns a typed error if one fails. Constraints that need a DB read stay unchecked on read: persistence is trusted (Dan, 2026-10-03).

## What works
- Components validate values; orchestrators validate entities and the world's state; both pass before persistence
- `reconstitute` builds from the row it is given and nothing else — no DB calls, no clock, no config — so it checks only row-local rules
- Composite validation ordering follows domain needs, not a rigid sequence

## What doesn't
- Making an entity `create` return a `Result` or throw to enforce a cross-field rule. That rule belongs to the orchestrator (write) and `reconstitute` (read).
- Skipping a layer because "we checked it elsewhere"
- Putting state-dependent checks inside construction — construction has no access to external state, and `reconstitute` in particular runs inside an open reader and can make no DB calls
