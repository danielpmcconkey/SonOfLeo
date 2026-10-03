# DU Case Collision Across `open`s

**Source:** `CashFlow.Payment` CRUD session, 2026-08-28.

When two `open`ed modules each declare a DU case with the same name, F# resolves the bare case
name to whichever module was opened *last* — silently. It does not error, and it does not merge
the two by type inference from context the way overload resolution sometimes appears to. A
pattern match against the "wrong" case fails with a confusing type error that names the
unintended type, not the intended one.

## What works

Fully qualify the case by its owning module wherever a collision is possible:
`CashFlowComponent.Posted` instead of bare `Posted`. Do this defensively any time a match
arm's case name is a common English word or short verb (`Posted`, `Staged`, `Active`,
`Pending`) — these are exactly the names likely to be reused across unrelated DUs in different
slices.

## What doesn't

Don't assume the compiler will catch a same-name collision, and don't assume `open` order is
neutral. It isn't: the later `open` wins for any bare identifier both modules define.

## Example

`Business.FinancialServices.CashFlow.CashFlowComponent.TransactionPointer` has cases
`Posted of JournalEntryLineId` and `Staged of StageEntryLineId` (header IDs when this was
written). `Business.FinancialServices.DataIngestion.StageEntryComponent`
separately declares `StagedEntryStatus` with a no-arg `Posted` case. A file that does
`open ...CashFlow.CashFlowComponent` followed by `open ...DataIngestion.StageEntryComponent` and
then writes a bare `| Posted journalEntryLineId -> ...` match arm gets misresolved: the compiler binds
`Posted` to `StagedEntryStatus.Posted` (the later-opened module), which takes no arguments, and
reports a type mismatch that never mentions `TransactionPointer` at all — confusing to debug
from the error alone. The fix, applied in the CashFlow `Payment.fs` (then under `Src/Model/`), was fully qualifying
both arms as `CashFlowComponent.Posted`/`CashFlowComponent.Staged`.

This is worth rechecking any time a new file opens both `CashFlowComponent` and
`StageEntryComponent` (or any two component files) together — the specific `Posted` collision
above will recur verbatim, and a similarly generic case name in a future component file could
introduce a new one.
