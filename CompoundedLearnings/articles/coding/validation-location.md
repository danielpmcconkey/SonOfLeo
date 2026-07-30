# Validation Location

**Source:** the retired Validation Location Doctrine (Doctrines.md removed 2026-07-30; this article is its salvage)

Validation logic belongs in F#, not in SQL. The efficiency cost of pulling data into memory and processing it through domain types is accepted as the price of keeping business logic in one place.

## The exception

A validation may go directly to SQL only when **all** of the following hold:

1. It is a **pure data question** — the comparison involves no validated types, just dates, counts, or existence. The F# alternative would reconstruct domain objects only to discard them.
2. **At least one practical cost is present** — implementing through F# would add exceptional complexity (new fetch functions, bespoke infrastructure) that would not otherwise be reused, OR would add significant performance degradation.

If the pure data question can be answered through existing F# infrastructure without meaningful complexity or performance cost, it stays in F#. SQL is not the default escape hatch for inconvenience.

## Examples

- **Zero balance check** (F#): `confirmZeroBalanceBeforeDeactivation` fetches the non-voided JournalEntryLines and sums them through `JournalEntryLine.sumLinesByType` and `Money.subtractVal1FromVal2`. Domain types are load-bearing — Money's precision rules do real work. Stays in F#.
- **No journal entries after deactivation date** (SQL): existence check across Account -> JournalEntryLine -> JournalEntry. The answer is yes/no. The comparison is `LocalDate > LocalDate` — identical in F# and Postgres. No domain type adds value. Goes to SQL.
