# Type Taxonomy

**Source:** Codebase observation, 2026-07-11; Dan's clarification on domain primitives. Updated 2026-07-25 for the July refactor.

The system has distinct kinds of types, each with different construction rules and responsibilities.

## Domain primitives

Single-value validated wrappers that serve as building blocks for entity and composite types. `AccountId`, `AccountName`, `AccountCode`, `Money`. Created via `create` or `fromString` (see descriptive-naming article).

Domain primitives exist to serve composites — don't invent a new one unless it's needed as a component of an entity or composite type. DU enumerations (`AccountType`, `AccountSubtype`, `LineType`) are a subset: fixed-case discriminated unions constructed via `fromString`.

## Entity types

Private records with a same-named companion module providing accessors, `create` (over already-validated component types), and persistence. `Account`, `FiscalPeriod`. These are what `Specs/Definitions.md` (Entity) defines. Owned by domain modules in `Model/`.

## Composite types

Multi-part entities composed and validated at the orchestrator. `JournalEntry` (header + lines + references + comments) — the orchestrator enforces collection-level rules (≥2 lines, debits = credits). The composite and its components live in the same namespace slice.

## Component types

Parts of a composite. `JournalEntryHeader`, `JournalEntryLine`, `JournalEntryExternalReference`, `JournalEntryComment`. Each has its own module with smart constructors. They participate in composite validation when a full composite is created, but may also be created, fetched, or modified independently (e.g., adding a comment to an existing JE).

## Interface contracts

DTOs at the CLI boundary, owned by `InterfaceBridge`. These use primitives (string, decimal, Guid, LocalDate, Instant) — not domain types. They validate *shape only* and translate between the outside world and the domain layer via boundary converters.

Each UI operation gets its own independent contract — think of them like Swagger docs, one per endpoint. Return types may be shared when operations return the exact same shape (e.g., fetch-by-parent and fetch-by-type both return `AccountReturn`). Input types are never shared across semantically different operations, even when they happen to have the same primitive shape (e.g., a string input for "fetch JE by external reference" is not the same contract as a string input for "fetch account by name").
