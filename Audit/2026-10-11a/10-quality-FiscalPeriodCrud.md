# requirements-quality-auditor:FiscalPeriodCrud.md

## FP-STALE-DEL-1 — stale-reference
- **Location:** Specs/Behavioral/FiscalPeriodCrud.md line 3 (preamble); cf. Specs/Behavioral/SystemWide.md §4 (lines 35-40)
- **Summary:** The FiscalPeriodCrud preamble says the deletion policy lives in SystemWide.md, but SystemWide.md §4 says there is no system-wide deletion policy and that each entity's spec decides it.
- **Resolution:** fix-spec
- **Prior ruling:** Audit 2026-10-04a disposition #137 (AC-STALE-1), accepted: the same stale 'deletion lives in SystemWide' preamble, fixed in AccountCrud only. This is not a re-raise of an overruled point. It is the same accepted ruling, not yet applied to this spec.

FiscalPeriodCrud.md line 3 reads: 'Cross-cutting policies (string trimming, data-state enforcement, audit timestamps, deletion) live in SystemWide.md and apply to everything below.' SystemWide.md §4 reads: 'No system-wide deletion policy. Whether an entity's records may be hard-deleted is a domain-level decision, made in each entity's spec', and REQ-SYS-4.1 (the old system-wide hard-delete ban) is withdrawn for that reason. The fiscal period deletion rule is actually REQ-FP-5.1 in this same spec. Audit 2026-10-04a #137 (AC-STALE-1) found the same defect in the AccountCrud preamble. It was accepted and fixed there: AccountCrud.md line 3 now reads 'Cross-cutting policies live in SystemWide.md and apply to everything below.' The FiscalPeriodCrud preamble was not changed to match. (JournalEntryCrud.md line 3 has the same wording, but that file is outside this scope.)

**Action:** Remove 'deletion' from the parenthetical list in the FiscalPeriodCrud.md preamble, or use the AccountCrud wording from #137: 'Cross-cutting policies live in SystemWide.md and apply to everything below.'

**Why:** The preamble is the first place a reader goes to learn where a fiscal period rule lives. Here it points to a SystemWide policy that was withdrawn, against SystemWide's own statement that deletion is decided per entity. Dan already ruled on this exact defect in AccountCrud, and this is the same fix.

---

## FP-XREF-POS157-1 — insufficient-elaboration
- **Location:** Specs/Behavioral/FiscalPeriodCrud.md REQ-FP-2.4, REQ-FP-2.5, REQ-FP-2.7; cf. Specs/Behavioral/Positions.md REQ-POS-15.7
- **Summary:** REQ-POS-15.7 can now reject the fiscal period creation that FiscalPeriodCrud specifies, including the REQ-FP-2.7 ensure operation, but the FP spec does not mention or point to that rejection anywhere.
- **Resolution:** fix-spec

Slice 2 added REQ-POS-15.7: 'Creating a fiscal period whose start date is on or before the balance date of any Pre-ledger Balance is rejected with a typed error naming the period and the earliest and latest such balance dates.' The code applies it to every creation path. Src/Business.CrossDomainOrchestration/FiscalPeriodCreation.fs `constructNewAndPersist` calls `confirmNoPreLedgerBalanceStranded` before it persists, and `ensureFiscalPeriods` goes through the same function. Tests/Tests.Integrated/CrossDomainOrchestration/PreLedgerBalanceGuards.fs line 208 tests the ensure path, which fails as a whole. FiscalPeriodCrud.md says none of this. REQ-FP-2.7 says, with no condition, 'Missing periods are created open (REQ-FP-2.6)', and it names exactly one failure: 'A start month later than the end month fails with a typed error.' REQ-FP-2.4 refers to 'all validations' but never lists them. FP-2.7 already cites a rule from another spec when that rule shapes its behaviour (REQ-STG-9.8 in its Why), so citing across specs is normal here. Someone reading only the spec that owns fiscal period creation would conclude that the ensure operation's only error is start-after-end and that any missing month is always created. Both conclusions have been false since 2026-10-10. The behaviour is built and tested, so this is a gap in the spec's documentation, not in the code.

**Action:** Add one cross-reference sentence to REQ-FP-2.7, and optionally to REQ-FP-2.5, for example: 'Creation is also refused under REQ-POS-15.7 (a period that would strand a Pre-ledger Balance); in an ensure, that refusal fails the whole operation and creates nothing (REQ-SYS-8.1).'

**Why:** FiscalPeriodCrud.md owns the period creation behaviours. When another spec adds a rejection to those operations and this spec lists failure modes without it, a maintainer or test writer working from the FP spec gets an incomplete contract. Any later change to the create or ensure path risks dropping the guard, and the result would be silent: pre-ledger balances would stop counting in net worth (REQ-RPT-8.7).

---

