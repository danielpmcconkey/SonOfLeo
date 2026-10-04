# spec-quality-DataAccessLayer

## STALE-DAL-1 — stale-reference
- **Location:** Specs/Behavioral/DataAccessLayer.md, Waived table rows REQ-DAL-2.1 (line 65) and REQ-DAL-3.2 (line 68)
- **Summary:** Two waiver reasons name source modules and a check script, which breaks the Specs/README linkage rule, and one of the module names is already out of date.
- **Resolution:** fix-spec

Specs/README.md (Linkage rules) says: "Spec documents **never** name source files, functions, or tests." The REQ-DAL-2.1 waiver reason says "the parameterization pattern in ExecuteReader/ExecuteScalar/ExecuteNonQuery". Those are source module names (Src/App.DataAccessLayer/ExecuteReader.fs, ExecuteScalar.fs, ExecuteNonQuery.fs). The REQ-DAL-3.2 waiver reason says "callers reference `DataAccessLayer.*` modules, never Npgsql directly. Checked by `check-npgsql.sh`". The modules are now `App.DataAccessLayer.*`: DbConnection.fs declares `module App.DataAccessLayer.DbConnection`, and Checks/check-npgsql.sh records the extraction from Utilities.DAL on 2026-07-25. So the waiver already shows the drift the README rule exists to prevent: a module was renamed and the spec now cites a name that does not exist. The 3.2 reason also names the check script `check-npgsql.sh`. The Scope paragraph (line 5) avoids this by calling it "the Npgsql boundary check" without a file name.

**Action:** Rewrite the two waiver reasons in operator and enforcement terms with no module or script names. For example, 2.1: "Enforced by code review and the DAL's parameterized-query pattern"; 3.2: "Enforced by project structure and the Npgsql boundary check".

**Why:** Specs are the most authoritative document type in the repo, and the README keeps code coordinates out of them because coordinates go stale silently. This one already has: `DataAccessLayer.*` no longer exists. An auditor or LLM that checks the waiver against the code will look for a namespace that is not there.

---

## UNENF-DAL-1 — contradiction
- **Location:** Specs/Behavioral/DataAccessLayer.md, Unenforceable table row REQ-DAL-3.3 (line 80); Specs/README.md Requirement anatomy
- **Summary:** REQ-DAL-3.3's unenforceable reason says the requirement is enforced, which contradicts the definition of the Unenforceable state.
- **Resolution:** dan-decides

Specs/README.md defines **Unenforceable** as "nothing in the system enforces it; it binds humans, not code", and **Waived from testing** as "something enforces it (the type system, schema constraints, construction patterns, code review...)". The DAL Unenforceable table's own preamble repeats this: "Nothing in the system enforces these." The REQ-DAL-3.3 row reads: "Operational requirement ... Enforced by environment isolation (separate env vars, network restrictions), not by application code". Separate env vars are part of the system as Definitions.md defines it ("any technology component whose source code or whose configuration exists in the SonOfLeo repository"). Each build configuration's appsettings names a distinct env var (SONOFLEO_DEV_CONNSTR / SONOFLEO_PROD_CONNSTR / SONOFLEO_TEST_CONNSTR), which REQ-DAL-1.20 requires and which the Ui.OperatorCli and Ui.ReportCli fsproj files select by Configuration. So the reason claims an enforcement mechanism, and by the README's definitions that puts the ID in Waived, not Unenforceable. Either the reason is wrong (the requirement really is a human policy that nothing enforces) or the table is wrong.

**Action:** Dan decides which state is true. If env-var separation counts as enforcement, move REQ-DAL-3.3 to the Waived table with that reason. If not, rewrite the reason so it says no system component enforces it, for example "binds operators: nothing stops a human from running tests against the production database".

**Why:** The three-state rule only works if each state's reason matches its definition. An Unenforceable row that claims enforcement makes the classification untrustworthy, and auditors checking waiver soundness cannot tell which claim to test.

---

## AMB-DAL-1.20 — ambiguity
- **Location:** Specs/Behavioral/DataAccessLayer.md REQ-DAL-1.20 (line 28)
- **Summary:** Read literally, "Each build configuration of an interface executable must define a unique ConnectionStringEnvVar value" requires all four (executable, configuration) pairs to differ, but the operator CLI and report CLI deliberately share values.
- **Resolution:** fix-spec

The first sentence makes uniqueness a property of each build configuration of each interface executable, without saying what it must be unique against. The second sentence narrows the scope ("The env var name used in Debug/Development must differ from the one used in Release/Production"), but it is worded as an extra rule, not as the definition of "unique". Repo state: Src/Ui.OperatorCli/appsettings.Development.json and Src/Ui.ReportCli/appsettings.Development.json both set SONOFLEO_DEV_CONNSTR, and both Production files set SONOFLEO_PROD_CONNSTR. DevDataStage/appsettings.json also uses SONOFLEO_DEV_CONNSTR. Under the reading "unique within one executable, across its configurations" the repo complies. Under the reading "unique across every interface executable's configurations" it does not. A developer adding a third interface executable (e.g. a web UI) could reasonably choose either: reuse the dev/prod names, or invent new ones.

**Action:** Reword so uniqueness is clearly per environment, for example: "Within each interface executable (the operator CLI and the report CLI), the Debug/Development build configuration and the Release/Production build configuration name different ConnectionStringEnvVar values. Executables may share a value for the same environment."

**Why:** "Unique" without a comparison set is the kind of word an LLM resolves to its strictest meaning. Today the shared env var is clearly intended, since both CLIs must hit the same database, but the wording makes compliant config look like a violation.

---


