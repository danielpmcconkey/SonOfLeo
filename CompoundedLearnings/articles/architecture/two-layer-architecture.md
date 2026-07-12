# Two-Layer Architecture

**Source:** Decisions.md, 2026-06-06

Domain modules own their entity end-to-end: type definition, validation, and persistence. Orchestration composes across modules. This is two layers, not the C# three-layer split (controller / service / repository).

## Why

F# smart constructors eliminate the dumb-model layer that ORMs force on C#. There is no separate "data layer" — persistence is a concern inside each domain module, not a cross-cutting layer beneath it.

## The two layers

1. **Domain modules** (`Model/`) — each module owns one entity or composite: its types, VTC, and persistence (read/write). A module may call Utilities but not another domain module.
2. **Orchestration** (`ModelOrchestrator/`) — composes across domain modules for operations that need data from more than one. No business logic of its own — only coordination.
