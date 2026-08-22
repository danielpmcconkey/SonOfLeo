# dal-auditor

_No findings._

## Reasoning

Audited all six DAL source files (DbConnections.fs, DbTransaction.fs, ExecuteNonQuery.fs, ExecuteReader.fs, ExecuteScalar.fs, QueryParameters.fs) against the DAL behavioral spec (19 active requirements across 3 sections), the database creation migration (202606131450-CreateDatabase.sql), all 12 audit conduct articles, and the resolved findings ledger (17 prior rulings reviewed).

CORRECTNESS CHECK (spec requirements vs code):
- Section 1 (Connection string handling): REQ-DAL-1.3 (config inaccessible -> error) enforced by getConfigValue error propagation. REQ-DAL-1.14 (missing entry -> error) enforced by getConfigValue. REQ-DAL-1.15 (empty value -> error) enforced by IsNullOrWhiteSpace check in getConnectionStringConfig. REQ-DAL-1.16 (connection string in config -> error) enforced by confirmConfigDoesntContainConnectionString (semicolon and Host= heuristic). REQ-DAL-1.17 (env var not set -> error) enforced by getRawConnectionString's Option.ofObj check. REQ-DAL-1.18 (resolved env var whitespace-only -> error) enforced by getValidConnectionString's trim-then-check. REQ-DAL-1.19 (trim final connection string) enforced by getValidConnectionString returning trimmed. REQ-DAL-1.20 (unique env var per config) is a build-configuration fact, waived from testing. All pass.
- Section 2 (Query execution): REQ-DAL-2.1 (parameterized data) enforced by all three execution functions taking QueryParameter list and converting through buildParamsList -> NpgsqlParameter. REQ-DAL-2.2 (verify expected rows for non-scalar queries) enforced by confirmNumRows call in both executeReaderQuery and executeNonQuery; scalar queries excluded by spec text ("non-scalar queries"). REQ-DAL-2.3 (user input parameterized, compile-time-safe values may interpolate) enforced by the same parameterization pattern; buildReadQuery's limit parameter is int option, matching the spec's explicit example. All pass.
- Section 3 (Architecture): REQ-DAL-3.1 (PostgreSQL 17.9) satisfied by Npgsql usage. REQ-DAL-3.2 (callers don't reference Npgsql) verified by grep across Src/Model/, Src/ModelOrchestrator/, Src/InterfaceBridge/, Src/SonOfLeoCli/, Src/Reports/, Src/Context/, Src/Logger/ -- zero hits. Also verified by check-npgsql.sh enforcement script. REQ-DAL-3.4 (UTF-8 encoding) confirmed in migration: ENCODING = 'UTF8'. REQ-DAL-3.5 (en_US.UTF-8 collation) confirmed in migration: LC_COLLATE = 'en_US.UTF-8'. REQ-DAL-3.7 (no now() in defaults) confirmed by grep across all 12 migration files -- zero hits. All pass.

CONTRADICTION CHECK: No behavioral differences found between spec requirements and code behavior.

PRACTICE CHECK (CompoundedLearnings catalogs):
- NodaTime discipline: DAL uses NodaTime Instant and LocalDate throughout RowReader and ExecuteScalar unboxing. NpgsqlDataSourceBuilder.UseNodaTime() registered in dataSource initialization. No DateTime/DateTimeOffset usage.
- Temporal persistence: No database-side temporal defaults in any migration. Temporal columns use timestamptz (for Instant) and date (for LocalDate) types.
- Validation layers: DAL validates at appropriate layer (connection string validation in DbConnections, row count validation in confirmNumRows).
- Descriptive naming: Function names follow canon (confirmNumRows, buildReadQuery, executeReaderQuery, etc.). Variable names are clear (configVal, rawConnectionString, trimmed, etc.).
- Environment separation: Connection string resolved at runtime from environment variable, not hardcoded.
- Debug/release access: DAL connection chain is the mechanism; agent restrictions prohibit manipulation.

SCHEMA CHECK: Database creation migration correctly specifies UTF-8 encoding and en_US.UTF-8 collation per REQ-DAL-3.4 and 3.5. No temporal defaults per REQ-DAL-3.7.

OBSERVATIONS CONSIDERED BUT NOT RAISED:
1. TransactionNeed and ManualTransactionResult types are defined but unused anywhere in the codebase -- dead code, but "nice to have" cleanup is not a finding.
2. ExecuteScalar uses Result.defaultWith + failwith for transaction extraction while ExecuteNonQuery/ExecuteReader use Result pattern matching -- internal inconsistency, but the code path is unreachable (isNone already checked before getTranAndConn is called) and the behavior is correct. Stylistic, not behavioral.
3. DalConnectionStringEnvVarNotFound error case fires for empty config values, not just missing ones -- naming imprecision, but the spec requires only "fail with an error" and does not constrain error names.
