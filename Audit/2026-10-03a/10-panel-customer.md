# customer-hobson

## CUST-1 — customer-gap
- **Location:** Src/Ui.InterfaceBridge/InterfaceContracts/ClassificationContracts.fs (PaymentAgreementDecisionReturn, RuleMatchReturn); Src/Ui.InterfaceBridge/Routes/ClassificationRoutes.fs (Update/DeletePaymentAgreementLink); Src/Business.FinancialServices.CashFlow/CashFlowError.fs:161; REQ-CF-12.7; plan-saturday-readiness §8.8 R-14
- **Summary:** The operator has no way to get the ID of a payment agreement link that ClassifyPaymentAgreements created, but UpdatePaymentAgreementLink and DeletePaymentAgreementLink both need that ID.
- **Resolution:** dan-decides

REQ-CF-12.7 requires a way to re-point and delete links. Both routes take `paymentAgreementLinkId: Guid`. Only `CreatePaymentAgreementLink` (a manual link) returns a link ID, in `PaymentAgreementLinkReturn`. Links created automatically by `Classification ClassifyPaymentAgreements` are reported through `PaymentAgreementDecisionReturn { stageEntryLineId; paymentAgreementName; ruleIds; outcome }`, which has no link ID. No route fetches links: `PaymentAgreementLink.fetchByStageEntryLineId` and `fetchAll` exist in Src (`PaymentAgreementLink.fs:123,161`) and are used only inside CashFlowOps. `FetchStageEntryFiltered`, `FetchAgreementSummary` (AgreementReturn), `FetchClassificationRun` and `PrePostingReview` return no link IDs either. The duplicate-link error `CashflowPaymentAgreementLinkLineAlreadyLinked` tells the operator to "Repoint that linkage or remove it". It supplies the line UUID and the Payment Agreement's UUID, not the link ID and not the agreement's name, even though REQ-CF-12.7 says "naming" and §14 addresses agreements by name. The Saturday runbook Hobson agreed in R-14 ("if a line to be split is already linked, delete the link, split, and re-link") therefore cannot be carried out through the CLI for any automatic link that has no Payment. A link that has a Payment can be removed only indirectly, through DeletePayment (REQ-CF-14.6). A second, smaller version of the same problem affects Payments: the step-10 review surface `PrePostingPaymentReturnRow` carries no `paymentId` or `invoiceId`, so acting on a wrong match it shows (DeletePayment) means calling FetchAgreementSummary and matching `transactionPointer = Staged <lineId>` by hand.

**Action:** Expose link identity where the operator sees links. Add `paymentAgreementLinkId` to PaymentAgreementDecisionReturn and to PrePostingLineReturnRow (or add a FetchPaymentAgreementLink-by-stage-line route), and have the already-linked error carry the link ID and the agreement name. Add paymentId and invoiceId to PrePostingPaymentReturnRow.

**Why:** Weekly. Step 8 (split) and step 9 (link) are the judgment steps of Saturday, and R-14 explicitly relies on deleting and re-linking. Without the ID, Hobson's only way out is raw SQL, which goes against the standing policy that the CLI is the only mutation path.

---

## CUST-2 — customer-gap
- **Location:** Specs/Behavioral/CashFlow.md §8 (REQ-CF-8.2, 8.3, 8.4), §9 (REQ-CF-9.8–9.10), §14; REQ-CF-5.6; Src/Ui.InterfaceBridge/Routes/CashFlowRoutes.fs (no instance/invoice retire route); Src/Business.FinancialServices.CashFlow/Instance.fs, Invoice.fs (no delete)
- **Summary:** No operation can retire an Instance or Invoice that will never be paid, so it stays permanently in ProjectCashFlow's outflows and in bills-to-chase, both of which deliberately have no lower date bound.
- **Resolution:** dan-decides

An Instance is fulfilled only when it has at least one Invoice and every Invoice is FullyPaid (REQ-CF-9.10). FullyPaid is derived only from Payments (REQ-CF-9.8). A Payment must sit on a real line on the agreement's account (REQ-CF-6.9). An Invoice amount must be positive (REQ-CF-5.6), so it cannot be zeroed. The invoice states are Generated/Sent/Expected/Received, with no cancelled or waived state. There is no DeleteInstance, DeleteInvoice or cancel route, and Instance.fs/Invoice.fs have no delete function. Meanwhile REQ-CF-8.2/8.3 count every non-FullyPaid invoice on an unfulfilled instance with "no lower bound on the due date", and REQ-CF-8.4 lists every un-invoiced leg with "no lower bound on the Instance date". Real cases that produce a permanent phantom: an Instance created by mistake with CreateInstance; a sweep instance from a wrong cadence (fixing the cadence with UpdateAgreement does not remove instances already spawned); a waived or forgiven tenant charge; a variable leg with nothing billed that month (a $0 invoice is illegal, so that leg stays a bill to chase); a bill settled outside the imported accounts. In each case "money you need to move" and "bills to chase" carry the item every week from then on. CreateUpcomingInstances and ClassifyPaymentAgreements (openInstances) also return it every week. The only workarounds are raw SQL or posting a fabricated ledger line to point a Payment at.

**Action:** Dan to decide on a retirement mechanism, for example a terminal invoice state such as 'Cancelled'/'Waived' that counts as satisfied for is-fulfilled and is excluded from REQ-CF-8.2–8.4, or a hard delete of a Payment-less Instance or Invoice with a reason. Then spec and route it.

**Why:** The projection and bills-to-chase are Saturday outputs Dan reads every week (plan step 14). Because both are unbounded backwards by design, one unretirable item corrupts every later week's numbers, not just one.

---

## CUST-3 — missing-requirement
- **Location:** Src/Business.CrossDomainOrchestration/JournalEntryVoiding.fs:93-118 (confirmNoPaymentReferencesEntry); Src/Business.FinancialServices.CashFlow/Payment.fs:299-312; Src/Business.CrossDomainOrchestration/CashFlowOps.fs:886ff (transitionPaymentsToPosted); REQ-JE-4.14; REQ-CF-10.1–10.3
- **Summary:** A journal entry posted from staging can be voided between PostStageEntries and TransitionPaymentsToPosted while a Payment still covers its cash, and the transition then attaches that Payment to the voided line.
- **Resolution:** dan-decides

REQ-JE-4.14 refuses a void only when a Payment's transaction pointer references a line of the entry. The guard queries `pmt.journal_entry_line_id in (...)` only. After step 11 (post), a matched Payment still has a Staged pointer (journal entry line ID null) until step 12 runs `CashFlow TransitionPaymentsToPosted`. So a void in that window passes the guard. Step 12 then reads the staged line's recorded journal entry line ID (REQ-CF-10.2/10.3) and sets it on the Payment without checking whether that entry is voided. The Invoice derives FullyPaid/PostedToLedger on cash the ledger no longer records, which is exactly what REQ-JE-4.14's Why says the guard exists to prevent. The window is open whenever step 12 is skipped, fails, or runs late (for example a state-machine crash between steps 11 and 12), or when a correction is made right after posting. Under R-17, void plus a manual repost is the only correction path, so voiding right after posting is a realistic move.

**Action:** Dan to decide between two fixes. (a) Extend REQ-JE-4.14 so the void also refuses when any Payment's staged line produced a line of the entry (via the staged line's recorded JE line, REQ-STG-9.10). (b) Have REQ-CF-10 skip or reject lines of voided entries. Also consider making the payment transition part of batch post (see CUST-8).

**Why:** The guard is meant to keep the ledger and the obligation state from drifting apart. A gap that depends on the order of operations brings back the silent 'paid on voided cash' state, and nothing on Saturday (reconciliation, integrity) reads Invoice posted state, so it would not be caught.

---

## CUST-4 — customer-gap
- **Location:** Src/Ui.InterfaceBridge/Routes/CashFlowRoutes.fs:78 (`AgreementOrchestration.updateAgreement context []`); Src/Ui.InterfaceBridge/InterfaceContracts/CashFlowContracts.fs (UpdateAgreementInput); Src/Business.CrossDomainOrchestration/AgreementOrchestration.fs:377-405; REQ-CF-14.1, 14.2
- **Summary:** Payment agreements cannot be changed or added after creation. The orchestration supports updates, but the route hard-codes an empty update list and the spec covers only the master agreement.
- **Resolution:** dan-decides

`AgreementOrchestration.updateAgreement` takes `paymentAgreementUpdates` and applies `PaymentAgreement.update` (line 396), but the only caller, the `UpdateAgreement` route, passes `[]`. The route description says "Its payment agreements are not touched". REQ-CF-14.2 lists master-agreement fields only, and no REQ provides adding a leg to an existing master agreement. Weekly consequence: the sweep creates fixed invoices from `expectedAmount` and `daysDueAfterInvoiceDate` (REQ-CF-7.8). After a rent increase, an escrow or premium change, or a wrong debit/credit account or days-due typed at creation, every swept invoice is wrong until someone corrects it by hand with UpdateInvoice each week. The other option is ending the agreement and creating a new one. Payment agreement names are globally unique (REQ-CF-3.9), so that needs a new name, and every classification rule that claims the old leg must be re-pointed. LeoBloom usage lists `obligation agreement create/update` for "lease changes and oddities".

**Action:** Specify payment-agreement update (expected amount, days-due, accounts by code, memo, name) and adding a leg to an existing master agreement. Then extend UpdateAgreementInput (or add an UpdatePaymentAgreement route) so the existing orchestration path is reachable.

**Why:** Lease renewals and bill changes happen several times a year across 9 agreements, and each one otherwise costs either a weekly manual invoice fix or a full rebuild of the agreement and its rules.

---

## CUST-5 — customer-gap
- **Location:** Src/Ui.InterfaceBridge/Routes/CashFlowRoutes.fs (FetchAgreementSummary takes agreementName only); CashFlowContracts.fs FetchAgreementSummaryInput; HobsonsNotes/cli-requirements-from-leobloom-usage.md §1
- **Summary:** No read-only route lists master agreements, payment agreements or open instances. The only cross-agreement views come from mutating routes.
- **Resolution:** dan-decides

`FetchAgreementSummary` requires an `agreementName` that the caller must already know. No route lists master agreements or payment-agreement names, although payment-agreement names are what classification rules (`ClassificationClaimantInput.PaymentAgreement of string`), links and CreateInvoice address by. Open or unfulfilled instances across agreements are returned only by `CreateUpcomingInstances` (which writes instances and invoices) and `ClassifyPaymentAgreements` (which writes links and Payments). `ProjectCashFlow` is read-only but shows only invoiced legs and bills-to-chase, not the agreement roster. LeoBloom usage data records `obligation agreement list/show` as a weekly command ("all 9 agreements, every Saturday"). The domain already has `Instance.fetchByIsFulfilled` and the `fetchByMasterAgreementIdList` functions.

**Action:** Add a read-only agreement listing (master agreements with their payment-agreement names, accounts and active dates) and a read-only open-instances fetch.

**Why:** Hobson works by name. Without a listing, agreement names live in wakeup notes like the account-id map LeoBloom forced on him, and checking 'what's open' needs a write.

---

## CUST-6 — enforcement-gap
- **Location:** Src/Ui.InterfaceBridge/InterfaceContracts/CashFlowContracts.fs PaymentAgreementReturn (debitAccountCode, creditAccountCode); AccountContracts.fs AccountReturn.parentCode, AccountActivityReturn.accountParentCode; REQ-NGUI-1.6
- **Summary:** Several return payloads identify an account by code only, which breaks REQ-NGUI-1.6. That REQ is waived from testing on the grounds that audit enforces it.
- **Resolution:** fix-code

REQ-NGUI-1.6: "All interface return payloads that identify an account must include the account name alongside the account code." Its waiver row reads "enforced by code review and periodic audit" (Dan, 2026-08-07), so this audit is that check. `PaymentAgreementReturn` has `debitAccountCode`/`creditAccountCode` and no names. It is returned by CreateAgreement, UpdateAgreement and FetchAgreementSummary, which are the views where Dan checks an agreement's accounts. `AccountReturn.parentCode` and `AccountActivityReturn.accountParentCode` likewise name the parent account by code only. Other returns (JournalEntryLineReturn, StageEntryLineReturn, TrialBalance, Reconciliation, PeriodActivity, PrePosting, ProjectedAccountReturn) do carry names.

**Action:** Add debitAccountName/creditAccountName to PaymentAgreementReturn and a parent name beside parentCode/accountParentCode.

**Why:** LeoBloom lesson: Dan reviews decisions by account name. An agreement wired to the wrong account is easiest to spot by name, and the spec already requires it.

---

## CUST-7 — statement-delta
- **Location:** Dan's statement (cash flow section); Src/Ui.InterfaceBridge/Routes/IngestionRoutes.fs post / StageEntryOrchestration.post; CashFlowRoutes TransitionPaymentsToPosted; REQ-CF-10
- **Summary:** Dan's model says a Payment "gains its journal entry line linkage when the staged entry eventually posts". In the repo, posting does not touch Payments; the link is made only by a separate operator-invoked route.
- **Resolution:** dan-decides

PostStageEntries (`postWithExternallyManagedTransaction`) runs `StageEntryOrchestration.post` and trial balances only. Payments move from Staged to Posted only when `CashFlow TransitionPaymentsToPosted` is run (plan step 12, REQ-CF-10). Until then Invoices read NotHandled/PartiallyPosted and the void guard has the gap described in CUST-3. Nothing in code ties the two steps together or detects that step 12 was skipped.

**Action:** Dan to confirm which model is intended. If linkage should follow posting, fold the transition into batch post's transaction. If not, update the mental model and make sure the Saturday runbook or state machine treats step 12 as mandatory immediately after step 11.

**Why:** The audit is Dan's backstop for agent-written code. A gap between his model and the code here is where CUST-3's integrity hole lives.

---

## CUST-8 — stale-reference
- **Location:** Src/Ui.InterfaceBridge/Routes/CashFlowRoutes.fs:199-201 (UpdateInvoice description); REQ-CF-9.11; REQ-NGUI-2.5 (withdrawn)
- **Summary:** The UpdateInvoice route description still says a payload that sets payment state or posted state "is rejected". After F-5 those fields are gone and are silently dropped.
- **Resolution:** fix-code

The description reads "Payment state and posted state are derived, so a package that sets either is rejected." Plan §8.10 F-5 removed `paymentStateUpdate`/`postedStateUpdate` from `UpdateInvoiceInput` and deleted the rejecting check. Under the REQ-NGUI-2.5 withdrawal, unknown fields are dropped, so a caller sending them now gets success with the fields ignored. The route table is the only per-route documentation the operator has.

**Action:** Reword the description to match REQ-CF-9.11 (the contract carries no derived state; such fields are ignored).

**Why:** An operator who relies on the documented rejection to catch a mistaken payload gets silent success.

---


