# DataAccessLayer.md spec-quality auditor

## DAL-SCOPE-MIGRATOR — ambiguity
- **Location:** Specs/Behavioral/DataAccessLayer.md REQ-DAL-1.14..1.20, REQ-DAL-2.2, REQ-DAL-3.2; DbMigration/Migrator/Program.fs; DbMigration/Migrator/appsettings.json; Specs/Definitions.md 'The system'
- **Summary:** DataAccessLayer.md does not say whether its requirements bind the new DB Migrator, which is part of 'the system' under Definitions.md but does its own data access outside App.DataAccessLayer and breaks several DAL requirements as written.
- **Resolution:** dan-decides

Definitions.md says 'The system' is 'Any technology component whose source code or whose configuration exists in the SonOfLeo repository', so DbMigration/Migrator counts. The Migrator opens connections with Npgsql directly (Program.fs lines 4, 132, 153) and does not use App.DataAccessLayer. (1) It never reads a ConnectionStringEnvVar. Its appsettings.json holds literal RDBMS connection strings ('Host=localhost;Database=sonofleo_prod;Username=sonofleo_migrator', etc.) and adds a password typed in at a prompt. Read literally, REQ-DAL-1.14..1.18 ('All data access functions must fail ... ConnectionStringEnvVar ...') and REQ-DAL-1.16 (reject a value that contains an actual connection string) do not hold for the Migrator. (2) It picks the environment from an interactive menu (dev/test/prod), not from build configuration, so REQ-DAL-1.20 does not describe how it chooses an environment. (3) registerFile and recordHistory run INSERTs with `cmd.ExecuteNonQuery() |> ignore` (Program.fs lines ~74, ~84), so they do not check rows affected as REQ-DAL-2.2 requires for 'All non-scalar queries (set-based read, insert, update, and delete)'. (4) Checks/check-npgsql.sh, the enforcement named in the REQ-DAL-3.2 waiver, scans only Src and Tests, so the Migrator is outside it without saying so. Two reasonable developers would disagree on whether the Migrator must conform. One reads the section headings ('Connection string handling', 'Query execution') and 'All data access functions' as system-wide. The other reads them as scoped to the App.DataAccessLayer project. This scope question did not exist at the last audit; Dan's statement introduces the migrator as new this slice.

**Action:** Dan decides whether the DAL spec covers the Migrator. Then add a scope sentence under the DataAccessLayer.md title, e.g. 'These requirements govern the App.DataAccessLayer project and its callers; the DbMigration tool is out of scope', or state which REQ-DAL items the Migrator must meet.

**Why:** The development process is now fully agentic, and the spec is the only signal an implementing agent gets. When scope is unstated, a future agent will either 'fix' the Migrator to use ConnectionStringEnvVar and rows-affected checks, which may not be wanted, or copy its direct-Npgsql pattern into new tooling and point to the Migrator as precedent.

---

## DAL-WD-3.2.2-STALE — stale-reference
- **Location:** Specs/Behavioral/DataAccessLayer.md Withdrawn table, REQ-DAL-3.2.2; DbMigration/Migrator/appsettings.json
- **Summary:** REQ-DAL-3.2.2 was withdrawn because 'config files no longer hold [connection strings]', but the Migrator's appsettings.json now holds RDBMS-specific connection strings for dev, test, prod and the migration database.
- **Resolution:** dan-decides

Withdrawn-table reason for REQ-DAL-3.2.2: 'Connection strings moved to environment variables; config files no longer hold them.' The original requirement allowed customer-facing apps to put RDBMS-specific connection strings in their external configuration. DbMigration/Migrator/appsettings.json now contains 'Connections': { dev/test/prod: 'Host=localhost;Database=sonofleo_*;Username=sonofleo_migrator' } and 'MigrationDb': 'Host=localhost;Database=sonofleo_migration;...'. The withdrawal's stated factual premise is therefore false in the current repo. This contradicts Dan's statement that the migrator was added as part of this slice. Either the withdrawal reason needs updating, for example by limiting it to the runtime interfaces, or the exception needs reinstating for the migration tool.

**Action:** Amend the REQ-DAL-3.2.2 withdrawal reason to say which components it covers, e.g. 'runtime interfaces (Ui.*) take connection strings from environment variables', and note that the DbMigration tool keeps passwordless connection strings in its config. Or Dan reinstates a scoped exception.

**Why:** Withdrawn-table reasons are the audit trail for why a guarantee no longer exists. A reason that the repo contradicts teaches later readers, both agents and auditors, a false fact about where connection strings live.

---

## DAL-WAIVE-2.2-SELF-CONTRADICT — contradiction
- **Location:** Specs/Behavioral/DataAccessLayer.md Waived table, REQ-DAL-2.2; Tests/Tests.Integrated/DataAccessLayer/DalTests.fs lines 87-199
- **Summary:** The REQ-DAL-2.2 waiver says the behavior is 'exercised by DalTests' and is waived only from 'REQ-ID citation'. That contradicts the Waived table's own definition: requirements 'deliberately not verified by tests'.
- **Resolution:** dan-decides

The Waived table's preamble says: 'Active requirements that are enforced ... but deliberately not verified by tests.' The REQ-DAL-2.2 row says: 'Enforced in code (typed AppError, exercised by DalTests). Behavior proven; waived from REQ-ID citation because the test exercises the mechanism, not the requirement by name.' DalTests.fs verifies rows-affected checking directly: the Theory case 'DalResultantRowsDidntMatchExpectation' (errorRowCount), 'a read requiring exactly one row that finds none returns DalNoOp' (line ~168), and 'an update requiring exactly one row that touches none returns DalNoOp' (line ~178). By the Specs/README three-state rule, a requirement whose behavior a test verifies should be in the tested state, linked by test name. The waiver exists only because the tests lack the REQ prefix. That is a naming gap, not a reason testing cannot or should not happen. The waiver also means check-traceability cannot detect it if those tests are later deleted.

**Action:** Rename the DalTests rows-affected tests to begin with 'REQ-DAL-2.2' and remove REQ-DAL-2.2 from the Waived table (Dan approves removing the waiver).

**Why:** The three-state rule only gives assurance when 'waived' means 'no test verifies this'. A waiver that covers an existing, uncited test makes the traceability check report less coverage than exists, and it lets the protecting tests be deleted without the gate noticing. Since the audit is Dan's main backstop for agent-written code, this matters.

---

## DAL-WD-1.3-RATIONALE — contradiction
- **Location:** Specs/Behavioral/DataAccessLayer.md Withdrawn table REQ-DAL-1.3 vs REQ-DAL-1.14..1.18 and their waivers; Src/App.DataAccessLayer/DbConnection.fs; Src/App.Utility/Config.fs; Src/Ui.InterfaceBridge/Startup.fs lines 5-6
- **Summary:** REQ-DAL-1.3's withdrawal says a bad configuration makes 'the program already stop[] with an exception', but the active REQ-DAL-1.14..1.18 and their waivers in the same file say a missing or invalid setting fails with a typed AppError.
- **Resolution:** fix-spec

REQ-DAL-1.3 (withdrawn 2026-09-28) covered 'the external configuration file cannot be read, or a required setting in it is missing or invalid'. Its withdrawal reason: '...the program already stops with an exception naming what is missing ... a loud crash is the house pattern.' The same file still has REQ-DAL-1.14..1.18 ('must fail with an error' when ConnectionStringEnvVar is missing, empty, a connection string, unresolvable, or whitespace), and their waivers all say 'Enforced in code (fails with a typed AppError)'. The code matches the waivers, not the withdrawal reason. DbConnection.fs returns Error DalConnectionStringConfigRetrievalError / DalConnectionStringEnvVarNotFound / DalConnectionStringEnvVarContainsConnectionString / DalEnvVarNotSet / DalConnectionStringIsEmpty. Config.fs wraps reads in try/with and returns ConfigReadError. Startup.fs routes typed errors to stderr with exit code 1. So for the 'required setting missing or invalid' half of 1.3, the system does what 1.3 required (message, non-zero exit), and the withdrawal reason describes behavior the spec's own active requirements forbid. No coverage gap results, because 1.14..1.18 still hold, but the file contradicts itself about how configuration failure surfaces.

**Action:** Reword the REQ-DAL-1.3 withdrawal reason so it does not claim an exception for missing or invalid settings, e.g. 'Missing/invalid ConnectionStringEnvVar is covered by REQ-DAL-1.14..1.18 (typed error); a missing configuration file needs no separate guarantee because it cannot fail silently.'

**Why:** An agent reading the withdrawal could conclude that throwing on a bad setting is the sanctioned house pattern and 'simplify' the typed-error path. That would break REQ-DAL-1.14..1.18, which are waived from testing, so no test would catch the regression.

---

## DAL-1.20-SCOPE — ambiguity
- **Location:** Specs/Behavioral/DataAccessLayer.md REQ-DAL-1.20; Tests/Tests.Integrated/appsettings.json; DevDataStage/appsettings.json; DevDataStage/DevDataStage.fsproj; Src/Ui.OperatorCli/Ui.OperatorCli.fsproj lines 17-18
- **Summary:** REQ-DAL-1.20 requires 'Each build configuration' to use a different ConnectionStringEnvVar for Debug and Release, but does not say which projects this covers. Tests.Integrated and DevDataStage ship one appsettings.json with the same value under every build configuration.
- **Resolution:** dan-decides

REQ-DAL-1.20: 'Each build configuration must define a unique ConnectionStringEnvVar value. The env var name used in Debug/Development must differ from the one used in Release/Production.' Ui.OperatorCli and Ui.ReportCli meet this by copying appsettings.Development.json or appsettings.Production.json, conditioned on $(Configuration). Tests.Integrated (SONOFLEO_TEST_CONNSTR) and DevDataStage (SONOFLEO_DEV_CONNSTR) each include a single unconditional appsettings.json, so their Debug and Release builds use the same env var name. That literally breaks 'Debug must differ from Release'. A third environment, test, exists and is not mentioned at all. The waiver ('build-configuration fact ... manually verified', 2026-07-06) predates DevDataStage and the test-role split. Two developers adding a new executable project would read the rule differently: one would add Development/Production config pairs to every project, the other only to operator-facing interfaces.

**Action:** Scope REQ-DAL-1.20 to the interface executables (Ui.OperatorCli, Ui.ReportCli), or restate it as 'no non-production build resolves the production env var name', and recognize the test environment.

**Why:** The requirement exists to keep non-production work from touching the production database (see REQ-DAL-3.3). If its scope is unclear, agents will apply it inconsistently to new projects, and a waived requirement has no test to catch a drift.

---

## DAL-RULING-EFFICACY-STALE — stale-ruling
- **Location:** Skills/SonOfLeoRequirementsAudit/resolved-findings.md 'DAL-EFFICACY' (2026-08-20/22); Specs/Behavioral/DataAccessLayer.md REQ-DAL-2.4; Tests/Tests.Integrated/DataAccessLayer/DalTests.fs lines 266-311
- **Summary:** The DAL-EFFICACY ruling rests on 'all 19 [REQ-DAL] are either waived ... or unenforceable', but REQ-DAL-2.4 (added 2026-09-26) is now a tested requirement with four citing tests plus ConnectionLeakGuard, so the ruling's premise no longer holds.
- **Resolution:** dan-decides

DAL-EFFICACY says: 'all 19 are either waived from testing or classified as unenforceable ... A test-efficacy auditor scoped to the DAL will always return "no findings" because there are no tested REQ IDs to audit against ... Do not flag the absence of DAL-specific efficacy findings.' The active count is still 19, but the mix has changed. REQ-DAL-2.4 is in neither table and is cited by four DalTests facts ('REQ-DAL-2.4 after every lookup cache has loaded...', '...typed error mid-transaction rolls back...', '...throws mid-transaction rolls back...', '...more failing operations than the pool holds connections never exhausts the pool') and by Tests/Tests.Integrated/ConnectionLeakGuard.fs. If the ruling is applied as written, a DAL test-efficacy pass would skip REQ-DAL-2.4's tests, even though they are now the only REQ-cited tests in the domain.

**Action:** Revise or retire the DAL-EFFICACY ruling so that REQ-DAL-2.4's tests fall within test-efficacy audit scope.

**Why:** The precedent ledger suppresses findings. A ruling whose factual premise has changed will quietly exempt the newest and most behavior-heavy DAL tests (transaction and connection release on every path) from efficacy review.

---


