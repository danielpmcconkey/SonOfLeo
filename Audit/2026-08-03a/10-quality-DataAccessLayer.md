# DAL Spec Auditor

## STALE-DAL-1 — stale-reference
- **Location:** Specs/Behavioral/DataAccessLayer.md, REQ-DAL-1.15 waiver row
- **Summary:** REQ-DAL-1.15 waiver cites the wrong AppError case as its enforcement mechanism.
- **Resolution:** fix-spec

The waiver for REQ-DAL-1.15 states: "Enforced in code (AppError case `DalConnectionStringIsEmpty`)." However, when the ConnectionStringEnvVar config value is empty, the actual code path in DbConnections.fs (line 18-19) returns `DalConnectionStringEnvVarNotFound`, not `DalConnectionStringIsEmpty`. The `DalConnectionStringIsEmpty` error is returned by `getValidConnectionString` (line 38-39), which handles the RESOLVED environment variable value -- that is REQ-DAL-1.18's scope, not REQ-DAL-1.15's. Both REQ-DAL-1.14 (entry missing) and REQ-DAL-1.15 (value empty) are enforced by the same code path and same error case (`DalConnectionStringEnvVarNotFound`) because `config["ConnectionStringEnvVar"]` returns null when missing and `String.IsNullOrWhiteSpace` catches both null and empty. The behavioral enforcement is correct -- the system does fail with an error -- but the waiver's documentation of which error case enforces it is factually wrong.

**Action:** Update the REQ-DAL-1.15 waiver row to cite `DalConnectionStringEnvVarNotFound` instead of `DalConnectionStringIsEmpty`.

**Why:** The waiver table is the system of record for how non-tested requirements are enforced. An auditor verifying the waiver's soundness by tracing `DalConnectionStringIsEmpty` through the config-value code path would not find it, potentially concluding the requirement is unenforced when it is not.



