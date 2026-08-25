# corner-painting-reviewer

## ARCH-CLI-1 — architecture
- **Location:** Src/SonOfLeoCli/SonOfLeoCli.fsproj
- **Summary:** SonOfLeoCli.fsproj carries project references to Model and ModelOrchestrator that no code file in the project uses, opening a direct path past the InterfaceBridge boundary layer.
- **Resolution:** fix-code

SonOfLeoCli.fsproj declares ProjectReference for both Model.fsproj and ModelOrchestrator.fsproj. However, SonOfLeoCli/Program.fs opens only InterfaceBridge.Routes.*, InterfaceBridge.CommandRoute, and Utilities.AppError. No source file in the SonOfLeoCli project imports anything from Model or ModelOrchestrator. The references are compile-time dead weight that tells any reader of the project file (human or agent) that these assemblies are legal dependencies from the CLI layer. Dan's statement explicitly identifies agentic development as a near-term goal: agents examining the .fsproj will see the references and may bypass InterfaceBridge to call ModelOrchestrator directly. That first bypass sets a precedent that makes InterfaceBridge optional rather than mandatory, eroding the boundary that separates interface-layer concerns (JSON serialization, field conversion, route dispatch) from domain logic (validation, orchestration, persistence).

**Action:** Remove the two unused ProjectReference entries (Model.fsproj and ModelOrchestrator.fsproj) from Src/SonOfLeoCli/SonOfLeoCli.fsproj. The project should reference only InterfaceBridge.fsproj, which transitively provides everything the CLI needs.

**Why:** The boundary layer only works if the dependency graph enforces it. Unnecessary project references are the architectural equivalent of leaving a door propped open -- the lock exists but nobody has to pick it. When agents start writing routes, the .fsproj is the first file they inspect for what they can use. Two extra references turn an enforced boundary into a suggestion.

---

## ARCH-TXN-1 — architecture
- **Location:** Src/Model/DataIngestion/StageEntryHeader.fs (insertNewToDb line 133, updateDb line 348)
- **Summary:** Multi-write staging functions accept any Context (including NoTransaction) without asserting that a transaction is present; atomicity is enforced by route-level convention alone.
- **Resolution:** dan-decides

StageEntryHeader.insertNewToDb performs two sequential writes: (1) INSERT into ingestion.staged_entry, then (2) INSERT into ingestion.staged_entry_audit via updateHeaderStatus. StageEntryHeader.updateDb can perform a status audit INSERT and a header field UPDATE within the same call. Both functions accept Context.Context, which may or may not carry a real DbTransaction. With NoTransaction, each SQL statement opens its own connection and auto-commits independently. If the first write commits and the second fails, the staged entry is left in an inconsistent state (e.g., a header row with no audit trail, or an audit trail recording a transition but the corresponding header field update missing). Dan's statement asked the audit team to verify: 'I believe all of the current routes that update status *do* use such a mechanism, but I haven't actually checked.' Verified: every current route that calls updateHeaderStatus or insertNewToDb does so through runCommandRouteAndAutoCompleteTransaction or runCommandRouteAndAutoRollback (IngestionRoutes.ingestRawEntries line 27, updateStageEntry line 136, post line 200-201). Dan's belief is correct -- all current call paths are transactional. The structural concern is that the function signatures are indistinguishable from single-write functions (both take Context.Context -> ... -> Result<unit, AppError>). Simpler routes like newClassificationRule and createNewSource correctly use NoTransaction for their single-write operations. An agent adding a new route that touches staging status might follow that pattern without realizing the multi-write nature of the functions it calls.

**Action:** This is a design decision for Dan. Options include: (a) add a runtime assertion at the top of insertNewToDb and updateDb that the Context's DbTransaction is not None, so the failure is loud and immediate rather than a silent partial write; (b) introduce a distinct type or wrapper that communicates 'this operation requires a transaction' at the type level; or (c) accept the convention-based approach and document the requirement in CompoundedLearnings for agent consumption.

**Why:** Dan identified agentic development as the path forward for expanding the system. Agents read function signatures and patterns, not institutional knowledge. The current signatures give no signal that these functions require transactional wrapping. The first agent-written route that updates staging status without a transaction will produce a data corruption bug that manifests only on partial failure -- the hardest kind to reproduce and diagnose.

---
