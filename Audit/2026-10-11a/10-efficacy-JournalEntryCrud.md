# test-efficacy-JournalEntryCrud

## EFF-JE-1 — test-gap
- **Location:** REQ-JE-3.7; Tests/Tests.Integrated/CrossDomainOrchestration/JournalEntryFetching.fs:257-286; Tests/Tests.Integrated/CrossDomainOrchestration/JournalEntryCommentsAndReads.fs:207-224; Tests/Tests.Integrated/InterfaceBridge/JournalEntryRoutes.fs:317-336
- **Summary:** No test citing REQ-JE-3.7 uses a range whose start date is before its end date. Every one is a single-day range (begin = end), so a real multi-day range is never checked against a derived expected set.
- **Resolution:** fix-test

REQ-JE-3.7 says the system returns 'all journal entries whose entry date falls within a caller-provided date range (start date and end date, both inclusive)'. These are all the tests that cite it:
- JournalEntryFetching.fs:265 calls `fetchByDateRange context today today`.
- JournalEntryFetching.fs:283 calls `fetchByDateRange context farDate farDate` with 2050-01-01 for both dates.
- JournalEntryCommentsAndReads.fs:216 calls `fetchByDateRange day day`.
- JournalEntryRoutes.fs:328 sends `{ beginDate = today; endDateInclusive = today }`.
- JournalEntryCommentsAndReads.fs:199 (REQ-JE-3.7.1) only covers begin > end.
The single-day tests do pin inclusivity at both bounds: the CommentsAndReads:207 test posts entries the day before and after and asserts DoesNotContain for each. But with begin = end they cannot tell `entry_date >= @begin_date AND entry_date <= @end_date` apart from implementations that are wrong for any wider range. Two examples: swapped parameters (`>= @end_date AND <= @begin_date`), or a filter equivalent to `entry_date = @begin_date`. Both return exactly the one-day set every test above expects. Smell test 1 (a function returning garbage of the right shape) passes for such an implementation on every REQ-JE-3.7-cited test.
The only multi-day call in the suite is incidental. JournalEntryRoutes.fs:89-93 sits inside a test cited as REQ-JE-2.13/2.3, and its only assertion is a count: `fetchByDateRange context absurdBegin absurdEnd` (±7 years), then `Assert.Equal(expected, newCount)` against `fixture.Data.journalEntries |> List.length`. It checks no identities and does not cite REQ-JE-3.7. Tests/README.md names behavior covered by only one test as where the cross-coverage protection is weak. Here the central claim of the requirement ("within a range") is covered only by a count in a test about something else.

**Action:** Add a REQ-JE-3.7 test with begin < end, e.g. begin = today-2, end = today. Post or use fixture entries dated begin-1, begin, an interior day, end and end+1. Assert the returned header-ID set equals the IDs of fixture/posted entries whose date lies within [begin, end], derived with list operations, and that the begin-1 and end+1 entries are absent.

**Why:** A range query has three areas to cover: the two bounds and the interior. When begin = end the interior is empty and both bounds are the same date, so a one-day range cannot show that the two parameters go to the right comparisons. Bugs like swapped parameters or an equality filter survive every test that cites the requirement.

---

## EFF-JE-2 — test-gap
- **Location:** REQ-JE-2.4 / REQ-JE-2.13 / REQ-JE-4.4 / REQ-JE-4.10 / REQ-JE-5.1; Tests/Tests.Integrated/InterfaceBridge/JournalEntryRoutes.fs:72-107, 181-225, 519-548, 645-664, 714-733
- **Summary:** Several sad-path route tests on committing routes cannot clean up if the rejection regresses. Some declare an `idToCleanUp` that is never assigned; others have no cleanup at all. A failed rejection would therefore leave committed rows behind, or permanently mutate fixture entries.
- **Resolution:** fix-test

Tests/README.md (Form 4) says a route that owns its transaction has already committed by the time it returns. The test must "Capture the ID into a `let mutable idToCleanUp = None` the instant the create succeeds — including on the `| Ok _ -> Assert.Fail "expected failure"` arm of a sad-path test". The README's contamination section names "a test with no cleanup at all where its form requires one" as worth reporting. These tests break that rule:
- JournalEntryRoutes.fs:72-107 (`REQ-JE-2.13 REQ-JE-2.3 PostNew route unhappy path cleans up after itself`). `idToCleanUp` is declared at line 80 but never assigned. The Ok arm (lines 100-101) only does `Assert.Fail "Expected failure; got success. You have data to clean up"`, so the `finally` at line 105 cleans up `None`.
- JournalEntryRoutes.fs:181-225 (`REQ-JE-2.13 REQ-JE-2.4 PostNew validates input as valid types`, 20 InlineData rows). `idToCleanUp` is declared at line 210 and never assigned. The `isCorrectErrorString` call discards the Ok payload and only appends the warning "Record should be cleaned up." If any row's validation regresses, a committed entry carrying a reference and a comment is left behind.
- JournalEntryRoutes.fs:519-548 (`REQ-JE-4.4 Void validates input and state`). It voids fixture entries (`basicJeId`, `voidedJeId`, `jeInClosedPeriodId`) through the committing Void route and has no cleanup. Its own warning text is "This probably caused other tests to fail." A wrongly accepted void of `basicJeId` is permanent, because there is no un-void.
- JournalEntryRoutes.fs:645-664 (`REQ-JE-4.10 AddExternalReference validates input as valid types`) and 714-733 (`REQ-JE-5.1 AddComment validates input as valid types`). Both target `fixture.Data.basicJeId` through committing routes with no cleanup, so a regression would commit a reference or comment onto a shared fixture entry.
For contrast, JournalEntryRoutes.fs:128-133 (the REQ-JE-2.12 test) does capture the ID on its Ok arm. So does the `withEntries` harness in JournalEntryCommentsAndReads.fs and RevisedRequirementsLedger.fs. The pattern exists in the suite; these tests do not use it.

**Action:** In each listed test, replace the bare `isCorrectErrorString` or Ok arm with a typed match whose Ok arm deserializes the payload and assigns `idToCleanUp` (for PostNew), or deletes the appended reference/comment (for AddExternalReference/AddComment), before failing. For the Void theory, run the voids against entries the test posts itself and deletes in `finally`, not against fixture entries.

**Why:** A sad-path test exists to go red when a rejection regresses. In these tests, that failure also commits writes the test cannot undo. The damage then spreads to every later test that derives expected values from the fixture, and the one real failure is buried among false ones. The README requires every committing sad-path test to clean up in exactly this situation.

---


