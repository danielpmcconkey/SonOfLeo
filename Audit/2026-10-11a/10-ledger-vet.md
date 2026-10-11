# resolved-findings-staleness-auditor

## STALE-AMB-6 — stale-ruling
- **Location:** Skills/SonOfLeoRequirementsAudit/resolved-findings.md lines 42-46 (AMB-6, REQ-SYS-5.1) vs lines 225-229 (DECIMAL-SCALE)
- **Summary:** AMB-6 says every read returns a 'byte-perfect' entity, but the later DECIMAL-SCALE ruling accepts that persistence changes a decimal's scale, so the two rulings contradict each other.
- **Resolution:** dan-decides

AMB-6 (2026-06-13) says: 'Any application of the system that writes a value to the database must do so such that any subsequent read returns a byte-perfect entity.' DECIMAL-SCALE (2026-10-03/04) says: 'persistence normalizes to the column's scale (two places for Money, six for Quantity and Price)' and holds that this is fine because 'Value equality holds.' In the current code that normalization is reachable and is not byte-identical. In Src/Business.FinancialServices/Money.fs lines 21-30, fromDecimal(3.990M) compares Math.Round(3.990M, 2) = 3.99M against raw. Decimal comparison ignores scale, so the check passes and it returns `Ok(create raw)` with amount 3.990M (scale 3). Every Money column is numeric(12,2) (e.g. DbMigration/Scripts/202609071105-CreateLedgerTables.sql line 98, journal_entry_line.amount), so the value reads back as 3.99M (scale 2). That decimal has different bits (Decimal.GetBits) from what was written. Taken literally, AMB-6 makes this a REQ-SYS-5.1 violation; DECIMAL-SCALE says not to flag it. REQ-SYS-5.1's own text (Specs/Behavioral/SystemWide.md line 44, 'perfectly reconstituted') has not changed. The problem is the extra word 'byte-perfect' in AMB-6's reasoning, which a later ruling has partly overtaken.

**Action:** Keep AMB-6, but rewrite the second sentence of its Ruling to: 'Any application of the system that writes a value to the database must do so such that any subsequent read returns an entity equal in value to the one written. Decimal scale (trailing zeros) is representation, not value: see DECIMAL-SCALE. The temporal precision and money 2dp constraints are deliberately planned for and coded against — they aren't edge cases, they're the design. "Perfectly" is accurate.'

**Why:** Two active precedents that disagree let an auditor cite whichever one supports the finding they want. AMB-6's 'byte-perfect' could be used to re-raise the decimal-scale finding that Dan has overruled in three audit rounds (2026-10-03a #111; 2026-10-04a #064, #076, #174). It could also lead a test writer to assert byte-level equality where value equality is the contract.

---

## STALE-IE-2 — stale-ruling
- **Location:** Skills/SonOfLeoRequirementsAudit/resolved-findings.md lines 75-79 (IE-2, REQ-DAL-3.6); Specs/Behavioral/DataAccessLayer.md line 81 (Unenforceable table)
- **Summary:** IE-2's Scope covers every REQ ('Whether REQs can contain non-assertable language'), while the specific case it ruled on (REQ-DAL-3.6) is now handled by the spec's own Unenforceable table.
- **Resolution:** dan-decides

IE-2 is titled for REQ-DAL-3.6, but its Scope is written for all requirements: 'Whether REQs can contain non-assertable language'. Its Ruling ('As long as the language doesn't create ambiguity or encourage test writers to write bullshit tests, additional elaboration is fine') does not limit itself to elaboration added to an otherwise testable REQ. Since 2026-08-02 the specs classify each REQ explicitly: REQ-DAL-3.6 is in DataAccessLayer.md's Unenforceable table ('Policy statement ... Binds database administrators and developers, not code', Dan, 2026-08-02). Every behavioral spec now has Waived-from-testing and Unenforceable sections. The SS-3 note in the same ledger (line 86) restates the current standard: an active REQ is 'tested, waived in the spec's Waived table, or unenforceable; if it is none of these, flag it.' As written, IE-2's Scope lets an auditor skip a REQ whose substance is non-assertable and which appears in none of those tables, calling it the 'non-assertable language' IE-2 allowed. That is a test-gap or enforcement-gap finding Dan did not rule on: IE-2 was about advisory prose attached to a policy REQ.

**Action:** Rewrite IE-2's Scope to: 'Whether a REQ may carry explanatory or advisory prose (e.g. REQ-DAL-3.6's note to DBAs) alongside its requirement.' Add to the Ruling: 'This does not exempt a REQ from being tested, listed in its spec's Waived table, or listed in its Unenforceable table; a REQ whose substance is non-assertable and appears in neither table is still a finding.' Otherwise retire IE-2 as absorbed by the Unenforceable-table classification of REQ-DAL-3.6 (2026-08-02).

**Why:** A precedent ledger is a suppression list, so a ruling scoped as a general principle silently suppresses every finding that matches it. When the Scope is wider than the question Dan actually decided, the ledger overrides his later framework (the tested / waived / unenforceable classification) without anyone noticing.

---

## STALE-JE-COMPOSITE-ORDER — stale-ruling
- **Location:** Skills/SonOfLeoRequirementsAudit/resolved-findings.md lines 169-173 (JE-COMPOSITE-ORDER); Specs/Behavioral/JournalEntryCrud.md line 77 (REQ-JE-2.4); Src/Ui.InterfaceBridge/BoundaryConverters/JournalEntryFieldConverters.fs line 36; Src/Business.CrossDomainOrchestration/JournalEntryOrchestration.fs lines 130-150
- **Summary:** JE-COMPOSITE-ORDER's reasoning names 'account existence' as line validity that can only be known after a write, but REQ-JE-2.4 requires a missing account to be rejected before any database write, and the code does that.
- **Resolution:** dan-decides

The ruling says: 'a line's validity depends on DB state (account existence, period state)', and on that basis composite checks run after each line has been 'constructed and persisted'. REQ-JE-2.4 now reads: 'When posting a journal entry, the system must reject any line whose account code does not resolve to an existing account (before any database write).' The code meets REQ-JE-2.4 at the boundary, not inside the per-line persistence. JournalEntryRoutes.postNew converts every line through ``convert JournalEntryLineInput to JournalEntryLinePrimitives``, which calls ``convert AccountCodeString to Id`` (JournalEntryFieldConverters.fs line 36). That happens before JournalEntryOrchestration.constructNewAndPersist writes the header. So account existence is not post-write line validity, and the ruling's reasoning contradicts a current REQ. The decision itself (minimum-2-lines and debit=credit composite checks may run after component writes inside the transaction) is unaffected and still matches the code: confirmLineList runs after createValidLines at line 145. The 'transaction bracket' the ruling relies on is now written down as REQ-SYS-8.1 and REQ-JE-2.12, and the ruling does not cite either. Note: I could not date REQ-JE-2.4's '(before any database write)' clause because git history is shallow (81 commits, earliest 2026-10-04). It may always have been there, in which case this is an error in the ruling's reasoning rather than staleness. Either way the fix is the same.

**Action:** Keep the ruling and rewrite the reasoning sentence to: 'It is impossible to validate that we have 2+ *valid* lines until each line has been constructed and persisted — a line's validity depends on DB state at the entry date (account active-at-entry-date per REQ-JE-2.8, period state per REQ-JE-2.6/2.7). Account existence (REQ-JE-2.4) is resolved at the boundary before any write and is not covered by this ruling.' Then replace 'The transaction bracket ensures atomicity' with 'REQ-SYS-8.1 and REQ-JE-2.12 ensure atomicity'.

**Why:** While the ruling says account existence can only be checked after a write, an auditor who finds a future regression (code-to-ID resolution moved after the header insert, violating REQ-JE-2.4) could dismiss it as covered by JE-COMPOSITE-ORDER, a ruling marked 'Vetoed repeatedly. Do not re-flag.' Grounding the ruling in the current REQs keeps the veto exactly as wide as Dan intended.

---

