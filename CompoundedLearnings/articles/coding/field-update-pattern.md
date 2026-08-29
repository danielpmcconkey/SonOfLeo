# Field Update Pattern

**Source:** Specs/Archive/Decisions.md, 2026-06-08. Granularity section added 2026-08-28 from
the `CashFlow.Payment`/`CashFlow.Invoice` CRUD sessions.

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

## Choosing granularity: one bundled field, or several independent ones

When a value spans multiple DB columns (a DU that decomposes, or a record), `<Entity>FieldUpdates` can expose it as one `FieldUpdate<TheWholeThing>` or split it into several independent `FieldUpdate<...>` fields — one per sub-part. The right choice depends on whether the sub-parts are independently *meaningful* in the real workflow, not on how they happen to be stored.

Ask: do these sub-parts get set or cleared at different times, for different reasons, in actual use? If yes, split — bundling would force every update to resupply parts that aren't changing, which is real lost control, not just an API preference. If the sub-parts are only ever meaningful together (a tagged variant's payload, a schedule's components), keep them bundled — splitting would let the caller write nonsensical partial states.

**Split, from `CashFlow.Payment`:** `transactionPointer` (`journal_entry_header_id`/`stage_entry_header_id`) became two independent `FieldUpdate` fields. A payment is staged first and posted later; the stage id must survive the promotion to posted, so `updateDb` needs to be able to set one column without touching the other.

**Split, from `CashFlow.Invoice`:** the life-cycle state (`invoiceState`/`paymentState`/`postedState`/`blocker`) became four independent fields — these advance on unrelated schedules (payment state and posted state move independently; a blocker sets and clears on its own timeline).

**Bundled, from `CashFlow.Invoice`:** `Blocker` (`blocker_state` + `blocker_note`) stayed one `FieldUpdate<Blocker option>` — the note has no meaning without knowing which case it belongs to, so the two columns are only meaningful as a pair. Same reasoning as `CashFlow.MasterAgreement`'s `Cadence` (5 columns, one `FieldUpdate<Cadence>`): a lone `WeekDay` doesn't mean anything without the rest of the cadence shape.

When a value spans multiple columns, decide this explicitly rather than defaulting to whichever shape the entity type happens to use for reads (`Payment.transactionPointer` is one field for reading, but two independent update targets — the read shape and the update shape don't have to match).
