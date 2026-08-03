# DAL Source Auditor

## DAL-SQL-1 — other
- **Location:** Src/DataAccessLayer/ExecuteReader.fs, lines 132-141
- **Summary:** buildReadQuery places the LIMIT clause before GROUP BY and ORDER BY, which is invalid PostgreSQL syntax when both are non-empty.
- **Resolution:** fix-code

The SQL template in buildReadQuery (lines 132-141) interpolates clauses in this order: SELECT, FROM, JOIN, WHERE, LIMIT, GROUP BY, ORDER BY. PostgreSQL grammar requires GROUP BY before ORDER BY before LIMIT. If a caller passes both a non-None limit and a non-None groupBy or orderBy, the generated SQL will fail with a syntax error at execution time.

The generated template:
```
select {selectColumns}
from {from}
{joinString}
{predicateString}
{limitString}       <-- too early
{groupByString}
{orderByString}
;
```

Correct PostgreSQL clause order: WHERE -> GROUP BY -> ORDER BY -> LIMIT.

Currently latent: every caller in the codebase passes limit as None (Account.fs, FiscalPeriod.fs, JournalEntryHeader.fs, JournalEntryLine.fs, JournalEntryComment.fs, JournalEntryExternalReference.fs all pass None for limit). Since None produces an empty string, the resulting SQL is valid today. The bug would manifest the first time a caller passes both a non-None limit and a non-None groupBy or orderBy.

The function's doc comment describes it as 'designed to produce a flexible read query that can satisfy diverse use cases,' and its signature accepts limit, groupBy, and orderBy as independent optional parameters, implying they should compose correctly.

**Action:** Reorder the interpolation in the SQL template to: predicateString, groupByString, orderByString, limitString.

**Why:** This is a latent correctness defect in a general-purpose query builder. The function's signature promises composability across all combinations of its optional parameters, but one combination produces invalid SQL. When a future domain needs both LIMIT and ORDER BY (plausible for paginated reads in the planned import/staging layer or reporting), it will fail at runtime with an opaque PostgreSQL syntax error.


