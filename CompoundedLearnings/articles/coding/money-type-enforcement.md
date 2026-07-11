# Money Type Enforcement

**Source:** Money.md; Decisions.md 2026-06-11

The system enforces a specific `Money` type any time it represents money. Money is the singularly most important concept to get right.

## Representation

- **F# application layer:** `decimal`
- **Postgres persistence layer:** `numeric(12,2)`
- **Any other primitive or column type is prohibited** for representing money amounts

## Currency and precision

- All Money is denominated in USD — no currency indicator in persistence or code
- All Money at penny precision in all layers (interface, application, persistence)
- No layer tracks or persists fractions of pennies
- Foreign transactions enter the ledger as the USD amount the FI actually settled — no FX revaluation

## What doesn't
- `float`, `double`, `int`, or any non-`decimal` type for money in F#
- `money`, `real`, `integer`, or column types other than `numeric(12,2)` in Postgres
- Sub-cent precision in the ledger (that belongs in price/quantity types in their own domains)
