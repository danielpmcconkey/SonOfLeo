# JournalEntryCrud Test-Efficacy Auditor

## IDIOM-JE-1 — test-gap
- **Location:** Tests/Tests.Integrated/ModelOrchestrator/AccountBalance.fs, lines 187-188; REQ-JE-3.6.1
- **Summary:** REQ-JE-3.6.1 net-balance-orientation test uses a cowardly inequality (Specimen 2) instead of asserting the exact expected amount.
- **Resolution:** fix-test

The test `REQ-JE-3.6.1 net balance is positive in normal-balance orientation` posts a known $200 debit to an expense account and a known $200 credit to a revenue account, then asserts:

```fsharp
Assert.True(expenseBal.netBalance |> Money.amount > zero)
Assert.True(revenueBal.netBalance |> Money.amount > zero)
```

The exact expected net balance is $200 for both accounts (expense: debits - credits = 200 - 0; revenue: credits - debits = 200 - 0). The `amount` variable holding 200.00M is already in scope. A balance computation that returned $0.01 or $999999 would pass this test. The debit and credit totals ARE asserted exactly (lines 185-186, 189-190: `Assert.Equal(amount, ...)` and `Assert.Equal(zero, ...)`), but the net balance — which is the property REQ-JE-3.6.1 specifically describes — uses only `> zero`.

**Action:** Replace both `Assert.True(... > zero)` with `Assert.Equal(amount, expenseBal.netBalance |> Money.amount)` and `Assert.Equal(amount, revenueBal.netBalance |> Money.amount)`. The value is already in the `amount` binding.

**Why:** Specimen 2 (the cowardly inequality) tolerates any positive value. The entire point of REQ-JE-3.6.1 is that the net balance computation uses the correct normal-balance orientation formula. A sign-correct but magnitude-wrong calculation passes this test. The smell test confirms it: if `fetchByAccountIdList` returned garbage of the right sign, this test would not fail.

---

## IDIOM-JE-2 — test-gap
- **Location:** Tests/Tests.Integrated/InterfaceBridge/JournalEntryRoutes.fs, line 662; REQ-JE-3.4
- **Summary:** REQ-JE-3.4 has no happy-path test; the only citing test exercises sad-path validation, leaving the actual line-retrieval behavior of JournalEntryLine.fetchByAccountId untested.
- **Resolution:** fix-test

REQ-JE-3.4 states: 'The system must be able to retrieve all journal entry lines for a given account.' The `FetchLinesByAccount` route exists and is wired to `JournalEntryLine.fetchByAccountId` (Src/InterfaceBridge/Routes/JournalEntryRoutes.fs:67). The only test citing REQ-JE-3.4 is a theory test that validates bad inputs (empty code, too-long code, nonexistent code) — three sad-path scenarios. No test anywhere exercises the happy path: passing a valid account code and verifying that the correct lines are returned with correct values.

REQ-JE-3.9 tests (AccountActivity.fs) exercise `fetchFiltered`, which is a different code path producing enriched results. REQ-JE-3.4's note explicitly states the underlying model code is retained separately: 'this requirement is retained alongside JE-3.9 because the underlying model code exists and may serve a future need.' That code path (`JournalEntryLine.fetchByAccountId`) is also used by `AccountDeactivation` (Src/ModelOrchestrator/AccountDeactivation.fs:69), so it is production code with no direct test.

**Action:** Add a happy-path test for REQ-JE-3.4 at the orchestrator or route level. Pass a valid account code for a fixture account with known lines, deserialize the result, and assert line count (derived from fixture data) and at least one line's value properties.

**Why:** A code path that exists in production and is called by the deactivation orchestrator has no test proving it returns correct data. If the SQL query or reconstitution in JournalEntryLine.fetchByAccountId were broken, no test would go red.

---

## IDIOM-JE-3 — test-gap
- **Location:** Tests/Tests.Integrated/ModelOrchestrator/JournalEntryCommentOrchestration.fs, line 64; REQ-JE-4.9
- **Summary:** Test cites REQ-JE-4.9 (external reference update) but exercises comment update no-op rejection, which is a different behavior.
- **Resolution:** fix-test

The test named `REQ-JE-4.9 updateComment rejects no-op when both fields are NoChange` calls `JournalEntryCommentOrchestration.updateComment` with NoChange for both fields and asserts the `JournalEntryCommentUpdateNoOp` error. REQ-JE-4.9 states: 'The system must provide a means for an actor to update a journal entry reference's FI and value.' That is about external references, not comments.

The external reference no-op test exists correctly at the route level: `REQ-JE-4.9 UpdateExternalReference rejects no-op update` (JournalEntryRoutes.fs:501). So the external reference behavior IS tested; the problem is that the comment-level test inflates REQ-JE-4.9's apparent coverage in traceability audits while leaving comment update no-op rejection without a proper REQ citation (it should cite REQ-SYS-6.1 or a JE-specific no-op REQ if one exists).

**Action:** Rename the test to cite the correct requirement. If comment update no-op falls under REQ-SYS-6.1, cite that. If no REQ covers comment update no-op specifically, either add one or note the behavior as an uncited extension of REQ-SYS-6.1.

**Why:** Traceability audits grep for REQ IDs. This test makes the audit believe REQ-JE-4.9 is tested here when it is not, and leaves the comment no-op behavior without a proper requirement anchor. The citation error is benign today (REQ-JE-4.9 is well-tested elsewhere) but misleads anyone using the traceability report to gauge coverage.

---

## IDIOM-JE-4 — test-gap
- **Location:** Tests/Tests.Integrated/ModelOrchestrator/JournalEntryCreation.fs, lines 136-255; REQ-JE-1.6, REQ-JE-1.26, REQ-JE-1.46, REQ-JE-1.55
- **Summary:** Six creation tests (Specimen 7) prove the call returned Ok but discard the returned entry and contain no assertion — they cannot detect silent data corruption of nullable/optional fields.
- **Resolution:** fix-test

The following tests bind the result of `createTestJournalEntryFromPrimitives` to `_` and contain zero Assert.* calls:

1. `REQ-JE-1.46 constructNewAndSaveToDb accepts an entry with zero external references` (line 136)
2. `REQ-JE-1.46 constructNewAndSaveToDb accepts an entry with multiple external references` (line 155)
3. `REQ-JE-1.55 constructNewAndSaveToDb accepts an entry with zero comments` (line 175)
4. `REQ-JE-1.55 constructNewAndSaveToDb accepts an entry with multiple comments` (line 195)
5. `REQ-JE-1.6 constructNewAndSaveToDb accepts an entry with null source` (line 217)
6. `REQ-JE-1.26 constructNewAndSaveToDb accepts lines with null memos` (line 237)

Smell test: if `constructNewAndSaveToDb` silently added a default source (when None was passed), added a phantom external reference (when [] was passed), or dropped a comment (when two were passed), every one of these tests would remain green. The returned entry is discarded without inspection.

This matches Specimen 7 from the bullshit-test-specimens doc: 'there is no assertion in it. It proves the call returned Ok.'

**Action:** In each test, bind the returned JournalEntry (instead of discarding with `_`) and add at minimum: for REQ-JE-1.6, assert returned source is None; for REQ-JE-1.26, assert returned line memos are None; for REQ-JE-1.46, assert `externalReferences |> List.length` equals the input count (0 or 2); for REQ-JE-1.55, assert `comments |> List.length` equals the input count (0 or 2).

**Why:** A test with no assertion passes unconditionally (modulo errors caught by the railroad). These tests assert acceptance but not preservation — they cannot catch bugs where the system transforms nullable inputs into defaults or drops collection members. Specimen 7 is the cheapest specimen to detect and the easiest to miss in review because the body looks like work.

---
