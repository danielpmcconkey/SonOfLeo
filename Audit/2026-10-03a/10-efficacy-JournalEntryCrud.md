# test-efficacy-JournalEntryCrud

## EFF-JE-1 — test-gap
- **Location:** Tests/Tests.Integrated/CrossDomainOrchestration/AccountActivity.fs:222-313 (REQ-JE-3.9.3)
- **Summary:** None of the three REQ-JE-3.9.3 sort tests checks that ascending results are actually in ascending order, so swapped asc/desc or a sort on the wrong key still passes.
- **Resolution:** fix-test

Each test (entry date at line 222, account code at 254, amount at 284) makes the same two assertions: `Assert.True(datesAsc |> List.pairwise |> List.exists (fun (a, b) -> a <> b))` and `Assert.True((datesAsc |> List.rev) = datesDesc)`. Neither one checks the direction. Three broken implementations would pass: (a) EntryDateAsc returning descending order and EntryDateDesc returning ascending; (b) both orders sorted by some other key (line id, say), one the exact reverse of the other; (c) both unsorted, with Desc built as List.rev of Asc. The REQ says the caller can choose ascending or descending on each key, and none of these tests can see whether that choice is honored. The behavior is checked properly elsewhere: REQ-AC-3.12.4 at Tests/Tests.Integrated/CrossDomainOrchestration/AccountCreateActivityBalance.fs:601 posts rows in an order that matches neither direction and asserts `inOrder` per direction. That test cites only REQ-AC-3.12.4, so REQ-JE-3.9.3's traceability depends entirely on the hollow tests.

**Action:** Assert direction in the REQ-JE-3.9.3 tests (for example `Assert.Equal<LocalDate list>(List.sort datesAsc, datesAsc)` and `Assert.Equal(List.sortDescending datesDesc, datesDesc)`), or add REQ-JE-3.9.3 to the name of the REQ-AC-3.12.4 theory and delete the three JE tests.

**Why:** Smell test: the function could return garbage of the right shape (a consistent but wrong or inverted order) and these tests would stay green. Comparing the two directions with each other only shows the function agrees with itself; it never says what correct order is.

---

## EFF-JE-2 — test-gap
- **Location:** Tests/Tests.Integrated/CrossDomainOrchestration/AccountBalance.fs:122-151 (REQ-JE-3.6.2); fixture Tests/Tests.Helpers/TestDataStage.fs temporal entries
- **Summary:** The REQ-JE-3.6.2 as-of test has no entry dated on the as-of date, so the 'end of the as-of date' boundary is never exercised and an exclusive `<` filter would pass.
- **Resolution:** fix-test

The test sets `asOfDate = today.PlusDays(-2)` against temporalExpense5700. The fixture's temporal entries for that account are dated today-3 (alpha), today-1 (beta) and today (gamma). Nothing is dated today-2, so `je.entry_date <= @as_of` (the current code, AccountBalance.fs:50) and `je.entry_date < @as_of` give the same totals. The second REQ-JE-3.6.2 test (line 154, as-of today-4) is before every entry and also cannot tell the two apart. The REQ asks for the balance 'at the end of the as-of date', which makes inclusivity the point of the requirement. REQ-AC-3.13.1 at AccountCreateActivityBalance.fs:697 does test the boundary (a line dated on the as-of date counts, the day after does not), but it does not cite REQ-JE-3.6.2.

**Action:** Use an as-of date equal to a temporal fixture entry's date (for example today-1, which then includes alpha and beta and excludes gamma). Alternatively, co-cite REQ-JE-3.6.2 on the REQ-AC-3.13.1 boundary test.

**Why:** Boundary semantics are exactly where off-by-one bugs live. A test that never puts a row on the boundary cannot tell `<` from `<=`.

---

## EFF-JE-3 — test-gap
- **Location:** Tests/Tests.Isolated/Model/Ledger/JournalEntryExternalReference.fs:27-29, 63-66 (REQ-JE-1.42, REQ-JE-1.44)
- **Summary:** The 'rejects whitespace-only string' tests for source FI and reference value pass an empty string, so the whitespace-only clause of REQ-JE-1.42 and REQ-JE-1.44 is never tested.
- **Resolution:** fix-test

Line 28: ``REQ-JE-1.42 JournalRefFinancialInstitution.create rejects whitespace-only string`` calls `JournalRefFinancialInstitution.create ""`. Line 65: ``REQ-JE-1.44 JournalExternalReferenceText.create rejects whitespace-only string`` calls `JournalExternalReferenceText.create ""`. Both duplicate the empty-string tests just above them (lines 22-24 and 59-61). The route theories that cover these fields (JournalEntryRoutes.fs:125,129,582,586,617,621,667,671) also send only "". The sibling component tests do it correctly: Description, Source, LineMemo and CommentText all pass "     " (JournalEntryComponent.fs:29,66,103,175). The current Src (trim, then IsNullOrWhiteSpace) does reject whitespace, but no test would notice if it stopped.

**Action:** Change the argument at lines 28 and 65 to a whitespace-only string such as "     ".

**Why:** The README calls testing the same thing twice bullshit. Here a test's name claims coverage of a vector it never sends, so the duplicate also hides a gap.

---

## EFF-JE-4 — test-gap
- **Location:** Tests/Tests.Integrated/CrossDomainOrchestration/JournalEntryCreation.fs:72, :95, :118; Tests/Tests.Integrated/Model/Ledger/JournalEntryExternalReference.fs:107; Tests/Tests.Integrated/Model/Ledger/JournalEntryComment.fs:85 (REQ-JE-2.1, 2.2, 1.21, 2.9, 1.40, 4.10, 5.2)
- **Summary:** Every 'generates a unique UUID' test asserts only `Assert.NotEqual(Guid.Empty, id)` (Specimen 2); none checks uniqueness or that the ID was newly generated.
- **Resolution:** fix-test

These tests make the same single check: 2.1 (`Assert.NotEqual(Guid.Empty, jeHappyId |> JournalEntryHeaderId.value)`), 2.2/1.21 (`List.iter(fun x -> Assert.NotEqual(Guid.Empty, ...))` over two lines), 2.9/1.40 (the same over two references), 4.10 (one reference) and 5.2 (comment ID). Nothing compares the two line IDs with each other, the two reference IDs with each other, or any child ID with the header ID. Nothing checks that a returned ID differs from every pre-existing fixture ID or that two posts get different IDs. A generator returning a fixed non-empty Guid, or reusing the header's Guid for every child, passes the assertion as written. Primary keys would reject duplicates within one table, so the realistic escape is cross-entity reuse, which no assertion rules out. RevisedRequirementsLedger.fs:144 repeats the pattern for comment IDs.

**Action:** Assert distinctness from input-derived expectations, for example `Assert.Equal(lineCount, ids |> List.distinct |> List.length)`, `Assert.DoesNotContain(headerId, lineIds)`, and that the new ID is not among `fixture.Data` IDs.

**Why:** `<> Guid.Empty` is an inequality that has given up: it shows the ID is not the default value, not that it is unique, which is what the REQ text says.

---

## EFF-JE-5 — test-gap
- **Location:** Tests/Tests.Integrated/CrossDomainOrchestration/JournalEntryCreation.fs:288-319; Tests/Tests.Integrated/CrossDomainOrchestration/JournalEntryFetching.fs:174-189 (REQ-JE-1.48)
- **Summary:** The REQ-JE-1.48 creation test contains no assertion (Specimen 7), and the REQ-JE-1.48 fetch test is count-only (Specimen 3).
- **Resolution:** fix-test

``REQ-JE-1.48 constructNewAndPersist accepts duplicate source_fi/reference pairs`` posts an entry carrying `[sameRef; sameRef]`, then a second entry carrying `[sameRef]`, and ends with `return ()`. It never checks that either entry kept its references. If constructNewAndPersist silently dropped or deduplicated a duplicate pair, the test would still pass. ``REQ-JE-3.5 REQ-JE-1.48 fetchByReference returns multiple entries when reference is shared`` asserts only `Assert.Equal(expected, fetched |> List.length)` and never looks at which entries came back. Mitigation: the route test at JournalEntryRoutes.fs:221 (cites REQ-JE-3.5) does compare entry IDs for F-SHARED-001, which covers the across-entries clause through fixture data.

**Action:** In the creation test, assert that each returned entry carries the expected (fi, reference) list, e.g. `Assert.Equal<(string*string) list>([sameRef; sameRef], refsOf first)`. In the fetch test, compare the set of returned entry IDs with the IDs derived from the fixture, as the route test already does.

**Why:** A test with no assertion only proves the call returned Ok. 'Permitted' means the duplicate is accepted and stored, and nothing checks that it was stored.

---

## EFF-JE-6 — test-gap
- **Location:** Tests/Tests.Integrated/CrossDomainOrchestration/JournalEntryCreation.fs:334-347 (REQ-JE-2.12)
- **Summary:** The only test citing REQ-JE-2.12 (atomicity) runs inside runCommandRouteAndAutoRollback and asserts only the error case (Specimen 9); the tests that do observe atomicity cite other REQ IDs.
- **Resolution:** fix-test

``REQ-JE-1.13 REQ-JE-2.12 constructNewAndPersist rejects unbalanced entry`` matches `JournalEntryDebitCreditMismatch` inside a test-owned transaction that is rolled back whatever happens. It cannot see whether the header and lines (which constructNewAndPersist writes before confirmLineList runs, JournalEntryOrchestration.fs:157-162) would have survived. The requirement 'if any validation fails, no rows may be persisted' is observable only after the route's transaction has closed. Tests that do check this from outside the boundary exist, but none cites REQ-JE-2.12: JournalEntryRoutes.fs:69 (insufficient lines, cites 2.13/2.3), RevisedRequirementsLedger.fs:120 (bad last comment, cites 2.11), and the JournalEntryCommentsAndReads.fs 2.15 theory (comment faults, `Assert.Empty(describedOn ...)`). None of these covers the debit/credit-mismatch vector the 2.12 test claims.

**Action:** Add REQ-JE-2.12 to the route-level atomicity tests (JournalEntryRoutes.fs:69, RevisedRequirementsLedger.fs:120), or add a route-level unbalanced-entry test that reads back through a fresh FetchOnly context. Remove REQ-JE-2.12 from the in-transaction CDO test name.

**Why:** When the behavior is atomicity, a test inside the transaction cannot fail, so the traceability matrix shows REQ-JE-2.12 as covered by a test that cannot detect the defect it names.

---

## EFF-JE-7 — test-gap
- **Location:** Tests/Tests.Integrated/InterfaceBridge/JournalEntryRoutes.fs:42-66, :197-218, :264-280; Tests/Tests.Integrated/InterfaceBridge/AccountRoutes.fs:295-327, :330-338; Tests/Tests.Integrated/CrossDomainOrchestration/JournalEntryCreation.fs:35-55 (REQ-JE-2.3, 2.11, 2.13, 3.3, 3.7, 3.9, 3.6)
- **Summary:** Several happy-path tests assert only a count (Specimen 3), sometimes a hard-wired one (Specimen 1), and never inspect the returned values.
- **Resolution:** fix-test

PostNew route (2.13/2.3, line 59): `Assert.Equal(2, returned.lines |> List.length)` plus the description. It never checks that the returned lines carry the input account codes (the subject of REQ-JE-2.3), amounts or line types. The CDO post (2.13/2.11, JournalEntryCreation.fs:52) has the same shape, `Assert.Equal(2, jeHappy |> jeLines |> List.length)`, although REQ-JE-2.11 requires returning 'the fully constructed journal entry with all generated IDs and timestamps'. FetchByPeriod route (3.3, line 215) and FetchByDateRange route (3.7, line 277) assert only `Assert.Equal(expected, returned |> List.length)`. FetchActivity route (3.9, AccountRoutes.fs:323) is count-only. FetchBalances route (3.6, AccountRoutes.fs:335) is `Assert.Equal(2, returned |> List.length)`: a hard-wired count of the input list with no debit, credit or net value inspected, so a route returning two zeroed balances passes. Mitigation: the CDO-layer tests for 3.3, 3.7 and 3.6 do compare IDs and amounts, and the AC-3.13 route tests check balances exactly. Tests/README.md still requires every happy path at each layer to assert values, with counts allowed only in addition.

**Action:** Add value assertions to each happy path: line (accountCode, amount, lineType) sets equal to the input; returned header and line IDs with createdAt; returned entry ID sets derived from fixture data for 3.3/3.7/3.9; and debit, credit and net amounts from fixture lines for the 3.6 route.

**Why:** A route that returns the right number of rows with the wrong contents (mis-mapped codes, wrong period, zeroed balances) passes every one of these tests.

---

## EFF-JE-8 — test-gap
- **Location:** Tests/Tests.Integrated/CrossDomainOrchestration/JournalEntryVoiding.fs:159, :264; Tests/Tests.Integrated/InterfaceBridge/JournalEntryRoutes.fs:305 (REQ-JE-4.3)
- **Summary:** Every REQ-JE-4.3 void test asserts only that voidedAt is Some, never that it equals the void operation's instant.
- **Resolution:** fix-test

Every positive REQ-JE-4.3 assertion has the form `Assert.True(voided |> header |> JournalEntryHeader.voidedAt |> Option.isSome)` (route: `Assert.True(voided.header.voidedAt |> Option.isSome)`). REQ-JE-4.3 sets the void marker, and REQ-JE-1.14 defines that marker as 'voided at that instant'. voidById (JournalEntryVoiding.fs:54-57) writes `context |> Context.getInitiationInstant`. If it wrote any other instant (a fresh clock read, the entry's createdAt, Instant.MinValue), every void test would still pass. The REQ-SYS-3.4 void case in OperationInstantAndAtomicity.fs checks modifiedAt, not voidedAt.

**Action:** In the CDO REQ-JE-4.3 test, assert `Assert.Equal(Some(context |> Context.getInitiationInstant), voided |> header |> JournalEntryHeader.voidedAt)`.

**Why:** `isSome` is a shape check. The value the marker records is part of the requirement, and garbage of the right shape passes.

---

## EFF-JE-9 — test-gap
- **Location:** Tests/Tests.Integrated/CrossDomainOrchestration/AccountActivity.fs:60-105, :108-152 (REQ-JE-3.9, REQ-JE-3.9.1)
- **Summary:** The REQ-JE-3.9 enrichment tests only ever observe a null voided_at, so an enrichment that always returned None would pass.
- **Resolution:** dan-decides

``REQ-JE-3.9 activity detail carries its parent entry's date, description, source, and voided-at`` checks the unvoided fixture entry 'Fixture JE with reference', so its voided-at assertion compares None with None (line 101-103). The 3.9.1 test asserts `detail.journalEntryVoidedAt |> Option.isNone` for every returned row. No REQ-JE-cited test reads an activity row for a voided entry and checks that its voided_at is populated. That case is covered only by REQ-AC-3.12.2 at AccountCreateActivityBalance.fs:511, which does not cite REQ-JE-3.9. Taken together with EFF-JE-1 and EFF-JE-2, the strong verification of the REQ-JE-3.6 and REQ-JE-3.9 families sits under REQ-AC-3.12 and REQ-AC-3.13 IDs, and the JE-cited copies are weaker duplicates of the same functions (AccountActivity.fetchFiltered, AccountBalance.fetchByAccountIdList).

**Action:** Either point the 3.9 enrichment test at a voided fixture entry's line (fixture.Data.voidedJeId) and assert `Some voidedAt`, or decide whether REQ-JE-3.6/3.9 and REQ-AC-3.12/3.13 are duplicate requirements. If they are, co-cite the AC tests and retire the weaker JE copies.

**Why:** Two requirement IDs covering one function means one of them is verified weakly. Traceability then reports full coverage for the ID whose tests cannot see the defect.

---

## EFF-JE-10 — test-gap
- **Location:** Tests/Tests.Isolated/Model/Ledger/JournalEntryComponent.fs:88-90 (REQ-JE-1.3); null clauses of REQ-JE-1.42, 1.44, 1.54
- **Summary:** The only REQ-JE-1.3 test ('description cannot be null') asserts that a valid string is accepted; no test anywhere sends a null description, FI, reference value or comment text.
- **Resolution:** dan-decides

``REQ-JE-1.3 Description.create accepts valid non-empty string`` is `Assert.True(Result.isOk (JournalEntryDescription.create "Monthly rent payment"))`, which checks the opposite property from the REQ. JournalEntryDescription.create (Src/Business.FinancialServices.Ledger/JournalEntryComponent.fs:69) calls `raw.Trim()` on a raw string, so a null reaching it would throw NullReferenceException rather than return a typed error. The contract field `description: string` (JournalContracts.fs:16) relies on FSharp.SystemTextJson rejecting null or missing fields at deserialization; Dan's NGUI-2.5 withdrawal rationale states that deserialization 'fails on any missing field'. No test sends `"description": null` (or omits it) through PostNew to show that a typed error comes back. The same untested null clause appears in REQ-JE-1.42 (FI), 1.44 (reference value) and 1.54 (comment text), whose citing tests cover only empty, whitespace and length. Other non-nullable fields in this spec (1.1, 1.9, 1.20) were waived as type-enforced. REQ-JE-1.3 was not waived, yet its citation tests nothing about null.

**Action:** Dan decides: either waive the null clauses of REQ-JE-1.3/1.42/1.44/1.54 as enforced by deserialization (as with REQ-JE-1.1/1.9), or add a route test posting a payload with a null description and asserting the typed JsonDeserializationFailed error.

**Why:** A citation that tests the opposite property, acceptance of a valid value, makes an untested rejection rule show up as covered in the traceability matrix.

---

## EFF-JE-11 — test-gap
- **Location:** Tests/Tests.Isolated/Model/Ledger/JournalEntryComponent.fs:40, 53, 77, 90, 114, 186; Tests/Tests.Isolated/Model/Ledger/JournalEntryExternalReference.fs:39, 52, 76, 89 (REQ-JE-1.5, 1.8, 1.28, 1.45, 1.49, 1.54, 1.42, 1.44)
- **Summary:** The 'accepts at exactly N characters' and 'accepts valid string' tests assert only `Assert.True(Result.isOk result)` and never inspect the constructed value.
- **Resolution:** fix-test

Example: ``REQ-JE-1.5 Description.create accepts string at exactly 1000 characters``: `let result = JournalEntryDescription.create(String('A', 1000)) ; Assert.True(Result.isOk result)`. If the constructor accepted the input but silently truncated it (say to 999 characters), or returned Ok wrapping a different string, every one of these tests would pass. The trimming tests in the same files show the correct pattern: they match Ok and `Assert.Equal(trimmed, X.value v)`.

**Action:** Replace `Assert.True(Result.isOk result)` with a match or railroad that asserts `Assert.Equal(input, X.value v)` (for example `String('A', 1000)`).

**Why:** isOk on the happy path has the same weakness as isError on the sad path: it accepts any Ok, including a wrong one.

---

## EFF-JE-12 — test-gap
- **Location:** Tests/Tests.Integrated/CrossDomainOrchestration/OperationInstantAndAtomicity.fs:164-166 (REQ-JE-1.53 cited in comment)
- **Summary:** A comment in this file cites REQ-JE-1.53, but the only assertion about that rejection is `Assert.True(attempt |> Result.isError)` (Specimen 4).
- **Resolution:** fix-test

In ``REQ-SYS-3.3 a rejected update leaves the record's modified-at unchanged``, the comment reads `// a comment can't name its own primary as its secondary (REQ-JE-1.53)` and is followed by `Assert.True(attempt |> Result.isError)`. Per Tests/README.md, an ID in a comment counts as a citation and is allowed only when a test in that same file genuinely covers it. Nothing in this file asserts the typed `JournalEntryCommentPrimaryAndSecondaryIdsAreSame`, and the isError check would pass on a not-found error, a DAL error or any other failure. The behavior is typed-tested elsewhere (JournalEntryRoutes.fs:532 'sameIds'; Model/Ledger/JournalEntryComment.fs:116), so REQ-JE-1.53 itself is covered. The defect is an untyped assertion sitting behind a REQ citation.

**Action:** Replace `Assert.True(attempt |> Result.isError)` with a typed match on `JournalEntryCommentPrimaryAndSecondaryIdsAreSame`, or rewrite the comment so it describes the rule without the REQ-ID pattern.

**Why:** isError passes for any failure, and the README forbids citing an ID that nothing in the file genuinely backs.

---

## EFF-JE-13 — test-gap
- **Location:** Tests/Tests.Helpers/TestDataStage.fs:662-664 (REQ-JE-4.10, REQ-JE-1.48)
- **Summary:** A fixture comment in a helper file that contains no tests names REQ-JE-4.10 and REQ-JE-1.48, the exact pattern Tests/README.md forbids.
- **Resolution:** fix-test

The comment above `duplicateRefJe` reads '...REQ-JE-4.10 appends a reference to an existing entry with no uniqueness check... REQ-JE-1.48 only speaks to duplicates across entries...'. Tests/README.md (Naming) forbids 'a comment explaining a fixture or helper by naming the requirement it exists to serve', because traceability-audit.sh greps every file under Tests/ and counts such mentions as citations. TestDataStage.fs has zero tests, so these two mentions are citations that no test backs. Both REQs are tested elsewhere, so today this creates no false coverage. If those tests were removed, however, the traceability check would still report both REQs as covered.

**Action:** Rewrite the comment in prose without the REQ-ID pattern (for example 'appending a reference (spec section 4) does no uniqueness check').

**Why:** The README records that a fixture comment of this kind once tripped the stale-waiver invariant. Citations in files with no tests make the traceability gate unreliable.

---

