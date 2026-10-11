# spec-quality-CashFlow

## CF-FULFIL-LEG-1 — contradiction
- **Location:** Specs/Behavioral/CashFlow.md REQ-CF-4.9 (line 142), REQ-CF-9.10 (line 266), REQ-CF-8.4 (line 233), REQ-CF-7.9 Why (line 213), REQ-CF-4.11 Why (line 145)
- **Summary:** Under REQ-CF-4.9/9.10 an Instance counts as fulfilled once its existing Invoices are paid, even when a Payment Agreement leg has no Invoice yet. Because REQ-CF-8.4 reports bills to chase only on open Instances, the un-invoiced leg then drops out of the bills to chase. That breaks the spec's own promise (REQ-CF-7.9 Why) that such legs show up through their Instance.
- **Resolution:** dan-decides

REQ-CF-9.10: "An Instance is fulfilled when it has at least one 'FullyPaid' Invoice and every Invoice is either 'FullyPaid' or cancelled." The test looks only at Invoices that exist. A Payment Agreement leg with no Invoice does not count against it. REQ-CF-8.4 reports a bill to chase only for "Payment agreements on open Instances (REQ-CF-4.14) ... that have no Invoice", and an open Instance is one that is neither fulfilled nor cancelled.

Take a two-leg agreement: a fixed rent leg (the sweep invoices it, REQ-CF-7.8) and a variable utility leg (the sweep leaves it un-invoiced, REQ-CF-7.9). Once the rent Invoice is matched and FullyPaid, the Instance is derived fulfilled. The utility leg then stops being a bill to chase before its bill has ever arrived. Three parts of the spec say otherwise:
(1) The REQ-CF-7.9 Why says these legs "appear in the projection as bills to chase via their Instance alone."
(2) REQ-CF-8.4 says "each missing agreement is a separate bill to chase."
(3) The REQ-CF-4.11 Why gives "a variable leg with nothing billed that period" as a reason to cancel an Instance. That only makes sense if an un-invoiced leg keeps the Instance open.

The code follows the literal rule. InstanceOrchestration.fs:475-481 deriveIsFulfilled checks only the existing invoiceComposites. CashFlowOps.fs:830ff builds billsToChase from fetchOpenComposites only. The CashFlowOps.fs:364 comment assumes the opposite ("its instance can still be open, waiting on a sibling leg"). ProjectionRules.fs:366 pins the drop ("a fulfilled Instance ... with one of its two Payment Agreements invoiced and that Invoice FullyPaid, yields no bill to chase for the other").

The gap does not stay hidden forever. When the utility cash is later linked, REQ-CF-13.1 excludes the fulfilled Instance, and REQ-CF-13.7 fails the whole matching run ("no open Invoice on that agreement covers the line's date"). Until that cash arrives, though, the projection hides the bill. There is no prior ruling on this. 2026-10-03a CUST-2 was about retiring items that will never be paid, not about this derivation.

**Action:** Dan decides which rule wins. Either (a) amend REQ-CF-4.9/9.10 so that an Instance with any Payment Agreement of its Master Agreement lacking an Invoice is not fulfilled (cancelling the Instance is the way to retire an unbilled leg, as the REQ-CF-4.11 Why already says), or (b) keep the derivation and correct the REQ-CF-7.9 and REQ-CF-4.11 Whys and REQ-CF-8.4 to state that a missing leg on a fulfilled Instance is not a bill to chase.

**Why:** Bills to chase is a Saturday output. A variable bill that disappears from it once the fixed leg is paid is the case that output exists to catch. The spec's rationale and its derivation rule now disagree, and the code and a test have taken the side that hides the bill.

---

## CF-AVAIL-PA-1 — ambiguity
- **Location:** Specs/Behavioral/CashFlow.md REQ-CF-2.26 (line 108) vs REQ-CF-14.8 (line 358) and REQ-CF-14.9 (line 360)
- **Summary:** REQ-CF-2.26 forbids updating a Master Agreement whose end date has passed. It does not say whether updating one of its Payment Agreements (REQ-CF-14.8) or adding a new one (REQ-CF-14.9) counts as such an update. The two developer readings give different observable behaviour, and the code takes the stricter one without any requirement saying so.
- **Resolution:** fix-spec

REQ-CF-2.26: "A Master Agreement may be created or updated before its start date, but not when its end date is earlier than the current date." REQ-CF-14.8 makes a Payment Agreement update "subject to §3" and REQ-CF-14.9 makes adding one "subject to §3". §3 holds the Payment Agreement data states and says nothing about the parent's dates. One developer would reject a Payment Agreement edit or addition under an ended agreement, treating it as an update to the agreement. Another would allow it, because only §3 applies. That is an observable difference: whether, after a lease ends, the operator can still correct a leg's account, rename it, or change its days-due.

The code takes the first path. AgreementOrchestration.updateAgreement (line 413ff) always runs confirmComposite on the fetched result (line 453). That calls confirmMasterAgreement → confirmAgreementDates (lines 150-162), which returns CashflowMasterAgreementUnavailable whenever the stored end date is before today, even if only paymentAgreementUpdates or newPaymentAgreements were sent. The REQ-CF-2.26 tests (MasterAgreementDataStates.fs:577-690) cover only Master Agreement fields. No test covers a Payment Agreement update or addition under an ended agreement. The note at MasterAgreementDataStates.fs:652 says agreements reach a past end date only through time passing ("no route can end one in the past"). So this case is just what happens to every agreement once its end date has passed.

**Action:** Amend REQ-CF-2.26 (or REQ-CF-14.8/14.9) to state whether updating or adding a Payment Agreement under a Master Agreement whose end date is before the current date is rejected. Then cite a test for that path.

**Why:** Whether an expired agreement's legs are frozen is user-visible behaviour. Right now it exists only as a side effect of how the code validates the composite. A refactor that validated only the Master Agreement fields actually changed would reverse it, and no spec text or test would show the change.

---


