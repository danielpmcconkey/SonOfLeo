# idiom-auditor

## IDIOM-NOOP-1 — idiom
- **Location:** Src/ModelOrchestrator/JournalEntryCommentOrchestration.fs, line 104
- **Summary:** updateComment emits JournalEntryReferenceUpdateNoOp instead of the purpose-built JournalEntryCommentUpdateNoOp, producing the wrong error message on a no-op comment update.
- **Resolution:** fix-code

In JournalEntryCommentOrchestration.updateComment (line 103-106), when both commentUpdate and secondaryIdUpdate are NoChange, the function emits Error(JournalEntryReferenceUpdateNoOp). The error case JournalEntryCommentUpdateNoOp exists in AppError.fs (line 92) with its own message ('Updating the Journal Entry Comment record failed...') but is never referenced anywhere in Src/ or Tests/. The user sees 'Updating the Journal Entry Reference record failed...' when the operation was actually a comment update. This appears to be a copy-paste from JournalEntryExternalReferenceOrchestration.updateFiAndReferenceText (line 80), which correctly uses JournalEntryReferenceUpdateNoOp.

**Action:** Replace JournalEntryReferenceUpdateNoOp with JournalEntryCommentUpdateNoOp at JournalEntryCommentOrchestration.fs line 104.

**Why:** In an FP codebase with a discriminated union error type, each error case is a named, semantic signal. Using the wrong case defeats the purpose of having separate cases: pattern-matching callers will match on the wrong branch, and the error message misleads the user. The DU exists precisely to make error identity a compiler-level fact, not a string-comparison accident. An unreferenced DU case (JournalEntryCommentUpdateNoOp) paired with a misused one (JournalEntryReferenceUpdateNoOp in the wrong context) is the DU equivalent of a dead code smell that points directly at a wiring bug.


## IDIOM-GUID-1 — idiom
- **Location:** Src/ModelOrchestrator/FetchFilterAndSort.fs, line 31
- **Summary:** AccountActivityFilter.journalEntryId is typed as Guid option instead of JournalEntryHeaderId option, breaking the domain-typed ID discipline every other field in the type follows.
- **Resolution:** fix-code

AccountActivityFilter has 10 fields. Nine use domain types (AccountId option, TemporalFilter option, JournalEntrySource option, AccountType option, AccountSubtype option, AccountId option, Money option, JournalEntryDescription option, bool). The tenth -- journalEntryId: Guid option -- uses a raw System.Guid. By contrast, the sister type JournalEntryFetchFilter (line 37 of the same file) correctly types its equivalent field as journalEntryHeaderId: JournalEntryHeaderId option. The raw Guid flows from AccountContracts.AccountActivityFilterInput.journalEntryId (Guid option) through OrchestrationConverters line 71 (journalEntryId = input.journalEntryId) straight into AccountActivity.fetchFiltered line 195 (UniqueId x) with no domain-type wrapping at any point.

**Action:** Change AccountActivityFilter.journalEntryId to JournalEntryHeaderId option. Update OrchestrationConverters to wrap input.journalEntryId via JournalEntryHeaderId.fromGuid, and update AccountActivity.fetchFiltered to unwrap via JournalEntryHeaderId.value before building the query parameter.

**Why:** Single-case DU wrappers like JournalEntryHeaderId exist to make the type system enforce identity distinctions that raw Guids cannot. An AccountId Guid and a JournalEntryHeaderId Guid are both System.Guid at runtime, but at compile time the wrapper prevents passing one where the other is expected. When a field in a domain-layer record reverts to raw Guid, that compile-time guard disappears: a caller could pass an AccountId's Guid as the journalEntryId filter and get no type error, only wrong (or empty) results at runtime. This is the core 'make illegal states unrepresentable' principle -- the type system should reject nonsense inputs at compile time, not at query time.


## IDIOM-NOOP-2 — test-gap
- **Location:** Tests/Tests.Integrated/ModelOrchestrator/JournalEntryCommentOrchestration.fs; Tests/Tests.Integrated/InterfaceBridge/JournalEntryRoutes.fs
- **Summary:** The comment update no-op path (both fields NoChange) has no test at any layer, leaving the IDIOM-NOOP-1 bug undetected and REQ-SYS-6.1 unverified for this operation.
- **Resolution:** fix-test

REQ-SYS-6.1 states: 'No state-transition operation may silently succeed as a no-op.' Its waiver table (SystemWide.md line 56) adds: 'Testing should be enforced by every individual write operation with a no-op possibility.' The comment update via JournalEntryCommentOrchestration.updateComment accepts FieldUpdate<CommentText> and FieldUpdate<JournalEntryHeaderId option>; when both are NoChange, the operation is a no-op and should produce Error(JournalEntryCommentUpdateNoOp). The orchestrator tests (JournalEntryCommentOrchestration.fs) have two tests covering repoint and clear of the secondary link. The route tests (JournalEntryRoutes.fs REQ-JE-5.3, lines 550-603) cover empty text, too-long text, and same-IDs. No test sends NoChange for both fields. The external reference update no-op IS tested (JournalEntryRoutes.fs line 540, asserts JournalEntryReferenceUpdateNoOp), making the comment update gap a clear omission. Adding the missing test would also surface IDIOM-NOOP-1 (wrong error case).

**Action:** Add a test that calls updateComment (or routes UpdateComment) with both commentUpdate=NoChange and secondaryIdUpdate=NoChange, asserting the error case is JournalEntryCommentUpdateNoOp. This test will fail until IDIOM-NOOP-1 is fixed.

**Why:** In functional programming, the error railway is only as trustworthy as the tests that exercise it. A no-op rejection path that has never been tested is an unverified claim about system behavior. The test suite already establishes the pattern (the external reference no-op test at line 540 proves the intent), so the gap is not a philosophical disagreement about test granularity -- it is a missed instance of an established testing practice.

