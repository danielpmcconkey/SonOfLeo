# test-efficacy-AccountCrud

## EFF-AC-1 — enforcement-gap
- **Location:** REQ-AC-1.4, REQ-AC-2.9; Tests/Tests.Integrated/Model/Ledger/Account.fs:37-79; Src/Business.CrossDomainOrchestration/AccountCreation.fs:60-92; Src/App.DataAccessLayer/DalError.fs:26
- **Summary:** Duplicate-code rejection is asserted only as the generic DalErrorDuringNonQueryExecution, which any failed insert satisfies, because account creation has no typed duplicate-code error.
- **Resolution:** dan-decides

The only test of REQ-AC-1.4/2.9 asserts `isCorrectError r DalErrorDuringNonQueryExecution None` (Account.fs:64). DalErrorDuringNonQueryExecution is `of exn` (DalError.fs:26) and wraps every non-query exception: unique, FK, check, length violations and connection faults alike. AccountCreation.constructNewAndPersist (lines 70-91) does no code-uniqueness check before calling Account.persist, so the rejection is the raw Postgres unique-violation surfaced as a DAL exception (ToMessage includes the stack trace, DalError.fs:70). The test's follow-up assertions (lines 67-75: incumbent still sole holder, same ID/name/type) rule out an overwrite. They do not show that the insert failed because of the code collision. If any other constraint started failing on this insert, the test would still pass. This is the Specimen 4 failure mode ('passes on a raw data access layer error'), here inside a typed match because no meaningful case exists to match. Other domains in the repo have typed uniqueness errors: PersonNameAlreadyExists (BizGeneralError.fs:22), IngestionSourceNameAlreadyExists (DataIngestionError.fs:35), and PositionsSecurityNameAlreadyExists, PositionsTickerAlreadyExists, PositionsInvestmentAccountNameAlreadyExists (PositionsError.fs). The spec preamble asks for 'meaningful error messages'.

**Action:** Decide whether Account creation should raise a typed AccountCodeAlreadyExists error (by pre-check or by mapping the unique violation). If so, change the REQ-AC-1.4/2.9 test to match that case and its code payload.

**Why:** An assertion that accepts every insert failure cannot show which rule rejected the input. Some other failure on this insert path would keep the test green while uniqueness enforcement is unverified, and the CLI user gets an exception dump instead of a domain error.

---

## EFF-AC-2 — test-gap
- **Location:** REQ-AC-4.3; Tests/Tests.Integrated/CrossDomainOrchestration/AccountDeactivation.fs:115-127 and 160-169
- **Summary:** Neither REQ-AC-4.3 test can tell whether 'active children' is judged as of the current date, as the amended requirement says.
- **Resolution:** fix-test

REQ-AC-4.3 (amended 2026-10-03) rejects deactivation when the account has children that are active as of the current date (the initiation instant's calendar date). There are two tests. Negative (line 115): parent assets1000Id, whose fixture children have open-ended activity windows. Positive (line 160): child active end = today-1, parent requested end = today. Two wrong implementations pass both: (a) 'reject if any child has a null active end' and (b) 'judge child activity as of the requested active end'. Neither test has a child whose activity differs between today and another candidate reference date. The missing cases are a child whose active end is today (still active under the inclusive boundary of REQ-AC-1.48, so the request must be rejected), a child with a future scheduled end (active, so reject), and a child that ended yesterday while the parent's requested end is in the past (inactive today, so permit). The current date-reference clause is the 2026-10-03 amendment, and no test pins it.

**Action:** Add a REQ-AC-4.3 negative test where the parent's only child has active end = today (or a future scheduled end), expecting AccountActiveChildrenBeforeDeactivation.

**Why:** The amendment changed the reference point, and the tests pass for implementations that ignore it. A test that both a correct and an incorrect reference date satisfy does not cover the requirement.

---

## EFF-AC-3 — test-gap
- **Location:** REQ-AC-2.7; Tests/Tests.Integrated/Model/Ledger/Account.fs:240-275
- **Summary:** Neither REQ-AC-2.7 test can tell 'parent active as of the current date' apart from 'parent has no active end'.
- **Resolution:** fix-test

Positive test (line 241): parent revenue4000Id, open-ended. Negative test (line 257): parent closedBank1290Id, deactivated two months ago in the fixture (TestDataStage.fs:634). An implementation that checks only `parent.activeEnd.IsNone` passes both. That implementation would wrongly reject a parent with a future scheduled end (REQ-AC-2.23 makes this reachable, and the parent is active today) and wrongly accept a parent whose active begin is in the future. The amended clause 'active as of the current date' (2026-10-03) is not pinned by either test. The positive test also has no assertion (see EFF-AC-4).

**Action:** Add a REQ-AC-2.7 positive test with a parent whose active end is scheduled in the future (or is today), and assert the child is created with that parent.

**Why:** Without a parent whose activity depends on the reference date, the suite cannot detect the shortcut the amendment was written to exclude.

---

## EFF-AC-4 — test-gap
- **Location:** REQ-AC-4.2: Tests/Tests.Integrated/CrossDomainOrchestration/AccountDeactivation.fs:103-112; REQ-AC-2.7: Tests/Tests.Integrated/Model/Ledger/Account.fs:241-254
- **Summary:** Two acceptance tests contain no assertion and pass on any Ok, which is Specimen 7.
- **Resolution:** fix-test

`REQ-AC-4.2 deactivateAccount accepts end equal to begin` (line 103) runs `let! _ = account |> deactivateAccount context equalEnd` and returns. The result is discarded and nothing is read back. `REQ-AC-2.7 parent account must be active at AuditEnvelope instant--positive` (Account.fs:241) pipes constructNewAndPersist straight into railroadWrapper. Neither body has an `Assert.` or a typed `| Error` arm, which is the Specimen 7 detection signature. A deactivate that returned Ok without storing an end equal to begin, or a create that returned Ok without persisting the parent link, would pass. Sibling tests in AccountDeactivation.fs already use `deactivateAndReadBack` (lines 55-61) and assert the stored end; the equality-boundary test does not.

**Action:** Make the 4.2 equality test use deactivateAndReadBack and assert that the stored active end equals active begin. Make the 2.7 positive test fetch the created account and assert its parentId.

**Why:** An acceptance test that asserts nothing proves only that the call returned Ok. Here, that a single-day window is stored and that an active parent is linked are both unverified.

---

## EFF-AC-5 — test-gap
- **Location:** REQ-AC-3.12; Tests/Tests.Integrated/CrossDomainOrchestration/AccountActivity.fs:32-57
- **Summary:** The unfiltered-activity test checks row counts and one row's non-blank description and non-empty GUID: Specimens 3 and 2.
- **Resolution:** fix-test

`REQ-AC-3.12 fetchFiltered by account returns all activity with no filters set` asserts `Assert.Equal(expectedCountTotal, activities |> List.length)`, `Assert.Equal(expectedCountDetails, withDetail |> List.length)`, `Assert.False(String.IsNullOrWhiteSpace descriptionText)` on the head row, and `Assert.NotEqual(Guid.Empty, ...)`. The right number of rows with wrong values passes, as do duplicated lines balanced by dropped ones, and so does any non-blank description or any non-empty GUID. These are a count that never looks inside (Specimen 3) and two cowardly inequalities (Specimen 2). Other REQ-AC-3.12 tests check row contents (AccountCreateActivityBalance.fs:340, AccountActivity.fs:60), so the behavior is covered elsewhere. This test adds no verification beyond its counts.

**Action:** Replace the head-row checks with a set-equality assertion of (accountId, lineId) keys derived from fixture.Data.journalEntryLines plus the line-less fixture accounts, or retire the test as redundant with the row-content tests.

**Why:** Counts and non-blank checks pass for well-shaped garbage. The README allows counts only alongside value assertions, never instead of them.

---

## EFF-AC-6 — test-gap
- **Location:** REQ-AC-3.12.1, REQ-AC-3.12.2; Tests/Tests.Integrated/CrossDomainOrchestration/AccountActivity.fs:182-219
- **Summary:** The amount-filter test takes its expected count from unvoided lines only, but the query includes voided lines. The two populations differ, and the test passes only because no voided line has the target amount.
- **Resolution:** fix-test

`expectedCount` is computed from `nonVoidedLines` (voidedAt filtered out, lines 183-196), but the filter sent has `unVoidedOnly = false` (line 209). Under REQ-AC-3.12.2 that query must also return lines of voided entries. The expected value is therefore built from a different population than the requirement defines. The test passes today only because the fixture's one voided entry (75.00M, TestDataStage.fs:533-541) does not have the most common amount. If the fixture changes so that a voided line shares the target amount, a correct implementation fails the test. An implementation that wrongly drops voided lines from amount-filtered queries passes it, which contradicts 3.12.2.

**Action:** Derive targetAmount and expectedCount from all fixture lines, voided and unvoided, to match unVoidedOnly = false. Alternatively, set unVoidedOnly = true and keep the unvoided-only derivation.

**Why:** An expected value must come from the population the requirement defines. Here the test passes because of fixture contents, not because the filter behaves as specified.

---

## EFF-AC-7 — test-gap
- **Location:** REQ-AC-3.13, REQ-AC-3.13.1; Tests/Tests.Integrated/CrossDomainOrchestration/AccountBalance.fs:35-66
- **Summary:** The REQ-AC-3.13 totals test includes voided lines in its expected sums, although REQ-AC-3.13.1 says balances exclude them.
- **Resolution:** fix-test

The expected values come from `sumJournalEntryLinesByAccountIdAndType context false id1 Debit` and similar calls (lines 40-47). The second argument `false` is `unvoidedOnly`, so voided lines are counted (EntityFunctions.fs:162-180). REQ-AC-3.13.1 says balances exclude voided entries. The test passes only because mortgage2210 and food5350 have no voided lines in the fixture. If one is added, a correct implementation fails, and an implementation that ignores voids passes. The sibling test at line 69 passes `true`, so the helper supports the correct derivation.

**Action:** Change the four helper calls in the REQ-AC-3.13 totals test to unvoidedOnly = true, or derive the sums from fixture.Data.journalEntries filtered on voidedAt = None.

**Why:** The expected value encodes a rule the requirement forbids. The test currently agrees with correct code only because of fixture contents.

---

## EFF-AC-8 — customer-gap
- **Location:** REQ-AC-3.12; Src/Ui.InterfaceBridge/InterfaceContracts/AccountContracts.fs:50; Src/Ui.InterfaceBridge/BoundaryConverters/JournalEntryFieldConverters.fs:254; Tests/Tests.Integrated/CrossDomainOrchestration/AccountCreateActivityBalance.fs:560
- **Summary:** Activity rows return the parent account's name and a test asserts it, but no requirement mentions that field.
- **Resolution:** dan-decides

REQ-AC-3.12 lists the row's account fields as 'code, name, type, subtype, parent code and external reference'. The contract AccountActivityReturn also carries `accountParentName: string option` (AccountContracts.fs:50), filled in JournalEntryFieldConverters.fs:254. The REQ-AC-3.12.3 test asserts `accountParentName = Some $"Account test {fst t.p}"` (AccountCreateActivityBalance.fs:560). A grep of Specs/Behavioral for 'parent name' or 'parentName' finds nothing. The README says tests must not exercise behavior without a requirement to cite.

**Action:** Either add 'parent name' to the field list of REQ-AC-3.12, or remove accountParentName from the contract and the assertion.

**Why:** Behavior that the code returns and a test asserts but no spec states can drift or be removed without any requirement being violated. The spec should describe what users receive.

---

## EFF-AC-9 — maintainability
- **Location:** REQ-AC-2.6: Tests/Tests.Integrated/Model/Ledger/Account.fs:218-238; REQ-AC-1.40: Tests/Tests.Integrated/CrossDomainOrchestration/AccountCreation.fs:49-65
- **Summary:** Two tests check the same failure (create with a random nonexistent parent ID, rejected with AccountIdDoesntMatch) at the same layer.
- **Resolution:** fix-test

Both call AccountCreation.constructNewAndPersist with `Guid.NewGuid() |> AccountId.fromGuid |> Some` as the parent and expect AccountIdDoesntMatch. The REQ-AC-2.6 test also checks the payload (`Assert.Equal(parentId, uuid)`); the REQ-AC-1.40 test checks only the case via isCorrectError. It is the same caller, the same layer, the same input shape and the same error. Tests/README.md lists 'Do not test the same thing twice' as unacceptable practice.

**Action:** Merge them into one test named for both REQ-AC-1.40 and REQ-AC-2.6, keeping the payload-checking version.

**Why:** Duplicate tests make coverage look larger than it is and must be maintained twice. The README forbids them.

---

## EFF-AC-10 — test-gap
- **Location:** REQ-AC-2.13; Tests/Tests.Integrated/CrossDomainOrchestration/AccountCreation.fs:28-47
- **Summary:** A test cites REQ-AC-2.13 (system-generated unique UUID) but asserts only the created/modified timestamps.
- **Resolution:** fix-test

`REQ-AC-2.13 REQ-SYS-3.2 constructNew sets timestamps from AuditEnvelope` asserts only `Assert.Equal(expected, Account.createdAt account)` and `Assert.Equal(expected, Account.modifiedAt account)`. Nothing about the generated ID is checked. REQ-AC-2.13 is covered by the separate test at AccountCreation.fs:82, which checks distinct IDs. The 2.13 citation on the timestamp test therefore counts as coverage it does not provide. The timestamp-return clause belongs to REQ-AC-2.14 (amended 2026-10-03: 'return an account record with the created ID and created/modified timestamps') or to REQ-SYS-3.2.

**Action:** Drop REQ-AC-2.13 from that test's name. Cite REQ-AC-2.14 instead if the timestamp-return clause is meant to be covered there.

**Why:** Traceability counts test names as coverage, so a citation the body does not support overstates coverage.

---


