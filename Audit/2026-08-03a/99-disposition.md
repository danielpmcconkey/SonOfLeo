# Disposition Record — 2026-08-03a Audit

27 findings from 19 auditors across 4 batches. Reviewed by Dan with Hobson, 2026-08-03.

| # | Auditor | ID | Summary | Status | Date |
|---|---------|----|---------|----- --|------|
| 001 | ledger-vet | STALE-IE4-TRIGGER | IE-4 trigger text ambiguous after GAAP-CLOSE split | accepted | 2026-08-03 |
| 002 | ai-maintainability | GUARD-1 | No check enforces assertion-shape standard; 73 tests use forbidden pattern | accepted | 2026-08-03 |
| 003 | ai-maintainability | TRACE-1 | REQ-SYS-6.1 stale waiver — tests co-cite it | accepted | 2026-08-03 |
| 004 | ai-maintainability | IDIOM-1 | AccountActivityFilter.journalEntryId raw Guid | accepted | 2026-08-03 |
| 005 | customer | CUST-NAME-1 | AccountBalanceReturn/JournalEntryLineReturn missing account name | accepted | 2026-08-03 |
| 006 | fsharp-ddd | IDIOM-NOOP-1 | updateComment emits wrong error case (Reference instead of Comment) | accepted | 2026-08-03 |
| 007 | fsharp-ddd | IDIOM-GUID-1 | Same as IDIOM-1 (duplicate finding) | accepted | 2026-08-03 |
| 008 | fsharp-ddd | IDIOM-NOOP-2 | No test for comment update no-op path | accepted | 2026-08-03 |
| 009 | gaap | MAINT-BATCH-1 | fetchByJournalEntryHeaderIdList empty-list guard | accepted | 2026-08-03 |
| 010 | quality:AccountCrud | AC-DUP-1 | Duplicate AC-3.3.1 in unenforceable table | accepted | 2026-08-03 |
| 011 | quality:AccountCrud | AC-AMB-1 | "(or inactive)" false synonym in REQ-AC-1.48 | overruled | 2026-08-03 |
| 012 | quality:DAL | STALE-DAL-1 | Waiver cites wrong AppError case | accepted | 2026-08-03 |
| 013 | quality:JournalEntryCrud | CON-JE-1 | REQ-JE-3.9.1 ordering contradiction with 3.9.3 | accepted | 2026-08-03 |
| 014 | quality:JournalEntryCrud | STALE-JE-1 | REQ-JE-3.4 stale "no test" note | accepted | 2026-08-03 |
| 015 | quality:JournalEntryCrud | STALE-JE-2 | REQ-JE-3.9.3 missing amount sort option | accepted | 2026-08-03 |
| 016 | quality:JournalEntryCrud | STALE-JE-3 | REQ-JE-2.4 cites wrong SYS requirement | accepted | 2026-08-03 |
| 017 | quality:SystemWide | STALE-SYS-1 | Promotion candidates section stale | overruled | 2026-08-03 |
| 018 | statement-delta | STMT-FP-1 | Statement omits fiscal periods | overruled | 2026-08-03 |
| 019 | truthfulness:dal | DAL-SQL-1 | LIMIT clause before GROUP BY/ORDER BY | accepted | 2026-08-03 |
| 020 | truthfulness:interface | IDIOM-IB-1 | FetchSort leaks into interface contract | overruled | 2026-08-03 |
| 021 | truthfulness:interface | IDIOM-IB-2 | Shared FiscalPeriodInput across four operations | accepted | 2026-08-03 |
| 022 | truthfulness:model | CONTRADICTION-COMMENT-NOOP-1 | Missing no-op guard on comment update | accepted | 2026-08-03 |
| 023 | truthfulness:model | CONTRADICTION-JE-COMPOSITE-ORDER-1 | Composite validation runs after component writes | overruled | 2026-08-03 |
| 024 | truthfulness:tests | TT-SYS51-ACCT | Account round-trip test never reads back from DB | accepted | 2026-08-03 |
| 025 | truthfulness:tests | TT-JE148-SCOPE | REQ-JE-1.48 test scope mismatch | accepted | 2026-08-03 |
| 026 | truthfulness:tests | TT-JE410-CLOSED | Missing closed-period test for reference append | accepted | 2026-08-03 |
| 027 | truthfulness:tests | TT-JE49-STATE | Missing voided/closed tests for reference update | accepted | 2026-08-03 |

## Rulings on overruled findings

**011 AC-AMB-1:** "Active" is the only state that matters. REQ-AC-1.48 changed to say "not active." No taxonomy of non-active states needed.

**017 STALE-SYS-1:** Promotion candidates section deleted. Each entity owns its own design decisions. System-wide promotion adds indirection that helps nobody.

**018 STMT-FP-1:** Fiscal periods are a temporal anchor for journaling, not an independent domain entity. The statement is accurate.

**020 IDIOM-IB-1:** The rule "contracts use only primitives" was overstated in the type-taxonomy article. Contracts use whatever makes sense at the boundary — usually primitives because domain types need validated construction, but stable boundary types (FetchSort, FieldUpdate) are fine. Article corrected.

**023 CONTRADICTION-JE-COMPOSITE-ORDER-1:** Permanently overruled. Cannot validate 2+ valid lines until each line is constructed and persisted (line validity depends on DB state). Pre-write validation would mean validating unvalidated input or validating twice. Transaction bracket ensures atomicity. Added to resolved-findings.md.

## Statuses
- **accepted** — finding valid, action taken or queued
- **overruled** — finding rejected with reason
