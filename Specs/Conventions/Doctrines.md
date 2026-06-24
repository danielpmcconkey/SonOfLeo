# Doctrines

How we build this system. Not requirements (what the system must do) but engineering doctrine (how we build it, how we reason about it, and what we enforce in every session). These govern the behavior of every developer and every AI agent working in this codebase.


## 1. Type Validation Doctrine

### The Constructor Rule

Every entity type has exactly one private function called `validateThenConstruct`. It takes primitives, validates every single-field and cross-field constraint, and returns `Result<T, string>`. No record literals may appear anywhere outside `validateThenConstruct`. Every other function that needs an instance of that type — new creation, reconstitution from persistence, assembly for any purpose — must call `validateThenConstruct`.

### Namespace Encapsulation

Types are grouped into namespaces by domain slice — the natural boundary of types that need to collaborate (e.g., `Model.Ledger.Accounts`, `Model.Ledger.Periods`, `Model.Ledger.Journaling`). The `private` keyword on record types scopes to the namespace, meaning only code within the same slice can construct the record. Types in one slice cannot bypass another slice's `validateThenConstruct`. A slice may contain multiple types when those types form a composite (e.g., JournalEntry and its header, lines, references, and comments).

### Validation Layers

Validation is layered. Each layer builds on the one below it:

1. **Type definitions** enforce single-value constraints at the compiler level (e.g., `AccountCode` can't exceed 10 chars, `Money` can't have sub-cent precision). These are unbypassable by design.

2. **`validateThenConstruct`** enforces cross-field constraints within a single record (e.g., type/subtype combinations, activeEnd >= activeBegin). The namespace boundary ensures all construction routes through this function.

3. **Composite validation** applies to types composed of other types (e.g., JournalEntry is composed of a header, lines, references, and comments). The composite's `validateThenConstruct` validates the relationships between components — minimum counts, balance invariants, cross-component consistency. The ordering of component validation versus composite validation is determined by the domain, not by doctrine. Some composites must validate the collective before constructing the individuals; others validate components first. The doctrine requires only that both happen before persistence.

4. **Operation functions** validate state-dependent constraints at the operation boundary (e.g., account is active, fiscal period is open). These depend on external state that can change independently of the record and cannot be encoded in types. `validateThenConstruct` validates the record's shape. The operation function validates the world's state. Both must pass before persistence.

### The Persistence Gate

No entity may be written to the persistence layer unless it was produced by `validateThenConstruct`. No entity may be returned from the persistence layer unless the read path reconstitutes it through `validateThenConstruct`. No entity may be returned to the UI unless it passed through `validateThenConstruct` at some point in its lifecycle.

### Why Convention, Not Compiler

F#'s `private` keyword on record types scopes to the enclosing namespace within a project. By grouping types into slice-level namespaces, we make the compiler enforce the constructor boundary across slices. Within a slice, the doctrine that only `validateThenConstruct` may write record literals is enforced by code review and agent audit. The doctrine exists because the F# language does not support file-scoped privacy on record types.


## 2. Naming Doctrine

Function names describe what they do, precisely and without abbreviation. We do not pay by the character. `constructNewAndSaveToDbUsingParentCode` is preferred over `create`. Clarity at the call site matters more than brevity in the definition.

The one reserved name is `validateThenConstruct` — it always means "the single private constructor that validates and assembles a record from primitives."

### Variable Naming

We do not pay by the keystroke. Variables must be obviously named — a reader should never have to trace a binding to understand what a variable holds. Single-letter names and cryptic abbreviations are prohibited except inside simple, short lambda expressions where the entire flow is graspable at a glance (e.g., `fun x -> x + 1`, `List.map (fun a -> Account.uniqueId a)`). Anywhere a variable persists across multiple lines or is referenced more than once, it gets a real name.
