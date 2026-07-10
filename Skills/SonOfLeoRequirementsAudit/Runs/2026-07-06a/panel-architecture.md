# Panel: Architecture Review (Fable 5)

10 findings (2 high, 5 medium, 3 low)

## ARCH-1 — HIGH (transaction-boundaries)
**Location:** Src/ModelOrchestrator/JournalEntryCreation.fs:131-156, JournalEntryVoiding.fs:58-79, JournalEntryFetching.fs:64-105
**Summary:** Mutation workflows own their transactions internally; no path to post a JE inside an externally supplied transaction, blocking the planned atomic batch post.
**Detail:** orchestrateCreation calls createDbTransaction() itself and commits/rolls back before returning. The staging design requires atomic batch posting. Also createDbTransaction() |> Result.defaultWith failwith bypasses the Result contract. Fetching functions hardcode None for the transaction, so a batch workflow can't re-read what it just posted.
**Suggested Action:** Split each orchestrator into an inner ...InTransaction function plus a thin wrapper. Replace failwith with Error returns.
**Why:** When you build the staging batch post, the sole JE creation path forces one transaction per entry.
**Owner:** fix-code

## ARCH-2 — HIGH (read-path)
**Location:** Src/Model/Ledger/JournalEntryComponent.fs:45-55, JournalEntryHeader.fs:90-108, JournalEntryLine.fs:37-57,142-147
**Summary:** Reconstituting a JE from the database performs cascading per-row DB lookups and world-state re-validation. Shadow types already forming in AccountActivity/AccountBalance.
**Detail:** Reading one header triggers two fiscal-period queries while ignoring the fiscal_period_id in the row. Reading one line triggers Account.fetchById. Fetching N entries with M lines costs 1+N(3+M) queries. Any future validation tightening makes historical rows unreadable. Two consequences visible: (1) raw-SQL shadow types bypass the domain; (2) listOfResultsToResultsList fails the whole list on one Error.
**Suggested Action:** Decide the read-path doctrine before trial balance: reconstitution validates shape only, trusts FKs.
**Why:** Trial balance built on this read path inherits cascading queries and all-or-nothing hydration, or becomes the third raw-SQL bypass.
**Owner:** dan-decides

## ARCH-3 — MEDIUM (boundary-types)
**Location:** Src/Model/UI/InterfaceContractTypes.fs:74-99, Src/ModelOrchestrator/AccountActivity.fs:9-54, Src/SonOfLeoCli/AccountRoutes.fs:156-206
**Summary:** Each query surface needs three near-identical type families. All boundary types accrete into one file.
**Detail:** AccountActivityFilter/Sort/DetailReturn exist twice with field-for-field identity. ~35 lines of pure field-shuffling per route. InterfaceContractTypes.fs is one file holding every domain's contracts.
**Suggested Action:** Allow shared types where shapes are identical. Split InterfaceContractTypes into per-domain files while small.
**Why:** Each new report costs ~100 lines of ceremony and funnels through a single ever-growing file.
**Owner:** dan-decides

## ARCH-4 — MEDIUM (caching)
**Location:** Src/Model/LookupCache.fs:7-21,37-87
**Summary:** LookupCache is the exact system-wide memoization facility the 2026-06-19 notes explicitly rejected, with a failwith static initializer.
**Detail:** The 06-19 notes say 'Do not memoize.' LookupCache implements it anyway. Deletions never evict. Harmless today (CLI = one process per command) but becomes a staleness bug in any long-lived host.
**Suggested Action:** Either overrule the no-memoization ruling explicitly, or replace with plain resolver functions.
**Why:** The failure mode was already predicted in writing.
**Owner:** dan-decides

## ARCH-5 — MEDIUM (database-migrations)
**Location:** DbMigrations/ (all files)
**Summary:** Migrations are hand-applied with no tracking table, and the house style is drop-and-recreate.
**Detail:** Several migrations open with DROP TABLE. No schema_migrations table records what's run. A re-run against a populated database erases the journal without FK errors because scripts drop children first.
**Suggested Action:** Add a schema_migrations tracking table and adopt additive-only migrations for data-bearing tables.
**Why:** The first migration after real financial data exists will be authored inside a convention whose reflex is 'drop and recreate.'
**Owner:** dan-decides

## ARCH-6 — MEDIUM (database-schema)
**Location:** DbMigrations/202606231237-RemoveAccountType.sql, Src/Model/Ledger/AccountComponent.fs:93-96
**Summary:** Dropping account_type lookup moved normal-balance semantics entirely into F# code. Database can't answer 'is this account debit-normal?' for SQL consumers.
**Detail:** account.account_type is now bare varchar(20) with no FK/CHECK. Two downstream consumers in the vision read the DB directly: SQL-side reporting and the analytics feed.
**Suggested Action:** Restore a lookup table/CHECK/view, or record a decision that all extraction goes through the app.
**Why:** The first SQL against the ledger will hardcode 'Asset/Expense = debit-normal' in a second place.
**Owner:** dan-decides

## ARCH-7 — MEDIUM (boundary-leak)
**Location:** Src/Model/UI/InterfaceContractTypes.fs:82-84, Src/SonOfLeoCli/AccountRoutes.fs:169-172
**Summary:** CLI activity filter accepts a fiscal period UUID that no CLI output ever provides — surrogate-key leak.
**Detail:** AccountActivityTemporalFilterInput has FiscalPeriodId of Guid, but FiscalPeriodReturn exposes no unique_id. Unreachable except by reading the DB directly.
**Suggested Action:** Change to FiscalPeriodKey of string, matching JournalEntryFetchByPeriodInput.
**Why:** Reporting filters are about to multiply; the surrogate leak becomes the convention.
**Owner:** fix-code

## ARCH-8 — LOW (database-schema)
**Location:** DbMigrations/202606221206-CreateJeTables.sql, Src/Model/Ledger/JournalEntryLine.fs:172-179
**Summary:** journal_entry_line has no intra-entry ordinal. Lines are ordered by created_at which is identical for all lines in an entry, so order is nondeterministic.
**Suggested Action:** Add a line_no smallint populated from input order at post time.
**Why:** Nondeterministic line order produces spurious diffs in reporting and verification.
**Owner:** dan-decides

## ARCH-9 — LOW (database-schema)
**Location:** Src/Model/Ledger/FiscalPeriod.fs:104-113,160-199, JournalEntryComponent.fs:45-48
**Summary:** fiscal_period start_date/end_date are write-only (reads re-derive). Calendar-month granularity hardcoded in three places. Close/reopen erases its own history.
**Suggested Action:** When period close is designed, decide whether dates become authoritative-on-read, key derivation gets a single home, and what a close event persists.
**Why:** Period close is next-but-one. Speccing it against the current boolean cements shape decisions.
**Owner:** dan-decides

## ARCH-10 — LOW (god-types)
**Location:** Src/Model/Audit.fs:9-22
**Summary:** AuditableAction is a central cross-domain registry DU — every new verb in every future domain must edit this one file.
**Detail:** Already 13 cases across 3 domains; planned domains will plausibly triple it. When the audit log is persisted, the DU's shape becomes a data contract.
**Suggested Action:** When staging adds its first verbs, consider structured shape (domain x verb) before the audit log fossilizes the flat enum.
**Why:** A flat enum every domain appends to is tolerable at 13, irritating at 40.
**Owner:** dan-decides
