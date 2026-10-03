# test-efficacy-CashFlow

## EFF-CF-1 — enforcement-gap
- **Location:** REQ-CF-2.6, REQ-CF-3.9; Tests/Tests.Integrated/CrossDomainOrchestration/MasterAgreementDataStates.fs:298,313; PaymentAgreementDataStates.fs:130 (refusedAndStored) used at :411,:425,:437
- **Summary:** The duplicate-name tests for Master and Payment Agreements say the request is 'rejected with a typed error', but no typed error exists for a duplicate name: only the database UNIQUE constraint enforces it, and the tests pass on whatever raw data-access error comes back.
- **Resolution:** dan-decides

Five tests cover name uniqueness. Two are for REQ-CF-2.6: 'creating an agreement with the name of an existing agreement is rejected with a typed error...' (MasterAgreementDataStates.fs:288) and the rename case (:303). Three are for REQ-CF-3.9: duplicate across agreements (PaymentAgreementDataStates.fs:401), duplicate after trimming (:417), and two legs with the same name in one payload (:430). All five assert only `Assert.True(attempt |> Result.isError)` (MasterAgreementDataStates.fs:298, :313) or `attempt |> Result.isError` inside refusedAndStored (PaymentAgreementDataStates.fs:130). In Src, CashFlowError.fs has no case for a duplicate agreement name or a duplicate payment agreement name. AgreementOrchestration.constructNewAndPersist (AgreementOrchestration.fs:197-242) runs confirmMasterAgreement, confirmPaymentAgreements and confirmComposite, and none of them checks names. Uniqueness is enforced only by `CONSTRAINT master_agreement_agreement_name_key UNIQUE (agreement_name)` and `payment_agreement_payment_agreement_name_key UNIQUE (payment_agreement_name)` in DbMigration/Scripts/202609071125-CreateCashFlowTables. The route therefore fails with a DAL exception wrapper, not a domain error. This is the exact failure in Specimen 4's August 2026 story: tests named 'typed error' that were passing on a raw DAL error.

**Action:** Decide whether a duplicate name must give a typed CashFlowError. If yes, add an app-level name check that returns a new typed case and match that case in all five tests. If no, rename the tests so they stop claiming a typed error and match the specific DAL case they actually receive.

**Why:** A test that passes on any failure cannot tell 'the system refused the duplicate on purpose' from 'the database threw'. The operator gets an opaque DAL message for a routine mistake, and no test notices, because no test ever asked which error it caught.

---

## EFF-CF-2 — test-gap
- **Location:** REQ-CF-2.25, REQ-CF-2.26, REQ-CF-6.11; MasterAgreementDataStates.fs:515,585,617; PaymentDataStates.fs:581,607,682
- **Summary:** Three REQs explicitly require a typed error, but their update-path and pointer tests only check `Result.isError` (Specimen 4), even though the typed cases exist and sibling tests already match them.
- **Resolution:** fix-test

REQ-CF-2.25 says a date that does not fit 'is rejected with a typed error naming the date and the cadence rule'. The create path matches `BizGeneralError.CadenceDateNotOnWeekDay (d, "Monday")` (MasterAgreementDataStates.fs:494). The update test 'updating an agreement's next-instance date to one that does not fit...' only asserts `Assert.True(attempt |> Result.isError)` (:515). REQ-CF-2.26 says such a create or update 'is rejected with a typed error'. The create path matches `CashflowMasterAgreementUnavailable` (:563). Both update tests assert only isError: the end date set to yesterday (:585), and changing another field on an already-ended agreement (:617). REQ-CF-6.11 says a pointer that does not resolve 'is rejected with a typed error'. InstanceOrchestration.fs:69/:73/:94/:105 return `LedgerError.JournalEntryLineIdDoesntExist` / `DataIngestionError.IngestionStageEntryLineIdDoesntExist`, yet PaymentDataStates.fs:682 (CreatePayment), :581 (CreateInvoice) and :607 (CreateInstance) assert only `Assert.True(attempt |> Result.isError)`. The :581 and :607 theories also mix a wrong-account case with a missing-line case, so neither branch proves which rule fired.

**Action:** Replace each `Assert.True(attempt |> Result.isError)` with a typed match on the named case, with both escape arms: CadenceDateNotOnWeekDay for 2.25 update; CashflowMasterAgreementUnavailable for both 2.26 updates; JournalEntryLineIdDoesntExist / IngestionStageEntryLineIdDoesntExist for 6.11, and CashflowPaymentLineNotOnAgreementAccount for the wrong-account rows.

**Why:** Where the requirement itself says 'typed error', the typed case is the behaviour under test. isError passes for a JSON parse failure, a DB fault, or the wrong validation firing. The create paths of the same REQs show the typed match is easy to write.

---

## EFF-CF-3 — test-gap
- **Location:** REQ-CF-2.3, 2.8, 2.9, 2.10, 2.11, 2.17; Tests/Tests.Integrated/CrossDomainOrchestration/MasterAgreementDataStates.fs:200,213,226,333(brokenThenWhole→344,357,374,430,445),416
- **Summary:** Every rejection test for REQ-CF-2.3, 2.8, 2.9, 2.10, 2.11 and 2.17 asserts only `Result.isError` (Specimen 4), while each test name says 'rejected with a typed error'. These are the only tests for those rejections.
- **Resolution:** fix-test

The tests are: null agreement name (:200), null counterparty (:213), unknown cadence 'Fortnightly' (:226), Weekly/EveryOtherWeek with no week day (:344, :357), Monthly with no month day (:374), NthWeekDay with no week day (:416), and Annually with no month or no month day (:430, :445). All rely on `Assert.True(attempt |> Result.isError)`, or on brokenThenWhole returning `(attempt |> Result.isError)` at :333. Each one edits the JSON payload by hand (setting fields to null, or writing a malformed DU case), so the most likely failure is the route's JSON deserialization, not any cadence or field rule in CashFlow or Business.General. Only the isolated tests for 2.4/2.5/2.7/2.13-2.16/2.18/2.19 (Tests.Isolated/Model/CashFlow/MasterAgreementDataStates.fs) match typed cases. Nothing at any layer proves which error 2.3, 2.8-2.11 or 2.17 produce. Smell test: if the route returned a DalError or a generic parse error for every one of these payloads, all of them would still pass.

**Action:** For each test, find the actual error (Specimen 4's DISCOVERED technique) and match that case. If it is a JSON deserialization error, decide whether that is the intended rejection for the REQ. If it is, match it by type; if not, add the domain validation.

**Why:** A Theory or Fact that can't fail on the wrong error is coverage on paper only. Cadence-field requirements are exactly where a refactor could quietly move rejection from the domain to the deserializer, or remove it, without any test going red.

---

## EFF-CF-4 — test-gap
- **Location:** REQ-CF-3.3, 3.7, 3.8, 3.9, 3.10; Tests/Tests.Integrated/CrossDomainOrchestration/PaymentAgreementDataStates.fs:130,168,240,260,295,308,352,373,465
- **Summary:** Every Payment Agreement rejection test asserts only isError, through the refusedAndStored helper or directly (Specimen 4). Typed cases exist for all of them and none is matched.
- **Resolution:** fix-test

refusedAndStored (:126-131) returns `(attempt |> Result.isError)`, and the tests that use it assert `Assert.True(refused)`: 3.7 non-positive (:240), 3.7 fraction of a cent (:260), 3.8 2001 chars (:295), 3.8 blank (:308), 3.9 251 chars (:373), 3.10 -1/366 (:465). 3.9 null/blank name (:352) and the 3.3 model-level orphan (:168) use `Assert.True(attempt |> Result.isError)` directly. CashFlowError.fs defines CashflowPaymentAgreementNonPositiveExpectedAmount, CashflowPaymentAgreementMemoIsEmpty/TooLong, CashflowPaymentAgreementNameIsEmpty/TooLong and CashflowDaysDueAfterInvoiceDateBelowMin/ExceededMax, and no test matches any of them. The 3.3 model-level test writes a Payment Agreement for a MasterAgreementId that does not exist, so it most likely passes on the FK violation from the DAL rather than on any domain check. These are the only tests citing REQ-CF-3.7, 3.8, 3.10 and the 3.3 negative case, so no layer of the suite checks which error is produced.

**Action:** Change refusedAndStored to take the expected typed case and match it. Change :168 and :352 to typed matches. For 3.3, decide whether the FK error is the intended rejection; if not, add an orchestrator check that returns CashflowMasterAgreementIdDoesntExist.

**Why:** These are boundary rules (0.00 vs 0.01, 365 vs 366, 2000 vs 2001). A boundary test is worth something only if it shows the boundary rule fired, not that something failed.

---

## EFF-CF-5 — test-gap
- **Location:** REQ-CF-4.3, 4.9, 4.10; Tests/Tests.Integrated/CrossDomainOrchestration/InstanceDataStates.fs:211,403,423,441,457,473,486,503
- **Summary:** Every Instance rejection test for 4.3, 4.9 and 4.10 asserts `Assert.True(attempt |> Result.isError)` (Specimen 4), even though each test name says 'rejected with a typed error' and the typed cases exist.
- **Resolution:** fix-test

The tests are: 4.3 unknown agreement (:211); 4.9 fulfilled with no invoices, fulfilled with an unpaid invoice, and set to unfulfilled when all invoices are paid (:403, :423, :441); 4.10 two invoices for the same leg on create and on add (:457, :473), and a foreign leg on create and on add (:486, :503). Typed cases exist: CashflowMasterAgreementIdDoesntExist, CashflowInstanceFulfilledWithNoInvoices, CashflowInstanceFulfilledWithUnpaidInvoice, CashflowInstanceManyInvoicesForPaymentAgreement, and CashflowInvoiceDiamondMismatch / CashflowPaymentAgreementNotUnderMasterAgreement. The same file already matches typed cases for 4.6 (:303-306) and 4.7 (:338-341). The 4.9 test at :441 is notable: 'sets is-fulfilled to false on an Instance whose every Invoice is FullyPaid'. No error case for that direction is visible in CashFlowError, so the test may be passing on an unrelated failure, or on a no-op error such as CashflowInstanceUpdateNoOp, rather than on a 4.9 rule.

**Action:** Match the specific case in each test. For :441, first find out which error is actually returned, and decide whether that rejection is the 4.9/9.10 rule or an artifact.

**Why:** 4.9 and 4.10 guard the Saturday 'what's still open' view. If the wrong error is being caught, the guard could be gone and the tests would stay green.

---

## EFF-CF-6 — test-gap
- **Location:** REQ-CF-5.3, 5.4, 5.5, 5.6, 5.7, 5.8, 5.9, 5.13, 5.14, 5.15, 5.16; Tests/Tests.Integrated/CrossDomainOrchestration/InvoiceDataStates.fs:260,280,302,322,336,355,370,383,409,423,450,465,501,544,563,584,604,618,652,676,690,708,720,747
- **Summary:** All 24 Invoice rejection assertions in InvoiceDataStates.fs use `Assert.True(attempt |> Result.isError)` (Specimen 4). For 5.5 (the diamond relation) this is the only test.
- **Resolution:** fix-test

Every negative test in the file, at the 24 lines listed, follows the pattern 'attempt ... Assert.True(attempt |> Result.isError); Assert.Empty(...)'. Matching typed cases exist: CashflowInstanceIdDoesntExist, CashflowPaymentAgreementNameDoesntMatchId (already matched in MaintenanceOperations.fs:784), CashflowInvoiceDiamondMismatch / CashflowInvoiceNotUnderMasterAgreement (5.5), CashflowInvoiceNonPositiveAmount (5.6, matched in SweepBehaviour.fs:710), CashflowInvalidInvoiceState (5.9), CashflowInvalidBlocker, CashflowBlockerNoteIsEmpty/TooLong (5.13/5.14), CashflowInvoiceMemoIsEmpty/TooLong (5.15) and CashflowInstanceManyInvoicesForPaymentAgreement (5.16). The null-amount and null-date rows (:370, :409, :423, :450) inject JSON null, so they most likely fail at deserialization and never reach the 5.6/5.7/5.8 rules. REQ-CF-5.5 (the diamond-relation consistency the spec singles out in a design note) is cited only by :336 and :355, and both accept any error.

**Action:** Change each assertion to a typed match with both escape arms. Start with 5.5 (CashflowInvoiceDiamondMismatch), 5.14 and 5.16, where an error from a different rule is most plausible.

**Why:** A route test that seeds a bad value and accepts any failure would stay green if the Invoice validator were deleted and the request failed for some other reason.

---

## EFF-CF-7 — test-gap
- **Location:** REQ-CF-6.3, 6.4, 6.7, 6.9, 6.10 (and 14.4/14.5 atomicity); Tests/Tests.Integrated/CrossDomainOrchestration/PaymentDataStates.fs:343,395,479,492,524,542,651,664; MaintenanceOperations.fs:510,563,583,632
- **Summary:** Every Payment rejection test asserts only isError (Specimen 4), and so do the 14.4/14.5 'nothing is written' atomicity tests, so none of them shows the rejection came from the rule the test names.
- **Resolution:** fix-test

PaymentDataStates.fs: 6.3 unknown invoice (:343), 6.4 null pointer (:395), 6.7 blank and 2001-char memo (:479, :492), 6.9 wrong account for 4 direction/account rows (:524) and Posted/Staged (:542), 6.10 posted-to-ledger date mismatch (:651) and date given for a staged line (:664). All use `Assert.True(attempt |> Result.isError)`. Typed cases exist: CashflowInvoiceIdDoesntExist, CashflowPaymentMemoIsEmpty/TooLong, CashflowPaymentLineNotOnAgreementAccount (already matched in DerivedStateRules.fs:460), CashflowPaymentPostedToLedgerDateMismatch and CashflowPaymentPostedToLedgerDateWithoutJournalEntry. MaintenanceOperations.fs :510 (14.4+9.3), :563 (14.5+9.3), :583 (14.5+9.3) and :632 (14.5+9.4) also assert only isError before checking that nothing was written. For an atomicity test, the claim 'the second invoice's blocker rule rolled back the first' holds only if the failure was CashflowInvoiceFullyPaidWithBlocker. The typed check exists elsewhere (DerivedStateRules.fs:213-216), but these tests don't use it.

**Action:** Match CashflowPaymentLineNotOnAgreementAccount, CashflowPaymentPostedToLedgerDate*, CashflowPaymentMemo* and CashflowInvoiceIdDoesntExist in PaymentDataStates. In the four MaintenanceOperations tests, reuse DerivedStateRules' isFullyPaidWithBlocker predicate (or the equivalent for 9.4).

**Why:** 6.9 and 6.10 decide which cash movements may count against an obligation. A test that accepts any failure cannot separate 'wrong account refused' from 'route crashed'.

---

## EFF-CF-8 — test-gap
- **Location:** REQ-CF-2.22; Tests/Tests.Integrated/CrossDomainOrchestration/AgreementCreation.fs:66,81
- **Summary:** REQ-CF-2.22's rejection rules (Master Agreement memo over 2000 characters, or whitespace-only) have no test at any layer. Its only citing tests are two happy-path read-backs.
- **Resolution:** fix-test

REQ-CF-2.22: 'When non-null, memo length cannot exceed 2000 characters and cannot be whitespace only (post-trim, per REQ-SYS-1.1).' Only two tests cite it, AgreementCreation.fs:66 (no memo) and :81 (memo 'Fixture agreement memo'), and both assert read-back equality. A grep of Tests/ for AgreementMemo (excluding PaymentAgreementMemo) finds no rejection test, isolated or integrated. CashFlowError defines CashflowAgreementMemoIsEmpty and CashflowAgreementMemoTooLong, and nothing exercises them. Compare REQ-CF-3.8, 5.15 and 6.7: identical memo rules for the other entities each have 2000/2001 and blank-string cases.

**Action:** Add isolated tests for AgreementMemo.create: 2000 accepted, 2001 rejected with CashflowAgreementMemoTooLong, and '', ' ', ' \t ' rejected with CashflowAgreementMemoIsEmpty. Optionally add one trimmed-memo route test for REQ-SYS-1.1.

**Why:** The REQ defines explicit rejection criteria and nothing proves any of them. AgreementMemo.create could accept a 10,000-character or whitespace-only memo and the suite would stay green.

---

## EFF-CF-9 — test-gap
- **Location:** REQ-CF-2.21; Tests/Tests.Integrated/CrossDomainOrchestration/AgreementCreation.fs:66,81
- **Summary:** REQ-CF-2.21's rejection of an end date earlier than the start date, and its allowance of an equal end date, are not tested for Master Agreements. The two citing tests only read back an absent end date or a valid one.
- **Resolution:** dan-decides

REQ-CF-2.21: 'When non-null, end date ... must not be earlier than start date. Equality is permitted.' The citing tests (AgreementCreation.fs:66, :81) use startDate = today-2 months and endDate = today+1 year, or None. The underlying ActivityPeriod.create check (Business.General/ActivityPeriod.fs:28, ActiveEndBeforeBegin) is tested only under REQ-AC-1.46 (Tests.Isolated/Model/Ledger/AccountComponent.fs:554,562). Tests/README.md counts 'a caller creating an agreement with end before start' as a separate failure vector from the component rule. No CreateAgreement or UpdateAgreement test sends activeEnd < activeBegin or activeEnd = activeBegin.

**Action:** Add a CreateAgreement route test (and an UpdateAgreement one for activeBeginUpdate/activeEndUpdate) with end = start - 1, matching ActiveEndBeforeBegin, plus one with end = start that is stored. Otherwise, rule that the REQ-AC-1.46 component tests are sufficient coverage.

**Why:** The update path assembles a new ActivityPeriod from separate begin/end FieldUpdates. That is a different code path from account creation, and a reversed period it lets through would make the agreement invisible to the sweep's active check (REQ-CF-7.2).

---

## EFF-CF-10 — test-gap
- **Location:** REQ-CF-14.2; Tests/Tests.Integrated/CrossDomainOrchestration/RevisedRequirementsCashFlow.fs:248,295,313
- **Summary:** The clause added to REQ-CF-14.2 on 2026-10-03, that a field set to the value it already holds still counts as named (and is not rejected as a no-op), has no test. The citing tests always change the value or name no field.
- **Resolution:** fix-test

REQ-CF-14.2: 'An update that names no field to change is rejected (REQ-SYS-6.1); a field set to the value it already holds counts as named. (Amended 2026-10-03)'. The per-field Theory at :248 asserts `Assert.NotEqual(fieldsOf before, expected)` (:295), so every row changes a value by construction. The no-op test at :313 names no field and expects CashflowAgreementUpdateNoOp. No test sends SetTo with the stored value and asserts success. An implementation that compares values and returns CashflowAgreementUpdateNoOp for an unchanged SetTo would pass every 14.2 test.

**Action:** Add a test that sends UpdateAgreement (route or orchestration) with, for example, counterpartyUpdate = SetTo <current counterparty> and asserts Ok, with the agreement unchanged except modifiedAt (if that is the intended behaviour).

**Why:** The clause was added specifically to reverse a natural implementation choice. Without a test, a later refactor toward compare-to-stored behaviour would be invisible.

---

## EFF-CF-11 — test-gap
- **Location:** REQ-CF-8.6; Tests/Tests.Integrated/CrossDomainOrchestration/ProjectionRules.fs:385-396; Src/Business.CrossDomainOrchestration/CashFlowOps.fs:703-707; Src/Business.FinancialServices.Ledger/Account.fs:211-222
- **Summary:** The REQ-CF-8.6 test derives its expected list of managed cash accounts with the same function and the same active filter that the projection uses (Specimen 6, fox guarding the hen house).
- **Resolution:** fix-test

The test builds its expected list as `Account.fetchAll s.Context false |> List.filter (subtype = Some Cash && ActivityPeriod.isActive s.today)` (:385-392). projectCashFlowNDaysForward computes the same list as `Account.fetchAll context true |> List.filter (subtype = Some AccountComponent.Cash)` (CashFlowOps.fs:703-707), and fetchAll with activeOnly=true applies `ActivityPeriod.isActive activeReference` (Account.fs:219-221). Both sides run the same query and the same isActive predicate. If isActive, or the activity-period read, were wrong, the expected and actual lists would be wrong in the same way and `Assert.Equal<string list>(expected, codes)` (:396) would still pass. The independent assertions are `Assert.Contains(code, codes)` (:395) and the separate inactive-account test (:400-406).

**Action:** Derive the expected codes from fixture.Data.accounts (subtype and activity period are known fixture values) plus the account the test creates, using only list operations, not Account.fetchAll or ActivityPeriod.isActive.

**Why:** Tests/README.md: 'No function in the call chain of the function under test may appear in the derivation of the expected value.' The 'every managed cash account appears' half of 8.6 currently checks the projection against itself.

---

## EFF-CF-12 — test-gap
- **Location:** REQ-CF-13.9; Tests/Tests.Integrated/CrossDomainOrchestration/LinkageAndMatching.fs:1304-1306; Src/Business.CrossDomainOrchestration/CashFlowOps.fs:612
- **Summary:** The REQ-CF-13.9 'open Instances' test derives its expected set by calling `InstanceOrchestration.fetchCompositesByIsFulfilled context false`, the same call classifyPaymentAgreements makes to build that result field (Specimen 6).
- **Resolution:** fix-test

The test is 'the result's open Instances are exactly the unfulfilled Instances after matching...' (:1292). Its exact-equality assertion takes `expected` from `false |> InstanceOrchestration.fetchCompositesByIsFulfilled context` (:1304-1305). classifyPaymentAgreements sets `openInstances = false |> InstanceOrchestration.fetchCompositesByIsFulfilled context` (CashFlowOps.fs:612). The `Assert.Equal<Set<InstanceId>>(expected, listed)` at :1306 therefore compares the function with itself. Only the DoesNotContain/Contains checks at :1302-1303 are independent. If is_fulfilled were not re-derived (REQ-CF-9.10), or the fetch filtered wrongly, both sides would agree.

**Action:** Derive 'exactly' from what the test knows: the fixture's unfulfilled Instance IDs (from fixture.Data) plus the test's own leftOpenId, minus fulfilledByRunId. Alternatively, drop the tautological equality and keep the membership assertions.

**Why:** An expected value that runs the production code path can't detect a defect on that path. The 'exactly' in the test name is not actually checked.

---

## EFF-CF-13 — test-gap
- **Location:** REQ-CF-8.1, 8.3, 8.9, 8.10; Tests/Tests.Integrated/CrossDomainOrchestration/ProjectionRules.fs:218-223; CashFlowProjection.fs:36-47,55-58
- **Summary:** Several projection tests take their expected values from the projection's own output, or copy amounts from fixture literals (Specimens 6 and 1). The 8.1 formula test cannot detect wrong inputs to the formula.
- **Resolution:** fix-test

(a) ProjectionRules.fs:207 'projected low equals current balance plus known inflows minus known outflows' asserts `Assert.Equal(account.currentBalance + account.knownInflows - account.knownOutflows, account.projectedLow)` (:221-223), using three fields of the same output. The only other assertions are `Assert.NotEqual(0M, ...)` (:218-220, cowardly inequality, Specimen 2). The scenario has fully known values (cash balance 30.00, inflows 70.00, outflows 100.00, projected low 0.00), but none is asserted. (b) CashFlowProjection.fs:36 (8.3/8.9) computes `fullAmounts` from `account.invoices`, the projection's own invoice list (:43), and subtracts a hard-wired `40.00M` (:40) justified only by the comment 'C's 40.00 Payment is the only Payment on any Invoice still open against F-1280'. That value mirrors the literal in TestDataStage.fs:1097/1101 and is not read from fixture.Data. If an Invoice were dropped from both the list and the outflows, the test would pass. (c) CashFlowProjection.fs:57-58 (8.10) hard-wires 100.00 and 40.00 instead of reading them from the fixture. Independent coverage of 8.3/8.9 exists in RevisedRequirementsCashFlow.fs:183/:224, which softens (b). Nothing independently pins currentBalance + inflows - outflows to known numbers in (a).

**Action:** In (a), assert each component and the projected low against values derived from the scenario's own inputs (30.00 / 70.00 / 100.00 / 0.00). In (b) and (c), expose C's amount and part payment on fixture.Data.cashFlow, derive the expected outflow from fixture invoices on F-1280, and stop deriving it from account.invoices.

**Why:** 'Garbage of the right shape' passes these tests. A projection whose inflows ignored partial payments would still satisfy projectedLow = balance + inflows - outflows.

---

## EFF-CF-14 — test-gap
- **Location:** REQ-CF-8.4; Tests/Tests.Integrated/CrossDomainOrchestration/ProjectionRules.fs:327,339
- **Summary:** Two REQ-CF-8.4 tests check only how many bills to chase come back (Specimen 3), so a projection that reported the same Payment Agreement twice and missed the other would pass.
- **Resolution:** fix-test

'an unfulfilled Instance with no Invoices dated on the horizon end yields a bill to chase for each of its Payment Agreements' asserts `Assert.Equal(2, billsFor projection onEnd |> List.length)` (:327). 'an unfulfilled Instance with no Invoices dated 60 days ago yields a bill to chase for each of its Payment Agreements' asserts `Assert.Equal(2, billsFor projection instanceId |> List.length)` (:339). REQ-CF-8.4 says 'each missing agreement is a separate bill to chase, reported with its agreement name, payment agreement name, Instance date and cadence'. Neither test checks which payment agreements the bills name, or the reported fields. The scenario's agreement returns legNames (ProjectionRules.fs:90), so the expected set is available.

**Action:** Assert that the set of bill.paymentAgreementName values equals the set of the agreement's two legNames, and that instanceDate equals the Instance's date.

**Why:** The test names claim 'for each of its Payment Agreements', a membership property, but the bodies only check cardinality.

---

## EFF-CF-15 — test-gap
- **Location:** REQ-CF-6.7 (with REQ-SYS-1.1); Tests/Tests.Integrated/CrossDomainOrchestration/PaymentDataStates.fs:499-506
- **Summary:** The Payment memo trimming test uses a string `Assert.Contains`, which also passes when the memo is stored with its surrounding spaces, so the 'post-trim' part of REQ-CF-6.7 is not checked.
- **Resolution:** fix-test

The test 'a CreatePayment payload whose memo has text surrounded by spaces is stored rather than rejected as whitespace-only' sends "  a memo  " and asserts `Assert.Contains("a memo", stored |> Payment.memo |> Option.map PaymentMemo.value |> Option.defaultValue "")` (:506). A stored value of "  a memo  " satisfies this assertion. The sibling tests for REQ-CF-3.8 (PaymentAgreementDataStates.fs:313-321) and REQ-CF-2.4/2.18 assert exact equality with the trimmed value. This is the only 6.7 test with surrounding whitespace. (NGUI-AQ-1 in resolved-findings covers CLI stderr Contains only; it does not cover this.)

**Action:** Change to `Assert.Equal(Some "a memo", stored |> Payment.memo |> Option.map PaymentMemo.value)`.

**Why:** Specimen 4 calls out string Contains as an untyped check. Here it also hides whether the SYS-1.1 trim applies to Payment memos.

---

## EFF-CF-16 — missing-requirement
- **Location:** REQ-CF-3.6 / REQ-CF-14.2; Tests/Tests.Integrated/CrossDomainOrchestration/AgreementUpdate.fs:45-65; Src/Business.CrossDomainOrchestration/AgreementOrchestration.fs:373-377; Src/Ui.InterfaceBridge/Routes/CashFlowRoutes.fs:78
- **Summary:** The code supports updating a Payment Agreement's name, accounts, expected amount, days due and memo through AgreementOrchestration.updateAgreement, but no REQ describes that operation, no route reaches it, and a REQ-CF-3.6 test exercises it directly.
- **Resolution:** dan-decides

AgreementOrchestration.updateAgreement takes a list of PaymentAgreement.PaymentAgreementFieldUpdates (doc comment at :373: 'changes the master agreement and its legs only'). The only interface caller, CashFlowRoutes.fs:78, always passes `[]` for the legs, and UpdateAgreementInput (CashFlowContracts.fs:248-257) has no leg fields. REQ-CF-14.2 lists only Master Agreement fields, and §14 has no Payment Agreement update operation. AgreementUpdate.fs:45 'REQ-CF-3.6 updating a leg's credit account to its debit account is rejected naming that account' therefore tests a capability that exists only at the orchestration layer and is not specified (debit/credit account change, expected amount, days-due, PA name). Changes to those fields would also affect the sweep (REQ-CF-7.8) and the 6.9 account matching of existing Payments, none of which is specified for an update.

**Action:** Decide whether Payment Agreement update is a supported operation. If yes, add a §14 REQ (fields, and the consequences for existing Invoices and Payments) and a route. If no, remove the leg-update parameter from updateAgreement and re-target the 3.6 update test or delete it.

**Why:** Per Tests/README.md, a test exercising behaviour no REQ describes should point at a missing REQ. Here an unspecified mutation path, which could change which accounts satisfy a live obligation, is maintained and tested but cannot be reached by the operator.

---


