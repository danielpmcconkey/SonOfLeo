# hobson-dataingestion-audit

## STALE-DEF-1 — stale-reference
- **Location:** Specs/Definitions.md — Staged line and Postable definitions
- **Summary:** Definitions.md still uses pre-migration 'account code' terminology for staged lines and the Postable posting process, contradicting the post-migration 'account_id' (UUID FK) terminology in DataIngestion.md and the schema.
- **Resolution:** fix-spec

Two definitions in Definitions.md were not updated after the code-to-ID migration (2026-08-22).

1. **Staged line** (line 46): says the staged line 'carries an amount, direction (line_type), and an account code that may be null until classification or manual review fills it in.' Post-migration, the field is `accountId: AccountId option` in the model (StageEntryLine.fs line 19) and `account_id uuid` with FK to `ledger.account` in the schema (migration 202608220946-RebuildStageEntryLine.sql line 16). REQ-STG-2.14 correctly says 'account_id foreign key to `ledger.account`.' The Definitions.md text says 'account code' — a string concept that requires resolution — when the actual field is a UUID FK that IS the account reference.

2. **Postable** (line 49): says 'The posting process validates that every staged line has a non-null account_code that resolves to an account in the chart of accounts.' Post-migration, no code-to-account resolution occurs at posting time. REQ-STG-9.4 explicitly states: 'Invalid non-null account IDs cannot occur: the staged line's account_id is FK-constrained against `ledger.account`.' The validation is a null check on account_id, not a code resolution step. The Definitions.md describes a mechanism that no longer exists.

Definitions.md is authority level 2; DataIngestion.md is authority level 3. DataIngestion.md and the schema agree with each other (both post-migration); Definitions.md is the stale outlier.

**Action:** Update both definitions in Specs/Definitions.md: (1) Staged line — change 'account code' to 'account ID' (or 'account reference'); (2) Postable — replace 'non-null account_code that resolves to an account in the chart of accounts' with language reflecting the FK-constrained account_id (e.g. 'non-null account_id').

**Why:** Definitions.md is the highest non-Dan authority in the system — it defines the terms that behavioral specs and code build on. A developer reading the Postable definition today would believe that posting involves an account-code resolution step, which it does not. A developer reading the Staged line definition would believe the field is a string code, not a UUID FK. Both readings would lead to incorrect implementation assumptions if anyone built against Definitions.md rather than the behavioral spec.

---
