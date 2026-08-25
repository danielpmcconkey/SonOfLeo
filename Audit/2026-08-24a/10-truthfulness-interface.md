# InterfaceBridge-Auditor

## CONTRA-IB-1 — contradiction
- **Location:** Src/InterfaceBridge/InterfaceContracts/IngestionContracts.fs (line 47), Src/InterfaceBridge/BoundaryConverters/IngestionFieldConverters.fs (line 54), REQ-STG-2.7
- **Summary:** StageEntryHeaderReturn.status is string option, contradicting REQ-STG-2.7 which says staged entry status cannot be null.
- **Resolution:** dan-decides

REQ-STG-2.7 states: "Staged entry status cannot be null. Must be one of the values defined in section 4." However, the boundary return contract StageEntryHeaderReturn (IngestionContracts.fs line 47) declares `status: string option`, and the converter in IngestionFieldConverters.fs line 54 propagates None silently via `let status = model |> StageEntryHeader.currentStatus |> Option.map StagedEntryStatus.toString`. The model type StageEntryHeader.fs line 23 defines `currentStatus: StagedEntryStatus option` with the documented rationale that it is a CTE-derived read cache. The CTE uses a LEFT JOIN against the audit trail (StageEntryHeader.fs line 252: `left join latest_statuses on e.unique_id = latest_statuses.entry_id`), which can return null. Per REQ-STG-3.9, every entry receives an initial audit record at ingestion, so the CTE should always return a value. But neither the model type, the converter, nor the return contract enforces this guarantee. If the invariant is broken (missing audit record, CTE bug), the system silently returns null status to the caller instead of failing with a typed error. The interface contract is owned by InterfaceBridge (per the type-taxonomy learning), so the decision to expose status as optional is within this layer's authority.

**Action:** Either (a) add a guard in the boundary converter that produces a typed AppError when currentStatus is None, enforcing REQ-STG-2.7 at the boundary, or (b) change the model's currentStatus to a non-optional type and fail at reconstitution if the CTE returns null, or (c) update REQ-STG-2.7 to acknowledge that status is derived and may be absent for a header in an intermediate construction state. Option (a) is the narrowest change scoped to InterfaceBridge.

**Why:** An invariant stated in the spec but not enforced by the type system or the boundary layer can be silently violated. When the JSON response contains null for status, the caller receives data that contradicts the spec's guarantee, potentially causing downstream logic errors in scripts or tooling that switch on status without null-checking a field the spec says will always be present.

---
