# code-truthfulness-auditor

## CONTRADICTION-COMMENT-NOOP-1 — contradiction
- **Location:** /media/dan/fdrive/codeprojects/SonOfLeo/Src/ModelOrchestrator/JournalEntryCommentOrchestration.fs, lines 90-111; REQ-SYS-6.1
- **Summary:** JournalEntryCommentOrchestration.updateComment lacks the empty-update no-op guard that Account.updateDb and ExtRef.updateFiAndReferenceText both enforce, allowing a vacuous update to silently mutate modified_at without changing any business field.
- **Resolution:** fix-code

Account.updateDb (Account.fs line 269) guards `if updates.IsEmpty then Error(AccountUpdateNoOp)`. JournalEntryExternalReferenceOrchestration.updateFiAndReferenceText (line 79) guards `if updates.IsEmpty then Error(JournalEntryReferenceUpdateNoOp)`. JournalEntryCommentOrchestration.updateComment builds its `updates` list from two FieldUpdate arguments (commentUpdate, secondaryIdUpdate) at lines 90-101 but never checks whether `updates` is empty before executing the UPDATE query at line 111. When both arguments are NoChange, the generated SQL is `UPDATE ledger.journal_entry_comment SET modified_at = @modified WHERE unique_id = @unique_id;` — a write that changes only the audit timestamp with no business-field mutation. REQ-SYS-6.1 states: 'No state-transition operation may silently succeed as a no-op. When a requested operation would change nothing... the operation must produce an error rather than update or insert nothing. A silent no-op masks a caller that believes the system is in a different state than it is, hiding an upstream problem the system should surface.' The comment update violates this by not rejecting the empty-update case.

**Action:** Add `do! if updates.IsEmpty then Error(JournalEntryCommentUpdateNoOp) else Ok()` (with a corresponding AppError case) before the executeNonQuery call in updateComment, matching the pattern in Account.updateDb and ExtRef.updateFiAndReferenceText.

**Why:** REQ-SYS-6.1 is a cross-cutting invariant. Two of the three FieldUpdate-based update functions enforce it; the third does not. A caller passing both NoChange gets a spurious modified_at bump with no signal that nothing was actually changed, which is exactly the silent-no-op the requirement exists to prevent.


## CONTRADICTION-JE-COMPOSITE-ORDER-1 — contradiction
- **Location:** /media/dan/fdrive/codeprojects/SonOfLeo/Src/ModelOrchestrator/JournalEntryOrchestration.fs, lines 163-169; REQ-SYS-2.1.1
- **Summary:** JournalEntry composite validation (minimum 2 lines, debit/credit balance) runs after all component DB writes, contradicting REQ-SYS-2.1.1 which requires property-determinable rejections before any database write.
- **Resolution:** fix-code

In JournalEntry.constructNewAndSaveToDb (lines 163-175), the execution order is: (1) createValidHeader persists the header to DB (line 164, via JournalEntryHeaderOrchestration.constructNewAndSaveToDb which calls insertNewToDb), (2) createValidLines persists each line to DB (line 166, via JournalEntryLineOrchestration.constructNewAndSaveToDb which calls insertNewToDb), (3) createValidExternalReferences persists each reference to DB (line 167), (4) createValidComments persists each comment to DB (line 168), and only then (5) confirmLineList checks that there are at least 2 lines and that total debits equal total credits (line 169). Both the line-count check and the balance check are determinable from the input data alone: the caller provides a `lines` list of `(AccountId * Money * JournalEntryLineType * JournalEntryLineMemo option)` tuples, from which count, debit/credit sums, and balance equality can all be computed without any database lookup. REQ-SYS-2.1.1 states: 'Rejections determinable from the entity's own properties must occur before any database write.' The code violates this ordering. While the database transaction ensures atomicity (a failed composite check rolls back all writes), the spec's mandate is about ordering, not just outcome.

**Action:** Add a pre-write composite check at the top of constructNewAndSaveToDb that validates line count >= 2 and debit/credit balance equality from the raw input tuples before entering the persist-each-component sequence. The existing confirmLineList call can remain as a defense-in-depth check after construction.

**Why:** REQ-SYS-2.1.1 is an active requirement (waived from dedicated testing, but active). Its purpose is to avoid unnecessary database round-trips for rejections that can be determined from the entity's own properties. The composite checks (line count, balance) are exactly this kind of rejection: purely input-determinable, no database state needed. The CompoundedLearnings validation-layers article says composite ordering follows domain needs, but per the requirements-stricter-than-conventions audit conduct article, the behavioral requirement narrows that general guidance.

