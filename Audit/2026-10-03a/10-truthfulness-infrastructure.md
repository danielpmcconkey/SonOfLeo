# code-truthfulness-auditor: App.Utility / App.Operation / App.Session

## SYS34-CLOCK-1 — contradiction
- **Location:** Src/App.Utility/Calendar.fs:11 (today), Src/App.Utility/Clock.fs:19 (now); callers Src/Ui.InterfaceBridge/Routes/ReportRoutes.fs:45, Src/Ui.InterfaceBridge/Routes/IngestionRoutes.fs:89, Src/Ui.InterfaceBridge/ReportVisualizationAssets/ReportFooter.fs:7; REQ-SYS-3.4, REQ-RPT-7.7, REQ-STG-3.12
- **Summary:** Two operations read the clock again partway through instead of using their context's initiation instant, which REQ-SYS-3.4 requires: the pre-posting review run date and the ingestion processed-file timestamp.
- **Resolution:** fix-code
- **Prior ruling:** IE-AC-1 (2026-07-06) ruled that reads use Calendar.today() rather than the AuditEnvelope instant. REQ-SYS-3.4 was written later (2026-09-26). It explicitly covers derived 'current date' values on read paths, giving 'the reference date for account activity' as its example, which is exactly the IE-AC-1 case. The ruling predates the requirement and is superseded by higher authority (Specs/Behavioral > precedent). The ingestion filename timestamp is a write-side timestamp that IE-AC-1 never covered.

REQ-SYS-3.4 (2026-09-26) says every operation reads one initiation instant from the clock when it begins. Every timestamp the operation writes, and every 'current date' it derives, must use that instant. App.Session.Context.getInitiationInstant exposes it, and about 35 Src call sites use it, including read paths (CashFlowOps.fs:34 and :701, CashFlowCompositeFetcher.fs:69, Account.fs:214, IngestionRoutes.fs:157). Two route handlers that already hold a context call the scope's free clock functions instead. (1) ReportRoutes.prePostingReview builds `context = Context.create NoTransaction FetchOnly`, fetches with it, then passes `Calendar.today()` as the run date to PrePostingReviewWriter.write. REQ-RPT-7.7 makes that date the title, the header date and the interpolated filename date. (2) IngestionRoutes (ingest handler) stages entries under `context`, so their status transition and created_at use the initiation instant. It then builds the processed-file prefix from a fresh `Clock.now()` (line 89), which REQ-STG-3.12 calls 'the ingestion timestamp'. Failure scenario: an ingestion that starts at 23:59:59.9 local time and finishes after midnight records its entries on day D, while the processed file name is stamped day D+1. The pre-posting review behaves the same way: it fetches before midnight and its header and filename carry the next day's date. ReportFooter.createReportFooter (line 7) also calls Clock.now() for every report's 'Generated' instant (REQ-RPT-3.2). That is a timestamp the operation writes into its output file, though whether REQ-SYS-3.4 reaches report-file contents is less clear-cut. check-clock bans only raw DateTime/SystemClock APIs outside Clock.fs/Calendar.fs. Nothing stops code from calling Clock.now()/Calendar.today() during an operation, so REQ-SYS-3.4 depends entirely on review here.

**Action:** Derive the pre-posting run date as `context |> Context.getInitiationInstant |> Calendar.dateFromInstant`, and build the ingestion file prefix from `Context.getInitiationInstant context`. Optionally, have the report footer take the instant as a parameter and extend check-clock to flag Clock.now()/Calendar.today() outside App.Operation/AuditEnvelope.fs.

**Why:** REQ-SYS-3.4 exists so that date-dependent output cannot straddle midnight within one run. This file prefix is the only artifact tying a processed FI file to its staged batch, and the pre-posting review run date is the Saturday-process report header. Both can silently disagree with the data they describe.

---

## SYS34-CTX-1 — contradiction
- **Location:** Src/App.Session/Context.fs:28-32 (updateInitiationInstant); REQ-SYS-3.4
- **Summary:** Context.updateInitiationInstant hands an in-flight operation a new initiation instant. Its doc comment describes that as the intended use, which REQ-SYS-3.4 forbids. Nothing in Src or Tests calls it.
- **Resolution:** fix-code

updateInitiationInstant replaces the context's AuditEnvelope with AuditEnvelope.create on the same action. That calls Clock.now() again (AuditEnvelope.fs:24), so the operation gets a new instant and a new uniqueId while keeping the same DbTransaction. The doc comment says it 'is used for long orchestrated events where you need tasks to show the order of operations through their logging'. REQ-SYS-3.4 says each operation has 'a single initiation instant ... Every timestamp the operation writes ... uses that instant', and its Why is 'One operation, one moment. A batch that writes hundreds of rows records them as happening together.' A grep of Src and Tests finds only the definition (Context.fs:30). The function is dead code, and following its documented purpose violates a behavioral requirement. Because development is fully agentic, a documented helper in the session tier is an open invitation. A future long-running Saturday state-machine step that calls it between batches would stamp rows from one transaction with several instants. It could also produce the same-entry transition collisions that REQ-STG-4.1.2's constraint exists to catch, or break the 'records them as happening together' guarantee.

**Action:** Delete Context.updateInitiationInstant. If Dan wants sub-step ordering in a future audit log, specify it first as a separate field that leaves the initiation instant unchanged.

**Why:** Code that the spec forbids using as documented should not exist in the lowest shared tier. Agent developers treat existing helpers as sanctioned patterns.

---

## LRN-TEMPORAL-1 — contradiction
- **Location:** CompoundedLearnings/articles/coding/temporal-arithmetic.md (Instant-to-date conversion section); REQ-SYS-3.4, REQ-SYS-7.1; Src/App.Utility/Calendar.fs, Clock.fs:9-12
- **Summary:** The temporal-arithmetic learning tells developers to centralize current-date derivation through Calendar.today() and to anchor to US Eastern Time. REQ-SYS-3.4 instead requires the operation's initiation instant, and REQ-SYS-7.1 requires one configured time zone.
- **Resolution:** fix-spec

The article says: 'Centralize through the Calendar module's `today()` function (or equivalent)' and 'Anchor to US Eastern Time (NYC)'. REQ-SYS-3.4 (2026-09-26) requires every current date an operation derives to come from its initiation instant. That means Calendar.dateFromInstant(Context.getInitiationInstant ctx), which is what the codebase does in about 10 places. Calendar.today() reads the clock fresh, so following the learning produces exactly the violation in SYS34-CLOCK-1 (ReportRoutes.fs:45 calls Calendar.today()). REQ-SYS-7.1 (revised 2026-09-28) says conversion uses 'one configured local time zone'. Clock.timeZoneLocal reads the config key 'LocalizedTimeZone', not a hard-coded NYC zone. The learning is lower authority than both requirements and contradicts them, and it is guidance that the implementing agent (SonOfLeoSrcDeveloper) reads.

**Action:** Amend temporal-arithmetic.md: derive current dates with `Context.getInitiationInstant >> Calendar.dateFromInstant` (REQ-SYS-3.4), keep Calendar.today() out of operation code, and describe the zone as the configured LocalizedTimeZone (REQ-SYS-7.1).

**Why:** In a fully agentic workflow, CompoundedLearnings is how coding practice reaches the developer agent. A learning that contradicts a behavioral requirement will keep producing spec violations that tests are unlikely to catch, because they only show up near midnight.

---

## STALE-RULING-TZ-1 — stale-ruling
- **Location:** Skills/SonOfLeoRequirementsAudit/resolved-findings.md (MAINT-TZ-1, IE-AC-1); Src/App.Utility/Calendar.fs:9-11
- **Summary:** The facts behind two precedent rulings are no longer true. MAINT-TZ-1 says Calendar and Clock have independent time-zone bindings and no dependency on each other, but Calendar now uses Clock.timeZoneLocal and Clock.now. IE-AC-1 is superseded by REQ-SYS-3.4.
- **Resolution:** dan-decides

MAINT-TZ-1 (2026-08-25) rules that 'Each reads the configured time zone independently ... Coupling them to share a single binding would create a dependency between modules that currently have none.' Today Calendar.fs:9 is `i.InZone(Clock.timeZoneLocal).Date` and Calendar.fs:11 is `Clock.now() |> dateFromInstant`. Calendar depends on Clock, and there is one binding (Clock.fs:9-12). Calendar.fs also still opens UtilityError and Config, which it no longer uses, a leftover of the old independent binding. IE-AC-1 (2026-07-06) says reads use Calendar.today() 'through US Eastern Time'. REQ-SYS-3.4 (2026-09-26) now requires derived current dates to come from the initiation instant, and the zone is configured (REQ-SYS-7.1). An auditor applying either ruling as written would suppress valid findings, or would treat the current code as a regression from an 'intentional' design that was in fact reversed.

**Action:** Mark MAINT-TZ-1 as moot (the duplication it defended no longer exists) and mark IE-AC-1 as superseded by REQ-SYS-3.4.

**Why:** The precedent ledger suppresses findings. A ruling whose premise is false steers later audits wrong, which matters more now that this audit is Dan's primary back-stop.

---

## CHK-APPERR-1 — stale-reference
- **Location:** Checks/check-apperror-coverage.sh (awk on Src/Utilities/AppError.fs); Src/App.Utility/IAppError.fs
- **Summary:** check-apperror-coverage still parses the retired monolithic Src/Utilities/AppError.fs. After the IAppError breakout it covers 0 of 0 error cases and passes vacuously.
- **Resolution:** fix-code

The script runs `awk '/^type AppError/ ...' Src/Utilities/AppError.fs`. That file no longer exists. Errors are now domain-specific DUs implementing App.Utility.IAppError.IAppError (UtilityError.fs, DalError, LedgerError, BizGeneralError, BizFinServError, DataIngestionError, CashFlowError, BridgeError, OperatorCliError, ReportCliError). run-all.sh prints 'awk: cannot open "Src/Utilities/AppError.fs"' followed by 'AppError coverage: 0/0' and PASS. Dan's statement says he 'broke out the monolithic error ... into domain-specific implementations of an interface'. The refactor happened, but the check that tracked error-case test coverage was not migrated. It now reports green while measuring nothing, for example whether UtilityError.FileIoDirectoryDoesntExist or any CashFlowError case is ever asserted.

**Action:** Rewrite the check to collect union cases from every `interface IAppError with` type under Src/, or retire it explicitly. In either case make it fail rather than pass when its input file is missing.

**Why:** Dan's assurance in the agentic workflow rests on the checks. A check that silently passes on missing input looks like coverage that does not exist.

---


