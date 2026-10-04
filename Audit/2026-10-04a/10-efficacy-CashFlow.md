# test-efficacy-CashFlow

## TE-CF-1 — test-gap
- **Location:** REQ-CF-5.10; Tests/Tests.Integrated/CrossDomainOrchestration/InvoiceStateByDirection.fs:179, :196; AgreementUpdate.fs:268; Src CashFlowComponent.fs:81-89 isValidFlowDirectionInvoiceStateCombination
- **Summary:** REQ-CF-5.10's direction/state rejection is tested only for an Outgo agreement given an Income state. No test creates or updates an Invoice on an Income agreement with InvoiceExpected or InvoiceReceived.
- **Resolution:** fix-test

The rule lives in isValidFlowDirectionInvoiceStateCombination: Income allows [InvoiceGenerated; InvoiceSent] and Outgo allows [InvoiceExpected; InvoiceReceived]. The only tests that expect CashflowInvoiceStateInvalidForFlowDirection are at InvoiceStateByDirection.fs:179 and :196, both Outgo-side, and at AgreementUpdate.fs:88/:268, which reach the Income side only through a flow-direction change on the Master Agreement. No Invoice create or update test puts InvoiceExpected on an Income agreement, and none puts InvoiceReceived on one. If the Income branch's list were widened, for example to include InvoiceReceived, the Invoice create and update paths would pass every current test.

**Action:** Add a truth-table test of isValidFlowDirectionInvoiceStateCombination covering every (FlowDirection x InvoiceState) pair. Or add Income-side Invoice create and update tests that expect CashflowInvoiceStateInvalidForFlowDirection for InvoiceExpected and InvoiceReceived.

**Why:** A rejection rule with two sides is only pinned down when both sides are tested to reject. One-sided negative coverage lets the untested branch drift without any test noticing.

---

## TE-CF-2 — test-gap
- **Location:** REQ-CF-2.20, REQ-CF-2.24; Tests/Tests.Integrated/CrossDomainOrchestration/AgreementCreation.fs:81, :99; compare MasterAgreementDataStates.fs:198, :212 (2.3, 2.17)
- **Summary:** The 'cannot be null' clauses of REQ-CF-2.20 (activeBegin) and REQ-CF-2.24 (nextInstance) have only round-trip tests. No null-payload refusal test exists, though the analogous REQ-CF-2.3 and 2.17 have one.
- **Resolution:** dan-decides

AgreementCreation.fs:81 and :99 assert only that a given date is stored and read back. Neither sends a payload with the field null or missing and asserts the refusal. Other non-null clauses in the same spec are tested at MasterAgreementDataStates.fs:198 and :212 by sending null and matching a typed JsonDeserializationFailed. Both fields are LocalDate, a value type, and REQ-CF-4.4 was waived on exactly that ground. 2.20 and 2.24 are not in the waiver table, though, so either a waiver is missing or a test is.

**Action:** Dan decides: add 2.20 and 2.24 to the waiver table on the same 'LocalDate is a value type' ground as REQ-CF-4.4. Otherwise add tests that send a CreateAgreement payload with activeBegin or nextInstance null, assert the typed refusal, and assert that no agreement is stored.

**Why:** A 'cannot be null' clause whose only test is a happy round trip has no negative evidence. The precedent (4.4) says to resolve this by an explicit waiver, not by leaving it silent.

---

## TE-CF-3 — test-gap
- **Location:** REQ-CF-14.8, REQ-CF-14.9; Tests/Tests.Integrated/CrossDomainOrchestration/PaymentAgreementUpdate.fs:214, :236 (14.8 account checks), :377 (14.9 + 3.6 only)
- **Summary:** 'Each subject to §3' in REQ-CF-14.8 (update) and 'subject to §3' in REQ-CF-14.9 (add) are tested only for the account rules. The name, amount, days-due and memo rules are never tested on these paths.
- **Resolution:** fix-test

For 14.8, the only negative tests on Payment Agreement update cover the account side (PaymentAgreementUpdate.fs:214, :236). Nothing tests an update that renames onto an existing Payment Agreement name or to blank or over 250 characters (3.9), sets a zero or negative expected amount (3.7), sets days-due outside 0..365 (3.10), or sets a whitespace-only or over-2000-character memo (3.8). For 14.9, the only negative test is :377, same debit and credit account (3.6). Adding a Payment Agreement whose name duplicates one on another agreement (3.9 uniqueness), or one with a non-positive amount, is untested. The §3 rules are tested on the create-agreement path (PaymentAgreementDataStates.fs:363-431), but update and add are different user interactions. Under the failure-vector precedent, coverage on create does not carry over to update or add.

**Action:** Add route-level sad-path tests on UpdatePaymentAgreement for: a duplicate name, a blank name, a non-positive expected amount, days-due of 366 and -1, and a whitespace memo. Add them on AddPaymentAgreement for a duplicate name and a non-positive amount. Each should match the typed error and read back from a fresh context that the stored record is unchanged.

**Why:** The spec says the §3 invariants hold on every path that writes a Payment Agreement. The update and add paths run their own converters and validators, so a regression there would not show in the create-path tests.

---

## TE-CF-4 — missing-requirement
- **Location:** Src AgreementOrchestration.fs:53-79 (confirmPaymentAgreementBelongsToAgreement / confirmAuthorityAndCohesion), :429; CashFlowError.CashflowPaymentAgreementNotUnderMasterAgreement
- **Summary:** UpdateAgreement rejects a paymentAgreementUpdates entry naming a Payment Agreement under a different Master Agreement, with CashflowPaymentAgreementNotUnderMasterAgreement. No REQ states this rule and no test exercises it.
- **Resolution:** dan-decides

Grep finds NotUnderMasterAgreement in no test file. The rejection is real behavior: an operator who names another agreement's leg in an update payload gets a typed error. But it is neither cited nor specified. Because Payment Agreement names are globally unique (REQ-CF-3.9), the route could also have resolved and updated that Payment Agreement regardless of its parent. Which behavior is intended is a product decision.

**Action:** Dan decides. Either specify it, e.g. 'a Payment Agreement named in a Master Agreement update must belong to that Master Agreement; otherwise the update is rejected with a typed error naming both', and add a route test matching CashflowPaymentAgreementNotUnderMasterAgreement with a fresh-context unchanged read-back. Or remove the check.

**Why:** Uncited, untested rejection logic can be removed or broken with no test failing, and the spec gives no basis for deciding whether it should exist.

---

## TE-CF-5 — test-gap
- **Location:** REQ-CF-14.7; Tests/Tests.Integrated/CrossDomainOrchestration/MaintenanceOperations.fs:793
- **Summary:** The REQ-CF-14.7 test claims to cover every route whose payload names a Payment Agreement, but it exercises only CreateInstance and CreateInvoice. An unknown name in UpdateAgreement.paymentAgreementUpdates is untested.
- **Resolution:** fix-test

The test at MaintenanceOperations.fs:793 asserts CashflowPaymentAgreementNameDoesntMatchId for the CreateInstance and CreateInvoice payloads only. UpdateAgreement also resolves Payment Agreement names, for each entry of paymentAgreementUpdates, through CashFlowLookupConverters / CashFlowFieldConverters (~:553). No test sends an UpdateAgreement whose paymentAgreementUpdates names a non-existent Payment Agreement and checks for the typed error naming it. The test name claims more than the test covers.

**Action:** Extend the test, or add one, that sends UpdateAgreement with a paymentAgreementUpdates entry naming an unknown Payment Agreement. It should match CashflowPaymentAgreementNameDoesntMatchId with the exact name and confirm from a fresh context that the agreement is unchanged. Alternatively, narrow the test name to the routes it actually covers.

**Why:** A test whose name claims all routes but covers two gives false assurance. A lookup regression on the update path would go unnoticed.

---

