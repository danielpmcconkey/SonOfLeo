# requirements-quality-auditor:DataIngestion.md

## STG-CON-POSTED-NOOP-1 — contradiction
- **Location:** Specs/Behavioral/DataIngestion.md REQ-STG-6.3.2 (line 223) vs REQ-STG-6.6 (line 229) and REQ-STG-4.8 (line 189); REQ-STG-4.6 first bullet (line 184)
- **Summary:** REQ-STG-6.3.2 says a manual update that sets only the status to the entry's current status always writes nothing and succeeds. REQ-STG-6.6 says a 'Posted' entry cannot be modified by the manual update in any way, and REQ-STG-4.8 says the manual update must reject a move to 'Posted'. So the spec gives two answers for a status-only update of 'Posted' on an entry that is already 'Posted'.
- **Resolution:** fix-spec

REQ-STG-6.3.2: "Setting only the status to the entry's current status is the exception stated under REQ-STG-4.6: it writes nothing and succeeds." There is no carve-out for any status. REQ-STG-4.6's bullet backs it up: "Setting a staged entry to the status it already has is not a transition: nothing is written and no audit record is created." Under that reading, Posted to Posted is not a transition and not a modification, so it succeeds.

REQ-STG-6.6: "A staged entry whose status is 'Posted' cannot be modified by the manual update in any way." REQ-STG-4.8: the manual update "must reject" a move to 'Posted'. Under these, the request is rejected.

The code takes the second reading. StageEntryOrchestration.fs lines 624-625 reject every manual update on a Posted entry (IngestionPostedStageEntryCannotBeModified), and lines 639-642 reject any `SetTo Posted` (IngestionManualUpdateCannotSetStatus). The REQ-STG-6.3.2 success test (Tests/Tests.Integrated/CrossDomainOrchestration/StagingIngestionRules.fs line 802) and the REQ-STG-6.6 test (StageEntryUpdate.fs line 705) never try this case, so neither reading is pinned. A developer who follows REQ-STG-6.3.2 and REQ-STG-6.1.1's explicit-exception rule would make the request succeed. A developer who follows REQ-STG-6.6 would reject it. The caller sees success in one build and a typed error in the other.

**Action:** Qualify REQ-STG-6.3.2's exception, for example: 'except on a 'Posted' entry, which rejects every manual update (REQ-STG-6.6)'. Or, if the no-op should succeed even on 'Posted', say so in REQ-STG-6.6 and REQ-STG-4.8 and change the code. Either way, add a test that pins the chosen outcome.

**Why:** REQ-SYS-6.1.1 requires every idempotent exception to be stated explicitly. Here one requirement states the exception with no qualifier and two others forbid it for one status. A caller re-sending the status of a posted entry gets a result that depends on which requirement the implementer read first, and no current test would catch a change from one to the other.

---

## STG-ELAB-PAIRING-MEMO-1 — insufficient-elaboration
- **Location:** Specs/Behavioral/DataIngestion.md REQ-STG-9.10 (line 285); related REQ-STG-9.4 (line 277), REQ-STG-6.4 (line 225), CashFlow REQ-CF-10.2/10.3
- **Summary:** REQ-STG-9.10 pairs staged lines to journal entry lines only by account, line type and amount. When two staged lines in one entry have the same account, line type and amount but different memos, either pairing meets the requirement, so a staged line can end up recording the journal entry line built from its sibling.
- **Resolution:** dan-decides

REQ-STG-9.10: "Staged lines are paired to journal entry lines by account, line type and amount, not by position; each journal entry line pairs with exactly one staged line." REQ-STG-9.4 builds each journal entry line from the staged line's account, amount, line_type and memo, so the memo is the one field that tells such lines apart, and the pairing key leaves it out.

The case is realistic. REQ-STG-6.4's own Why is a split tenant payment, and a parser or an operator split can produce two legs with the same account and amount (two tenants' equal rent to one revenue account, two equal escrow components) that differ only in memo. Each can then be linked to a different Payment Agreement (CashFlow §12) and matched to a different Payment.

The code follows the spec as written. StageEntryOrchestration.fs lines 732-765 (isSameLine, pairStageLinesToJournalEntryLines) compare account, amount and line type only and take the first unpaired journal entry line that matches. So staged line A (memo 'Smith rent') can record the journal entry line carrying memo 'Jones rent'. The payments-to-posted transition (REQ-CF-10.2/10.3) then moves A's Payment to that line, and the REQ-STG-10.2 journal-entry-line filter traces the ledger line back to the wrong staged line. One developer would add memo to the key, since that is what the line was built from. Another, reading 9.10 literally, would not. The pairings that result are different and both can be seen.

**Action:** Amend REQ-STG-9.10 to pair on account, line type, amount and memo (null memo matching null). Or state explicitly that lines identical on account, line type and amount may pair either way, and that Payments and traces may cross between them.

**Why:** 9.10 exists so the payment-to-posted transition can follow a staged line to the ledger line it produced (scope exclusion 2). A key that cannot tell apart two lines the parser or operator deliberately kept separate can silently swap the provenance of the very split legs REQ-STG-6.4 was written to support.

---


