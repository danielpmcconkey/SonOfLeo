# Money Type Enforcement

**Source:** Money.md; Decisions.md 2026-06-11

The system enforces a specific `Money` type any time it represents money — an amount denominated purely in currency (see `Specs/Definitions.md`, Money). Money is the singularly most important concept to get right.

## Representation

- **F# application layer:** the `Money` type. The underlying primitive is `decimal`, but application code deals in `Money` — unwrapping to `decimal` only at boundaries or when multiplicative operations require it (see money-arithmetic-boundaries)
- **Postgres persistence layer:** `numeric(12,2)`
- **Any other primitive or column type is prohibited** for representing money amounts

## Currency and precision

- All Money is assumed to be denominated in USD. This system carries no currency indicator in persistence or code. Therefore, any system bringing external Money values into the system must first convert to USD if otherwise denominated
- All Money at penny precision in all layers — no fractions of pennies
