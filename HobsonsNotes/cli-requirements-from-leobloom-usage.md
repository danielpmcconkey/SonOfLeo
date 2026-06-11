# CLI Requirements — What a Year of LeoBloom Actually Taught Us

**Author:** Hobson (comptroller — the CLI's primary user)
**Date:** 2026-06-11
**Method:** Cross-referenced the LeoBloom CLI spec surface (`Specs/CLI/*.feature`,
~3,700 lines, 17 command groups) against what the weekly Saturday routine and its
ops scripts (`LeoBloomOps/Scripts/`) actually invoke. This is usage data, not
opinion. SonOfLeo is not a 1:1 port; this is the comptroller's input on what to
keep, what to add, and what to leave buried.

**The headline:** roughly a third of LeoBloom's CLI surface carries the entire
weekly load. The functions we most wanted — search, reclass, repair, recon —
were never built, while transfers, periods, and extracts were built and never
invoked once. LeoBloom's backlog optimized for accounting-textbook
completeness. SonOfLeo should optimize for the Saturday routine — that is the
actual product.

---

## 1. Used every week — must exist in SonOfLeo

| LeoBloom surface | Weekly role |
|---|---|
| `ledger post` | The workhorse. All 7 importers shell out to it (one process per JE); also obligations, true-ups, adjusting entries, reclasses. By far the most-exercised command. |
| `ledger void` | Reclass = void + repost. Weekly. Takes a `--reason`. |
| `ledger show` | Post-mutation verification. |
| `obligation agreement list/show` | Phase 3 batch — all 9 agreements, every Saturday. |
| `obligation instance list/spawn/transition/post` | Instance lifecycle: `expected → in_flight → confirmed → posted` (also `expected → confirmed`). `posted` is terminal. |
| `obligation upcoming --days` | Feeds the summary's "Upcoming bills" table. |
| `report net-worth --json` | `render_networth.py` is a thin HTML skin over this. The `--json` flag is the integration seam that works — keep machine-readable output on everything. |
| `portfolio position record/latest/list` | Fidelity / T. Rowe / HealthEquity snapshots weekly; real estate occasionally. |
| `portfolio account list` | Weekly. |
| `account list` | Weekly COA sync against the markdown reference. |

## 2. Used rarely — keep, but don't gold-plate

- `account show` / `account balance` — occasional recon triage (real triage is SQL).
- `account create/update` — COA changes, occasional.
- `obligation agreement create/update/deactivate` — lease changes and oddities.
- `obligation overdue` — occasional.
- `portfolio fund create/show`, `portfolio account create/show` — setup-time only.
- `ledger reverse` — never yet fired in anger; conceptually reachable.
- **`report schedule-e` — never yet used but LOAD-BEARING.** Tax reporting is
  purpose #1 of the entire system (see `feedback_leobloom_books_vs_tax`);
  first filing season is early 2027. Rare ≠ optional. This one survives.

## 3. Doesn't exist — would genuinely use (the SonOfLeo opportunity)

Ranked. These are the gaps where reality currently routes around the CLI into
raw SQL, throwaway scripts, or Dan running guarded UPDATEs by hand.

1. **`ledger search` / `ledger list`** — query JEs by date range, account,
   description pattern, amount. The single biggest hole. The CLI can write
   entries but cannot find them; every Saturday triage session is raw SQL.
2. **`ledger reclass`** — atomic void+repost that carries date / description /
   source / ref and changes only the account. The May 2026 review was 53
   manual void+post pairs driven by a throwaway Python script.
3. **Account references by CODE, not id** — ids drift; the comptroller carries
   an id↔code map in wakeup notes like a medieval scribe. Accept `5650`,
   resolve internally. (If ids must exist, never expose them on the CLI.)
4. **Obligation instance repair** — (a) relink a posted instance to a different
   JE; (b) edit a posted instance's `expected_date`. Both have required Dan
   personally running guarded raw `UPDATE`s because no `posted→posted`
   transition and no relink command exist. Obligation-linked JEs can't be
   void+reposted (orphans the link), forcing adjusting-JE workarounds.
5. **`ledger post --batch <file>`** — importers spawn a dotnet process per JE.
   One process per import run, fed a JSON file, would meaningfully shorten
   Saturdays and make importer failures atomic per-file instead of per-line.
6. **First-class reconciliation** — the recon_balances-CSV-vs-ledger check is
   THE control gate of the week and lives in an external psycopg2 script
   (`reconcile_csv.py`); same for `ledger_integrity.py` (debits=credits +
   the net-income identity) and the brokerage true-up. Citizens, not squatters.
7. **The monthly spending report** — Dan's one non-negotiable weekly read is
   rendered by the *legacy prototyper binary*, not LeoBloom at all. SonOfLeo
   should own its marquee report.
8. **Cash-coverage projection** — LeoBloom specced `report projection` but the
   Saturday "money you need to move" section is still done by hand with `bc`.
   Design it around the actual algorithm in the Saturday skill (per-account:
   balance + known inflows − known outflows vs ~$500 cushion, with the
   transfer-precedence rules), not a generic projection.

## 4. Never in a million years — do not port

- **The entire `transfer` group** (initiate/confirm/show/list) — a two-phase
  in-flight state machine for a cash-basis ledger. In practice every transfer
  is one balanced JE posted when cash moves. Zero invocations ever.
- **The entire `period` group and the closing machinery** — close, reopen,
  pre-close validation, post-close adjustments, closed-period enforcement,
  close metadata, reversing entries. Seven behavioral spec files of
  accrual-world apparatus, never exercised. Dan deferred closing entries and
  the books are provably fine without them (the integrity identity:
  Assets = Liabilities + Equity + Net Income, by design, no closing entries).
  *Caveat: deferred ≠ rejected. Keep the **schema** able to support a period
  close someday; build none of the tooling until Dan actually wants it.*
- **The entire `extract` group** (account-tree / balances / positions /
  je-lines JSON) — built as integration plumbing; every real consumer uses
  `report net-worth --json` or direct SQL. `je-lines` is keyed by
  fiscal-period-id — a parameter for a concept we don't use.
- **The `invoice` CLI group** — `generate_bills.py` owns invoicing end to end
  (PDFs + `ops.invoice` upsert). The CLI commands were bypassed entirely.
- **The textbook report shelf** — trial-balance, general-ledger,
  income-statement, balance-sheet, cash-receipts, cash-disbursements,
  pnl-subtree, allocation, gains, portfolio-history, portfolio-summary.
  Eleven reports; what Dan reads is net worth and monthly spending.
  (Schedule E is the one exception — see §2.)
- **`portfolio dimensions`**; and the API layer (already cancelled in
  LeoBloom — CLI-only consumption is settled policy).

## Cross-cutting lessons (cheap to honor from day one)

- **`--json` on everything.** The one integration seam that worked.
- **Machine-friendly errors + non-zero exits** — scripts drive this CLI more
  than humans do.
- **Names, not just codes, in human output** — Dan reviews decisions by
  account name.
- **No direct DB writes for ledger/obligation state** is standing policy; the
  CLI is the only mutation path. Every gap in the CLI (see §3) therefore
  becomes either a Dan-runs-raw-SQL exception or a workaround. A complete CLI
  is what makes the policy livable.
