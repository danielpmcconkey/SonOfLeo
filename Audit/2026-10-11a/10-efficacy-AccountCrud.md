# test-efficacy-AccountCrud

## EFF-AC-1 — test-gap
- **Location:** Tests/Tests.Integrated/Model/Ledger/Account.fs:82-106 (REQ-AC-1.5)
- **Summary:** The only REQ-AC-1.5 test claims each case-variant code 'resolves to its own account', but it does the resolving with an F# string filter over Account.fetchAll, not with the system's code lookup.
- **Resolution:** fix-test

REQ-AC-1.5: "Account code is case sensitive. 'ACCT-100' and 'acct-100' are distinct account codes." The test creates an account under the lower-cased form of F-1000's code. It then checks `holderOf code = all |> List.filter(fun a -> a |> Account.code |> AccountCode.value = code)` (lines 100-103). That filter is .NET ordinal string equality written in the test. Every operation that identifies an account by code goes through something else: the route lookup Account.codeToId (LookupCache.stringToIdCache, which runs `fetchOne table keyColumn ...` or reads a Map) and the FetchByCode / UpdateName / Deactivate / FetchActivity / FetchBalances routes. No test anywhere passes a case variant of an existing code to any of these. A grep of Tests/Tests.Integrated for ToLower or 'lowered' finds only this test, plus two unrelated tag strings in AccountCreateActivityBalance.fs. So the half of 1.5 that the storage layer enforces is proven: if the unique constraint ignored case, the create would fail. The half that users actually reach is not proven: looking up 'f-1000' must not return F-1000. Smell test: change codeToId's query to `lower(code) = lower(@code)` or ILIKE, and this test stays green.

**Action:** Extend the REQ-AC-1.5 test (or add a sibling) so that after creating the lower-cased account, it resolves both codes through the operational lookup: the FetchByCode route, or Account.codeToId. Assert that each code returns its own account ID, and that a third case variant that was never created fails with AccountCodeDoesntMatchAccountId.

**Why:** The test name promises resolution, but the body verifies storage. A lookup that folds case would silently send postings, deactivations and balance queries to the wrong account, and the only test of case sensitivity would stay green.

---

## EFF-AC-2 — test-gap
- **Location:** Tests/Tests.Integrated/Model/Ledger/Account.fs:267-286 (REQ-AC-2.7 negative); Tests/Tests.Helpers/TestDataStage.fs:220-430 and PositionsFixture.fs:228 (every fixture account begins lastYear)
- **Summary:** REQ-AC-2.7's rejection is tested only with a parent whose active end has passed. No test offers a parent whose active begin is still in the future, so the not-yet-begun half of 'active' is never checked at the create gate.
- **Resolution:** fix-test

REQ-AC-2.7: "the system must confirm that the parent account is active as of the current date." REQ-AC-1.50 defines active with two inclusive boundaries: begin <= ref AND (end null OR end >= ref). REQ-AC-1.48 states that an account whose begin is later than the reference date "is not active". The only negative test (`REQ-AC-2.7 parent account must be active at AuditEnvelope instant--negative`, line 268) uses fixture.Data.closedBank1290Id, which was deactivated two months ago, so only the end boundary can fire. The positive Theory (line 243, offsets 0 and 30) also varies only the parent's active end. Every fixture account is created with begin = lastYear (TestDataStage.fs lines 220-430; PositionsFixture.fs:228), so no fixture parent has a future begin. The isolated test `REQ-AC-1.50 isActive returns false when ref precedes begin` (AccountComponent.fs:615) covers the predicate. But a create caller offering a not-yet-begun parent is a separate failure vector at the orchestrator, and nothing proves AccountCreation.confirmParentAccountIsActive rejects it. Smell test: replace that check with 'parent has no past active end' (a deactivated check), and every 2.7 test still passes.

**Action:** Add a REQ-AC-2.7 negative case. Inside a rolled-back transaction, create a parent with active begin = today + 1 and a null end. Attempt to create a child under it, and assert Error (AsError (AccountParentIsInactive id)) with id equal to the parent's ID and nothing stored.

**Why:** REQ-AC-1.48 deliberately separates 'not active' from 'deactivated'. A gate written against the narrower 'deactivated' notion is the plausible regression here, and the current tests cannot tell the two apart.

---

## EFF-AC-3 — test-gap
- **Location:** Tests/Tests.Integrated/Model/Ledger/Account.fs:197-216 (REQ-AC-3.9); Tests/Tests.Integrated/CrossDomainOrchestration/AccountDeactivation.fs:213-286 (REQ-AC-4.3)
- **Summary:** The REQ-AC-3.9 active-only fetch test and the REQ-AC-4.3 active-children tests never meet an account whose active begin is after today, so the begin conjunct of 'active' has no effect on either assertion.
- **Resolution:** fix-test

REQ-AC-3.9 test: the expected set is `begins <= today && (ends.IsNone || ends.Value >= today)` (line 206) over fixture.Data.accounts. Every fixture account begins lastYear, so the `begins <= today` term removes nothing, and the only excluded account is closedBank1290 (asserted at line 208). An Account.fetchAll activeOnly implementation that filtered only on active end would return the identical set and pass both Assert.Equal calls (lines 212-213). REQ-AC-4.3 tests: the rejecting Theory (line 230) and the succeeding test (line 277) vary only the child's active end (offsets 0/30 and -1). The fixture-based rejection (line 213) uses F-1000, whose children all began last year. No test has a child whose begin is after today, which per REQ-AC-1.48/1.50 is not active and therefore must not block deactivation. Smell test: if confirmNoActiveChildrenBeforeDeactivation tested 'child not deactivated' (end null or end >= ref) instead of ActivityPeriod.isActive, a parent with only a future-dated child would be wrongly refused, and every 4.3 test would still pass. The same substitution in fetchAll's activeOnly filter would wrongly include future-dated accounts, and the 3.9 test would pass.

**Action:** For 3.9: in a rolled-back transaction, create an account with begin = today + 1. Assert that fetchAll activeOnly=true excludes it and fetchAll activeOnly=false includes it. Alternatively, add a future-begin archetype to the fixture so the expected-set derivation can actually remove something. For 4.3: add a case where the parent's only child has begin = today + 1 and a null end, and assert that deactivation succeeds with the requested end read back.

**Why:** An expected-value filter whose conjunct never removes anything is not asserting that conjunct. The begin boundary is half of the REQ-AC-1.50 definition that both requirements rely on.

---

## EFF-AC-4 — test-gap
- **Location:** Tests/Tests.Isolated/Model/Ledger/AccountComponent.fs:605-613, 635-643 (REQ-AC-1.48); consumer Src/Business.CrossDomainOrchestration/BalanceSheetIntegrity.fs:42-46
- **Summary:** Both tests citing REQ-AC-1.48 assert ActivityPeriod.isActive (the REQ-AC-1.50 predicate). Nothing exercises 1.48's own classification, 'deactivated', including its rule that an account not yet begun is not deactivated.
- **Resolution:** fix-test

REQ-AC-1.48 defines a separate term: deactivated means active end is non-null and earlier than the reference date. It adds that an account whose begin is later than the reference date is not active but is not deactivated. The two tests citing it are `isActive returns false when end < ref (deactivated)` and `isActive returns true when the reference point exactly equals end`. Both call ActivityPeriod.isActive, so they prove 'a deactivated Account is not active' and the inclusive end boundary. That is the REQ-AC-1.50 predicate again. No deactivated predicate exists in Src. The classification is computed inline where it is consumed (BalanceSheetIntegrity.fs:42-46: `activeEnd |> Option.filter (fun activeEnd -> activeEnd < today)`). No test under any citation builds an account with a future begin to show that it is not classified as deactivated. Every fixture account begins lastYear, and the BalanceSheetIntegrity and ReportRoutes tests create none. Smell test: if the inline classification became `not (isActive today)`, a not-yet-begun account would be reported as deactivated, and no REQ-AC-1.48 test would notice.

**Action:** Either add a test that asserts the deactivated classification directly at its consumer: an account with begin = today + 1 and a null end must not appear among deactivated accounts, while one with end = yesterday must. Or, if 1.48 is meant only as a definition with its behavior verified through REQ-AC-1.50 and the reporting requirements, have Dan say so and re-cite the two isolated tests to REQ-AC-1.50.

**Why:** A test cited for a requirement should prove that requirement's distinguishing claim. Here the distinguishing claim (not active is not the same as deactivated) is the one part never checked.

---

## EFF-AC-5 — test-gap
- **Location:** Tests/Tests.Integrated/CrossDomainOrchestration/AccountBalance.fs:429-460 (REQ-AC-3.13.1); helper Tests/Tests.Helpers/EntityFunctions.fs:162-180
- **Summary:** The REQ-AC-3.13.1 'excludes voided entry amounts' test works out which lines are voided by re-reading each header from the database with JournalEntryHeader.fetchById, rather than from fixture.Data. Its oracle for the behavior under test therefore comes from the same persisted state the balance query reads.
- **Resolution:** fix-test

The expected values come from `fixture.Data.journalEntryLines |> sumJournalEntryLinesByAccountIdAndType context true id Debit`. With unvoidedOnly = true, the helper decides voided status by calling `JournalEntryHeader.fetchById tran ... |> JournalEntryHeader.voidedAt` for every line (EntityFunctions.fs:171-177). Tests/README.md, Test data advisories: 'Use fixture.Data for setup values, not re-fetches from the DB... Re-querying ... introduces a dependency on the system under test for values that should be known constants from your fixture.' AccountBalance.fetchByAccountIdList decides voided status from `je.voided_at` in the same table (AccountBalance.fs:89). Both sides therefore agree whatever voided_at holds. The test's only claim is void exclusion, and it cannot detect a fixture void whose persisted state differs from what the fixture recorded. The same file already contains the compliant derivation: `unvoidedSum` (lines 391-396) filters fixture.Data.journalEntries by their in-memory voidedAt, and the REQ-AC-3.13 test at line 399 uses it. The test also has no non-vacuity guard showing that creditCard2220 / entertainment5650 actually carry a voided line. It relies silently on the fixture's 'Fixture voided JE' (TestDataStage.fs:537-646).

**Action:** Replace the four sumJournalEntryLinesByAccountIdAndType calls with the file's own fixture-only `unvoidedSum` helper. Add a guard asserting that fixture.Data.journalEntries contains a voided entry with a line on each of the two accounts, so the test cannot pass vacuously.

**Why:** Expected values must come from fixture data using list operations, not from a database read of the very state the code under test reads. Otherwise a wrong void state leaves the oracle and the result wrong in the same way.

---

## EFF-AC-6 — test-gap
- **Location:** Tests/Tests.Integrated/Model/Ledger/Account.fs:362-374 (REQ-AC-4.19)
- **Summary:** REQ-AC-4.19 permits updates to a deactivated Account, and there are two update operations (name, REQ-AC-4.8, and external reference, REQ-AC-4.9). Only the name update is tested against a deactivated account.
- **Resolution:** fix-test

REQ-AC-4.19: 'Updates to a deactivated Account record are permitted, provided that those updates meet all other requirements herein.' The single citing test confirms closedBank1290 is inactive and then calls only Account.updateAccountNameById. updateExternalReferenceById, the other user-facing update (route UpdateExternalReference), is never run against a deactivated account; grep shows no other REQ-AC-4.19 citation. Today both functions go through Account.update with no activity guard, so the behavior holds. Smell test: an activity guard added to updateExternalReferenceById, or to its route, would refuse a permitted update, and no test would fail.

**Action:** Turn the REQ-AC-4.19 test into a Theory over both update operations, or add a sibling. In a rolled-back transaction, update closedBank1290's external reference (to a value, and to None), read it back, and assert the stored value.

**Why:** A requirement covering several operations is only partly covered when one of them is tested, and the untested operation is the one a future guard is just as likely to touch.

---

