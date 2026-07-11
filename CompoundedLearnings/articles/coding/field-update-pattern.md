# Field Update Pattern

**Source:** Decisions.md, 2026-06-08

`FieldUpdate<'a>` is a discriminated union with two cases: `NoChange` and `SetTo of 'a`. There is no `Clear` case.

## Why no Clear

Nullability lives in the type parameter. `SetTo None` clears a nullable field; `SetTo someValue` sets it. Attempting to clear a NOT NULL field is unrepresentable at the type level (`SetTo` of a non-option type has no `None` to pass), rather than merely invalid at runtime.

## What works
- `NoChange` — the update does not touch this field
- `SetTo value` — the field is set to this value
- `SetTo None` — clears a nullable field (where `'a` is `'b option`)

## What doesn't
- A `Clear` case — it makes "clear a required field" representable, shifting the error from compile time to runtime
- Representing "no change" by passing the current value — that conflates "didn't touch" with "set to same," which matters for audit trails and change detection
