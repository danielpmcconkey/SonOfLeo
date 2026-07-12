# Descriptive Naming

**Source:** Doctrines.md, Naming Doctrine; Naming.md

Names describe what they do, precisely and without abbreviation. We do not pay by the character.

## Functions

`constructNewAndSaveToDbUsingParentCode` is preferred over `create`. Clarity at the call site matters more than brevity in the definition.

### `create` vs `fromString` (non-entity types only)

These apply to value types and wrapper types — not entities (see `Specs/Definitions.md`, Entity). Entity types have their own construction naming rules (still being refined).

Does the type *wrap* the input? Use `create` (e.g., `AccountName.create`, `AccountActivityPeriod.create`). Does the input merely *name* one of a fixed set of cases? Use `fromString` (e.g., `AccountType.fromString`, `AccountSubtype.fromString` — parsing an enumeration's label to a DU case). `create` scales to multi-arg constructors; `fromString` is the honest name for parsing an enumeration label. Don't unify them.

## Variables

Variables must be obviously named — a reader should never trace a binding to understand what it holds. Single-letter names and cryptic abbreviations are prohibited except inside simple, short lambda expressions where the entire flow is graspable at a glance (e.g., `fun x -> x + 1`, `List.map (fun a -> Account.uniqueId a)`).
