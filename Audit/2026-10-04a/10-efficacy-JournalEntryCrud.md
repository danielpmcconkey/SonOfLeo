# test-efficacy-JournalEntryCrud

## EFF-JE-1 — test-gap
- **Location:** REQ-JE-2.5, REQ-JE-1.11 — Tests/Tests.Integrated/CrossDomainOrchestration/JournalEntryCreation.fs:407-425
- **Summary:** REQ-JE-2.5 (derive the fiscal period from the entry date) and REQ-JE-1.11 (the entry date falls inside the assigned period) are cited only on a test that checks the REQ-JE-2.6 rejection. No test asserts the derived period or the in-range invariant.
- **Resolution:** fix-test

The only test that cites REQ-JE-2.5 or REQ-JE-1.11 is `REQ-JE-2.5 REQ-JE-2.6 REQ-JE-1.11 constructNewAndPersist rejects entry date w/ no matching fiscal period` (JournalEntryCreation.fs:407). It posts with `today.PlusYears(-3)`, a date with no fiscal period, and asserts only `Error (AsError (JournalEntryDateNotInFiscalPeriod _))`. That is REQ-JE-2.6. REQ-JE-2.5 describes a positive derivation: parse year and month, build the PeriodKey, look up that period. REQ-JE-1.11 states the invariant that the derivation must keep, which the AMB-JE-1 ruling affirmed: the entry date lies within the assigned period's start and end dates. No test anywhere asserts that a posted entry's `EntryDate.fiscalPeriodId` equals the fixture period whose start..end contains the entry date. The REQ-JE-3.3 tests (JournalEntryFetching.fs:104, JournalEntryRoutes.fs:241) build their expected sets from `EntryDate.fiscalPeriodId` on fixture entries. Those ids were written by the same derivation at fixture time, so they take the derivation's output as given rather than checking it. A month-boundary derivation bug (for example, a first-of-month or last-of-month date landing in the adjacent month) is caught only by accident, through other tests whose dates sit mid-period (JE-2.7 uses closedPeriod start+14).

**Action:** Add a test under REQ-JE-2.5 and REQ-JE-1.11 that posts entries dated on the first and last day of a fixture period. For each, assert that the header's `EntryDate.fiscalPeriodId` equals the id of the fixture.Data.fiscalPeriods row whose startDate <= date <= endDate, found with list operations only.

**Why:** Smell test 1: if the derivation returned the wrong period, but one that exists and is open, the cited test would still pass, because it only checks the case where no period exists. The cited test checks a different property than the REQ states, so REQ-JE-2.5 and REQ-JE-1.11 are cited but not covered.

---

## EFF-JE-2 — test-gap
- **Location:** REQ-JE-5.6 — Tests/Tests.Integrated/Model/Ledger/JournalEntryComment.fs:206-220
- **Summary:** The only REQ-JE-5.6 test cannot fail. Neither the update operation nor its contract has any way to change the primary link, so the test passes even if the update does nothing.
- **Resolution:** dan-decides

`REQ-JE-5.6 updateComment does not change the primary JE link` calls `updateComment context fixtureCommentId (SetTo text) NoChange` and asserts `Assert.Equal(fixture.Data.basicJeId, updatedComment |> primaryJournalEntryId)`. The signature `updateComment (context) (commentId) (FieldUpdate<CommentText>) (FieldUpdate<JournalEntryHeaderId option>)` (Src/Business.CrossDomainOrchestration/JournalEntryCommentOrchestration.fs:64-68) and the contract `JournalEntryUpdateCommentInput = { id; secondaryJournalEntryId; commentText }` (JournalContracts.fs:104-105) have no primary field at all. The test cannot ask for a re-point, so it never exercises a rejection or any other path. Smell test 2 (delete the operation and assert against the untouched state) passes, because the fixture comment's primary is basicJeId whether or not the update runs. The guarantee is structural, the same shape as REQ-JE-2.14, which is waived because "no creation input contract exposes `voidedAt`".

**Action:** Dan decides: either move REQ-JE-5.6 to the Waived table with a structural reason (no update contract or orchestrator parameter exposes the primary link), mirroring REQ-JE-2.14, or keep it tested and accept that the test only guards against an update SQL that rewrites primary_journal_entry_id.

**Why:** A test that would pass against a no-op implementation reports coverage it does not give. Where a type or signature already enforces a requirement, a waiver states that honestly; a test that cannot fail does not.

---

## EFF-JE-3 — test-gap
- **Location:** REQ-JE-3.8 — Tests/Tests.Integrated/CrossDomainOrchestration/JournalEntryFetching.fs:211-231, 234-254
- **Summary:** The tests that cite REQ-JE-3.8 check only a count plus a per-entry membership test, never which entries came back. The second one tests a reference-value-only lookup, which is not REQ-JE-3.8's behavior.
- **Resolution:** fix-test

`REQ-JE-3.8 fetchByReference with FI only returns all entries for that FI` (L211) asserts `Assert.Equal(expected, fetched |> List.length)` and `Assert.All(fetched, fun e -> Assert.Contains(fi, ...))`. It never compares entry ids. A result that repeats one matching entry N times, where N is the number of distinct matching fixture entries, passes. The file's own header comment (L37-42) says a count of distinct entries is the thing to guard. This is Specimen 3 in the count-plus-predicate form; the sibling test at L173 does it properly with `Assert.Equal<Set<JournalEntryHeaderId>>`. The second test that cites REQ-JE-3.8, `fetchByReference with reference text only ...` (L234), calls `fetchByReference context None (Some refText)`. That is REQ-JE-3.5's reference-value-alone path; REQ-JE-3.8 is FI only. The only FI-alone test that compares identities is the theory row `RevisedRequirementsLedger.fs:216` (InlineData "source FI"), and it cites REQ-JE-3.5, not REQ-JE-3.8.

**Action:** In the L211 test, replace the count + Assert.All with a set equality of header ids against the distinct fixture entries carrying that FI, the same shape as L173-191. Re-cite the L234 test to REQ-JE-3.5 and switch it to set equality as well.

**Why:** Specimen 3: a count plus a predicate that every row satisfies cannot tell the right set from the right number of copies of one member. Citing a REQ on a test of a different path inflates that REQ's apparent coverage.

---

## EFF-JE-4 — maintainability
- **Location:** REQ-JE-2.11/2.12/2.15, REQ-JE-3.5.1, REQ-JE-3.7.1, REQ-JE-5.8 — RevisedRequirementsLedger.fs:120; JournalEntryCommentsAndReads.fs:116-137, 194, 203, 250; OperationInstantAndAtomicity.fs:243; JournalEntryRoutes.fs:506, 522, 640
- **Summary:** Several route-level failure cases for JE requirements are each tested two or three times, at the same layer, with the same input and the same expected error.
- **Resolution:** fix-test

These pass through the same route with the same bad input and expect the same typed error, so by the failure-vector article each group is one vector, not several. (a) Posting through PostNew with a valid comment followed by one whose secondary id matches no entry, asserting JournalEntryCommentSecondaryJeHeaderIdNotFound and that nothing is stored for the tagged description: RevisedRequirementsLedger.fs:120 (REQ-JE-2.11), JournalEntryCommentsAndReads.fs:116 theory row "a secondary ID that matches no journal entry" (REQ-JE-2.15), and OperationInstantAndAtomicity.fs:243 row "posting a journal entry" (REQ-SYS-8.1/REQ-JE-2.12). The last of these posts through the committing route outside any withEntries/finally that would delete a JE if the post unexpectedly succeeds; its `finally` cleans only staged ids. (b) FetchByExternalReference with fi=None and reference=None, expecting JournalEntryFetchByReferenceBothArgumentsNull, through the route: JournalEntryRoutes.fs:640 theory row "bothNull" (cited REQ-JE-3.5) and JournalEntryCommentsAndReads.fs:194 (REQ-JE-3.5.1). (c) FetchByDateRange with begin after end, expecting JournalEntryFetchByDateRangeBeginAfterEnd, through the route: JournalEntryRoutes.fs:506 (cited REQ-JE-3.7) and JournalEntryCommentsAndReads.fs:203 (REQ-JE-3.7.1). (d) AddComment with a secondary id that matches no entry, expecting JournalEntryCommentSecondaryJeHeaderIdNotFound, through the route: JournalEntryRoutes.fs:522 (cited REQ-JE-5.1) and JournalEntryCommentsAndReads.fs:250 (REQ-JE-5.8).

**Action:** In each group keep the strongest test: the one that cites the precise REQ and also asserts nothing was stored (JournalEntryCommentsAndReads 2.15 row or RevisedRequirementsLedger:120 for (a); CommentsAndReads:194, :203, :250 for (b)–(d)). Delete the others or the redundant theory rows. If the OperationInstantAndAtomicity posting row stays, give it JE cleanup on the unexpected-Ok path.

**Why:** Tests/README.md names "Do not test the same thing twice" as a bullshit practice. Duplicate tests make a vector look more heavily covered than it is, and they multiply maintenance when the behavior changes.

---

## EFF-JE-5 — test-gap
- **Location:** JournalEntryFetching.fs:257; JournalEntryRoutes.fs:506, 522; JournalEntryExternalReferenceOrchestration.fs:28; Model/Ledger/JournalEntryHeader.fs:26
- **Summary:** Several tests cite a JE REQ other than the one whose behavior they verify, so the traceability audit reports coverage that is not there.
- **Resolution:** fix-test

(1) JournalEntryFetching.fs:257 `REQ-JE-3.5 REQ-JE-3.8 fetchByReference with both parameters None returns Error` tests the REQ-JE-3.5.1 typed-error rule, which REQ-JE-3.8 (FI-only lookup) does not contain. (2) JournalEntryRoutes.fs:506 `REQ-JE-3.7 FetchByDateRange rejects begin date after end date` tests REQ-JE-3.7.1. (3) JournalEntryRoutes.fs:522 `REQ-JE-5.1 AddComment rejects non-existent secondary JE header ID` tests REQ-JE-5.8. (4) JournalEntryExternalReferenceOrchestration.fs:28 `REQ-JE-4.9 updateFiAndReferenceText rejects no-op when both fields are NoChange`: REQ-JE-4.9 says nothing about no-op rejection. That behavior is the REQ-SYS-6.1 default ("For an update, 'would change nothing' means the request names no field to change... an entity spec need not restate it"). Unlike comments (REQ-JE-5.7), the reference no-op has no JE REQ, so the test should cite REQ-SYS-6.1. (5) Model/Ledger/JournalEntryHeader.fs:26 cites REQ-JE-2.7 (posting rejection) on a read-path test that a closed-period header is still fetchable. That is REQ-JE-3.2 and the REQ-JE-3.1.1 read semantics, not anything REQ-JE-2.7 states.

**Action:** Re-cite: (1) to REQ-JE-3.5.1; (2) to REQ-JE-3.7.1; (3) to REQ-JE-5.8; (4) to REQ-SYS-6.1; (5) drop REQ-JE-2.7 from the name.

**Why:** Tests/README.md: "A name is a claim." The traceability audit counts a cited ID as covered, so a wrong citation can hide a gap in the REQ that is really being tested, and it overstates coverage of the REQ that is named.

---

## EFF-JE-6 — test-gap
- **Location:** REQ-JE-2.13/2.11 — JournalEntryCreation.fs:35-55; REQ-JE-3.2 — JournalEntryRoutes.fs:235, Model/Ledger/JournalEntryHeader.fs:37-40
- **Summary:** The orchestrator-level post happy path checks only the description and a hard-wired line count. Two REQ-JE-3.2 tests assert against hard-coded fixture description strings instead of fixture.Data.
- **Resolution:** fix-test

JournalEntryCreation.fs:52 `Assert.Equal(2, jeHappy |> jeLines |> List.length)` is the only line assertion in the REQ-JE-2.13/2.11 orchestrator happy path. Account, amount and entry type of the returned lines are never looked at, which is Specimen 3 (the count never looks inside). REQ-JE-2.11 requires returning the fully constructed entry. The route test (JournalEntryRoutes.fs:60-62) does compare line tuples, but README requires every happy path at each layer, and the orchestrator layer has no value check for lines. JournalEntryRoutes.fs:235 `Assert.Equal("Basic journal entry", returned.header.description)` and JournalEntryHeader.fs:38 `"Fixture JE in closed period"` hard-code fixture text. Specimen 1 notes the magic string "has the same disease" as the magic count. The orchestrator twin at JournalEntryFetching.fs:53-60 already derives the description from fixture.Data.journalEntries.

**Action:** In JournalEntryCreation.fs:35, assert the set of (accountId, amount, lineType) on the returned lines equals the input tuples. In the two REQ-JE-3.2 tests, derive the expected description from fixture.Data.journalEntries by id, as JournalEntryFetching.fs:53 does.

**Why:** Specimens 1 and 3: a literal count passes when any two lines come back, and a literal string ties the test to fixture wording rather than to the relationship it claims to check.

---

