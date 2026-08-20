# JournalEntryCrud Auditor

## CON-JE-1 — contradiction
- **Location:** Specs/Behavioral/JournalEntryCrud.md, REQ-JE-4.2 vs REQ-JE-4.9 and REQ-JE-4.10
- **Summary:** REQ-JE-4.2's "only" clause excludes the external-reference operations that REQ-JE-4.9 and REQ-JE-4.10 explicitly permit.
- **Resolution:** fix-spec

REQ-JE-4.2 (line 111) states: "The only changes permitted after posting are: (a) voiding the entry, which sets its void marker and excludes its lines from all balance computations; and (b) attaching or amending explanatory comments via the comment record." The word "only" creates a closed enumeration: nothing outside (a) and (b) is a permitted post-posting change. REQ-JE-4.9 (line 118) then says: "The system must provide a means for an actor to update a journal entry reference's FI and value. The FI and value may be updated regardless of whether the entry is voided or its fiscal period is closed." REQ-JE-4.10 (line 119) says: "The system must provide a means to attach a new external reference to an existing journal entry... A reference may be appended regardless of whether the entry is voided or its fiscal period is closed." Both 4.9 and 4.10 permit a third category of post-posting change (external reference modification and attachment) that the "only" in 4.2 explicitly excludes. External references are part of the composite JournalEntry type (per REQ-JE-3.1), so modifying them is a change to the entry. The design note at the top characterizes external references as "audit traceability only," but 4.2 does not qualify "changes" -- it is a blanket statement.

**Action:** Amend REQ-JE-4.2's enumeration to include external reference operations as a third permitted category, e.g.: "(c) attaching new external references or amending existing ones (REQ-JE-4.9, REQ-JE-4.10)." Alternatively, soften the "only" to language that does not create a closed list, such as "The principal changes permitted after posting are..." though the explicit enumeration is preferable for spec clarity.

**Why:** A developer reading REQ-JE-4.2 in isolation would conclude that external reference operations are prohibited after posting, since 4.2 says those are the "only" permitted changes. This directly contradicts 4.9 and 4.10 in the same section. In a spec-driven codebase where requirements are the source of truth for test development, this contradiction could lead a test author to write a test asserting that external reference updates on posted entries are rejected -- the exact opposite of the intended behavior.

---
