# GAAP domain expert (ledger, periods, chart of accounts, ingestion-to-ledger)

## GAAP-VOID-1 — enforcement-gap
- **Location:** Specs/Behavioral/JournalEntryCrud.md REQ-JE-4.14; Src/Business.CrossDomainOrchestration/JournalEntryVoiding.fs confirmNoPaymentReferencesEntry; Src/Business.FinancialServices.CashFlow/Payment.fs fetchByJournalEntryLineIdList (line 299-312); Src/Business.CrossDomainOrchestration/CashFlowOps.fs transitionPaymentsToPosted (lines ~885-935); Tests/Tests.Integrated/CrossDomainOrchestration/JournalEntryVoiding.fs:276
- **Summary:** The REQ-JE-4.14 void guard only finds Payments that have already moved to the Posted pointer. A Payment still on its Staged pointer, whose staged line has already posted into the entry, does not block the void, and the next REQ-CF-10 run then points that Payment at the voided line.
- **Resolution:** fix-spec

REQ-JE-4.14 rejects a void 'when any Payment's transaction pointer references a line of the entry being voided'. The code does this in confirmNoPaymentReferencesEntry, which calls Payment.fetchByJournalEntryLineIdList, and that query filters on `pmt.journal_entry_line_id in (...)`. A batch post (StageEntryOrchestration.postStageEntry) records the JE line on the staged line (REQ-STG-9.10). It never touches the Payment. The Payment only gets journal_entry_line_id when the separate Payment-to-posted transition runs (CashFlow §10; CashFlowOps.transitionPaymentsToPosted). Between those two steps the Payment's pointer is Staged(stageLineId), and that staged line records a line of the posted JE. A void in that window passes the guard and succeeds. The next transitionPaymentsToPosted then: (1) selects Payments where journal_entry_line_id is null; (2) reads the staged line's journalEntryLineId, which is the voided entry's line, with no voided check; (3) sets Payment.journal_entry_line_id to it; (4) re-derives the Invoice as posted. That is exactly the state the REQ-JE-4.14 Why forbids: 'the Invoice reading FullyPaid and PostedToLedger on cash the ledger no longer records'. Even if the transition never runs, the Invoice is still counted as paid by a Staged Payment whose cash went into a JE that was voided. The REQ-JE-4.14 Theory test (JournalEntryVoiding.fs:276) only builds Payments whose pointer is already Posted (it asserts `CashFlowComponent.Posted _ -> true`), so this path is untested. The window is real: the batch post and the §10 transition are separate operations (Phase 7 per CashFlow §10 preamble), and Dan's planned state machine runs the Saturday routine as smaller separate pieces.

**Action:** Amend REQ-JE-4.14 to also reject the void when any Payment's staged line records a journal entry line of the entry being voided, whichever pointer the Payment currently shows. Make the void guard check both pointers, have transitionPaymentsToPosted refuse to bind a Payment to a line of a voided entry, and add a REQ-JE-4.14 test case where the Payment is still Staged after the batch post.

**Why:** Under cash basis, a Payment proves cash moved. A void says the ledger no longer records that cash. When an Invoice is marked paid and posted on voided cash, the cash-flow domain and the ledger disagree, and no reconciliation report shows it, because the TB correctly drops the voided lines while the Invoice still shows paid.

---

## GAAP-STMT-1 — statement-delta
- **Location:** Dan's statement (cash flow section); Src/Business.CrossDomainOrchestration/StageEntryOrchestration.fs postStageEntry/post; Specs/Behavioral/CashFlow.md §10 (REQ-CF-10.1-10.3)
- **Summary:** Dan says a Payment 'gains its journal entry line linkage when the staged entry eventually posts'. In the code, posting does not touch Payments; a separate Payment-to-posted transition does it.
- **Resolution:** dan-decides

StageEntryOrchestration.post / postStageEntry write the JE, then record the JE header ID on the staged entry and the JE line ID on each staged line (REQ-STG-9.10). Payments are not touched. The Payment's journal_entry_line_id is set only by CashFlowOps.transitionPaymentsToPosted (REQ-CF-10.1-10.3), which is its own operation. Until it runs, the Payment's pointer stays Staged even though its line is in the ledger. This gap between posting and Payment linkage is what opens the void loophole in GAAP-VOID-1.

**Action:** Dan to update his model: Payment linkage is a separate step (CashFlow §10) that runs after batch post, not part of posting. Then decide whether batch post should run the transition in the same transaction, or whether the two-step design stays and the void guard covers the window (GAAP-VOID-1).

**Why:** This audit is Dan's backstop on a fully agentic codebase, so his model of when ledger-linked state changes must match the code. Otherwise windows like the one in GAAP-VOID-1 go unnoticed.

---

## GAAP-AC-1 — missing-requirement
- **Location:** Specs/Behavioral/AccountCrud.md REQ-AC-4.4, REQ-AC-4.5, REQ-AC-4.6; Specs/Behavioral/JournalEntryCrud.md REQ-JE-2.8, REQ-JE-4.5; Src/Business.CrossDomainOrchestration/AccountDeactivation.fs; Src/Business.CrossDomainOrchestration/JournalEntryVoiding.fs
- **Summary:** The zero-balance rule for deactivation (REQ-AC-4.4) is checked only at the moment of deactivation. Afterwards, a void or a backdated post can leave a deactivated account with a non-zero balance, and there is no way to reactivate it.
- **Resolution:** dan-decides

AccountDeactivation.confirmZeroBalanceBeforeDeactivation checks the balance only when deactivation is requested. After that: (a) REQ-JE-2.8 allows posting to the account with any entry date on or before active_end, as long as that period is open (REQ-JE-2.7). A backdated entry in an open period therefore changes the balance of a deactivated account. (b) Voiding (REQ-JE-4.5; JournalEntryVoiding.voidById) checks only that the voided entry's period is open. It never checks whether that entry's accounts are deactivated, so voiding one side of a zeroing pair leaves a residual balance. The usual remedy, an offsetting entry (REQ-JE-4.8), has to be dated on or before active_end to pass REQ-JE-2.8. If that period has since closed, the only way out is to reopen it. No operation clears active_end: grep finds only one active_end write path, AccountDeactivation.updateActiveEnd, and REQ-AC-4.5 rejects deactivating an already-deactivated account. The TB (REQ-RPT-1.2) and balance-sheet integrity still include the account, so totals stay correct. What is lost is the guarantee that a deactivated account holds no balance, and REQ-AC-3.9 active-only listings hide the account that holds it.

**Action:** Dan to decide one of: (1) a void that would leave a deactivated account with a non-zero balance is rejected; (2) REQ-AC-4.4 is stated as a point-in-time gate only, and a balance on a deactivated account is acceptable and shown somewhere (e.g. integrity report); (3) add a reactivation operation so corrections have a legal target.

**Why:** When an account is retired, its balance should be zero and stay zero. Otherwise money sits on an account that no active-account view shows. Period close and the planned retirement-planning engine both depend on balances being where the chart of accounts says they are.

---

## GAAP-PER-1 — missing-requirement
- **Location:** Specs/Behavioral/DataIngestion.md REQ-STG-8.2, REQ-STG-9.8, REQ-STG-6.2, scope exclusion 5; Specs/Behavioral/FiscalPeriodCrud.md REQ-FP-4.1; Specs/Behavioral/JournalEntryCrud.md REQ-JE-2.7, REQ-JE-4.8
- **Summary:** No spec says what to do with a postable staged entry dated in a closed fiscal period. Under the all-or-nothing batch rule, one such entry blocks the whole Saturday post.
- **Resolution:** dan-decides

FI exports often deliver transactions late: a card charge posts days after month end, or a corrected file re-sends a prior month. Once the operator closes a period (REQ-FP-4.1), any postable staged entry whose entry_date falls in it fails REQ-JE-2.7 at posting. REQ-STG-9.8 then rolls back the whole batch. The REQ-STG-8.2 Why expects shadow post to surface 'closed fiscal period' failures, but no requirement says how to resolve them. REQ-JE-4.8 covers corrections to already-posted entries in closed periods, not late-arriving new data. Scope exclusion 5 says nothing reaches back into staging after posting. The only paths left are workarounds: (a) the operator edits the staged entry_date into an open period via the manual update (REQ-STG-6.2), which makes the JE's date differ from the cash date the FI reported. Staging carries no comment field (scope exclusion 1), so the JE gets no note of the original date at posting. (b) Reopen the closed period (REQ-FP-4.2). (c) Ignore the entry and post it by hand. Each choice has a GAAP consequence: re-dating moves recognition to a different period than the cash date; reopening changes figures that may already have been reported. Dan has not chosen between them.

**Action:** Dan to rule on the policy for late-arriving staged data dated in a closed period. For example: re-date into the earliest open period and keep the FI date (staged line memo or a post-posting comment), or the Saturday routine reopens the period. Then capture the rule as a REQ in DataIngestion.md §9 or FiscalPeriodCrud.md §4.

**Why:** Under cash basis the transaction date is the cash date. Silently re-dating to get through a closed period loses that date, and the planned ML engine needs it. Leaving the choice to an agent operator on Saturday means the ledger's period discipline is decided case by case and goes unrecorded.

---

## GAAP-STG-1 — contradiction
- **Location:** Specs/Behavioral/DataIngestion.md REQ-STG-7.3 (Why), REQ-STG-7.2, scope exclusion 5; Specs/Behavioral/JournalEntryCrud.md REQ-JE-4.13; Src/Business.FinancialServices.DataIngestion/StageEntryHeader.fs fetchDuplicates (lines 338-391)
- **Summary:** The rationale in REQ-STG-7.3 says a voided entry's external reference should not block re-import. Under REQ-JE-4.13 and REQ-STG-7.2, re-importing a staged transaction whose JE was voided is always flagged Duplicate. The rationale also calls a void a reversal of the economic event, which is not what a void is.
- **Resolution:** fix-spec

The REQ-STG-7.3 Why reads: 'Voided entries are excluded because voiding is a soft delete — the economic event the entry recorded has been reversed, so its external reference should not block re-import of the same transaction.' Since 2026-09-27, REQ-JE-4.13 leaves the source staged entry 'Posted' after a void. REQ-STG-7.2 counts every staged entry, Posted included, when choosing the original by ingestion order. fetchDuplicates implements this with `ordinal > 1 OR ail.journal_entry_id is not null`. So re-importing a transaction whose ledger entry was voided is flagged Duplicate through the staging ordinal, whatever 7.3 does. That matches scope exclusion 5 ('a correction... is made with the manual journal entry routes, not by re-posting from staging') and the REQ-STG-4.5 Why, but it contradicts the stated purpose of 7.3. The ledger-side exclusion now changes the outcome only for JEs that never came from staging. Separately, 'the economic event ... has been reversed' mixes up void and reversal. The JournalEntryCrud design note keeps them apart: a void means the entry should not count; a reversal is an ordinary offsetting entry.

**Action:** Reword the REQ-STG-7.3 Why: the ledger-side voided exclusion applies to entries posted outside staging, and re-import of staging-originated transactions is governed by REQ-STG-7.2. Drop the 'economic event has been reversed' wording.

**Why:** Agents implement from the Why text as well as the rule. A rationale saying a voided reference should not block re-import invites a future agent to 'fix' REQ-STG-7.2 so voided postings can be re-staged, which would bring back the double-posting that REQ-JE-4.13 and scope exclusion 5 were written to prevent.

---

## GAAP-SYS-1 — enforcement-gap
- **Location:** Specs/Behavioral/SystemWide.md REQ-SYS-6.1.1; Checks/check-traceability (Invariant 2)
- **Summary:** REQ-SYS-6.1.1 is an active REQ with no citing test and no entry in the waiver or unenforceable table, so check-traceability fails.
- **Resolution:** dan-decides

REQ-SYS-6.1.1 ('Any exception to REQ-SYS-6.1 ... must be stated explicitly in the relevant entity spec') is a rule for spec authors, not runtime behavior. The only place it is applied is the REQ-FP-2.7 idempotence exception, which cites it. grep finds no test naming REQ-SYS-6.1.1, and the SystemWide.md Waived and Unenforceable tables are empty for it. Specs/README requires every active REQ to be tested, waived, or unenforceable, and the mechanical baseline reports this as the only Invariant-2 failure. The same pattern has precedent: REQ-AC-1.48.1, also a 'policy for spec writers', sits in AccountCrud.md's Unenforceable table.

**Action:** Dan to approve adding REQ-SYS-6.1.1 to SystemWide.md's Unenforceable table (as with REQ-AC-1.48.1: it binds people writing specs, not code).

**Why:** Each REQ must have exactly one disposition, or the traceability check stops working as a gate. As long as it fails, a new real gap will look like the existing known failure.

---


