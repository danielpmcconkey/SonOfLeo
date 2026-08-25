# JournalEntryCrud Spec Auditor

## EGAP-JE-1 — enforcement-gap
- **Location:** Specs/Behavioral/JournalEntryCrud.md, Section 5 (REQ-JE-5.3, REQ-JE-5.7, REQ-JE-1.56)
- **Summary:** Section 5 lacks an explicit behavioral mandate for updating a comment's secondary journal entry link, even though REQ-JE-5.7 assumes this capability exists.
- **Resolution:** fix-spec

REQ-JE-5.7 defines the no-op rejection rule for comment updates using the phrase 'text and secondary journal entry both NoChange,' establishing that the comment update operation has two mutable fields. However, section 5 only provides an explicit 'must provide a means' behavioral mandate for one of them: REQ-JE-5.3 mandates 'a means to amend a comment's text.' No section 5 requirement mandates providing the means to update the secondary journal entry link.

REQ-JE-1.56 (section 1, data states) says 'Comment secondary journal entry ID may be updated to be pointed at a different JE or to no JE,' which authorizes the mutation as a legal data state. REQ-JE-4.2(b) lists 'amending explanatory comments' as a permitted post-posting change. But neither is a behavioral mandate — they describe what's legal, not what the system must provide.

Every other update operation in the spec follows the pattern of an explicit behavioral mandate: REQ-JE-4.3 (void), REQ-JE-4.9 (ext ref update), REQ-JE-4.10 (ext ref append), REQ-JE-5.1 (comment creation), REQ-JE-5.3 (text amendment). The secondary JE update is the sole exception.

The code does implement this capability (JournalEntryCommentOrchestration.updateComment handles both fields, and the JournalEntryUpdateCommentInput contract carries both secondaryJournalEntryId and commentText as FieldUpdate values). The test suite verifies it under two tests named 'REQ-JE-1.56 updateComment repoints the secondary JE link...' and 'REQ-JE-1.56 updateComment clears the secondary JE link...' — citing the data-state requirement because no behavioral requirement exists to cite.

**Action:** Add a REQ-JE-5.x requirement parallel to REQ-JE-5.3 that explicitly mandates the ability to update a comment's secondary journal entry link (e.g., 'The system must provide a means to update a comment's secondary journal entry link, repointing it at a different journal entry or clearing it to null. Updating updates the modified-at timestamp (per REQ-SYS-3.3).'). Alternatively, broaden REQ-JE-5.3 to cover all mutable comment fields rather than text alone.

**Why:** A developer implementing from section 5 would build text-only comment updates from REQ-JE-5.3, then discover the secondary JE mutable field only when implementing REQ-JE-5.7's no-op check — a backwards dependency where error handling reveals an undocumented capability. The spec's own pattern (one explicit 'must provide a means' per update operation) is broken for this single case. The test attribution to a data-state requirement (1.56) rather than a behavioral requirement is evidence of the gap.

---
