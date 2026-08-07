# Action Items — 2026-08-03a Audit

| # | Finding | What | Owner | Status |
|---|---------|------|-------|--------|
| 1 | TT-JE410-CLOSED | Add test: append external reference to entry in closed fiscal period (REQ-JE-4.10 closed-period half). Mirror JournalEntryComment.fs:149 pattern, use fixture.Data.jeInClosedPeriodId | Dan/BD | done |
| 2 | TT-JE49-STATE | Add two tests for REQ-JE-4.9: (a) update FI/value on reference belonging to a voided entry, (b) update FI/value on reference belonging to an entry in a closed fiscal period | Dan/BD | done |
| 3 | CUST-NAME-1 | Add accountName to AccountBalanceReturn and JournalEntryLineReturn. Amend or add REQ requiring account name alongside code in all return payloads. Converters resolve from LookupCache (pattern exists in AccountActivityReturn) | Dan | done, but needs specs written to support it |
| 4 | GUARD-1 | Add Checks/check-assertion-shape.sh — grep Tests/ for Result.isError and Result.isOk, fail pre-commit hook when found | Dan | done |
| 5 | GUARD-1 | Modernize 73 existing tests using Result.isError/isOk to match typed AppError DU cases (isolated tests are the primary offenders) | Dan/BD | done |
| 6 | TRACE-1 | Remove REQ-SYS-6.1 from test names that co-cite it (JournalEntryRoutes.fs:512, JournalEntryVoiding.fs:115) — clears the stale waiver flag | Dan | done |
| 7 | IDIOM-1 | Add test for AccountActivityFilter with journalEntryId = Some (no test currently exercises the non-None path) | Dan/BD | done |
| 8 | IDIOM-NOOP-1/2 | Add test for comment update no-op: both commentUpdate=NoChange and secondaryIdUpdate=NoChange, assert JournalEntryCommentUpdateNoOp | Dan/BD | done |
| 9 | AUDIT-SKILL | Auditors with no findings must still write their reasoning — what they checked, what they considered, why nothing rose to finding level. "No findings" with no explanation is indistinguishable from a shallow run | Hobson | done |
| 10 | AUDIT-SKILL | Record FT-1 revision: auditors now run in parallel batches of 5, not sequentially. Update any FT-1 references in CompoundedLearnings or audit docs | Hobson | done |
