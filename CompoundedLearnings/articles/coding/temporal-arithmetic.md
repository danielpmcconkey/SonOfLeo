# Temporal Arithmetic

**Source:** Temporal.md, Instants / Dates / Calendar Periods

Instants and dates are separate algebras with separate arithmetic rules. Do not mix them.

## Instants

- Arithmetic may use hours, minutes, or seconds
- Arithmetic may **never** use years or months — those require calendar conventions
- Arithmetic using days is **discouraged** — prefer hours, minutes, or seconds where practical
- Precision: the system must reconstitute any instant to seconds precision at minimum

## Dates

- Arithmetic may use years, months, or days — calendar units only
- Calendar periods are discriminated unions whose contents may vary by domain
- Calendar period arithmetic always uses standard NodaTime library functions

## Instant-to-date conversion

- Anchor to US Eastern Time (NYC). Example: 2026-07-06 02:00 UTC -> 2026-07-05 because it was still July 5, 10PM in NYC
- Conversion should be **rare** — very few, very deliberate points in the code. Always question whether you need it
- Centralize through the Calendar module's `today()` function (or equivalent). Don't scatter conversion logic

## What doesn't
- Adding months to an instant
- Subtracting days from an instant when hours would work
- Ad-hoc instant-to-date conversion outside a centralized module
