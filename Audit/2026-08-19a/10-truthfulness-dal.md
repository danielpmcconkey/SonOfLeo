# dal-auditor

_No findings._

## Reasoning

Audited all 6 DAL source files (DbConnections.fs, DbTransaction.fs, ExecuteNonQuery.fs, ExecuteReader.fs, ExecuteScalar.fs, QueryParameters.fs) against DataAccessLayer.md (32 active requirements), SystemWide.md, Definitions.md, all 12 audit-conduct articles, relevant CompoundedLearnings (NodaTime discipline, temporal persistence, environment separation, debug/release access, validation layers), the resolved-findings precedent ledger (17 rulings), database migrations, and appsettings configuration files.

CORRECTNESS checks performed:
- REQ-DAL-1.3/1.14/1.15: getConnectionStringConfig correctly errors on missing config file (exception caught), missing ConnectionStringEnvVar key (IsNullOrWhiteSpace on null returns true), and empty value. All produce typed AppErrors.
- REQ-DAL-1.16: confirmConfigDoesntContainConnectionString uses semicolon/Host= heuristic. Per resolved AMB-DAL-01, the spec need not prescribe the heuristic.
- REQ-DAL-1.17: getRawConnectionString correctly fails with DalEnvVarNotSet when Environment.GetEnvironmentVariable returns null (via Option.ofObj).
- REQ-DAL-1.18: getValidConnectionString trims and rejects whitespace-only strings.
- REQ-DAL-1.19: getValidConnectionString applies Trim() before returning the connection string.
- REQ-DAL-1.20: Verified appsettings files. Dev uses SONOFLEO_DEV_CONNSTR, Prod uses SONOFLEO_PROD_CONNSTR, Test uses SONOFLEO_TEST_CONNSTR. SonOfLeoCli.fsproj CopyEnvConfig target copies the correct appsettings per build configuration.
- REQ-DAL-2.1/2.3: All three execution functions (executeReaderQuery, executeNonQuery, executeScalar) accept QueryParameter lists and use buildParamsList to create typed NpgsqlParameter objects. No callers outside DAL reference Npgsql directly (verified via grep). Callers use parameterized values consistently (spot-checked Account.fs, JournalEntry modules, Ingestion modules).
- REQ-DAL-2.2: executeReaderQuery confirms numRows after construction via confirmNumRows. executeNonQuery confirms numRows after execution. executeScalar correctly excluded (scalar query). AcceptableExpectedRows DU covers Zero/ExactlyOne/OneOrMany/AnyQuantityIsAcceptable, satisfying the resolved ruling CON-DAL-02.
- REQ-DAL-3.1: DAL references Npgsql 10.0.3 (compatible with PostgreSQL 17.x).
- REQ-DAL-3.2: No non-DAL source file opens Npgsql namespace (grep-verified). RowReader abstracts DbDataReader behind DAL-defined accessor functions.
- REQ-DAL-3.4/3.5: CreateDatabase migration specifies ENCODING=UTF8, LC_COLLATE=en_US.UTF-8.
- REQ-DAL-3.7: No now() in any migration (grep-verified). No database-side temporal defaults.

CONTRADICTION checks:
- No code behavior contradicts any active spec requirement. The executeScalar function uses Result.defaultWith(failwith(...)) for the transaction-fetch path (line 178), which differs from the sibling functions' proper Result pattern matching — but the path is unreachable (guarded by isNone check on an immutable record) and the developer explicitly commented on the choice. Not a correctness issue.
- runWithAutoCompleteTransaction drops the original func error when rollback also fails (lines 96-98). This is a design choice about error priority in double-failure scenarios, not a spec contradiction (no spec addresses error priority in transaction error handling).

PRACTICE checks:
- NodaTime discipline: UseNodaTime() registered on NpgsqlDataSourceBuilder. RowReader exposes Instant and LocalDate accessors via NodaTime types. No DateTime/DateTimeOffset usage.
- Temporal persistence: Instants persisted as timestamptz, dates as Postgres date. No database-originated temporal values.
- Environment separation: Separate env var names per environment, build-configuration gate in fsproj, four independent backstops verified against debug-release-access article.

SCHEMA checks:
- CreateDatabase migration matches REQ-DAL-3.4 (UTF8) and REQ-DAL-3.5 (en_US.UTF-8).
- No temporal defaults, triggers, or stored procedures in any migration.

Items considered but not raised:
- executeScalar's Result.defaultWith(failwith(...)) pattern: Unreachable code path on an immutable record. Developer-commented deliberate choice. Would be overruled as "nice to have."
- Lazy dataSource caching failed initialization: Appropriate for CLI application. No spec requires recovery from configuration errors.
- rollback-error-over-func-error priority in runWithAutoCompleteTransaction: Design trade-off with no spec requirement governing it.
