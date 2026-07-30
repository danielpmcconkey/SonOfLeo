# Validation Layers

**Source:** the retired Type Validation Doctrine. Updated 2026-07-25: the `validateThenConstruct` function it named never existed in this codebase; layer 2 lives in component smart constructors and entity `create`.

Validation is layered. Each layer builds on the one below it. The layers are not optional — all four apply, in combination, before persistence.

## The layers

1. **Type definitions** — single-value constraints at the compiler level (e.g., `AccountCode` can't exceed 10 chars, `Money` can't have sub-cent precision). Unbypassable by design.
2. **Smart constructors** — per-field validation in component `create`/`fromString`; cross-field constraints within a single record in the entity's construction path (e.g., type/subtype combinations via `AccountSubtype.validFor`, activeEnd >= activeBegin).
3. **Composite validation** — relationships between components of a composite type (e.g., JournalEntry's minimum line count, balance invariant, cross-component consistency). Ordering of component vs composite validation is domain-determined, not doctrine-determined.
4. **Operation functions** — state-dependent constraints at the operation boundary (e.g., account is active, fiscal period is open). These depend on external state that changes independently of the record.

## What works
- Constructors validate the record's shape; operation functions validate the world's state; both pass before persistence
- Composite validation ordering follows domain needs, not a rigid sequence

## What doesn't
- Skipping a layer because "we checked it elsewhere"
- Putting state-dependent checks (layer 4) inside constructors (layer 2) — construction has no access to external state, and `reconstitute` in particular runs inside an open reader and can make no DB calls
