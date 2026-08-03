# Hobson-SonOfLeo-Audit

## GUARD-1 — enforcement-gap
- **Location:** Tests/README.md (assertion-shape section) + 73 instances across Tests/
- **Summary:** No mechanical check enforces the assertion-shape standard; 73 existing tests use the forbidden Result.isError/Result.isOk patterns that BD will replicate.
- **Resolution:** fix-code

Tests/README.md explicitly states: 'Never Result.isError. Never string-matching on error text.' and prescribes matching the typed DU case with both escape arms. However, 73 test assertions across Tests.Isolated and Tests.Integrated use Assert.True(Result.isError result) or Assert.True(Result.isOk result). No Checks/ script enforces the assertion-shape standard. The nine existing checks cover compile order, Npgsql boundaries, clock discipline, naming conventions, REQ traceability, etc., but nothing validates how test assertions are structured. Examples: Tests/Tests.Isolated/Model/Ledger/JournalEntryComponent.fs lines 188, 193, 200 use Assert.True(Result.isError result) for REQ-JE-1.24 tests. Tests/Tests.Isolated/Model/Ledger/AccountComponent.fs has 32 such assertions. Tests/Tests.Integrated/ModelOrchestrator/JournalEntryFetching.fs line 48 also uses this pattern. The check-apperror-coverage.sh (report-only, exits 0) found 25 AppError cases unreferenced in Tests/ (e.g., JournalEntryLineNonPositiveAmount), confirming the gap: even tests that cite REQ-JE-1.24 do not reference the specific AppError case by name. When BD takes over test writing (starting now with tests, later code), it will encounter 73 existing examples of the weak pattern for every occurrence of the documented standard. Pattern replication from existing code is the primary learning signal for agents -- the 73 anti-patterns will outweigh the prose standard.

**Action:** Add a Checks/check-assertion-shape.sh that greps Tests/ for Result.isError and Result.isOk and fails the pre-commit hook when found. Separately, address the 73 existing violations -- the integrated tests generally use the correct match pattern while the isolated tests are the primary offenders.

**Why:** This is the highest-risk gap for agent-maintained code. A test using Result.isError passes regardless of which AppError case is returned. If BD refactors a validation function and changes which error it produces, the test still passes green. The wrong error propagating to production could mean the ledger rejects correct journal entries or accepts incorrect ones -- and the test suite would not catch it. The combination of (a) a documented standard, (b) no mechanical enforcement, and (c) 73 counterexamples in the codebase makes this the most likely vector for BD to introduce a plausibly-wrong edit that passes build and tests but corrupts ledger semantics.


## TRACE-1 — stale-reference
- **Location:** Specs/Behavioral/SystemWide.md, waiver table, REQ-SYS-6.1
- **Summary:** REQ-SYS-6.1 is listed as waived from testing but two tests cite it, making the waiver stale.
- **Resolution:** fix-spec

The traceability audit (Checks/check-traceability.sh) reports 'Stale waivers: waived from testing but tests exist: REQ-SYS-6.1'. SystemWide.md waiver table lists REQ-SYS-6.1 with reason 'This is a general requirement. Testing should be enforced by every individual write operation with a no-op possibility' (Dan, 2026-07-06). Two tests cite REQ-SYS-6.1: (1) Tests/Tests.Integrated/InterfaceBridge/JournalEntryRoutes.fs line 512: 'REQ-JE-4.9 REQ-SYS-6.1 UpdateExternalReference rejects no-op update' and (2) Tests/Tests.Integrated/ModelOrchestrator/JournalEntryVoiding.fs line 115: 'REQ-JE-4.6 REQ-SYS-6.1 voidJournalEntryOrchestration rejects void on already-voided entry'. The traceability system correctly identifies this as a stale waiver but does not fail on it (stale waivers are a consistency report, not a hard failure). The requirement is simultaneously classified as 'waived' and 'tested' -- both cannot be true.

**Action:** Remove REQ-SYS-6.1 from the SystemWide.md waiver table. The two existing tests satisfy the waiver's own stated intent ('testing should be enforced by every individual write operation with a no-op possibility') -- they test per-entity no-op rejections and co-cite REQ-SYS-6.1 as the general rule they instantiate.

**Why:** A stale waiver sends the wrong signal to BD. When BD reads the waiver table to plan test coverage, it sees REQ-SYS-6.1 as 'no test needed.' But tests already exist. The contradiction means BD might skip writing no-op rejection tests for new entities (believing the waiver exempts them) when the actual intent was that every entity's no-op scenario should be tested and co-cite REQ-SYS-6.1. Cleaning this up keeps the waiver table honest as BD's primary guide for which requirements need tests.


## IDIOM-1 — idiom
- **Location:** Src/ModelOrchestrator/FetchFilterAndSort.fs, line 31
- **Summary:** AccountActivityFilter.journalEntryId uses raw Guid instead of JournalEntryHeaderId, breaking the typed-wrapper pattern used by every other ID field in both filter types.
- **Resolution:** fix-code

In FetchFilterAndSort.fs, the AccountActivityFilter record has 'journalEntryId: Guid option' (line 31) while every other ID field uses a typed wrapper: accountId is AccountId option, accountParentId is AccountId option. In the same module, JournalEntryFetchFilter uses 'journalEntryHeaderId: JournalEntryHeaderId option' (line 37) for the equivalent field. The raw Guid flows through to AccountActivity.fs line 195 where it is used directly: 'filter.journalEntryId |> Option.map(fun x -> ("and je.unique_id = @je_id", { name = "@je_id"; value = UniqueId x }))'. Compare with JournalEntryOrchestration.fs line 224-225 which unwraps the typed ID: 'UniqueId(x |> JournalEntryHeaderId.value)'. The codebase establishes a strong convention of typed wrappers for all IDs (AccountId, FiscalPeriodId, JournalEntryHeaderId, JournalEntryLineId, JournalEntryCommentId, JournalEntryExternalReferenceId). This single field is the only orchestrator-level ID that breaks the pattern.

**Action:** Change AccountActivityFilter.journalEntryId from 'Guid option' to 'JournalEntryHeaderId option' and update the usage site in AccountActivity.fs to unwrap via JournalEntryHeaderId.value. Update the boundary converter that constructs this filter.

**Why:** Typed wrappers exist to prevent category errors at compile time -- passing a line ID where a header ID is expected, or a comment ID where an entry ID is expected. A raw Guid accepts any of these silently. In a codebase where an agent progressively takes over, every pattern inconsistency is amplified: BD sees this raw Guid and infers that orchestrator-level filter types may use raw primitives. When BD builds the next domain's filter types (obligations, portfolio), it replicates the weaker pattern. The type system is the single strongest guardrail in an F# codebase; undermining it in the application layer weakens the very defense that makes agent-written code safe.

