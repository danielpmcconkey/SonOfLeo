# Cash-Basis Ledger

**Source:** Decisions.md, 2026-06-11; Dan's clarification 2026-07-12

The ledger (`Model/Ledger/`) is a double-entry accounting system tracking cash-basis USD transactions. It records money that moved — the USD amount the financial institution actually settled. No accrual, no FX revaluation.

The broader system is planned to include obligations and investments, but those are separate domains living in their own model namespaces outside of `Ledger/`. The ledger itself is purely cash-basis.

## What cash-basis means

- A transaction is recorded when cash moves, not when an obligation is created
- Foreign transactions enter as the USD settlement amount — no multi-currency machinery
- The ledger has no concept of unrealized gains, accrued interest, or pending obligations
