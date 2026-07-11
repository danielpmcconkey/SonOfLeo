# Persistence Gate

**Source:** Doctrines.md, Type Validation Doctrine — The Persistence Gate

No entity may cross the persistence boundary — in either direction — without passing through `validateThenConstruct`.

## The rules

- No entity may be **written** to the persistence layer unless it was produced by VTC
- No entity may be **returned from** the persistence layer unless the read path reconstitutes it through VTC
- No entity may be **returned to the UI** unless it passed through VTC at some point in its lifecycle

## What works
- Write paths construct via VTC before persisting
- Read paths reconstitute through VTC, catching any data that has drifted out of compliance

## What doesn't
- Mapping database rows directly to record types without VTC
- Trusting that "the data was valid when we wrote it" — the read path re-validates because constraints can change between write and read
