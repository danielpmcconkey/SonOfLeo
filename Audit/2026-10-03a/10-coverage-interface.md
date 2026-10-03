# code-outward-coverage-ui

## COV-CR-ROUTE-1 — test-gap
- **Location:** Src/Ui.InterfaceBridge/Routes/ClassificationRoutes.fs (createPaymentAgreementLink l.126, updatePaymentAgreementLink l.140, deletePaymentAgreementLink l.151, classifyPaymentAgreements l.162); Src/Ui.InterfaceBridge/BoundaryConverters/ClassificationFieldConverters.fs (convert [PaymentAgreementLink] to [PaymentAgreementLinkReturn], convert [UpdatePaymentAgreementLinkInput] to [PaymentAgreementLinkFieldUpdates], convert [PaymentAgreementClassificationResult] to [PaymentAgreementClassificationResultReturn], convert [InvoiceDecision] to [InvoiceDecisionReturn]); REQ-CF-12.7, REQ-CF-13.9, REQ-CF-14.7
- **Summary:** No test calls the four Classification verbs CreatePaymentAgreementLink, UpdatePaymentAgreementLink, DeletePaymentAgreementLink and ClassifyPaymentAgreements, so none of their route-level conversions or lookups is tested.
- **Resolution:** fix-test

I grepped Tests/ for each verb string: zero hits for "CreatePaymentAgreementLink", "UpdatePaymentAgreementLink", "DeletePaymentAgreementLink" and "ClassifyPaymentAgreements" as route verbs. The only route callers for this domain are RevisedRequirementsClassification.fs:44 (send) and a few direct calls, and they reach only NewClassificationRule, FetchClassificationRuleById/ByName/Filtered, UpdateClassificationRule, FetchClassificationRun and ClassifyAccounts. The REQ-CF-12.7 tests (LinkageAndMatching.fs:736-813) use these verbs' audit actions as runCommandRouteAndAutoRollback labels, but their bodies call the domain functions directly: CashFlowOps.constructNewPaymentAgreementLinkAndPersist, PaymentAgreementLink.update and PaymentAgreementLink.delete. The REQ-CF-12.8 and REQ-CF-13.9 tests call CashFlowOps.classifyPaymentAgreements directly. So the following route-only code has no test: (a) resolving a Payment Agreement *name* to its ID in CreatePaymentAgreementLink and UpdatePaymentAgreementLink. The REQ-CF-14.7 test (MaintenanceOperations.fs:773) lists only CreateInstance and CreateInvoice as routes that name a Payment Agreement, so the unknown-name typed error is untested for the link routes. (b) DeletePaymentAgreementLink's fetch-then-delete sequence and its return of the row as it stood before deletion. (c) The whole PaymentAgreementClassificationResultReturn conversion: the decision log, the invoice decision log with its PaymentCreated/ManyCandidateEntries/Overpayment outcome mapping, open instances and sorting. That conversion is the operator-facing shape of REQ-CF-13.9. A broken name lookup, a swapped outcome case or a serialization failure in any of these routes would pass the whole suite. Unlike every other route set (AccountRoutes, FiscalPeriodRoutes, JournalEntryRoutes, IngestionRoutes, ClassificationRuleRoutes, ReportRoutes), these routes have no Tests.Integrated/InterfaceBridge file.

**Action:** Add route-level tests through routeUiCommandForTesting "Classification" for each of the four verbs. Cover at least: a link created by Payment Agreement name, a re-point by name, an unknown Payment Agreement name rejected with the typed error naming it (extending the REQ-CF-14.7 coverage), delete returning the pre-delete row, and ClassifyPaymentAgreements returning a result whose decision logs and open instances match what the domain call produced.

**Why:** The operator reaches Saturday linkage and link maintenance only through these routes, and the route layer does work of its own (name lookups, fetch-before-delete, result conversion) that the domain-level tests skip. Under fully agentic development, a route that a test never runs can be miswired without any test failing.

---

## COV-CF-ROUTE-1 — test-gap
- **Location:** Src/Ui.InterfaceBridge/Routes/CashFlowRoutes.fs createUpcomingInstances (l.18-27); CashFlowFieldConverters.fs convert [InstanceComposite list] to [InstanceCompositeReturn list]; REQ-CF-7.1, REQ-CF-7.14
- **Summary:** No test calls the CashFlow verb CreateUpcomingInstances. The sweep tests call CashFlowOps.createUpcomingInstances directly, so the route's input parsing, horizon conversion and output conversion are untested.
- **Resolution:** fix-test

I grepped Tests/ for "CreateUpcomingInstances" and got zero hits. InstanceDataStates.fs:145/267/375 and SweepBehaviour.fs:202/279-448 wrap `horizon |> CashFlowOps.createUpcomingInstances context` in runCommandRouteAndAutoRollback/AutoCompleteTransaction with the CashFlowCreateUpcomingInstances audit action, so they never call the route handler. Untested as a result: deserializing CreateUpcomingInstancesInput, the route's ProjectionHorizonInDays.create call on that input, and `convert [InstanceComposite list] to [InstanceCompositeReturn list]`, which is the JSON the operator receives for REQ-CF-7.14 (every unfulfilled Instance with its Invoices and Payments, sorted by agreement name, instance date and ID). The other ten CashFlow verbs each have at least one route-level caller (PaymentDataStates, InvoiceDataStates, MasterAgreementDataStates, MaintenanceOperations, ProjectionRules). There is no Tests.Integrated/InterfaceBridge/CashFlowRoutes.fs.

**Action:** Add a route-level test that sends {"projectionHorizonInDays":N} through routeUiCommandForTesting "CashFlow" "CreateUpcomingInstances". Assert that the returned InstanceCompositeReturn list holds every unfulfilled Instance with its Invoices and Payments, and that an out-of-range horizon is rejected at the route.

**Why:** The sweep is the first step of the planned Saturday state machine. Its domain behavior is well tested, but nothing tests the command the operator, or a future orchestrator, will actually run.

---

## ENF-FP-ENSURE-1 — enforcement-gap
- **Location:** Src/Ui.InterfaceBridge/Routes/FiscalPeriodRoutes.fs l.26-35 (ensure); Src/Business.CrossDomainOrchestration/FiscalPeriodCreation.fs l.34-63 (ensureFiscalPeriods); REQ-SYS-8.1, REQ-FP-2.7
- **Summary:** FiscalPeriod Ensure runs with no transaction (Context.create NoTransaction FiscalPeriodEnsure) but writes one row per missing month, so a multi-month Ensure can partly commit, which contradicts REQ-SYS-8.1.
- **Resolution:** fix-code

REQ-SYS-8.1: "An operation that makes more than one write performs them in a single database transaction that commits only when the whole operation succeeds and rolls back otherwise... Read-only operations and single-write operations may run without a transaction." The ensure route builds its context with NoTransaction, and Context.create maps that to createNoTransaction(), which has no Npgsql transaction, so each statement autocommits. ensureFiscalPeriods then runs `missingKeys |> List.map (constructNewAndPersist context)`, one insert per missing month. REQ-FP-2.7 has it create every missing month from start through end, so this is routinely a multi-write operation. Failure scenario: Ensure 2026-10 through 2027-03 with all six months missing; the fourth insert fails (a concurrent Create or Ensure of the same key hits the unique key, or the connection drops). Periods 2026-10..2026-12 stay committed while the call returns Error, which is a half-applied operation. A further problem: List.map runs every persist before convertListOfResultsToResultsList collects the results, so the later months are still attempted after one fails. No REQ-SYS-8.1 test covers Ensure: OperationInstantAndAtomicity.fs:248/297/316 cover JE posting, batch post and the command runner only. By contrast, every multi-write CashFlow, Ingestion and JE route uses runCommandRouteAndAutoCompleteTransaction.

**Action:** Run the Ensure handler under runCommandRouteAndAutoCompleteTransaction FiscalPeriodEnsure. Add a REQ-SYS-8.1 test that makes one month's insert fail mid-range and asserts that no period from the range was created.

**Why:** REQ-SYS-8.1 sits above the code in the authority order. The Saturday run calls Ensure every week (REQ-FP-2.7's rationale), so a partial result that comes back as an error while leaving rows committed is the kind of half-applied state the requirement exists to rule out.

---

## SPEC-CF-LINK-1 — missing-requirement
- **Location:** Src/Ui.InterfaceBridge/Routes/ClassificationRoutes.fs updatePaymentAgreementLink (l.140) / deletePaymentAgreementLink (l.151); Src/Business.FinancialServices.CashFlow/PaymentAgreementLink.fs delete (l.226) and update (l.186); REQ-CF-12.7, REQ-CF-14.6
- **Summary:** The link update and delete routes will re-point or delete a link whose staged line already has a Payment, and no REQ says whether that should be allowed.
- **Resolution:** dan-decides

REQ-CF-12.7 requires a way to create, re-point and delete a link, and constrains only creation (an already-linked line is rejected). REQ-CF-14.6 covers the opposite direction: deleting a Payment deletes its link. Nothing covers re-pointing or deleting a link while a Payment still references its line. The code has no guard: PaymentAgreementLink.delete is a bare `delete ... where unique_id = @unique_id` with ExactlyOne, and update checks only for a no-op. So (a) DeletePaymentAgreementLink on a paid line succeeds and leaves a Payment against an Invoice of Payment Agreement X with no link explaining it, and the line becomes a REQ-CF-12.3 linkage candidate again. (b) UpdatePaymentAgreementLink re-points the link to Payment Agreement Y while the Payment stays on X's Invoice, so the link and the Payment disagree about which obligation the cash paid. Both are observable through the operator routes, and both produce states the rest of the CashFlow spec never contemplates. By comparison, REQ-STG-6.5 explicitly blocks changing a linked or paid line's amount, type or account.

**Action:** Dan to decide whether re-pointing or deleting a link whose line a Payment references should be rejected with a typed error (mirroring REQ-STG-6.5), should cascade to the Payment, or is allowed. Then add the REQ to CashFlow §12 and a citing test.

**Why:** Whether the linkage matches the Payments decides whether the cash-flow records can be trusted as input to the retirement-planning engine. With no requirement, an agent developer will treat either behavior as correct.

---

## COV-RPT-HDR-1 — test-gap
- **Location:** Src/Ui.InterfaceBridge/ReportWriters/TrialBalanceWriter.fs l.234-239 (createAsOfHeader "Trial Balance Report" asOf); Src/Ui.InterfaceBridge/ReportWriters/PrePostingReviewWriter.fs l.157-164 (run date + entry/line count subtitle); REQ-RPT-3.1, REQ-RPT-7.7
- **Summary:** No test asserts the trial balance HTML header (title and as-of date, REQ-RPT-3.1) or the pre-posting review header's run date and entry/line counts (REQ-RPT-7.7). The only test citing REQ-RPT-3.1 checks the period activity writer.
- **Resolution:** fix-test

REQ-RPT-3.1 sits in §3 "Trial balance HTML rendering": "The rendered HTML report must contain a header section displaying the report title and the as-of Calendar Date." The only test that cites it is ReportRoutes.fs:461 ("REQ-RPT-6.4 REQ-RPT-3.1 the period activity rendered report header shows the begin and end dates"), which reads PeriodActivityWriter output. The trial balance report-mode tests (ReportRoutes.fs:153 REQ-RPT-2.3, :174 REQ-RPT-2.4) assert only that the file exists and where it is; neither reads the HTML. A grep for "Trial Balance Report" and "<header" in Tests/ finds only the period activity test. So the traceability check passes while TrialBalanceWriter's header path is untested. Likewise, REQ-RPT-7.7 says "the rendered header shows that date and the number of entries and lines". The REQ-RPT-7.7 report test (ReportRoutes.fs:227) asserts only that an entry description appears somewhere in the HTML, and nothing checks the "Run on <date> · N entries, M lines" subtitle that PrePostingReviewWriter builds at l.159-163.

**Action:** Add a trial balance report-mode test that reads the written HTML and asserts that the <header> contains the report title and the as-of date (yyyy-MM-dd). Extend the REQ-RPT-7.7 report test to assert that the header contains the run date and the exact entry and line counts for the committed fixture.

**Why:** Code-inward, two header paths that the spec names explicitly are never checked, and one of them is hidden behind a REQ ID that looks tested only because a different writer's test cites it.

---

