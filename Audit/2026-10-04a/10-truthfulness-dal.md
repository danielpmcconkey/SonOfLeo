# code-truthfulness-dal

## MAINT-DAL-1 — maintainability
- **Location:** Src/App.DataAccessLayer/ExecuteReader.fs:20-28 (confirmNumRows, internal); Src/Business.CrossDomainOrchestration/JournalEntryOrchestration.fs:239-246
- **Summary:** The DAL's row-count rule (confirmNumRows) is `internal`, so JournalEntryOrchestration copies it by hand, and the comment there says it is a copy.
- **Resolution:** fix-code

ExecuteReader.fs:20 declares `let internal confirmNumRows (numRows: int) (expectation: AcceptableExpectedRows)`. This function holds the REQ-DAL-2.2 rule: Zero/ExactlyOne/OneOrMany/AnyQuantityIsAcceptable, with zero rows giving DalNoOp and any other mismatch giving DalResultantRowsDidntMatchExpectation. The journal-entry query in JournalEntryOrchestration.fs:239-246 de-duplicates headers after reading with AnyQuantityIsAcceptable. It then repeats the same match arm for arm, with the comment `// same rule as the DAL's own row check: zero rows where rows were required is DalNoOp`, and builds DalNoOp / DalResultantRowsDidntMatchExpectation itself. The 2026-10-03 DalNoOp split (dal-errors-are-backstops.md, 'What works') had to be made in both places. If the DAL rule changes again (a new AcceptableExpectedRows case, a different zero-row signal), the copy in orchestration has to be found and changed by hand. Nothing would fail if it were missed.

**Action:** Make confirmNumRows public, for example as `AcceptableExpectedRows.confirm` or `confirmNumRows`, and have JournalEntryOrchestration call it on dedupedCount in place of its own copy.

**Why:** There should be one source of truth for an invariant. REQ-DAL-2.2's row-expectation semantics, and the DalNoOp-versus-mismatch split that whenNoRows relies on, now live in two layers, and only the DAL copy is covered by the REQ-DAL-2.2 DalTests.

---

## STALE-DAL-1 — stale-reference
- **Location:** Src/README.md:36 (LookupCache row), Src/README.md:38 (Context row); Src/Ui.InterfaceBridge/CommandRoute.fs:33
- **Summary:** The infrastructure inventory in Src/README.md understates what LookupCache covers, and it and a doc comment name a rollback bracket (`runFuncAndAutoRollback`) that does not exist.
- **Resolution:** fix-code

Src/README.md:36 says LookupCache is for 'Account code ↔ ID and fiscal period key ↔ ID', and its Never column says 'Hand-writing a code-to-ID lookup query. It exists.' The code defines more caches than that. Account.fs:319 has idToName (account_name). MasterAgreement.fs:328-329 has nameToId/idToName over cashflow.master_agreement.agreement_name, and PaymentAgreement.fs:342-343 has the same over payment_agreement_name. Because the README undercounts, a developer working from it could hand-write an agreement-name lookup, which is exactly what its own Never column forbids. Separately, Src/README.md:38 names `Ui.InterfaceBridge.CommandRoute.runFuncAndAutoRollback` as 'the rolled-back bracket', and the doc comment at CommandRoute.fs:33 uses the same name. The function is actually `runCommandRouteAndAutoRollback` (CommandRoute.fs:35). Grep finds no symbol `runFuncAndAutoRollback` in Src or Tests.

**Action:** Change the LookupCache row in Src/README.md:36 to list every cache that exists: account code/name, fiscal period key, master agreement name and payment agreement name. Rename `runFuncAndAutoRollback` to `runCommandRouteAndAutoRollback` in Src/README.md:38 and in the doc comment at CommandRoute.fs:33.

**Why:** Src/README.md is the stated authority for what infrastructure already exists (architecture catalog header). A wrong inventory entry steers new code toward duplicating machinery or looking for a symbol that is not there.

---

## NAME-DAL-1 — maintainability
- **Location:** Src/App.DataAccessLayer/DalError.fs:11,14,54-58; Src/App.DataAccessLayer/DbConnection.fs:15,28 (REQ-DAL-1.14, 1.15, 1.17, 1.18)
- **Summary:** The names and messages of the connection-string errors do not match the conditions that raise them: 'EnvVarNotFound' is raised for an empty setting, and DalEnvVarNotSet claims to cover 'empty' values that go to a different case.
- **Resolution:** fix-code

DbConnection.fs:15 raises `DalConnectionStringEnvVarNotFound` when the ConnectionStringEnvVar setting is null or whitespace, which is the REQ-DAL-1.15 'value is empty' condition. Its ToMessage (DalError.fs:55) correctly says 'is empty'. A setting that is genuinely missing (REQ-DAL-1.14) does not raise the 'NotFound' case: it surfaces as `DalConnectionStringConfigRetrievalError` through the getConfigValue mapError (DbConnection.fs:14). In the other direction, `DalEnvVarNotSet` (REQ-DAL-1.17, DbConnection.fs:28) has the message 'Environment variable {envVarName} not set or empty.' (DalError.fs:58). An env var that resolves to an empty or whitespace value never produces that case. getRawConnectionString returns Ok on any non-null string, and getValidConnectionString then raises `DalConnectionStringIsEmpty` (REQ-DAL-1.18). So one case name describes the wrong condition, and one message claims a condition its case never covers. All five requirements are waived from testing (DataAccessLayer.md Waived table: 'impossible to provoke'), so no test would catch the mismatch. A reader who maps requirements to error cases by name gets 1.14 and 1.15 the wrong way round.

**Action:** Rename DalConnectionStringEnvVarNotFound to describe an empty setting (e.g. DalConnectionStringEnvVarSettingIsEmpty), and drop 'or empty' from the DalEnvVarNotSet message.

**Why:** descriptive-naming.md: names must describe precisely what they mean, so that a reader never has to trace a binding to understand it. These cases are the only enforcement of waived requirements, so their names and messages are the only check that the enforcement is right.

---

