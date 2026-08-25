# SystemWide Test Efficacy Auditor

## STALE-SYS-1 — stale-reference
- **Location:** Tests/Tests.Isolated/Model/Ledger/JournalEntryComponent.fs lines 153, 158; REQ-SYS-1.2, REQ-SYS-1.3, REQ-JE-1.26
- **Summary:** Two LineMemo tests cite REQ-SYS-1.2 (required text) but LineMemo is an optional field, so the applicable system-wide requirement is REQ-SYS-1.3.
- **Resolution:** fix-test

The tests `REQ-JE-1.27 REQ-SYS-1.2 LineMemo.create rejects empty string` (line 153) and `REQ-JE-1.27 REQ-SYS-1.2 LineMemo.create rejects whitespace-only string` (line 158) cite REQ-SYS-1.2, which applies to required (non-nullable) text fields. However, REQ-JE-1.26 in JournalEntryCrud.md explicitly declares LineMemo optional: "Journal entry line memo is optional (nullable)." REQ-JE-1.27 itself says "When provided" -- the language of optional fields. The system-wide requirement for optional text fields rejecting empty/whitespace when provided is REQ-SYS-1.3, not REQ-SYS-1.2. The test bodies are correct (they verify the right rejection behavior); only the REQ-SYS citation is wrong. The traceability audit (traceability-audit.sh) counts this as REQ-SYS-1.2 coverage, inflating that requirement's test count by two while contributing nothing to REQ-SYS-1.3 traceability. REQ-SYS-1.2 retains genuine coverage from AccountCode, AccountName, Description, and Source tests. REQ-SYS-1.3 retains coverage from the two AccountExternalReference tests. Neither requirement loses actual behavioral coverage, but the citation is factually incorrect per the field's own entity spec.

**Action:** Rename both tests to cite REQ-SYS-1.3 instead of REQ-SYS-1.2.

**Why:** Test names are claims of coverage. The Tests README says an ID that no test in the file backs is forbidden because the traceability audit reads it as coverage. Here the test backs REQ-SYS-1.3 behavior but claims REQ-SYS-1.2, creating an incorrect traceability record.

---
