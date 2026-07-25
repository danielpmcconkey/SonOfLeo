# Orchestration Layer

**Source:** Decisions.md, 2026-06-11; Dan's clarification 2026-07-11

The orchestration layer (`ModelOrchestrator/`) is for any function that coordinates multiple distinct activities. Domain modules own single-concern operations on their own types; orchestration composes.

## When a function belongs in orchestration

- It needs data from more than one domain module (e.g., deactivating an account requires checking journal entries)
- It coordinates multiple distinct steps even within one domain (e.g., constructing a new entity AND saving it to the database — two activities)

The test is not "does it cross domain boundaries?" but "does it orchestrate?" If a function does more than one thing, it belongs here — even if both things live in the same domain module.

## F# compile order

F# compile order makes cross-domain composition structural rather than optional. A module cannot reference another module that appears later in the build. This means cross-domain functions physically cannot live in a single domain module — they must live above both.

## Example

`deactivateAccount` started in the Account module. When it needed JournalEntry data for its checks (REQ-AC-4.4, REQ-AC-4.6), it moved to `ModelOrchestrator/AccountDeactivation.fs`. The `constructNewAndSaveToDb` functions follow the same principle — they orchestrate construction + persistence (PATTERNS.md P4.4).

*Post-refactor note (2026-07-25): consistent with PATTERNS.md P1.1 — ModelOrchestrator also owns cross-entity business validation and read-model types; `InterfaceBridge` now sits above it as the boundary layer.*
