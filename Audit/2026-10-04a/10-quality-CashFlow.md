# spec-quality-CashFlow

## CF-DERIVE-1 — insufficient-elaboration
- **Location:** Specs/Behavioral/CashFlow.md REQ-CF-9.10 (line 264), REQ-CF-9.8, REQ-CF-4.9, REQ-CF-14.5, REQ-CF-14.10
- **Summary:** REQ-CF-9.10 names only Payment create/re-point/remove as triggers for re-deriving payment state, posted state and is-fulfilled. Changing an Invoice's amount, adding an Invoice to a fulfilled Instance, and cancelling an Invoice also change those derived values, and the spec does not say what happens in those cases.
- **Resolution:** fix-spec

REQ-CF-9.10: "Any operation that creates, re-points, or removes a Payment must re-derive the payment state and posted state of the affected Invoice, and the is-fulfilled flag of its Instance, in the same transaction." Its own Why says: "Derived state that is recomputed only by some operations drifts." Three operations the spec allows change these derived values without touching a Payment:
(a) REQ-CF-14.5 lets the operator update an Invoice's amount. Under REQ-CF-9.8, a FullyPaid Invoice whose amount changes is no longer FullyPaid.
(b) REQ-CF-14.5 lets the operator add an Invoice to an existing Instance. A new NotYetPaid Invoice on a fulfilled Instance breaks REQ-CF-4.9.
(c) REQ-CF-14.10 / REQ-CF-5.17 cancel an Invoice. Under REQ-CF-4.9, cancelling an Instance's last unpaid Invoice can make the Instance fulfilled.
Two reasonable developers would diverge here. One follows the §9 preamble (validate the composite after any Invoice update, roll back on failure) and rejects (a) and (b), because the stored FullyPaid / is-fulfilled values now break REQ-CF-9.1 / REQ-CF-4.9. The other re-derives and accepts. The code takes the second path. InstanceOrchestration.fs preConstructInvoiceComposite (around line 565) re-derives payment state from the *updated* invoice. updateInstanceComposite (line 672) re-derives is-fulfilled. cancelInvoice (lines 836-870) re-derives is-fulfilled, with the comment "since a cancelled Invoice no longer holds the Instance open". None of these three behaviours is stated by a REQ. Every test citing REQ-CF-9.10 (DerivedStateRules.fs, InstanceDataStates.fs, Cancellation.fs, DerivedInvoiceState.fs, MaintenanceOperations.fs:674) exercises a Payment operation or Instance creation. No test pins re-derivation after an Invoice amount update, after an Invoice is added to an already-fulfilled Instance, or after a lone Invoice cancellation flips is-fulfilled to true.

**Action:** Amend REQ-CF-9.10 so that its trigger is any operation that changes an Invoice's Payments, an Invoice's amount, an Instance's set of Invoices, or an Invoice's cancellation, rather than only Payment operations. Then cite tests for the amount-update, add-Invoice-to-fulfilled-Instance and cancel-Invoice cases.

**Why:** State derived from a value is only reliable if every change to that value triggers re-derivation, as the requirement's own Why says. Today the code is more complete than the spec. A future change could narrow the code back to the letter of REQ-CF-9.10 without breaking any test, and the Saturday views (bills to chase, open Instances) would then read stale FullyPaid / fulfilled values.

---

## CF-CANCEL-1 — ambiguity
- **Location:** Specs/Behavioral/CashFlow.md REQ-CF-4.12 (line 146), REQ-CF-5.19, REQ-SYS-6.1
- **Summary:** REQ-CF-4.12 says that cancelling an Instance cancels every Invoice it holds, 'each carrying the Instance's cancellation reason note'. It does not say what happens to an Invoice that is already cancelled.
- **Resolution:** fix-spec

REQ-CF-4.12: "Cancelling an Instance also cancels every Invoice it holds, each carrying the Instance's cancellation reason note." An Instance can hold an Invoice that was already cancelled on its own (REQ-CF-5.17, REQ-CF-14.10). The rules point three ways:
- The literal text of 4.12 says to overwrite that Invoice's note with the Instance's note.
- REQ-CF-5.19 says cancellation is terminal and a cancelled Invoice "cannot be updated", which suggests leaving its note alone.
- REQ-SYS-6.1 forbids silent no-ops, which suggests rejecting the Instance cancellation outright because one of its Invoice cancellations would change nothing.
These are three different observable behaviours. The code picked one in silence. InstanceOrchestration.fs:799-800 documents "An Invoice already cancelled keeps its own note", and lines 826-828 filter out cancelled Invoices before cancelling the rest. The two REQ-CF-4.12 tests (Cancellation.fs:337, :350) do not cover an Instance that holds an already-cancelled Invoice.

**Action:** Amend REQ-CF-4.12 to say that an Invoice already cancelled keeps its own cancellation reason note and is not cancelled again (an exception to REQ-SYS-6.1, stated explicitly as REQ-SYS-6.1.1 requires). Then cite a test for an Instance cancellation where one Invoice was already cancelled.

**Why:** The cancellation reason note exists so that the reason is kept (REQ-CF-4.11 Why). Whether a specific Invoice's reason survives a later Instance cancellation is observable data. Today the spec text and the code disagree on it, and no test notices.

---

## CF-DET-1 — contradiction
- **Location:** Specs/Behavioral/CashFlow.md REQ-CF-8.5 (line 236) vs Waived table rows REQ-CF-7.13 and REQ-CF-10.6 (lines 392-393)
- **Summary:** REQ-CF-8.5 (the projection is deterministic) is treated as tested. Its twin requirements REQ-CF-7.13 and REQ-CF-10.6 are waived on the stated ground that determinism cannot be observed and a repeat-and-compare test only catches the visible failure. So the same kind of requirement is in two different states for the same reason.
- **Resolution:** dan-decides

The 7.13 waiver reads: "'Deterministic' is not observable: nothing separates it from an operation that happens to give the same answer twice. The visible failure is caught by the repeat-and-compare tests of REQ-CF-7.12." REQ-CF-10.6 is waived "As REQ-CF-7.13". REQ-CF-8.5 makes the same claim about the projection ("a deterministic `[DET]` operation. It performs arithmetic only and makes no judgment calls"), yet it does not appear in the Waived table. It is cited by ProjectionRules.fs:380, "REQ-CF-8.5 two projections with the same horizon over unchanged data give identical results, including the order of accounts, Invoices and bills to chase". That is exactly the repeat-and-compare test the 7.13 waiver says cannot verify determinism. The three-state rule holds formally (8.5 is cited by a test), but the classification is inconsistent: either the waiver reasoning is right and the 8.5 test is credited with verifying something it cannot verify, or a repeat-and-compare test does verify a [DET] requirement and 7.13/10.6 could be tested the same way instead of waived.

**Action:** Dan picks one treatment for all three [DET] requirements. Either waive REQ-CF-8.5 with the 7.13 reason (and re-cite the ProjectionRules.fs:380 test to whatever it really verifies, e.g. ordering stability), or move REQ-CF-7.13 and REQ-CF-10.6 to tested by citing their existing repeat-and-compare tests.

**Why:** A waiver reason is a claim about what a test can prove. If the same claim is accepted for two requirements and ignored for a third identical one, the Waived table stops being a reliable account of what has actually been verified.

---

## CF-EXTINV-1 — insufficient-elaboration
- **Location:** Specs/Behavioral/CashFlow.md §5 (REQ-CF-5.1 to 5.19) and REQ-CF-14.5 (line 352)
- **Summary:** REQ-CF-14.5 lets the operator update an Invoice's 'external invoice ID', but no §5 data-state requirement defines that field: whether it is nullable, what its length limit is, or that a whitespace-only value is rejected.
- **Resolution:** fix-spec

The only mention of the field in the spec is REQ-CF-14.5: "to update an Invoice's external invoice ID, invoice date, due date, amount, ...". §5 gives data states for every other Invoice text field: memo (REQ-CF-5.15: nullable, at most 2000, not whitespace), blocker note (REQ-CF-5.14: at most 500), cancellation note (REQ-CF-5.17: at most 500). The external invoice ID has no entry, so the spec uses a field it never defines. The repo does enforce rules: DbMigration/Scripts/202609071125-CreateCashFlowTables.sql:111 declares `external_invoice_id character varying(100)` as nullable, and CashFlowComponent.fs:211-223 (ExternalInvoiceId.create, maxLength = 100) rejects an empty or whitespace-only value and anything over 100 characters with CashflowExternalInvoiceIdIsEmpty / CashflowExternalInvoiceIdTooLong. A developer working from the spec alone could pick any limit, or none. Whitespace rejection is covered globally by REQ-SYS-1.3; the 100-character limit is not covered by any requirement. The coverage-model pass in this same audit (Audit/2026-10-04a/10-coverage-model.md:44-49) independently reached the same conclusion from the code side. Raised here as a spec-quality defect in CashFlow.md itself.

**Action:** Add a §5 REQ: the external invoice ID may be null; when non-null it cannot be whitespace only (post-trim, per REQ-SYS-1.1) and cannot exceed 100 characters. Then cite tests for both rejection branches.

**Why:** The limits of a field the operator can set are observable behaviour: a 101-character ID is rejected. In this spec that behaviour exists only in code and DDL, against the authority hierarchy. Every sibling Invoice text field has its limits stated in a REQ.

---

