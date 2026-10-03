# test-efficacy-AccountCrud

## EFF-AC-3.9 — test-gap
- **Location:** Tests/Tests.Integrated/Model/Ledger/Account.fs:188-204 (REQ-AC-3.9); Src/Business.FinancialServices.Ledger/Account.fs:211-224
- **Summary:** The REQ-AC-3.9 active-accounts test works out its expected value with ActivityPeriod.isActive, the same predicate Account.fetchAll uses to filter. This is the Specimen 6 'fox guarding the hen house' pattern, and apart from one excluded ID the test only checks a count.
- **Resolution:** fix-test

Account.fetchAll(activeOnly=true) filters rows with `List.filter(fun x -> x.activityPeriod |> ActivityPeriod.isActive activeReference)` (Src Account.fs:221). The test builds its expected set the same way: `fixture.Data.accounts |> List.filter(fun a -> a |> Account.activityPeriod |> ActivityPeriod.isActive today)` (test line 193). It then asserts `Assert.Equal(expectedCount, fetched |> List.length)` (line 198) and that closedBank1290Id is absent (lines 199-201). Both sides of the comparison run the predicate under test, so the expected value cannot catch a bug in the active-relative-to-current-date rule. The isolated isActive tests guard the predicate's semantics. What nothing guards is how fetchAll wires it in: the reference date (the context initiation instant converted to an Eastern date), and whether the right accounts come back rather than the right number of accounts. A fetch that returned N copies of one active account would pass. The README rule 'No function in the call chain of the function under test may appear in the derivation of the expected value' is broken here.

**Action:** Build the expected active-account ID set from the fixture's raw activeBegin/activeEnd dates with plain list comparisons against the Eastern calendar date, without calling ActivityPeriod.isActive. Assert set equality of the account IDs, not just the count.

**Why:** When the expected value runs through the code under test, the test only shows the function agrees with itself. A wrong reference date or a wrong predicate gives the same wrong answer on both sides.

---

## EFF-AC-3.7 — test-gap
- **Location:** Tests/Tests.Integrated/Model/Ledger/Account.fs:177-186; Tests/Tests.Integrated/InterfaceBridge/AccountRoutes.fs:166-176 (REQ-AC-3.7)
- **Summary:** Both tests that cite REQ-AC-3.7 (retrieve all accounts with no filter) check only the row count and never look at which accounts came back (Specimen 3).
- **Resolution:** fix-test

The model test asserts only `Assert.Equal(expectedCount, fetched |> List.length)` (line 183). The route test asserts only `Assert.Equal(expected, fetchedAccounts |> List.length)` (line 173), with expected = fixture.Data.totalAccounts. Neither inspects one account ID, code or field. A fetch that returned the right number of rows with duplicates, or with one account in place of another, passes both. No other test establishes that fetch-all returns every fixture account.

**Action:** In at least one of the two tests, compare the set of returned account IDs (or codes) with the set derived from fixture.Data.accounts, alongside the count.

**Why:** Specimen 3: counts are allowed in addition to value assertions, never instead of them. A count cannot tell the right N rows from the wrong N rows.

---

## EFF-AC-3.13.3 — test-gap
- **Location:** Tests/Tests.Integrated/CrossDomainOrchestration/AccountCreateActivityBalance.fs:759-762 (REQ-AC-3.13.3)
- **Summary:** REQ-AC-3.13.3 requires that an empty code list 'fails with a typed error'. Its only citing test asserts `Assert.True(attempt |> Result.isError)`, which is Specimen 4 word for word.
- **Resolution:** fix-test

The test body is `let attempt = balances [] None` followed by `Assert.True(attempt |> Result.isError)`. Any error passes: a JSON deserialization failure, a DAL error, a missing route. The requirement's own wording ('typed error') is the property it leaves unchecked. A typed check does exist for this vector: the AccountRoutes.fs theory at lines 628-652 (`emptyList` -> `AccountBalanceFetchInvalidArguments`). That test cites REQ-JE-3.6, not REQ-AC-3.13.3, so the AC citation points at the untyped test.

**Action:** Replace the isError assertion with a typed match on AccountBalanceFetchInvalidArguments, with both escape arms. Alternatively, cite REQ-AC-3.13.3 on the existing typed route theory row and delete the untyped test.

**Why:** Specimen 4: `isError` passes for any failure, including the wrong validation firing or a broken route. A requirement that names a typed error needs a test that names the type.

---

## EFF-AC-2.4-MAP — test-gap
- **Location:** Tests/Tests.Isolated/Model/Ledger/AccountComponent.fs:85-108 (REQ-AC-2.4, REQ-AC-1.10), 120-159 (REQ-AC-1.18)
- **Summary:** The tests for accepted account type and subtype strings assert only `Result.isOk`. They never check which DU case the string maps to, and no test rejects a case variant of a valid name, even though REQ-AC-2.4 requires an exact match.
- **Resolution:** fix-test

Each of the five REQ-AC-2.4/1.10 accept tests reads, for example, `Assert.True(Result.isOk(AccountType.fromString "Liability"))` (lines 86-103). Each of the nine REQ-AC-1.18 accept tests reads, for example, `Assert.True(Result.isOk(AccountSubtype.fromString "Cash"))` (lines 121-154). If fromString "Equity" returned Ok Revenue, or "OtherRevenue" returned Ok OperatingRevenue, all of these pass. The integrated tests would not catch it either. Persist writes AccountType.toString and reconstitute reads back through fromString, so a swapped mapping round-trips symmetrically. The fixture's Equity accounts are themselves built through fromString, so the REQ-AC-3.6 fetch-by-type tests would still pass, and Equity and Revenue share a normal balance, so balance tests would not notice. The only rejection test for 2.4 uses "Valley Girl" (line 107). Nothing shows that "asset" or "ASSET" is rejected, which is what 'must match ... exactly' demands, given the input is trimmed but not case-folded.

**Action:** Change the accept tests to assert the exact DU case (for example `Assert.Equal(Ok Liability, AccountType.fromString "Liability")`), ideally as a Theory over every name-to-case pair for both types and subtypes. Add a rejection case for a wrong-case spelling of a valid type name, asserting AccountTypeInvalid.

**Why:** A parser's job is the mapping. Asserting only that it returned something is Specimen 10's disease: the shape is right and the content goes unexamined.

---

## EFF-AC-1.28-NEG — test-gap
- **Location:** Src/Business.CrossDomainOrchestration/AccountCreation.fs:70-79,110; Tests/Tests.Isolated/Model/Ledger/AccountComponent.fs:167-484 (REQ-AC-1.28..1.36)
- **Summary:** No test shows that creating an account with an illegal type/subtype pair is rejected. The 45-cell truth table tests only the predicate, and the AccountInvalidTypeSubtypeCombo error is never asserted anywhere in Tests/.
- **Resolution:** fix-test

REQ-AC-1.28 through 1.36 define rejection rules ('can only be applied to', 'can only have'). The isolated tests cover every type x subtype cell of `AccountSubtype.validTypeSubtypeCombination`, which is the right shape for the predicate. However, the enforcement point is `confirmTypeAndSubtypeAreValid` in AccountCreation.constructNewAndPersist (Src line 110), and a grep of Tests/ for `AccountInvalidTypeSubtypeCombo` returns nothing. The ledger.account DDL (DbMigration/Scripts/202609071105-CreateLedgerTables.sql) has no CHECK constraint on the type/subtype pair, so the orchestrator check is the only guard. Deleting line 110, `do! confirmTypeAndSubtypeAreValid accountType subType`, would leave the whole suite green while illegal states such as Equity/Cash became persistable. The route theory at AccountRoutes.fs:341-381 tests only an unknown subtype string ("Fluffy"), not a legal subtype paired with the wrong type.

**Action:** Add a test (CDO or route level) that creates an account with a legal subtype on the wrong type, for example Liability with Cash, and asserts Error AccountInvalidTypeSubtypeCombo with both escape arms and that nothing was stored.

**Why:** Negative coverage has to reach the gate the user goes through, not only the helper it calls. A predicate proven correct but never wired in is an unenforced rule.

---

## EFF-AC-1.4-DAL — enforcement-gap
- **Location:** Tests/Tests.Integrated/Model/Ledger/Account.fs:36-79 (REQ-AC-1.4, REQ-AC-2.9); Src/App.DataAccessLayer/DalError.fs:27
- **Summary:** The duplicate-account-code test accepts `DalErrorDuringNonQueryExecution`, a catch-all DAL case that wraps any SQL exception. It therefore cannot tell a unique-code rejection from any other failed insert, and the user sees a raw database exception with a stack trace.
- **Resolution:** dan-decides

The test asserts `isCorrectError r DalErrorDuringNonQueryExecution None` (line 64). That case is `DalErrorDuringNonQueryExecution of exn` (DalError.fs:27), and its ToMessage renders `ex.Message` plus `ex.StackTrace` (DalError.fs:73). An FK violation, a permission denial or a malformed insert lands in the same case. The follow-up survivor assertions (lines 67-75) correctly rule out the overwrite scenario, but nothing ties the refusal to the account_code_key constraint. AccountCreation does no pre-insert uniqueness check (Src AccountCreation.fs:81-113), so REQ-AC-2.9's 'system must reject' is enforced only by the DB constraint, and the error reaching the caller is untyped in substance. The spec preamble asks for rejection 'with meaningful error messages'. No route-level test covers the duplicate-code create vector.

**Action:** Dan decides one of two options: (a) add a typed LedgerError for a duplicate account code (a pre-check or a mapped unique-violation) and assert that case, or (b) accept the DB-constraint path and have the test at least confirm the wrapped exception is a unique violation on account_code_key.

**Why:** A typed DU case is only as precise as its meaning. A case that means 'any SQL exception' brings back Specimen 4's isError problem, as the August 2026 ingestion tests showed.

---

## EFF-AC-1.5 — test-gap
- **Location:** Tests/Tests.Integrated/Model/Ledger/Account.fs:81-99 (REQ-AC-1.5)
- **Summary:** The case-sensitivity test's only assertion, `Assert.NotEqual(fixture.Data.assets1000Id, returned |> Account.accountId)`, can never fail. constructNewAndPersist always generates a fresh UUID.
- **Resolution:** fix-test

constructNewAndPersist assigns `AccountId.create()` = `Guid.NewGuid()` (Src AccountCreation.fs:92; AccountComponent.fs:9), so the returned ID differs from assets1000Id whether or not codes are case sensitive. The only real evidence is that the insert did not fail, which proves codes are case-sensitive for the unique constraint only. The test hard-wires "f-1000" and never checks that the fixture actually holds "F-1000". If that fixture code changed, the test would keep passing without testing anything. The other half of 'distinct account codes' is also untested: a fetch-by-code for "f-1000" should return the new account and not F-1000 (lookup goes through LookupCache.accountCodeToId).

**Action:** Take the code from the fixture account (for example `assets1000` code |> lower-case), assert that it differs from the original, create the account, then resolve both codes and assert each resolves to its own distinct account (ID and name).

**Why:** An assertion that the code cannot violate is decoration. If deleting the operation's behavior would still pass the test, the test measures nothing.

---

## EFF-AC-2.13 — test-gap
- **Location:** Tests/Tests.Integrated/CrossDomainOrchestration/AccountCreation.fs:151-169 (REQ-AC-2.13)
- **Summary:** The UUID-generation test asserts only `Assert.NotEqual(Guid.Empty, id)` (Specimen 2), so the 'unique' half of REQ-AC-2.13 is never checked directly.
- **Resolution:** fix-test

REQ-AC-2.13 says the creation function must generate a unique UUID. The test's lone assertion (line 167) passes for any constant non-empty GUID. Uniqueness is the property that waivers REQ-AC-1.22 and REQ-AC-2.8 lean on ('structurally guaranteed', 'duplication is structurally impossible'), so it is load-bearing. Today a constant-GUID regression would surface indirectly as a PK violation in tests that create several accounts, such as the AccountCreateActivityBalance tree. That surfacing is incidental, and the test citing 2.13 would stay green.

**Action:** Create two accounts in one transaction and assert that their IDs differ from each other and that neither collides with any fixture account ID.

**Why:** `<>` against a sentinel gives up on stating the property. The requirement says unique, so the test should compare against other generated IDs.

---

## EFF-AC-4.3 — test-gap
- **Location:** Tests/Tests.Integrated/CrossDomainOrchestration/AccountDeactivation.fs:80-93 (REQ-AC-4.3); Src/Business.CrossDomainOrchestration/AccountDeactivation.fs:53-66
- **Summary:** REQ-AC-4.3 rejects deactivation only when there are ACTIVE children (as of the current date), but the one test uses a parent with active children. Nothing shows a parent whose children are all inactive can be deactivated.
- **Resolution:** fix-test

The test deactivates assets1000Id, which has active children, and asserts AccountActiveChildrenBeforeDeactivation. An implementation that rejected any account with children (dropping the `ActivityPeriod.isActive referenceDate` filter at Src line 61) passes it. The REQ-AC-4.1 route theory row 'activeChildren' (AccountRoutes.fs:388, F-5000) is also negative-only. The qualifier that makes the rule specific, 'active children ... as-of the current date', has no test.

**Action:** Add a positive case: a parent whose only child has an active end before today is deactivated successfully (Form 3, rolled back).

**Why:** Testing only the rejection side of a qualified rule proves the gate exists, not where it sits. An over-broad gate goes unnoticed.

---

## EFF-AC-4.6 — test-gap
- **Location:** Tests/Tests.Integrated/InterfaceBridge/AccountRoutes.fs:418-456 (REQ-AC-4.6); Src/Business.CrossDomainOrchestration/AccountDeactivation.fs:88-111; Tests/Tests.Integrated/CrossDomainOrchestration/AccountDeactivation.fs:125
- **Summary:** REQ-AC-4.6's one test covers only the rejection. Two things are untested: the inclusive boundary the requirement spells out (an entry dated on the active-end date is permitted), and the code's unspecced exclusion of voided entries.
- **Resolution:** fix-test

The test posts a JE dated today and deactivates as of yesterday, asserting AccountDeactivationWithJournalEntriesDatedAfterDeactivationDate. No test deactivates with active end equal to an entry's date. Changing the SQL comparison `je.entry_date > @deactivation_date` (Src line 100) to `>=` would keep the suite green while breaking the spec's explicit 'inclusive boundary' clause. Separately, the query also filters `and je.voided_at is null` (Src line 101). REQ-AC-4.6 says 'referenced by a journal entry line' and says nothing about voided entries. The behavior may be intended, given that 'void' means excluded from calculations, but no REQ states it and no test pins it either way. There is also a stale comment at test AccountDeactivation.fs:125 ('todo: we need a test for AccountDeactivationWithJournalEntriesDatedAfterDeactivationDate') even though the test exists in AccountRoutes.fs.

**Action:** Add a test that deactivates with active end equal to the entry date of the account's latest line and asserts success. Have Dan state whether voided entries are exempt from 4.6, then pin that with a test. Remove the stale todo.

**Why:** When a requirement spells out its boundary, the boundary is the behavior. Off-by-one at an inclusive edge is the classic bug these clauses exist to prevent.

---

## EFF-AC-4.4 — test-gap
- **Location:** Tests/Tests.Integrated/CrossDomainOrchestration/AccountDeactivation.fs:95-108 (REQ-AC-4.4); Src/Business.CrossDomainOrchestration/AccountDeactivation.fs:68-86
- **Summary:** REQ-AC-4.4 is tested only by rejecting an account with a plainly non-zero balance. No test shows that voided lines are left out of the deactivation balance check, which is the GAAP 'balance' meaning ruled on in AMB-AC-2.
- **Resolution:** fix-test

The test deactivates mortgage2210Id and asserts AccountNonZeroBalanceBeforeDeactivation. The route theory row 'nonZeroBalance' (AccountRoutes.fs:389, F-2210) is the same vector. The check computes the balance from `JournalEntryLine.fetchByAccountId context true` (Src line 71). No test covers an account whose unvoided lines net to zero but which also carries a voided non-zero entry. An implementation that summed voided lines would still pass every REQ-AC-4.4 test. The positive path with lines netting to zero is exercised only by fixture setup (closedBank1290 in TestDataStage.fs:565-617), not by any test citing 4.4.

**Action:** Add a test: an account with offsetting unvoided lines plus a voided one-sided entry deactivates successfully, with active end on or after its latest entry date.

**Why:** Precedent AMB-AC-2 defines 'balance' as the net of non-voided entries. That definition is part of the requirement and needs a test that would go red if voided lines were counted.

---

## EFF-AC-2.21 — test-gap
- **Location:** Tests/Tests.Integrated/InterfaceBridge/AccountRoutes.fs:41-59 (REQ-AC-2.21); Tests/Tests.Helpers/EntityFunctions.fs:77-85
- **Summary:** The interface-level create happy path asserts nothing about the account it created. It finds the account by the returned code (Specimen 8) and checks only that the lookup succeeded, so the route's mapping of name, type, subtype, reference and active begin is never verified.
- **Resolution:** fix-test

The test sends createAccountInput "AC-2.21", deserializes the AccountReturn and calls `accountReturn.code |> LookupCache.accountCodeToId.fetch`, purely to capture a cleanup ID. It contains no Assert. The only other route-level creates are the REQ-AC-2.22 and REQ-AC-2.23 tests, which assert the parent ID and the active end only. Every route create input in the suite uses subType = None and reference = None (createAccountInput; AccountCreateActivityBalance.fs:215-223), so the contract-to-domain conversion of subtype and external reference is never exercised on a success path. Accounts with subtypes and references elsewhere in the suite are built with createTestAccountFromPrimitives, which bypasses the route. A converter that dropped subtype or reference, or swapped name and reference, would pass.

**Action:** Make the create happy path send a payload with every field populated (non-null subtype, reference and parent code), read the stored account back independently, and assert each field equals the input.

**Why:** Specimen 7 plus Specimen 8: a test with no assertion, whose locator is the value it would assert, proves only that the route returned Ok.

---

## EFF-AC-3.6 — test-gap
- **Location:** Tests/Tests.Integrated/Model/Ledger/Account.fs:162-175; Tests/Tests.Integrated/InterfaceBridge/AccountRoutes.fs:149-164 (REQ-AC-3.6)
- **Summary:** Neither REQ-AC-3.6 test shows that fetch-by-type returns exactly the fixture's accounts of that type. One checks a hard-wired subset, and the other checks a count plus an all-match-the-type predicate.
- **Resolution:** fix-test

The model test hard-wires `expectedIds = [ fixture.Data.equity3000Id; fixture.Data.retirement3030Id ]`, asserts both are present, and asserts every returned row is Equity. It never compares the count or the set, so a fetch that dropped a third Equity account added later would pass (Specimen 1, snapshot instead of a fixture-derived set). The route test derives the count from the fixture and asserts `List.forall(fun x -> x.accountTypeSt = explicitType)` plus a count. The right number of rows of the right type passes even if they are duplicates of one account (Specimen 3). Neither test establishes set equality.

**Action:** In at least one layer, derive the expected ID or code set from fixture.Data.accounts filtered by type, and assert set equality with the result.

**Why:** 'Returns all records of type X' is a statement about membership. Only a set comparison checks both completeness and exclusivity.

---

