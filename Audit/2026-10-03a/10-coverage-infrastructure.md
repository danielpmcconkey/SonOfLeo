# code-outward-coverage-auditor (App.Operation / App.Session / App.Utility)

## SESSION-1 — contradiction
- **Location:** Src/App.Session/Context.fs:28-32 (updateInitiationInstant); REQ-SYS-3.4
- **Summary:** Context.updateInitiationInstant is a public production function with no production caller; its docstring describes a use (re-stamping the initiation instant partway through a long orchestration) that REQ-SYS-3.4 forbids.
- **Resolution:** dan-decides

Context.fs:28-32 defines `updateInitiationInstant`, documented as 'used for long orchestrated events where you need tasks to show the order of operations through their logging'. It builds a new AuditEnvelope with a fresh Clock.now() instant. grep over Src/, DbMigration/ and DevDataStage/ finds no caller apart from its own definition. Every caller is in Tests: Tests.Helpers/TestDataStage.fs:1044, plus about 20 sites in Reconciliation.fs, FiscalPeriodCreation.fs, RevisedRequirementsCashFlow.fs, ClassificationRuleCrud.fs, JournalEntryVoiding.fs, StageEntryPosting.fs and StageEntryIngestion.fs. The tests use it to simulate separate operations inside one rolled-back transaction. REQ-SYS-3.4 (2026-09-26) says: 'Every operation carries ... a single initiation instant read from the system clock when the operation begins. Every timestamp the operation writes ... uses that instant.' Its Why adds 'One operation, one moment.' The docstring therefore recommends to production developers the exact pattern the spec rules out. The function has existed since a1bc950 (2026-09-27), and Src agents read Src docstrings as guidance. The behavior is unspecced and has no production caller. It exists only as test support placed in the Session tier.

**Action:** Decide whether updateInitiationInstant moves to Tests.Helpers as a test-only helper (preferred, since it has no production caller), or stays in Src with its docstring rewritten so it no longer describes a use REQ-SYS-3.4 forbids.

**Why:** Development is fully agentic. A public Src function whose docstring invites mid-operation re-stamping is the kind of 'right-looking' tool an implementing agent will reach for, and the result would silently break the single-instant guarantee of REQ-SYS-3.4. Tests cover the tests' own use of it, not any production behavior.

---

## OP-1 — contradiction
- **Location:** Src/App.Operation/CoreAuditableAction.fs:6 (FetchOnly); Src/Ui.InterfaceBridge/Routes/*.fs (25 call sites); REQ-SYS-3.4
- **Summary:** REQ-SYS-3.4 requires every operation's auditable action to identify what the operation is, but 25 read route handlers in 7 route modules all carry the same CoreAuditableAction.FetchOnly.
- **Resolution:** dan-decides
- **Prior ruling:** IE-AC-1 (2026-07-06) ruled that AuditEnvelope serves mutations. Re-raised because REQ-SYS-3.4 was written after that ruling (2026-09-26) and its text covers every operation, so the ruling no longer matches the current spec.

REQ-SYS-3.4: 'Every operation carries an auditable action identifying what the operation is ...'. CoreAuditableAction has one case, FetchOnly. `Context.create NoTransaction FetchOnly` appears in AccountRoutes (6), ReportRoutes (5: trialBalance, prePostingReview, reconciliation, balanceSheetIntegrity, periodActivity), JournalEntryRoutes (5), ClassificationRoutes (4), FiscalPeriodRoutes (2), CashFlowRoutes (2: projectCashFlow, fetchAgreementSummary) and IngestionRoutes (1: fetchStageEntryFiltered). Because all of these share one action, their envelopes cannot be told apart. The only REQ-SYS-3.4 action test (OperationInstantAndAtomicity.fs:231, 'every operation run through the interface's command runner carries an auditable action identifying that operation, and two different operations carry different actions') checks only two write operations (JournalEntryPostNew vs JournalEntryVoid). No test covers a read operation's action. The prior ruling IE-AC-1 (2026-07-06) said 'AuditEnvelope is for mutations', but REQ-SYS-3.4 was written later (2026-09-26) and says 'every operation' with no read exemption.

**Action:** Either amend REQ-SYS-3.4 to exempt read-only operations from the identifying-action clause (for example, 'read-only operations may carry a generic fetch action'), or give each read route its own auditable action case.

**Why:** The spec makes a universal claim that the code meets only for writes, and the test that cites the REQ checks only writes, so the gap is invisible to traceability. When the planned external audit log (SystemWide.md:32 todo) arrives, every read will log as an indistinguishable 'FetchOnly'.

---

## CFG-1 — missing-requirement
- **Location:** Src/App.Utility/Config.fs:9-14 (AddEnvironmentVariables); REQ-DAL-1.14, REQ-DAL-1.20, REQ-SYS-7.1
- **Summary:** Config layers process environment variables over appsettings.json, so an env var named ConnectionStringEnvVar or LocalizedTimeZone silently overrides the build-configuration file; no REQ describes this and no test covers it.
- **Resolution:** dan-decides

Config.fs:10-14 builds the configuration as `.AddJsonFile("appsettings.json", optional = false).AddEnvironmentVariables()` with no prefix. Both settings the system reads through getConfigValue are therefore overridable from the process environment: "ConnectionStringEnvVar" (DbConnection.fs:13) and "LocalizedTimeZone" (Clock.fs:10). This conflicts with the configuration REQs. REQ-DAL-1.14 says data access must fail 'if the external configuration file is missing an entry named ConnectionStringEnvVar', but with the env provider a missing file entry plus a same-named env var succeeds (REQ-DAL-1.14 is waived as un-provokable, so no test would catch this). REQ-DAL-1.20 says each build configuration defines a unique ConnectionStringEnvVar so Debug/Development and Release/Production differ; Src/Ui.OperatorCli/appsettings.{Development,Production}.json carry SONOFLEO_DEV_CONNSTR and SONOFLEO_PROD_CONNSTR. An exported ConnectionStringEnvVar overrides both, so a Debug build can be pointed at the prod connection-string variable with no error. REQ-SYS-7.1 promises 'one configured local time zone, system-wide', and a stray LocalizedTimeZone env var can change it per process. No REQ in Specs/Behavioral states that environment variables override the configuration file, and grep of Tests/ finds no test of this precedence.

**Action:** Decide whether env-var override of the config file is intended. If it is, add a REQ (DAL §1) stating it and reconcile REQ-DAL-1.14 and 1.20 with it. If it is not, record that the config file is the sole source of these settings so the code can drop AddEnvironmentVariables().

**Why:** The DAL connection-string REQs exist to keep dev and prod apart by build configuration. An unspecced, untested precedence rule that bypasses that separation is exactly the kind of behavior a code-outward audit exists to surface.

---

## CLK-1 — stale-ruling
- **Location:** Src/App.Utility/Calendar.fs:11 (today), Src/App.Utility/Clock.fs:19 (now) as used at Src/Ui.InterfaceBridge/Routes/ReportRoutes.fs:45, Src/Ui.InterfaceBridge/ReportVisualizationAssets/ReportFooter.fs:7, Src/Ui.InterfaceBridge/Routes/IngestionRoutes.fs:89; REQ-SYS-3.4
- **Summary:** Three production operations derive their 'current date' or a timestamp from a fresh Calendar.today()/Clock.now() read instead of the operation's initiation instant, which REQ-SYS-3.4 requires; the ruling that allowed this predates REQ-SYS-3.4.
- **Resolution:** dan-decides
- **Prior ruling:** IE-AC-1 (2026-07-06): 'Reads use Calendar.today() ... by design.' The point is the same, but the ruling predates REQ-SYS-3.4 (2026-09-26), which covers 'every operation'. The ingestion file-name timestamp (a write operation) is outside that ruling's read-only scope in any case. SYS-CLK-1 concerns the wording of REQ-SYS-3.3, not this.

Calendar.today() and Clock.now() are public and read the system clock directly, outside any Context. Three production call sites use them as operation-derived values. (1) ReportRoutes.fs:45: prePostingReview builds `context` at line 36 but passes `Calendar.today()` as the report's run date, which becomes the report title and the interpolated file name (PrePostingReviewWriter.fs:152, :173). (2) ReportFooter.fs:7: every HTML report's 'Generated:' stamp is a fresh Clock.now(). (3) IngestionRoutes.fs:89: the processed-file prefix that REQ-STG-3.12 calls 'the ingestion timestamp' is a fresh Clock.now() taken after commit, not the operation's instant. Elsewhere the code takes the date from the context, e.g. IngestionRoutes.fs:157 `context |> Context.getInitiationInstant |> Calendar.dateFromInstant`, CashFlowOps.fs:34 and AccountCreation.fs:108. REQ-SYS-3.4: 'Every timestamp the operation writes, and every "current date" it derives ..., uses that instant.' Its Why says 'date-dependent logic cannot straddle midnight partway through a run'. A pre-posting review run just before midnight can now title itself with a date different from its footer timestamp. No test asserts which instant these values come from.

**Action:** Decide whether REQ-SYS-3.4 binds report run dates, report footers and the ingestion file-name timestamp. If it does, they should come from the context's initiation instant. If it does not, amend REQ-SYS-3.4 (or re-affirm IE-AC-1) to exempt read and presentation timestamps explicitly.

**Why:** The public clock functions make a second, competing 'now' easy to reach. REQ-SYS-3.4 was written to remove that, and an older ruling is the only thing excusing these call sites.

---

