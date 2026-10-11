# code-outward-coverage: App.Utility / App.Operation / App.Session

_No findings._

## Reasoning

Before reporting, I read all 13 files in scope, the audit-conduct catalog and every article it lists, the precedent ledger entries that touch these modules, and the disposition of the last coverage-infrastructure report (Audit/2026-10-04a/99-disposition.md #004 and #005, plus 2026-10-03a #005 and #136). Since 22af7f4 the only change in scope is Calendar.fs (+11 lines: monthEndsBetween). Nothing in App.Operation or App.Session has changed.

What I found, behavior by behavior:

1. Calendar.monthEndsBetween (new, Src/App.Utility/Calendar.fs:17-26). It has two production callers: NetWorthHistory.fs:50 and InvestmentWealthHistory.fs:66. REQ-RPT-9.2 and REQ-RPT-10.2 describe exactly what it does: every month-end on or after the begin date and on or before the end date, in date order. Both callers have tests on the boundaries:
   - a range starting mid-month (NetWorthHistory.fs:72, InvestmentWealthHistory.fs:61)
   - a range starting on a month-end, and one ending mid-month (NetWorthHistory.fs:79)
   - a range inside one month, giving no points (NetWorthHistory.fs:86, InvestmentWealthHistory.fs:68)
   - begin and end on the same month-end (InvestmentWealthHistory.fs:74)
   - February in leap and non-leap years (InvestmentWealthHistory.fs:81)
   It has a REQ and tests, so it is not a finding.

2. Calendar.dateFromInstant and Clock.timeZoneLocal. REQ-SYS-7.1 covers them, and Tests.Integrated/SonOfLeoCli/Configuration.fs:12 tests the late-evening case, where the UTC date differs from the configured zone's date.

3. Clock.instantToString and Calendar.localDateToString. In production they only render file names and report headers or footers. REQ-STG-3.12 and REQ-SYS-3.4 cover those, and the tests are IngestionRoutes.fs:795 and :1017 and ReportRoutes.fs:644 and :691.

4. Clock.now truncates to microseconds. No REQ names this. It is how REQ-SYS-3.2/3.3 and REQ-SYS-5.1 get timestamps that survive a round trip through the database, and the tests that compare stored timestamps with the initiation instant cover it indirectly. Per specs-define-what-not-how, it is an implementation detail, not unspecced behavior.

5. Calendar.today. It has no caller in Src; it is used only in tests. Nothing in production can trigger it, so there is no behavior to cover, and REQ-SYS-3.4 forbids using it in production reads anyway.

6. File.fs: createFullPath, confirmFileExists, confirmDirectoryExists, readTextFileLines, writeTextFile, moveFile.
   - The ingestion route's preconditions are REQ-STG-3.11, tested at StagingIngestionRules.fs:575.
   - The move after commit, and the staged-but-not-moved error, are REQ-STG-3.12, tested at IngestionRoutes.fs:761 and :795.
   - Report-writing callers are reached through the tested report routes.
   - Each function's FileIoError catch branch only rejects loudly with a typed error. Under the no-bubblewrap precedent (10-04a #008, #017), that needs no REQ and no test.

7. Json.fromJson and missingFieldMessage. Both were ruled on last audit. #004 overruled: naming the missing field is message quality and earns no REQ. #005 overruled: the rewrite covers the top-level record only, by design, and the statement was corrected. The code is unchanged since then, so these are exact matches and suppressed. Missing-field tests still exist (PersonRoutes.fs:85, PositionsRoutes.fs:443/832/847, StagingIngestionRules.fs:362). Json.toJson's error branch only rejects loudly.

8. Config.getConfigValue (ConfigNotFound, ConfigReadError). The only production readers are DbConnection.fs:13 (in DAL scope) and Clock.timeZoneLocal. A missing or bad setting throws or fails loudly, and the "Why" note on REQ-SYS-7.1 already relies on that. This is not a coverage gap.

9. FieldUpdate.fs and Result.fs (the ResultBuilder, the list and option converters, valueOrCurrent and so on). These are internal helpers. They are only reached through the tested update and create paths of the domain orchestrators and converters, so the method excludes them.

10. IAppError.AsError. It is an active pattern, and every production use is a translation inside another module's code path (AgreementOrchestration, JournalEntryCommentOrchestration, the JournalEntry converters, AccountRoutes, Account, JournalEntryComponent, DalError, ClassificationRule). Those auditors own them, and here it is a helper only.

11. AuditEnvelope, CoreAuditableAction.FetchOnly and IAuditableAction. Nothing in Src reads AuditEnvelope.action, apart from the TODO for a future audit log (AuditEnvelope.fs:20, which matches the TODO in SystemWide.md §3). The clause in REQ-SYS-3.4 that reads carry FetchOnly was added by 10-03a #005. The test for the auditable action was deleted as tautological under 10-03a #136, which leaves that clause with no observable effect to test. Re-raising it would contradict an exact precedent. AuditEnvelope.instant and Context.getInitiationInstant are covered by the REQ-SYS-3.4 tests (OperationInstantAndAtomicity.fs:323 and :348, IngestionRoutes.fs:1017, ReportRoutes.fs:644 and :691).

12. Context.create. Opening a NewTransaction calls failwith if the transaction cannot be created (Context.fs:18). That is a loud exception at the start of an operation, before any write. REQ-SYS-8.1 already expects operations to roll back on exceptions, so there is no unspecced or silent behavior here. ExistingTransaction and NoTransaction are exercised throughout the integrated suites.

13. UtilityError. Its ToMessage strings only describe the errors above. A message's wording is not a behavior that needs a REQ (#004).

Statement-delta check: Dan's statement makes no claim about these infrastructure modules. The slice-2 change here (monthEndsBetween moved into Calendar for the net worth history) is consistent with "All UI routes and reports are wired in", and the change is tested.
