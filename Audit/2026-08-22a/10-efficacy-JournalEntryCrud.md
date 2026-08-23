# je-efficacy-auditor

## JE-ROUTE-EXTREF-1 — test-gap
- **Location:** Tests/Tests.Integrated/InterfaceBridge/JournalEntryRoutes.fs, lines 219-224; REQ-JE-3.5
- **Summary:** FetchByExternalReference route test derives expected count from references, not distinct entries, producing a value that agrees with the function's return only by fixture coincidence.
- **Resolution:** fix-test

The expected value at line 220-224 is `fixture.Data.journalEntryExternalReferences |> List.filter(...) |> List.length`, which counts matching *reference rows*. The function under test (`FetchByExternalReference`) returns distinct *journal entries* -- a single entry with two matching references is one result, not two. The orchestrator-level test in JournalEntryFetching.fs already corrected this with the `distinctEntryCountMatching` helper (lines 32-37), which maps to `journalEntryHeaderId`, applies `List.distinct`, then counts. The route test was not updated. The counts currently agree because -- as the comment in JournalEntryFetching.fs (lines 25-31) explicitly notes -- 'The fixture currently gives each entry at most one per institution-and-text pair, which made the two quantities agree by luck rather than by construction.' Adding a fixture entry with two references sharing the same (fi, refText) pair would break this test for the wrong reason.

**Action:** Replace the raw filter-and-count derivation with `distinctEntryCountMatching` (or an equivalent map-distinct-count over `journalEntryHeaderId`), mirroring the orchestrator test's approach.

**Why:** An expected value derived by counting the wrong quantity is a latent test bug. Today the fixture makes both counts agree; a future fixture change breaks the test even though the code is correct, or worse, a code bug that conflates references with entries passes because the test has the same confusion.

---

## JE-FETCH-PERIOD-1 — test-gap
- **Location:** Tests/Tests.Integrated/ModelOrchestrator/JournalEntryFetching.fs, lines 93-111; REQ-JE-3.3
- **Summary:** fetchByPeriod test asserts only count, never verifying that returned entries belong to the target period (Specimen 3).
- **Resolution:** fix-test

The test `REQ-JE-3.3 fetchByPeriod returns all entries for a given fiscal period` derives an expected count from fixture data filtered by period ID (line 101-104), calls `fetchByPeriod` (line 106), and asserts `Assert.Equal(expected, actual)` on the list lengths (line 108). No assertion checks that any returned entry's fiscal period matches the target. The REQ-JE-3.5 fetchByReference test in the same file (lines 143-151) shows the corrective pattern: after the count assertion, it adds `Assert.True(fetched |> List.forall(fun fetchedEntry -> ...))` to verify every returned entry carries the matching criterion. Smell test: if fetchByPeriod returned entries from a different period but the same count, this test passes.

**Action:** After the count assertion, add a membership check: `Assert.True(fetchList |> List.forall(fun je -> je |> header |> JournalEntryHeader.entryDate |> EntryDate.fiscalPeriodId = fpId))`. The existing REQ-JE-3.5 test in the same file is the template.

**Why:** Tests/README.md: 'Counts only in addition to values, never instead of them.' This is the primary test for REQ-JE-3.3, and no other layer adds a membership check (the route test is also count-only). A count-only filter test cannot distinguish correct filtering from a function that returns the wrong rows in the right quantity.

---

## JE-FETCH-DATERANGE-1 — test-gap
- **Location:** Tests/Tests.Integrated/ModelOrchestrator/JournalEntryFetching.fs, lines 228-249; REQ-JE-3.7
- **Summary:** fetchByDateRange test asserts only count, never verifying that returned entries fall within the requested date range (Specimen 3).
- **Resolution:** fix-test

The test `REQ-JE-3.7 fetchByDateRange returns entries within inclusive date range` derives an expected count from fixture entries filtered by entry_date (lines 231-236), calls `fetchByDateRange` (line 237), and asserts only `Assert.Equal(expected, entries |> List.length)` (line 239). No assertion verifies that every returned entry's entry_date falls within [today, today]. Smell test: if fetchByDateRange returned entries from outside the date range but the same count, the test passes.

**Action:** After the count assertion, add: `entries |> List.iter(fun je -> let d = je |> header |> JournalEntryHeader.entryDate |> EntryDate.entryDate in Assert.True(d >= today && d <= today))`.

**Why:** Same codebase rule as JE-FETCH-PERIOD-1. This is the primary test for the date-range filter behavior of REQ-JE-3.7, and the route-level test is also count-only. The inclusive-boundary semantics the REQ describes ('start date and end date, both inclusive Calendar Dates') are never structurally verified against the returned data.

---

## JE-FETCH-REF-VARIANTS-1 — test-gap
- **Location:** Tests/Tests.Integrated/ModelOrchestrator/JournalEntryFetching.fs, lines 190-217; REQ-JE-3.8
- **Summary:** The two REQ-JE-3.8 fetchByReference tests (FI-only at line 190 and ref-text-only at line 205) are count-only, lacking the membership check that the REQ-JE-3.5 test in the same file already applies.
- **Resolution:** fix-test

Both tests derive an expected distinct-entry count via `distinctEntryCountMatching`, call `fetchByReference`, and assert count equality. Neither verifies that every returned entry actually carries a reference matching the search criterion. Contrast with the REQ-JE-3.5 test (lines 143-151), which adds `Assert.True(fetched |> List.forall(fun fetchedEntry -> fetchedEntry |> externalReferences |> List.exists(...)))`. The pattern already exists in this file; the two REQ-JE-3.8 tests simply omit it.

**Action:** Add the same `List.forall` membership assertion used in the REQ-JE-3.5 test. For the FI-only test, verify every entry carries a reference whose FI matches; for the ref-text-only test, verify the reference text matches.

**Why:** An inconsistency within the same test class: REQ-JE-3.5 proves both count and membership, while REQ-JE-3.8's two variants prove only count. If fetchByReference's optional-parameter branching had a bug that returned entries without matching criteria (but the right count), these tests would pass.

---

## JE-ACTIVITY-VOIDED-1 — test-gap
- **Location:** Tests/Tests.Integrated/ModelOrchestrator/AccountActivity.fs, lines 52-81; REQ-JE-3.9.1
- **Summary:** fetchFiltered with unVoidedOnly test asserts only count, never verifying that no voided entries appear in the result (Specimen 3).
- **Resolution:** fix-test

The test `REQ-JE-3.9.1 fetchFiltered with unVoidedOnly excludes voided entries` derives an expected count from fixture data by filtering out voided journal entries and their lines (lines 53-65), then calls `AccountActivity.fetchFiltered` with `unVoidedOnly = true` (line 78), and asserts only `Assert.Equal(expectedCountTotal, activities |> List.length)` (line 80). The REQ says 'The caller may filter to non-voided entries only (per REQ-JE-4.7).' No assertion inspects the returned activities to confirm that none have a voided parent entry. Smell test: if the filter included one voided entry while excluding one non-voided entry of the same count, this test passes. Other tests in this file (e.g., fetchFiltered by amount, line 146-148) do verify field values on returned rows, so the pattern is already established.

**Action:** After the count assertion, add a membership check on the activities with detail: verify that each `activityDetail` row's `voidedAt` is `None`. The test already derives `unVoidedJournalEntries` by filtering on `Option.isNone` -- apply the same check to the actual result.

**Why:** Void exclusion (REQ-JE-4.7, REQ-JE-3.9.1) is a critical ledger invariant. The orchestrator-level balance tests (AccountBalance.fs) verify void exclusion with value assertions, but the activity report's void filter is verified only by count. A count-only void test cannot catch a filter that erroneously includes voided entries while dropping an equal number of non-voided ones.

---
