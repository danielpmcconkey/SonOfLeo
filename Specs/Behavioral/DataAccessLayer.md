# Data Access Layer (DAL)

Generic database functions: connecting, executing queries, parameterization, and architectural constraints.

## 1. Connection string handling

- **REQ-DAL-1.1** stricken
- **REQ-DAL-1.2** stricken
- **REQ-DAL-1.3** If the external configuration file cannot be accessed by the system, all data access functions must fail with an error 
- **REQ-DAL-1.4** stricken
- **REQ-DAL-1.5** stricken
- **REQ-DAL-1.6** stricken
- **REQ-DAL-1.7** stricken
- **REQ-DAL-1.8** stricken
- **REQ-DAL-1.9** stricken
- **REQ-DAL-1.10** stricken
- **REQ-DAL-1.11** stricken
- **REQ-DAL-1.12** stricken
- **REQ-DAL-1.13** stricken
- **REQ-DAL-1.14** All data access functions must fail with an error if the external configuration file is missing an entry named ConnectionStringEnvVar
- **REQ-DAL-1.15** All data access functions must fail with an error if the ConnectionStringEnvVar value is empty
- **REQ-DAL-1.16** All data access functions must fail with an error if the ConnectionStringEnvVar value contains an actual connection string
- **REQ-DAL-1.17** All data access functions must fail with an error if the value of ConnectionStringEnvVar is not the name of a an actual environment variable, resolvable at runtime
- **REQ-DAL-1.18** All data access functions must fail with an error if the resolved value of the ConnectionStringEnvVar environment variable is white-space only
- **REQ-DAL-1.19** The system must trim the final connection string before attempting connection
- **REQ-DAL-1.20** Each build configuration must define a unique ConnectionStringEnvVar value. The env var name used in Debug/Development must differ from the one used in Release/Production.

## 2. Query execution

- **REQ-DAL-2.1** All data inserted into the database must be parameterized in accordance with industry standard best practice to prevent SQL injection
- **REQ-DAL-2.2** All non-scalar queries (set-based read, insert, update, and delete) must verify against expected rows affected
- **REQ-DAL-2.3** All values originating from user input must be parameterized to prevent SQL injection. Values whose type makes injection structurally impossible (e.g. `limit: int option`, where F# enforces the type at compile time) may be interpolated directly.

## 3. Database and data access architecture

- **REQ-DAL-3.1** The DAL must be written to interface with a PostgreSQL 17.9 database
- **REQ-DAL-3.2** The DAL modules must build abstraction layers such that callers of DAL modules need not require any reference to PostgreSQL (preserving the ability to shift RDBMS architecture without upending the entire application).
- **REQ-DAL-3.2.1** An exception to REQ-DAL-3.2 is that client modules can pass non-Ansi-generic SQL strings to the DAL if needed.
- **REQ-DAL-3.2.2** stricken
- **REQ-DAL-3.3** There must be a distinct production database where testing and development activities are not permitted
- **REQ-DAL-3.4** The database must default all character encoding to UTF-8.
- **REQ-DAL-3.5** The database must default collation to "en_US.UTF-8".
- **REQ-DAL-3.6** The system will generally not enforce business logic in the database layer outside of foreign key and unique key constraints. The application layer is responsible for all enforcement of legal data states. Therefore, it should be noted for all database administrators that granting write access to any table within this database should be kept to a minimum. Caveat emptor.
- **REQ-DAL-3.7** The database may never be the originator of temporal values (no use of now() in any defaults, triggers, stored procedures, etc.).


## Waived from testing

Active requirements that are enforced (by type system, code review, schema, or
construction pattern) but deliberately not verified by tests.

| ID | Reason testing is waived | Approved |
|---|---|---|
| REQ-DAL-1.3 | Enforced in code (fails with a typed AppError), but impossible to provoke from the test harness without corrupting the environment | Dan, 2026-08-02 |
| REQ-DAL-1.14 | Enforced in code (fails with a typed AppError), but impossible to provoke from the test harness without corrupting the environment | Dan, 2026-08-02 |
| REQ-DAL-1.15 | Enforced in code (fails with a typed AppError), but impossible to provoke from the test harness without corrupting the environment | Dan, 2026-08-02 |
| REQ-DAL-1.16 | Enforced in code (fails with a typed AppError), but impossible to provoke from the test harness without corrupting the environment | Dan, 2026-08-02 |
| REQ-DAL-1.17 | Enforced in code (fails with a typed AppError), but impossible to provoke from the test harness without corrupting the environment | Dan, 2026-08-02 |
| REQ-DAL-1.18 | Enforced in code (fails with a typed AppError), but impossible to provoke from the test harness without corrupting the environment | Dan, 2026-08-02 |
| REQ-DAL-1.19 | Enforced in code (trim before connection), but impossible to provoke — the test harness always connects with the correctly configured env var | Dan, 2026-08-02 |
| REQ-DAL-1.20 | It's a build-configuration fact, not something we can dynamically test. I've manually verified it works | Dan, 2026-07-06 |
| REQ-DAL-2.1 | Negative existence claim — "all inserted data must be parameterized." Enforced by code review and the parameterization pattern in ExecuteReader/ExecuteScalar/ExecuteNonQuery | Dan, 2026-08-02 |
| REQ-DAL-2.2 | Enforced in code (typed AppError, exercised by DalTests). Behavior proven; waived from REQ-ID citation because the test exercises the mechanism, not the requirement by name | Dan, 2026-08-02 |
| REQ-DAL-2.3 | Negative existence claim — "all user-input values must be parameterized." Enforced by code review and the parameterization pattern | Dan, 2026-08-02 |
| REQ-DAL-3.1 | Architectural fact — every integration test proves the DAL interfaces with PostgreSQL | Dan, 2026-08-02 |
| REQ-DAL-3.2 | Enforced by module structure — callers reference `DataAccessLayer.*` modules, never Npgsql directly. Checked by `check-npgsql.sh` | Dan, 2026-08-02 |
| REQ-DAL-3.4 | Schema/config fact — database created with UTF-8 encoding. Verified by `psql \l` | Dan, 2026-08-02 |
| REQ-DAL-3.5 | Schema/config fact — database created with en_US.UTF-8 collation. Verified by `psql \l` | Dan, 2026-08-02 |
| REQ-DAL-3.7 | It's impossible to test that a behavior isn't present | Dan, 2026-07-06 |

## Unenforceable

Active requirements that bind humans, not code. Nothing in the system enforces these.

| ID | Why it cannot be enforced | Approved |
|---|---|---|
| REQ-DAL-3.2.1 | Policy statement — client modules "can pass non-ANSI-generic SQL if needed." This permits an exception, not a testable constraint | Dan, 2026-08-02 |
| REQ-DAL-3.3 | Operational requirement — "distinct production database where testing is not permitted." Enforced by environment isolation (separate env vars, network restrictions), not by application code | Dan, 2026-08-02 |
| REQ-DAL-3.6 | Policy statement — "generally not enforce business logic in the database layer." Binds database administrators and developers, not code | Dan, 2026-08-02 |

## Withdrawn

| ID | Original Requirement | Reason |
| --- | --- | --- |
| REQ-DAL-1.1 | The environment variable LEOBLOOM_ENV must be in place or all data access functions must fail with an error | LEOBLOOM_ENV eliminated; environment selection moved to build configuration (Debug→Dev, Release→Prod) |
| REQ-DAL-1.2 | The environment variable LEOBLOOM_ENV will be used to determine which external configuration file to use (Production vs Development vs...) | Same as REQ-DAL-1.1 |
| REQ-DAL-1.4 | The external configuration file must define a connection string named "SonOfLeo" that the system must use to connect to the database | Config file now holds an env var name (ConnectionStringEnvVar), not a connection string |
| REQ-DAL-1.5 | If the system cannot access the "SonOfLeo" connection string configuration, all data access functions must fail with an error | rearchitected the connection string process |
| REQ-DAL-1.6 | If the "SonOfLeo" connection string configuration is empty or all white space, all data access functions must fail with an error | rearchitected the connection string process |
| REQ-DAL-1.7 | The environment variable LEOBLOOM_DB_PASSWORD must be in place or all data access functions must fail with an error | rearchitected the connection string process |
| REQ-DAL-1.8 | Any connection string in this system must use a parameter to represent the database password that will only be resolved at runtime when the system will read the password from a configured secret vault or environment variable | rearchitected the connection string process |
| REQ-DAL-1.9 | The system will "inject" the environment variable LEOBLOOM_DB_PASSWORD contents into the final connection string at run-time | rearchitected the connection string process |
| REQ-DAL-1.10 | The system will trim leading and trailing white space from the LEOBLOOM_DB_PASSWORD environment variable | rearchitected the connection string process |
| REQ-DAL-1.11 | If the trimmed LEOBLOOM_DB_PASSWORD environment variable is empty, all data access functions must fail with an error | rearchitected the connection string process |
| REQ-DAL-1.12 | The system will trim leading and trailing white space from the LEOBLOOM_ENV environment variable | rearchitected the connection string process |
| REQ-DAL-1.13 | If the trimmed LEOBLOOM_ENV environment variable is empty, all data access functions must fail with an error | rearchitected the connection string process |
| REQ-DAL-3.2.2 | An exception to REQ-DAL-3.2 is that customer-facing applications (e.g.: SonOfLeoCli) will need to create RDBMS-specific connection strings in their external configurations | Connection strings moved to environment variables; config files no longer hold them |
