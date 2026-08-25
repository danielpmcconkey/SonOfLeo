# DAL Code Truthfulness Auditor

_No findings._

## Reasoning

Examined all six DAL source files (DbConnections.fs, DbTransaction.fs, ExecuteNonQuery.fs, ExecuteReader.fs, ExecuteScalar.fs, QueryParameters.fs) against DataAccessLayer.md (19 active REQs), SystemWide.md cross-cutting requirements, all 12 audit-conduct articles, the resolved-findings ledger (18 entries), and the CompoundedLearnings coding/architecture catalogs (temporal-persistence, nodatime-discipline, environment-separation, debug-release-access, validation-layers).

CORRECTNESS checks performed:

1. Connection string handling (DbConnections.fs) vs REQ-DAL-1.14 through 1.20: All validation stages are correctly implemented — config key retrieval (1.14), empty/whitespace value rejection (1.15), connection-string-in-config detection via semicolons and Host= (1.16), env var resolution (1.17), whitespace-only resolved value rejection (1.18), trimming (1.19). The lazy NpgsqlDataSource is built with UseNodaTime() for proper Instant/LocalDate round-tripping.

2. Query execution (ExecuteNonQuery.fs, ExecuteReader.fs, ExecuteScalar.fs) vs REQ-DAL-2.1 through 2.3: All three functions accept QueryParameter lists and build NpgsqlParameter objects through the centralized convertParamToDbParam function — data values are never interpolated into SQL strings. Non-scalar queries (executeNonQuery, executeReaderQuery) both call confirmNumRows to verify against AcceptableExpectedRows, satisfying REQ-DAL-2.2. The buildReadQuery function interpolates only structural SQL fragments and a typed int option limit parameter, which REQ-DAL-2.3 explicitly permits.

3. Transaction management (DbTransaction.fs): runWithAutoCompleteTransaction correctly handles all three cases — func returns Ok (commit), func returns Error (rollback), func throws (rollback + wrap exception). Types DbTransaction and NpgTranAndConn are private, preventing callers from constructing illegal states. Dispose is always reached via try/finally in commitOrRollbackAndDispose.

4. Parameter and reader type coverage (QueryParameters.fs, ExecuteReader.fs, ExecuteScalar.fs): QueryParameterValue covers all schema column types (int, decimal, varchar, timestamptz, date, uuid, boolean, jsonb) with both required and nullable variants. RowReader covers the same types for read-side. ExecuteScalar has int64 unboxing functions (used for PostgreSQL COUNT(*) bigint results, confirmed in AccountDeactivation.fs). No schema column types are unrepresented.

SCHEMA checks performed:

5. Verified 202606131450-CreateDatabase.sql sets ENCODING='UTF8' and LC_COLLATE='en_US.UTF-8' (REQ-DAL-3.4/3.5). Grep across all 17 migrations found zero uses of now(), CURRENT_TIMESTAMP, or temporal defaults (REQ-DAL-3.7). check-npgsql.sh script exists and correctly enforces the DAL abstraction boundary (REQ-DAL-3.2).

PRACTICE checks performed:

6. NodaTime discipline: DbInstant maps to NpgsqlDbType.TimestampTz, DbLocalDate to NpgsqlDbType.Date. RowReader uses GetFieldValue of Instant and GetFieldValue of LocalDate. No DateTime/DateTimeOffset usage anywhere in DAL code.

7. Temporal persistence: All instant-capable columns in the schema use timestamptz, all date columns use date. No database-originating temporal values.

STATEMENT-DELTA verification:

8. Dan stated he believes all routes that update stage entry status use auto-commit transactions. Verified by examining IngestionRoutes.fs: ingestRawEntries uses runCommandRouteAndAutoCompleteTransaction, updateStageEntry uses runCommandRouteAndAutoCompleteTransaction, post uses runCommandRouteAndAutoCompleteTransaction (or runCommandRouteAndAutoRollback for shadow). The three routes that do NOT use transaction wrappers (newClassificationRule, updateClassificationRule, createNewSource) each perform at most a single DB write, so auto-commit from the connection is sufficient. Dan's belief is confirmed — no statement-delta.

Items considered but not raised to finding level:

- executeScalar uses Result.defaultWith plus failwith for getTranAndConn error handling (line 178), unlike the pattern-matching in executeNonQuery and executeReaderQuery. This is in an unreachable code path — the isNone check on line 168 guarantees npgTranAndConn is Some when the false branch is reached, so getTranAndConn always succeeds there. Cosmetic inconsistency in dead code is not a finding.

- The dataSource Lazy does not wrap NpgsqlDataSourceBuilder.Build() in a try/with, so a syntactically valid but Npgsql-unparseable connection string would throw rather than returning a Result error. No spec requires a specific error mechanism for this edge case, and the exception would surface clearly. Not a spec contradiction.

- No getLong/getLongOption in RowReader — int64 is only consumed via executeScalar for COUNT(*) aggregates (confirmed in AccountDeactivation.fs). No reader-based query needs to read int64 columns.
