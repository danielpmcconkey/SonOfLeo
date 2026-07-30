# Cash-Basis Ledger

**Source:** Specs/Archive/Decisions.md, 2026-06-11; Dan's clarification 2026-07-12

What cash basis *means* is standard GAAP and is not restated here. What is specific to
SonOfLeo is the **scope**:

`Model/Ledger/` is a double-entry, cash-basis, USD ledger and nothing else. It records the
USD amount the financial institution actually settled. It has no concept of unrealized
gains, accrued interest, or pending obligations, and no multi-currency machinery — a foreign
transaction enters as its USD settlement amount.

Obligations and investments are planned, but they are separate domains in their own model
namespaces **outside** `Ledger/`. If a feature needs accrual semantics, it does not belong
in the ledger.
