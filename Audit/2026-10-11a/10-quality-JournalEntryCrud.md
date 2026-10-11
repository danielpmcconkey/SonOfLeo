# requirements-quality-auditor:JournalEntryCrud.md

## JE-CONTRA-2 — contradiction
- **Location:** Specs/Behavioral/JournalEntryCrud.md line 13 (Design note — voiding and reversal) vs REQ-JE-4.2 (line 113), REQ-JE-4.9 (line 120), REQ-JE-4.10 (line 121), REQ-JE-5.5 (line 135)
- **Summary:** The voiding design note says a voided entry 'is never edited', but REQ-JE-4.2 says a posted entry is not immutable and forbids any spec from calling it immutable, and REQ-JE-4.9, 4.10 and 5.5 let a voided entry's references and comments be added to and changed.
- **Resolution:** fix-spec

Line 13 says: 'Voiding is a soft-delete marker: a voided entry remains in the ledger (it is never edited or hard-deleted) but is excluded from all balance computations.' REQ-JE-4.2 says: 'A posted journal entry is not immutable. The only changes permitted after posting are: (a) voiding ... (b) attaching or amending explanatory comments ... (c) attaching new external references or updating existing ones ... no spec, requirement, or tooling may characterize journal entries as immutable or append-only.' Several requirements allow changes after a void. REQ-JE-4.9 lets a reference's FI and value be updated 'regardless of whether the entry is voided'. REQ-JE-4.10 lets a new reference be attached to a voided entry. REQ-JE-5.5 lets a comment be appended to a voided entry. The void itself also writes the header: the header update in JournalEntryVoiding.fs sets voided_at and modified_at. So a voided entry is edited, both by the void and afterwards. The intended meaning is probably 'its posted fields (REQ-JE-4.1) are never edited'. As written, though, the note says the same thing REQ-JE-4.2 forbids, in the same spec. That is the reading an LLM agent picks up when it decides whether a voided entry can still take a reference correction.

**Action:** Reword the parenthetical on line 13 to '(its posted fields, REQ-JE-4.1, are never edited, and it is never hard-deleted)', or cut 'never edited' and point to REQ-JE-4.2.

**Why:** REQ-JE-4.2 forbids calling journal entries immutable because readers would otherwise refuse legitimate post-void changes. A design note in the same spec that says 'never edited' brings back exactly that reading.

---

## JE-AMB-2 — ambiguity
- **Location:** Specs/Behavioral/JournalEntryCrud.md REQ-JE-3.5 (line 98)
- **Summary:** REQ-JE-3.5 does not say whether a 'matching' source FI or reference value means an exact, case-sensitive match or a partial or case-insensitive one, although the comparable filters elsewhere in the specs state this.
- **Resolution:** dan-decides

REQ-JE-3.5: 'retrieve the journal entries carrying a matching external reference, by the caller providing a source FI, a reference value, or both. When both are provided, a single external reference must match both.' The requirement never defines 'matching'. Other specs state it explicitly for their text filters. REQ-AC-3.12.1 says 'journal entry source (exact match)' and 'journal entry description (case-sensitive partial match)'. REQ-SYS-1.4 governs partial-match filters. Developers could reasonably differ here. An FI lookup is the sort of query where 'Ally' might be expected to find 'Ally Bank', and FI strings are free text from outside the system (REQ-JE-1.43 withdrawn: no controlled vocabulary). One developer would build exact equality; another would build a case-insensitive or contains match. The user would see different results. The current code (JournalEntryOrchestration.fs, fetchHeadersFromFilter: 'jer.financial_institution = @financial_institution', 'jer.reference = @reference') does exact, case-sensitive equality. The spec does not pin that down, so a refactor to ILIKE or contains would violate nothing written.

**Action:** Append to REQ-JE-3.5: 'Source FI and reference value each match exactly (case-sensitive equality), not partially.' Or state the partial-match rule Dan prefers, with REQ-SYS-1.4 applying.

**Why:** Every other text filter in the specs says whether it is exact or partial and whether it is case-sensitive. When the spec leaves it open, the result set depends on whoever implemented the filter, and a test would pin behavior that no requirement states.

---

## JE-AMB-3 — ambiguity
- **Location:** Specs/Behavioral/JournalEntryCrud.md REQ-JE-5.3 (line 133), REQ-JE-1.56 (line 69) vs REQ-JE-4.9 (line 120), REQ-JE-4.10 (line 121), REQ-JE-5.5 (line 135), REQ-JE-4.5 (line 116)
- **Summary:** The spec says outright that adding a comment and adding or updating an external reference are allowed on a voided entry or in a closed period, but says nothing either way about amending a comment's text (REQ-JE-5.3) or re-pointing its secondary entry (REQ-JE-1.56) in those cases.
- **Resolution:** fix-spec

Three sibling requirements state explicitly that the void state and the closed period do not gate them. REQ-JE-5.5 says a comment may be appended 'even if the JE is voided or if the JE's fiscal period is closed'. REQ-JE-4.10 says a reference may be appended 'regardless of whether the entry is voided or its fiscal period is closed'. REQ-JE-4.9 says a reference's FI and value 'may be updated regardless of whether the entry is voided or its fiscal period is closed (mirrors REQ-JE-4.10 and REQ-JE-5.5)'. The void operation, by contrast, is gated on an open period (REQ-JE-4.5). REQ-JE-5.3 (amend comment text) and REQ-JE-1.56 (re-point the secondary entry) carry no such statement. A developer who reads the explicit permission on the three siblings as meaningful may conclude that its absence on amendments means amendments are gated the way voiding is. Another will read REQ-JE-4.2(b), 'attaching or amending explanatory comments', as ungated. The difference is observable: amending a comment in a closed period would succeed or be refused. The current code (JournalEntryCommentOrchestration.updateComment) applies no void or period check, so it follows the ungated reading. Nothing in the spec says so, and nothing would stop a later change from adding a gate.

**Action:** Append to REQ-JE-5.3: 'A comment's text and secondary entry may be amended regardless of whether its primary entry is voided or that entry's fiscal period is closed (mirrors REQ-JE-4.9 and REQ-JE-5.5).'

**Why:** When a spec states the void and period exemption on three of its four post-posting changes, a reader treats the missing statement on the fourth as a difference. The fix is one sentence, and it matches what the code does now.

---

## JE-STALE-3 — stale-reference
- **Location:** Specs/Behavioral/JournalEntryCrud.md Waived table, REQ-JE-1.14 row (line 164)
- **Summary:** The REQ-JE-1.14 waiver reason names a code identifier (`voidedAt` is `Instant option` on the header type). The specs README forbids naming code in a spec, and the previous audit rewrote three sibling waiver rows in this table for the same reason.
- **Resolution:** fix-spec
- **Prior ruling:** Not a re-raise. It is the same class of defect as audit 2026-10-04a #158 (REQ-JE-2.14 row) and 2026-10-03a #262 (REQ-JE-1.23/1.51 rows). Both were accepted and fixed, but neither covered the REQ-JE-1.14 row.

Line 164: 'Enforced by the type definition — `voidedAt` is `Instant option` on the header type. Void behavior tested under REQ-JE-4.3/4.7'. Specs/README.md, 'Linkage rules (the star chart)', says: 'Spec documents **never** name source files, functions, or tests.' A requirement is written in operator concepts, 'never how the code is structured'. `voidedAt` is the F# field and accessor name (JournalEntryHeader.voidedAt, used in JournalEntryVoiding.fs, BalanceSheetIntegrity.fs and CashFlowOps.fs). It is not the column or concept name, which the spec writes as `voided_at` and 'void marker'. Audit 2026-10-04a #158 rewrote the REQ-JE-2.14 row to drop its file and type names. Audit 2026-10-03a #262 restated the REQ-JE-1.23 and REQ-JE-1.51 rows 'in type and operator terms rather than code names'. The REQ-JE-1.14 row was not included in either fix and still names code. The claim is true today, but if the field is renamed it goes stale without anyone noticing.

**Action:** Restate the REQ-JE-1.14 waiver reason without the identifier, for example: 'Enforced by the type definition — the void marker is an optional Instant on the journal entry header, so no other state can be represented. Void behavior tested under REQ-JE-4.3/4.7.'

**Why:** The star-chart rule exists because coordinates written in prose cannot be checked mechanically and go stale silently. This is the last code identifier in this spec's waiver table after the two earlier rewrites.

---

