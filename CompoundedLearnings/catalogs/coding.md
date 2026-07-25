# Coding

F# idioms, naming, temporal handling, constructor discipline, and conventions for the SonOfLeo codebase. Read this catalog before writing or reviewing any F# code.

| Concept | Article | Read when... |
|---|---|---|
| Validation layers | `articles/coding/validation-layers.md` | Adding validation logic and deciding where it belongs (type def, constructor, composite, or operation) |
| Validation location | `articles/coding/validation-location.md` | Considering whether a validation check should be SQL or F# |
| Descriptive naming | `articles/coding/descriptive-naming.md` | Naming a function, variable, or smart constructor — especially choosing between `create` and `fromString` |
| NodaTime discipline | `articles/coding/nodatime-discipline.md` | Working with any temporal value in the application layer |
| Temporal persistence | `articles/coding/temporal-persistence.md` | Writing a migration or persistence function that involves temporal columns |
| Temporal arithmetic | `articles/coding/temporal-arithmetic.md` | Performing arithmetic on instants or dates, or converting between them |
| Money type enforcement | `articles/coding/money-type-enforcement.md` | Creating, persisting, or accepting a monetary value |
| Money arithmetic boundaries | `articles/coding/money-arithmetic-boundaries.md` | Performing any arithmetic on money — especially multiplication, division, splitting, or rounding |
| Field update pattern | `articles/coding/field-update-pattern.md` | Writing an update function that needs to distinguish "no change" from "set to value" |
