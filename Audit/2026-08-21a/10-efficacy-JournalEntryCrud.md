# je-efficacy

## ASSERT-JE-1 — test-gap
- **Location:** Tests/Tests.Integrated/ModelOrchestrator/JournalEntryFetching.fs, lines 52-64 (REQ-JE-3.1)
- **Summary:** REQ-JE-3.1 test asserts only counts on lines, refs, and comments without inspecting any values (Specimen 3).
- **Resolution:** fix-test

The test for 'fetchById returns header, lines, external references, and comments' derives expected counts from fixture.Data.jeWithLinesRefsAndComments and asserts Assert.Equal on List.length for lines, externalReferences, and comments, plus Assert.NotNull on header. No value is ever inspected -- no line ID, no reference text, no comment text, no amount. The smell test: if fetchById returned a JournalEntry with the right number of lines but every line had a wrong accountId, wrong amount, and wrong lineType, this test would pass. Tests/README.md: 'Assert on domain values. Counts only in addition to values, never instead of them.' The test has only counts. Value coverage for fetchById exists in sibling tests (REQ-JE-3.2 checks header description; REQ-SYS-5.1 round-trips check all comment and ext-ref fields), but this test itself violates the counts-only prohibition.

**Action:** Add at least one value-level assertion per child collection -- e.g., compare the sorted list of line IDs (or amounts) from the fixture against the fetched result. The count assertions can stay; the values make them meaningful.

**Why:** Specimen 3 is the codebase's documented antipattern for this exact shape. A count-only assertion on a fetch function's structural completeness proves the query returned rows, not that it returned the right rows. The existing round-trip tests cover individual field fidelity, but this test's claim -- that all associated children are returned -- is only substantiated if it can distinguish the right children from the wrong ones.

---

## ASSERT-JE-2 — test-gap
- **Location:** Tests/Tests.Integrated/ModelOrchestrator/JournalEntryFetching.fs, line 127 (REQ-JE-3.5)
- **Summary:** Assert.True(matchedCount > 0) is a cowardly inequality (Specimen 2) on the matched-reference count within a returned entry.
- **Resolution:** fix-test

In the REQ-JE-3.5 fetchByReference test (line 104-130), after asserting the correct number of entries was returned, the test takes the first entry, filters its external references for the (fi, refText) pair, and asserts Assert.True(matchedCount > 0). The fixture data for ('TestBank', 'TXN-001') has exactly 2 entries, each carrying exactly 1 matching reference. The correct assertion is Assert.Equal with the expected count derived from fixture data -- e.g., count the matching references on the specific returned entry's JE ID within fixture.Data.journalEntryExternalReferences. The > 0 floor tolerates any positive count, which would mask a duplication bug (returning the same reference twice) or an over-fetch.

**Action:** Replace Assert.True(matchedCount > 0) with an Assert.Equal whose expected value is derived from fixture.Data.journalEntryExternalReferences filtered to both the (fi, refText) pair AND the specific journal entry header ID of the returned entry.

**Why:** Specimen 2 exists because floor assertions encode a stale lower bound instead of the exact expected value. The test standard says 'Asserting equality is preferred to asserting truth. You should know what you expect and you should assert the outcome is exactly what you expect.'

---

## ASSERT-JE-3 — test-gap
- **Location:** Tests/Tests.Integrated/ModelOrchestrator/JournalEntryFetching.fs, lines 112-117, 141-145, 173-175, 189-191 (REQ-JE-3.5, REQ-JE-3.8, REQ-JE-1.48)
- **Summary:** Expected-value derivation in four fetchByReference tests counts matching external references instead of distinct journal entries, comparing the wrong quantity against the actual entry count.
- **Resolution:** fix-test

Four tests derive their expected value as: fixture.Data.journalEntryExternalReferences |> List.filter(matching condition) |> List.length. This counts the number of external reference records that match the filter. But fetchByReference returns journal entries (distinct, per line 276 of JournalEntryOrchestration.fs which calls List.distinctBy on header ID). These are different quantities: a single JE can carry multiple matching references. With the current fixture, each JE has at most one reference per (FI, refText) combination, so ref-count == entry-count by coincidence. But the derivation is structurally wrong.

Affected tests:
- REQ-JE-3.5 fetchByReference returns entries matching source FI and reference value (line 112)
- REQ-JE-3.5 REQ-JE-1.48 fetchByReference returns multiple entries when reference is shared (line 141)
- REQ-JE-3.8 fetchByReference with FI only returns all entries for that FI (line 173)
- REQ-JE-3.8 fetchByReference with reference text only (line 189)

If a future fixture JE is given two references from the same FI (e.g., jeWithLinesRefsAndComments gains a second 'TestBank' reference), the FI-only test would expect 5 entries but get 4 -- a false failure. Conversely, if the dedup in fetchHeadersFromFilter were accidentally removed, the tests would still pass because expected and actual would both count raw rows.

**Action:** Change the expected-value derivation to count distinct journal entry header IDs among the matching references: fixture.Data.journalEntryExternalReferences |> List.filter(matching condition) |> List.map(fun jer -> jer |> JournalEntryExternalReference.journalEntryHeaderId) |> List.distinct |> List.length. This makes the expected value measure the same quantity the function returns.

**Why:** When the expected value and the actual value measure different things that happen to be equal, the test passes by coincidence of the fixture rather than by correctness of the comparison. This masks both false passes (dedup removed but counts still match) and creates false failures (fixture gains a second same-FI reference on one entry). The derivation should reflect what the spec says the function returns: a set of journal entries.

---
