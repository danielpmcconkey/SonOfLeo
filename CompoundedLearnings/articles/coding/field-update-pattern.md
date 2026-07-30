# Field Update Pattern

**Source:** Specs/Archive/Decisions.md, 2026-06-08

`FieldUpdate<'a>` is a discriminated union with two cases: `NoChange` and `SetTo of 'a`. There is no `Clear` case.

## Rationale

In traditional multi-field update functions, `None` is ambiguous — it could mean "I don't want to change this field" or "I want to clear a nullable field." `FieldUpdate` forces the caller to state their intent explicitly: `NoChange` means don't touch it, `SetTo value` means set it.

Every entity update function should accept `FieldUpdate` parameters — even when the entity has no nullable fields. The pattern is about explicit caller intent, not just nullable disambiguation.

## Usage

- `NoChange` — the update does not touch this field
- `SetTo value` — the field is set to this value
- `SetTo None` — clears a nullable field (where `'a` is `'b option`)

## Why no Clear

Nullability lives in the type parameter. `SetTo None` clears a nullable field; `SetTo someValue` sets it. A separate `Clear` case would make "clear a NOT NULL field" representable at the type level, shifting the error from compile time to runtime.
