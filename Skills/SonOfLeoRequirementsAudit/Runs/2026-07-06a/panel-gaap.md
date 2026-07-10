# GAAP Panel — Phase 4 Findings (SonOfLeo ledger domain)

Scope: Specs/Behavioral/{JournalEntryCrud,AccountCrud,FiscalPeriodCrud,SystemWide}.md, Definitions.md, Decisions.md, and Src/Model/Ledger/* + Src/ModelOrchestrator/*. Read resolved-findings.md first; IE-4, AMB-AC-2, CV-2/4, and the JE/MON rulings are respected.

**Overall:** The journal model is accounting-sound. Balanced-entry invariant (JournalEntryCreation.fs:26-33, exact `totalCredits = totalDebits`, no epsilon) matches the tolerance-free decision. Positive-amount + entry-type model is clean. Void-as-soft-delete with a mandatory reason comment and `voided_at IS NULL` in the WHERE not the join (AccountBalance.fs:69) is correct and audit-safe. Period-derivation-from-entry-date and is_open posting gating are sound. Findings concentrate on **forward-readiness for period close** (the load-bearing part).

---

## GAAP-1 — "Period close" is overloaded; monthly-only grain has no anchor for GAAP closing entries
- **Category:** gaap-gap (forward-readiness)
- **Severity:** high
- **Location:** Specs/Behavioral/FiscalPeriodCrud.md (design note L5, REQ-FP-1.2, REQ-FP-4.1); Src/Model/Ledger/FiscalPeriod.fs:187-199
- **Summary:** The next slice ("period close") names an operation the spec already half-defines as something else — is_open is a *posting lock*, not GAAP book-closing — and the strictly-monthly period grain gives annual closing entries nowhere to anchor.
- **Detail:** Two distinct GAAP operations share the word "close": (1) **Locking** a period — already implemented (closeFiscalPeriod flips is_open; posting gates on it at JournalEntryHeader.fs:148-157; the design note L5 says SonOfLeo "keeps the open/closed state for posting gating but defers closing tooling"). (2) **Closing the books** — posting closing entries that zero Revenue/Expense into equity and roll net income to retained earnings. These differ in periodicity: closing entries are conventionally **annual**, but REQ-FP-1.2 constrains every period to YYYY-MM; there is no fiscal-year entity and no way to group twelve monthly periods. Monthly grain (2) writes retained earnings 12x/year and zeroes each month's income-statement detail; annual (2) has no year to close; lock-only (1) already exists and makes the slice a no-op.
- **Suggested action:** Before building the slice, Dan decides what "period close" *is*: pure posting-lock (done), or GAAP closing entries — and if the latter, at what grain, which forces a fiscal-year concept or an explicit "close the books" operation separate from the monthly lock.
- **Why:** The taxonomy already carries what net-income computation needs (AccountType.normalBalance, AccountComponent.fs:93-96; Revenue/Expense types), so the arithmetic isn't the risk — the *shape of the operation and its period grain* is, and getting it wrong reshapes the fiscal-period model rather than extending it.
- **Resolution owner:** dan-decides
- **Prior ruling:** Touches IE-4 (deferred, "revisit when period closure is designed"). NOT re-raising IE-4's equity-subtype mechanism — trigger is imminent (next slice) but not yet met, and Dan ruled against speculating on the RE-identification mechanism ahead of closure design. Flagging the surrounding scope question, which IE-4 does not cover.
[Dan]I get the question and we've discussed before. Your past counsel was to keep periods as months-only, with no quarterly or annual roll-up. But this finding is asserting that there are 2 concepts being mapped to one. I think there needs to be a revisit here at the beginning of the closing slice. But we'll forget. So I guess add an action item to hash this out now.[/Dan]
---

## GAAP-2 — closeFiscalPeriod is a model-layer flag toggle with no atomic seam to post closing entries
- **Category:** gaap-gap (forward-readiness)
- **Severity:** medium
- **Location:** Src/Model/Ledger/FiscalPeriod.fs:160-199; interacts with Src/ModelOrchestrator/JournalEntryCreation.fs:135,150-156
- **Summary:** If close grows GAAP closing-entry semantics, the closing JE(s) and the is_open flip must commit atomically, but the current function cannot post entries.
- **Detail:** Closing JE(s) + is_open flip must be one transaction, else a crash leaves the books closed-but-unbalanced or entries posted into a period reported closed. Today closeFiscalPeriod lives in the FiscalPeriod **model** module and only toggles the flag. It cannot post closing entries: (a) posting needs Account + JE data → cross-domain, so per the 2026-06-11 decision (deactivateAccount graduated to the orchestrator when it needed another domain's data) this must move to Src/ModelOrchestrator/; and (b) orchestrateCreation **creates its own transaction internally** (JournalEntryCreation.fs:135) and commits it (L155), so a close orchestrator cannot enlist JE creation into an outer transaction. That is the ARCH-1 seam.
- **Suggested action:** When closure gains closing-entry behavior, graduate closeFiscalPeriod to an orchestrator and give JE creation an external-transaction parameter (resolve jointly with ARCH-1) so compute-net-income → post-closing-JE → flip-is_open is one atomic unit.
- **Why:** A non-atomic close can leave the ledger unbalanced across a period boundary — the one thing the tolerance-free decision exists to prevent.
- **Resolution owner:** dan-decides (design), then fix-code
- **Prior ruling:** none directly; compounds ARCH-1.
  [Dan]I may or may not agree to this. It's a design decision for future me. But I don't think it has to be one atomic action. Why can't you post the closing JEs and then close the period?[/Dan]

---

## GAAP-3 — Account balance is account-type-agnostic; a correct GAAP balance is normal-balance-oriented
- **Category:** gaap-gap
- **Severity:** medium
- **Location:** Src/ModelOrchestrator/AccountBalance.fs:93 (`MoneyModule.subtract debits credits`); REQ-JE-3.6
- **Summary:** netBalance applies one fixed sign to every account type; neither "debits−credits" nor the spec's "credits−debits" is correct for all types.
- **Detail:** netBalance is a single fixed convention (debits−credits in code; spec text says credits−debits) applied uniformly regardless of type. A correct balance is expressed in the account's **normal balance** orientation (asset/expense → debits−credits; liability/equity/revenue → credits−debits), so positive means "more of what this account normally holds." As written, a credit card (Liability) reports a *negative* netBalance for money owed and revenue reports negative earned income — technically consistent but misleading to any report or the downstream retirement engine treating netBalance as a magnitude. Note: raw totalDebits/totalCredits per account *are* both returned, so a **trial balance** can be built correctly from those columns — the defect is specifically in the derived netBalance field.
- **Suggested action:** Define netBalance in normal-balance orientation using AccountType.normalBalance (already available), and align REQ-JE-3.6's wording to that rule rather than a fixed "credits minus debits."
- **Why:** Statements and any consumer of a signed balance depend on the number reading positive in the account's natural direction; a type-agnostic sign silently inverts half the chart of accounts.
- **Resolution owner:** dan-decides (convention), then fix-code + fix-spec
- **Prior ruling:** Overlaps CUST-3 (spec-vs-code sign mismatch). Re-raising with the added dimension CUST-3 doesn't state: *neither* direction is right because the balance must be normal-balance-relative, and AccountType is the missing input.
  [Dan]Fixed in code. Also removed the hard-wired "debits minus credits" from REQ-JE-3.6 and replaced it with REQ-JE-3.6.1[/Dan]

---

## GAAP-5 — fetchByReference can return duplicate journal entries, violating the "result is a set" contract
- **Category:** enforcement-gap
- **Severity:** low
- **Location:** Src/ModelOrchestrator/JournalEntryFetching.fs:12-42,87-95; REQ-JE-3.5, REQ-JE-3.8
- **Summary:** No DISTINCT on the reference-lookup join, so an entry with multiple matching external references is returned multiple times.
- **Detail:** fetchHeaderIdsByReference LEFT JOINs journal_entry_ext_reference and selects je.unique_id with no DISTINCT. An entry carrying two external references that both match the FI filter (REQ-JE-1.46 permits many; REQ-JE-3.8 filters on FI only) yields two rows with the same header id → fetchByReference fetches and returns that entry twice. REQ-JE-3.5/3.8 both state the result "is a set."
- **Suggested action:** Add DISTINCT (or dedupe the id list) in fetchHeaderIdsByReference.
- **Why:** Audit-traceability lookups that double-count an entry can double-count its lines in any ad-hoc reconciliation built on top of them.
- **Resolution owner:** fix-code
- **Prior ruling:** none.
  [Dan]Fixed[/Dan]

---

**No finding (deliberately):** void soft-delete under cash-basis GAAP (sound; Dan's decision 2026-06-22); closed-period correction via offsetting entry (REQ-JE-4.8, GAAP-correct); balanced-entry allowing a self-cancelling same-account entry (harmless); FSDDD-05 (voided-only accounts vanishing) and CUST-1 (no as-of date) — confirmed present, not re-raised as already flagged by prior panels.
