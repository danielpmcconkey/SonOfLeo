# Check the Schema Before Questioning Waivers

**Source:** Audit 2026-07-06a — WAV-FP-1 overrule, skill improvement item #33a

Before flagging a waiver as unsound, verify the claim against the DB schema (NOT NULL constraints, types, FKs) and the F# type system — not just the spec prose.

## Example

WAV-FP-1 questioned the fiscal period key's null waiver, arguing that strings are nullable in F#/.NET so "impossible to represent" doesn't hold. The DB column is `NOT NULL`. The waiver is sound at the persistence level regardless of the language-level nullability of `string`.

## The rule

A waiver that says "impossible state to represent" may be justified by any combination of: the F# type system (value types, DUs), the DB schema (NOT NULL, CHECK, FK), or the smart constructor. Check `DbMigrations/` for the relevant table definition and the F# type before concluding the waiver is unsound.
