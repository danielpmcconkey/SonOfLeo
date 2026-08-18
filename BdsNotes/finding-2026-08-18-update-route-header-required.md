# Finding — UpdateStageEntry cannot perform a line-only edit

Logged 2026-08-18 while implementing the REQ-STG-6.1 / REQ-STG-6.2 route test.
Recording only; no Src change made.

## What happens

`ModelOrchestrator.StageEntryOrchestration.updateStageEntry` calls
`StageEntryHeader.updateDb` unconditionally
(`Src/ModelOrchestrator/StageEntryOrchestration.fs:452`). `updateDb` builds its SET
clause from the six header `FieldUpdate` values and errors when all six are `NoChange`
(`Src/Model/DataIngestion/StageEntryHeader.fs:248`).

So an `UpdateStageEntryInput` carrying line updates and an all-`NoChange` header is
rejected with:

```
Updating the StageEntryHeader record failed because at least one updatable parameter must be set.
```

Reproduced through the route (`Ingestion` / `UpdateStageEntry`), not just the orchestrator.

## Why it matters

REQ-STG-6.1 says the system must provide a means for an operator to assign or override
the `account_code` on a staged line. The means exists, but it cannot be used on its own —
the caller must also change a header field in the same call, even when nothing about the
header is changing.

## What the test does instead

`Tests/Tests.Integrated/InterfaceBridge/IngestionRoutes.fs` covers REQ-STG-6.1 / 6.2 with
two calls the route does accept:

1. line `account_code` override plus `status = SetTo "Reviewed"` — the ordinary operator
   review flow.
2. line `account_code` override plus `description` set to the value it already holds,
   `status = NoChange` — proves REQ-STG-6.2's "does not infer or auto-assign status."

Call 2's redundant `description` exists only to satisfy the constraint above. If the
constraint goes away, that field should come out of the test.
