# test-efficacy-CashFlow

## TE-CF-1 — test-gap
- **Location:** REQ-CF-8.11; Tests/Tests.Integrated/CrossDomainOrchestration/ProjectionRules.fs:380-389; Src/Ui.InterfaceBridge/BoundaryConverters/CashFlowFieldConverters.fs:310-342; Src/Business.CrossDomainOrchestration/CashFlowOps.fs:716-856
- **Summary:** The ordering in REQ-CF-8.11 (accounts, Invoices and bills to chase) is set only in the route converter. The only 8.11 test calls the orchestrator twice in one transaction and never runs that converter.
- **Resolution:** fix-test

REQ-CF-8.11 says a repeat projection over unchanged data returns the same result, 'including the order of accounts, Invoices and bills to chase'.

The orchestrator (CashFlowOps.projectCashFlowNDaysForward, CashFlowOps.fs:716-856) does not order anything. Accounts come from Account.fetchAll. Invoices come from Invoice.fetchByInstanceIdList via fetchOpenComposites. Bills come from PaymentAgreement.fetchByMasterAgreementIdList, which supplies the order of bills within an Instance. None of these queries has an ORDER BY: grep finds 'order by' in none of Account.fs, Invoice.fs, PaymentAgreement.fs or Instance.fs.

The only deterministic ordering is in the route converter, CashFlowFieldConverters.fs:
- `convert [CashFlowProjection] to [CashFlowProjectionReturn]` sorts accounts by code and bills by (instanceDate, paymentAgreementName).
- `convert [ProjectedAccount] to [ProjectedAccountReturn]` sorts invoices by (dueDate, invoiceId).

The single 8.11 test (ProjectionRules.fs:380) calls `s.project 30` twice through the orchestrator inside one rolled-back transaction and asserts `Assert.Equal<CashFlowProjection>(first, second)`. Two identical unordered reads within one transaction come back in the same heap order, so this assertion cannot fail. It also never reaches the converter. If every List.sortBy in the converter were deleted, the order a caller sees would depend on database read order, and every test would still pass.

The only route-level ProjectCashFlow tests are ProjectionRules.fs:185 (NotEmpty), :448 (table digests) and AccountNamesInPayloads.fs:207-209 (account names). None of them asserts order.

**Action:** Add a route-level ProjectCashFlow test whose fixture has at least two managed accounts, two Invoices on one account with different due dates, and two bills to chase that share an Instance date. Build the agreements and Invoices in reverse of the required order. Assert that the returned accounts list in code order, each account's invoices list in (dueDate, invoiceId) order, and billsToChase list in (instanceDate, paymentAgreementName) order. Derive each expected order from the inputs the test created, by list sort.

**Why:** A determinism test that repeats the same unordered read cannot fail, so it proves nothing about order. A property tested at a layer that does not implement it is untested. The Saturday 'money you need to move' output is read in order, and a reshuffle between runs is the failure 8.11 exists to prevent.

---

## TE-CF-2 — test-gap
- **Location:** REQ-CF-8.1, REQ-CF-8.4, REQ-CF-8.7, REQ-CF-8.10; Tests/Tests.Integrated/CrossDomainOrchestration/ProjectionRules.fs:182-191; Src/Ui.InterfaceBridge/BoundaryConverters/CashFlowFieldConverters.fs:302-342
- **Summary:** The only ProjectCashFlow route happy-path test asserts just `Assert.NotEmpty(projection.accounts)` (Specimen 3). The route's conversion of balances, inflows, outflows, projected low, amount, outstanding amount and bills to chase is never asserted.
- **Resolution:** fix-test

ProjectionRules.fs:185, `REQ-CF-8.1 for each of 1 and 365, a ProjectCashFlow payload with that horizon returns a projection rather than an error`, deserialises CashFlowProjectionReturn and asserts only `Assert.NotEmpty(projection.accounts)`. Every other projection test (ProjectionRules, CashFlowProjection, RevisedRequirementsCashFlow, Cancellation) calls CashFlowOps.projectCashFlowNDaysForward directly.

The route converter maps every money field and each bill field by hand (CashFlowFieldConverters.fs:302-328): currentBalance, knownInflows, knownOutflows, projectedLow, invoice amount, outstandingAmount, and the bill's agreementName, paymentAgreementName, instanceDate and cadenceType. For example, swapping knownInflows and knownOutflows, mapping outstandingAmount from invoice.amount, or returning an empty billsToChase would pass every test.

The smell test fails: a route that returned well-shaped garbage would satisfy NotEmpty. AccountNamesInPayloads.fs:207 covers only accountName on ProjectedAccountReturn. The ROUTE-ORACLE precedent expects route tests to assert converted values against an independently tested oracle. Here no route test asserts any converted value.

**Action:** Extend the 8.1 boundary route test, or add one REQ-CF-8.7/8.10/8.4 route test, that builds a Cash account with one part-paid Income Invoice, one unpaid Outgo Invoice and one uninvoiced leg. It should call ProjectCashFlow through routeUiCommandForTesting and assert, for that account, the exact currentBalance, knownInflows, knownOutflows, projectedLow, each invoice's amount and outstandingAmount, and the bill's agreementName, paymentAgreementName, instanceDate and cadenceType. Derive all of these from the scenario inputs, as ProjectionRules.fs:207-227 does at the orchestrator layer.

**Why:** Tests/README requires every happy path at each layer. The route is where Hobson reads the projection. A converter that silently mislabels inflows as outflows is a different failure from a wrong orchestrator sum, and it is the one no current test can catch.

---

## TE-CF-3 — test-gap
- **Location:** REQ-CF-4.7; Tests/Tests.Integrated/CrossDomainOrchestration/InstanceDataStates.fs:411-426
- **Summary:** The REQ-CF-4.7 sweep test throws away the sweep's result (`let _ = s.sweep 5`) and asserts only that no Instance on or before the latest date exists. It passes unchanged if the sweep call is deleted.
- **Resolution:** fix-test

`REQ-CF-4.7 the sweep creates no Instance on a date earlier than or equal to the latest existing Instance of the agreement` sets up one Instance at today-3, rewinds the next-instance date to today-10, then runs `let _ = s.sweep 5`. Its only assertion is `Assert.Equal<LocalDate list>([ latest ], dates |> List.filter (fun d -> d <= latest))`.

The Result of the sweep is discarded, so the test accepts any outcome: a typed CashflowInstanceDateNotAfterLatestInstance, a DAL error, any other failure, or success.

It fails smell test 2 (Specimen 9 family; 'if I deleted the operation and asserted against the untouched state, would this test still pass?'). Without the sweep call, dates is [latest] and the assertion holds. The comment says 'whether the sweep then skips them or fails as a whole, none may be created'. But REQ-CF-4.7 requires an earlier or equal date to be 'rejected with a typed error naming the latest existing date'. Silently skipping is not the specified behaviour.

The sweep path of this rejection is covered elsewhere: SweepBehaviour.fs:699 (REQ-CF-7.15) builds the same setup and matches CashflowInstanceDateNotAfterLatestInstance. So this test adds a citation without adding evidence.

**Action:** Match the sweep result. Replace `let _ = s.sweep 5` with a typed match: `Error (AsError (CashFlowError.CashflowInstanceDateNotAfterLatestInstance(_, _, l))) when l = latest -> ()`, with Wrong-error and Ok escape arms. Then keep the dates assertion. Or delete the test as a duplicate of the SweepBehaviour REQ-CF-7.15 test.

**Why:** A test whose operation can be removed without turning it red measures nothing the operation caused. Discarding a Result is Specimen 4's `| Error _ -> Ok ()` with the match removed: it cannot tell the specified rejection from any other failure.

---

## TE-CF-4 — test-gap
- **Location:** REQ-CF-10.7; Tests/Tests.Integrated/CrossDomainOrchestration/PaymentDataStates.fs:415; Tests/Tests.Integrated/CrossDomainOrchestration/PaymentsToPosted.fs:401; Src/Ui.InterfaceBridge/BoundaryConverters/CashFlowFieldConverters.fs:286-300
- **Summary:** REQ-CF-10.7's return list (agreement name, invoice amount, journal entry line ID) is asserted only at the orchestrator. The one test that calls the TransitionPaymentsToPosted route ignores what it returns.
- **Resolution:** fix-test

PaymentsToPosted.fs:401 asserts the PaymentPostingTransition list that CashFlowOps.transitionPaymentsToPosted returns, and does so well. The route converts that list through `convert [PaymentPostingTransition] to [PaymentPostingTransitionReturn]` (CashFlowFieldConverters.fs:286-292): paymentId, agreementName, invoiceAmount and journalEntryLineId, each mapped by hand, then sorted.

The only test that calls the route is PaymentDataStates.fs:415: `let! _ = routeUiCommandForTesting "CashFlow" "TransitionPaymentsToPosted" [] "{}"`. It discards the payload and asserts only the stored Payment.

If the converter took invoiceAmount from the wrong field, or dropped or mis-mapped journalEntryLineId, no test would fail. The REQ's own rationale says the returned fields exist so the caller (Hobson) can build the Saturday summary without re-querying. That caller reads the route payload, not the orchestrator record.

**Action:** In PaymentDataStates.fs:415, or in a new route test, deserialise the TransitionPaymentsToPosted payload as PaymentPostingTransitionReturn list. Assert that it contains exactly one row for the moved Payment, with the test's agreement name, the Invoice's amount, and the jeLine's journal entry line ID, all taken from the test's own setup.

**Why:** Tests/README calls for every happy path at each layer. A field this REQ promises to the caller can be lost at the boundary with every orchestrator test still green.

---

