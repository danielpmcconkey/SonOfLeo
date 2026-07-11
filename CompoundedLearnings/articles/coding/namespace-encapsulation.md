# Namespace Encapsulation

**Source:** Doctrines.md, Type Validation Doctrine — Namespace Encapsulation

Types are grouped into namespaces by domain slice — the natural boundary of types that need to collaborate. F#'s `private` keyword on record types scopes to the namespace, so only code within the same slice can construct the record. Types in one slice cannot bypass another slice's `validateThenConstruct`.

## What works
- One namespace per domain slice (e.g., `Model.Ledger.Accounts`, `Model.Ledger.Periods`, `Model.Ledger.Journaling`)
- A slice may contain multiple types when they form a composite (e.g., JournalEntry and its header, lines, references, comments)
- The compiler enforces the constructor boundary across slices; within a slice, code review and agent audit enforce that only VTC writes record literals

## What doesn't
- Putting unrelated types in the same namespace to share construction access
- Reaching across slices to construct another slice's records directly

## Example
`Model.Ledger.Journaling` contains JournalEntry, JournalEntryHeader, JournalEntryLine, ExternalReference, and JournalComment — all part of one composite. Code in `Model.Ledger.Accounts` cannot construct any of these directly; it must go through their public API.
