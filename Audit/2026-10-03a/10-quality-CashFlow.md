# spec-quality-auditor:CashFlow.md

## CF-STALE-1 — stale-reference
- **Location:** Specs/Behavioral/CashFlow.md REQ-CF-9.6, REQ-CF-9.7 (lines 245-248); also §1 Payment prose line 60 and design note line 11
- **Summary:** REQ-CF-9.6 and 9.7 still define 'Posted' as a Payment having a 'journal entry header ID', but the transaction pointer moved to line level on 2026-09-26 (REQ-CF-6.4, 6.5, 10.1).
- **Resolution:** fix-spec

REQ-CF-9.6: 'Posted state cannot be PostedToLedger unless all Payments for the Invoice have a journal entry header ID (i.e. all transaction pointers resolve to Posted).' REQ-CF-9.7: '...unless at least one Payment for the Invoice has a journal entry header ID.' REQ-CF-6.4's Why says the pointer was 'moved to line level 2026-09-26', and REQ-CF-6.5, 10.1 and 10.3 all talk about a journal entry line ID. A Payment no longer has a header ID field. 9.6 and 9.7 were not updated in that revision. The entity prose has the same leftover wording: line 60 says a Payment is 'linked to a staged entry' or 'linked to a journal entry', and the design note on line 11 says 'initially linked to a staged entry ... later linked to a journal entry'. Line 66 correctly says 'staged entry line or journal entry line'. The tests (DerivedStateRules.fs:370, :380) use the line-level meaning, so the code follows the current model and only the spec text is stale.

**Action:** In REQ-CF-9.6 and 9.7, replace 'journal entry header ID' with 'journal entry line ID'. Change the line-60 and line-11 prose to say staged line and journal entry line.

**Why:** Every code and test change is made by an agent working from the spec. A requirement that names a field the model no longer has invites someone to 'restore' a header-level pointer, or to treat 9.6 and 9.7 as unimplementable.

---

## CF-CONTRA-1 — contradiction
- **Location:** Specs/Behavioral/CashFlow.md REQ-CF-9.3 vs REQ-CF-13.1, REQ-CF-13.4
- **Summary:** REQ-CF-13.4 says matching always creates a Payment when an Invoice has exactly one candidate line, but REQ-CF-9.3 forbids the FullyPaid state that this Payment produces on an Invoice that has a blocker. The spec does not say which rule wins.
- **Resolution:** dan-decides

REQ-CF-13.1 makes candidates of every Invoice on an unfulfilled Instance that is not FullyPaid and not overpaid. It does not exclude Invoices with a blocker. REQ-CF-13.4: 'When an Invoice has exactly one candidate line, a Payment is created against it.' REQ-CF-9.8 and 9.10 then derive payment state, and if the line's amount equals the Invoice amount the state becomes FullyPaid. REQ-CF-9.3: 'Payment state cannot be FullyPaid while blocker state is non-null.' The code (CashFlowOps.fs matchInvoicesAndCreatePayments, lines 373-399 and 494) filters only FullyPaid and overpaid Invoices and sends the Payment through InstanceOrchestration.updateInstanceComposite. That path enforces 9.3, as the test DerivedStateRules.fs:296 shows. So one blocked Invoice whose bill gets paid (for example a 'NoFunds' blocker on a bill that is then paid in full) makes the whole linkage-and-matching run fail and roll back under REQ-SYS-8.1. That error is not one of the outcomes REQ-CF-13.9 lists. Two developers could reasonably do this in three different ways: exclude blocked Invoices from matching, clear the blocker automatically, or report the Invoice and skip it. The current code does a fourth thing: it fails the whole run.

**Action:** Dan decides how matching treats an Invoice with a blocker: exclude it from REQ-CF-13.1, report it as an outcome in REQ-CF-13.9, or fail the run on purpose. Then state that choice in §13.

**Why:** This happens in the Saturday routine. Under the current code, one blocked bill getting paid stops every other obligation from being matched that week, and the error does not say that the blocker is the cause.

---

## CF-AMB-1 — ambiguity
- **Location:** Specs/Behavioral/CashFlow.md REQ-CF-12.4 vs REQ-CF-6.9
- **Summary:** When a line-type-constrained rule decides leg selection (REQ-CF-12.4), the account is never checked. The link that results can fail REQ-CF-6.9 when matching turns it into a Payment, and the whole run fails instead of reporting the claim as 'not linked'.
- **Resolution:** dan-decides

REQ-CF-12.4: 'when any claiming rule constrains line type, the lines that rule matched are kept'. Only the default path checks the account ('the one on the Payment Agreement's credit account ... or on its debit account'). Classification rules cannot match on account: REQ-CR-1.13 limits field matches to Source, Description, Memo, LineType and Amount. So the kept line can be on any account. REQ-CF-6.9 requires a Payment's line to be on the agreement's credit account (Income) or debit account (Outgo). Example: an Outgo rule constrained to Credit, written to catch refunds, also matches that merchant's normal payment entry. That entry's Credit line is on the cash account, which is the agreement's credit account. The line is linked (CashFlowOps.fs:243-246 keeps line-type-rule survivors with no account filter), and then REQ-CF-13.4 matching creates a Payment that breaks 6.9. The whole operation then rolls back. REQ-CF-12.4 says that a bad selection should mean 'no link, and the claim is reported with the reason', so the spec does not say whether the account constraint also applies to rule-selected lines. Reasonable developers would differ.

**Action:** State in REQ-CF-12.4 whether lines kept by a line-type-constrained rule must also be on the account that REQ-CF-6.9 requires for the agreement's direction. If they must, say that a kept line on the wrong account counts as zero kept lines, which means it is reported and not linked.

**Why:** Without that sentence, one broad rule can stop the obligation-matching run every week, and the failure shows up far from its cause. The cause is a rule, but the error reports a Payment data-state violation.

---

## CF-ELAB-1 — insufficient-elaboration
- **Location:** Specs/Behavioral/CashFlow.md REQ-CF-14.2 vs REQ-CF-4.6, REQ-CF-4.7, REQ-CF-6.9, REQ-CF-7.7; SystemWide REQ-SYS-2.1
- **Summary:** REQ-CF-14.2 allows changing cadence, next-instance date and flow direction, but does not say whether existing Instances must still fit the cadence (4.6), whether the new next-instance date must come after the latest Instance (4.7), or whether existing Payments must still be on the right account (6.9).
- **Resolution:** dan-decides

REQ-CF-14.2 checks only one consequence of an update: a flow-direction change against invoice states (REQ-CF-5.10). But REQ-CF-4.6 is a data-state rule ('An Instance's date must fit its Master Agreement's cadence'), and REQ-SYS-2.1 requires data-state rules to hold on every operation that reconstitutes an entity. Changing a Monthly-on-the-1st agreement to the 15th leaves every existing Instance in a state that 4.6 calls illegal. Setting the next-instance date on or before the latest Instance breaks the guarantee in REQ-CF-7.7 ('no date is enumerated twice'). The next sweep then hits a 4.7 rejection, and under REQ-CF-7.15 nothing from the whole sweep, across all agreements, is kept. A flow-direction change swaps which account REQ-CF-6.9 requires for every existing Payment. The code (AgreementOrchestration.fs updateAgreement plus confirmComposite/confirmInstance, lines 72-91 and 177-195) checks only that each Instance belongs to the agreement. It does not re-check 4.6, 4.7 or 6.9. The spec does not say whether that is correct, and a second developer reading 4.6 together with SYS-2.1 would reject the update.

**Action:** In REQ-CF-14.2, state the rule for each case: (a) 4.6 applies only when an Instance is created, or a cadence change is rejected when an existing Instance would no longer fit; (b) an updated next-instance date must be later than the latest existing Instance; (c) a flow-direction change is rejected when an existing Payment's line would break 6.9.

**Why:** A cadence edit is routine (a biller moves the due date). As things stand, one such edit can make every following Saturday sweep fail, or can leave records that break a data-state rule the spec calls universal.

---

## CF-AMB-2 — ambiguity
- **Location:** Specs/Behavioral/CashFlow.md REQ-CF-13.1
- **Summary:** REQ-CF-13.1 orders candidate Invoices by due date but gives no tie-break, while its own Why says 'Fetch order must never decide it'.
- **Resolution:** dan-decides

REQ-CF-13.1: 'They are considered in order of due date, oldest first. Why: The oldest bill gets first claim on a line two invoices could both take. Fetch order must never decide it.' Two Invoices on the same Payment Agreement can have the same due date. Operator-entered Invoices (REQ-CF-14.5) can have any due date, for example two late-entered bills both due at month end. When they do, the spec sets no order. The code (CashFlowOps.fs:398-400) breaks the tie on the Invoice UUID, and Guid.NewGuid() generates it (CashFlowComponent.fs:45). That order is effectively random. It is not fetch order, but it does not do what the Why asks either: which bill gets the line becomes a matter of chance. Another developer might break the tie by invoice date, by Instance date, or by treating it as contested.

**Action:** Add an explicit tie-break to REQ-CF-13.1 (for example Instance date, then invoice date), or state that Invoices with equal due dates that cover the same line are reported as contested.

**Why:** The requirement exists so that matching is deterministic and explainable. A tie settled by a random UUID gives an answer that can be repeated but not explained, which is the outcome the Why rules out.

---

## CF-CONTRA-2 — contradiction
- **Location:** Specs/Behavioral/CashFlow.md §9 preamble (line 233) vs REQ-CF-14.5 (line 330); design note line 17 vs REQ-CF-4.10, REQ-CF-5.5 Why
- **Summary:** The spec gives two conflicting orders for composite validation: §9 says to persist first, then validate and roll back, while REQ-CF-14.5 says to validate 'before anything is written'. It also gives two scopes for the diamond check: 'at creation time' versus 'whenever ... created or changed'.
- **Resolution:** fix-spec

§9 preamble: 'persist the new state, fetch the resulting composite (Invoice + its Payments), validate the composite, rollback on failure.' REQ-CF-14.5: 'Each validates the Instance as a whole (§4, §5, §9) before anything is written.' Separately, the diamond design note (line 17) and the REQ-CF-5.5 Why both say the diamond is 'validated by the orchestrator at creation time', but REQ-CF-4.10 says it is 'validated whenever the Instance or any of its Invoices or Payments is created or changed'. The code persists first and validates after (the AgreementOrchestration.updateAgreement doc comment says 'updates are sent to the DB *before* aggregate validation'). So the code follows §9, not REQ-CF-14.5's literal text. Because REQ-SYS-8.1 rolls back the whole operation, the persisted state ends up the same either way. The difference shows in which error the caller sees when a schema constraint (for example REQ-CF-5.16 uniqueness) and a composite rule both fail, and in which of the two statements a future agent treats as binding.

**Action:** Make the two statements agree. Either drop 'before anything is written' from REQ-CF-14.5 (and rely on §9 plus REQ-SYS-8.1), or rewrite the §9 preamble. Also change the line-17 note and the 5.5 Why from 'at creation time' to match REQ-CF-4.10.

**Why:** Agents implement from the spec text. Two authoritative statements that disagree about write order and validation scope lead to inconsistent implementations across the CashFlow operations, and to tests that assert whichever version the test author happened to read.

---

## CF-CONTRA-3 — contradiction
- **Location:** Specs/Behavioral/CashFlow.md Template/Event design note (line 6) vs §1 Master Agreement (line 30), REQ-CF-2.24, REQ-CF-4.8
- **Summary:** The design note says Templates 'do not change period to period', but the Master Agreement template's next-instance date changes every time an Instance is created (REQ-CF-4.8).
- **Resolution:** fix-spec

Line 6: 'Templates are created once and describe what *will happen* each period. They do not change period to period.' Line 9 assigns 'when' to Events ('Event entities own the "when and whether" — dates'). But REQ-CF-2.24 puts a next-instance date on the Master Agreement's cadence, and REQ-CF-4.8 advances it on every Instance creation, which is once per period by design. The 4.8 Why also calls it 'how the sweep knows where to resume'. So the top-level template holds per-period progress state, and the 'organizing principle' says templates never do.

**Action:** Amend the Template/Event design note so that the next-instance date is named as the one piece of per-period progress state held on the template.

**Why:** The design note is called 'the organizing principle'. An agent that applies it literally might move the next-instance date onto an Event, or treat its update as a defect, which would undo the 2026-09-26 sweep redesign.

---

## CF-3STATE-1 — other
- **Location:** Specs/Behavioral/CashFlow.md Unenforceable table (REQ-CF-7.13, REQ-CF-10.6) vs REQ-CF-8.5; Specs/README.md lines 82-85
- **Summary:** REQ-CF-7.13 and 10.6 ('[DET] deterministic') are filed as Unenforceable for a reason that does not fit the README's definition. REQ-CF-8.5 makes the same kind of claim and is filed as tested.
- **Resolution:** dan-decides

Specs/README.md defines Unenforceable as 'nothing in the system enforces it; it binds humans, not code ... policy, convention, or responsibility assignments'. 7.13 and 10.6 bind the code's behaviour, not humans. The stated reason ('Deterministic is not observable ... Repeat-and-compare tests catch the visible failure') says the requirement cannot be verified, which is the Waived category, not Unenforceable. Meanwhile REQ-CF-8.5 ('The projection is a deterministic [DET] operation. It performs arithmetic only and makes no judgment calls') is classified as tested, and is cited by exactly the repeat-and-compare test the 7.13 reason describes (ProjectionRules.fs:369). The same kind of requirement therefore sits in two different states within one file. The '[DET]' tag is also not defined anywhere in Specs/. Dan approved both entries on 2026-10-03 (commit 366b75d), so this is raised for consistency, not as a challenge to the ruling.

**Action:** Dan decides one consistent treatment for the three '[DET]' requirements (7.13, 8.5, 10.6): either all tested by repeat-and-compare, or all waived with a 'not observable' reason. Move the two Unenforceable entries to match.

**Why:** The three-state rule is only useful if each state means one thing. Unenforceable is meant for rules no code can break. Filing code-behaviour rules there weakens the category, and later audits will apply it inconsistently.

---

## CF-STALE-2 — stale-reference
- **Location:** Specs/Behavioral/CashFlow.md §10 preamble (line 259)
- **Summary:** §10 places the payments-to-posted transition in 'Phase 7 of the Saturday routine', but Saturday phases are defined only in HobsonsNotes, which is history and not authority.
- **Resolution:** fix-spec

Searching the whole repo (excluding BdsNotes) finds 'Phase 7' only in CashFlow.md and in HobsonsNotes/saturday-state-machine-draft-2026-09-04.md, saturday-routine-sonofleo-draft-2026-08-28.md and cashflow-cli-routes-proposal-2026-08-28.md. Specs/README.md classes HobsonsNotes as 'history, never authority'. Dan's statement describes the Saturday state machine as planned future work. A behavioral spec therefore depends on a phase number from a draft that is not authoritative and may be renumbered. REQ-CF-13.7's second Why also relies on 'the Saturday order' (sweep, then variable bills, then linkage) as the reason for failing on orphans, and that order is likewise not specified anywhere authoritative.

**Action:** Replace 'Phase 7 of the Saturday routine' with the actual precondition ('after staged entries have been batch-posted', REQ-STG-9.x), or move the Saturday phase order into an authoritative spec and cite it from there.

**Why:** An authoritative requirement should not depend on a non-authoritative draft. When the Saturday state machine is built, 'Phase 7' may no longer mean the same step.

---


