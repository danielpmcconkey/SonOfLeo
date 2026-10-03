# test-efficacy-NGUI

## EFF-NGUI-1 — test-gap
- **Location:** REQ-NGUI-4.5; Tests/Tests.Integrated/InterfaceBridge/ReportRoutes.fs:200-206; Tests/Tests.Helpers/RouteResolver.fs:32-39; Src/Ui.ReportCli/Program.fs:7-10
- **Summary:** The only typed-error test for REQ-NGUI-4.5 checks an error that the test helper creates itself. It never runs the production Reports CLI dispatch.
- **Resolution:** fix-test

`REQ-NGUI-4.5 unknown report name fails with typed error` asserts `isCorrectError (routeReportingCommandForTesting "BogusReport" [] "{}") ReportingUnknownReportName None`. `routeReportingCommandForTesting` (RouteResolver.fs:32-39) is a copy of `route` in Src/Ui.ReportCli/Program.fs. Its own `| None -> Error(ReportingUnknownReportName name)` arm (line 39) creates the very case the test then asserts. The test only proves that the helper's None arm returns what the helper's None arm returns. Change production Program.fs to return a different error, or to throw, and this test stays green. This is Specimen 6, applied to the error instead of an expected value: the expected outcome comes from the code that produces the actual outcome. The process-level test Reports/Program.fs:104 does reach production code, but it checks an exact stderr string, not a typed case. So the 'typed error' part of REQ-NGUI-4.5 has no test that can fail on a production defect. RouteResolver.fs:19-30 copies the main CLI in the same way (its own `commandRoutes` list and `CliUnknownCommand` arm). REQ-NGUI-3.9 is spared only because its only citing test (SonOfLeoCli/Program.fs:103) runs the real process.

**Action:** Delete ReportRoutes.fs:200-206, or rewrite it so it cannot pass without production code. For example: move `route` from Ui.ReportCli/Program.fs (and Ui.OperatorCli/Program.fs) into a module the tests can call, and assert the typed case on that function rather than on the RouteResolver copy.

**Why:** A test whose expected error comes from the test's own helper can never fail on a production defect. It counts as coverage for REQ-NGUI-4.5 in the traceability audit while checking nothing. In an agentic workflow where Dan does not review tests, this kind of false green is what the audit exists to catch.

---

## EFF-NGUI-2 — test-gap
- **Location:** REQ-NGUI-1.3.1, REQ-NGUI-4.4 (withdrawn REQ-NGUI-1.3.2); Tests/Tests.Integrated/Reports/Program.fs:52-76 (assertions at 71-72)
- **Summary:** On 2026-10-03 the Reports stderr test was weakened from full-message equality to first-line equality, and it now asserts a stack trace is present, which is behaviour Dan withdrew on 2026-09-28.
- **Resolution:** fix-test

Commit 09ee0f4 (2026-10-03) replaced `Assert.Equal($"{intendedError.ToMessage()}{Environment.NewLine}", e)` with `Assert.Equal(expectedFirstLine, firstLine e)` and `Assert.Matches(@"(?m)^\s+at \S", e)`. Two problems. (a) REQ-NGUI-1.3.1 says the error payload comprises the error message, but now only the first line of stderr is compared. Anything after it (truncated, duplicated, or a different trace) passes as long as some line starts with `at `. (b) The new `Assert.Matches` pins a stack trace in a typed error's stderr. That is the content of REQ-NGUI-1.3.2 ('the error payload will additionally include the full stack trace'), which Dan withdrew on 2026-09-28. The trace only appears because UtilityError.FileIoError.ToMessage (Src/App.Utility/UtilityError.fs:24) puts `ex.StackTrace` into the message, so no active REQ backs the assertion. It also sits awkwardly next to StartupErrorReporting.fs:34, whose name promises 'an error payload consisting of that error's message and nothing else, with no stack trace'.

**Action:** Remove the `Assert.Matches` stack-frame assertion, which cites no active REQ. Then either compare the full message again, so all of it is checked (e.g. normalise or strip only the runtime frame lines on both sides), or have Dan decide whether FileIoError's message should contain a stack trace at all.

**Why:** An assertion on behaviour whose requirement was withdrawn keeps that requirement alive in the tests. A later change that follows the withdrawal would fail for no specified reason. Weakening an equality to a first-line check also lets the rest of the payload go unchecked, which is the requirement this test cites.

---

## EFF-NGUI-3 — test-gap
- **Location:** REQ-NGUI-3.7 (Tests/Tests.Integrated/SonOfLeoCli/Program.fs:42-51); REQ-NGUI-4.4 failure clause (Tests/Tests.Integrated/Reports/Program.fs:52-76)
- **Summary:** Neither test that cites the failure clause of REQ-NGUI-3.7 / REQ-NGUI-4.4 checks the exit code. Both discard it.
- **Resolution:** fix-test

REQ-NGUI-3.7 says the error goes to stderr AND the exit code is non-0. Its only citing test does `let _, _, e = runCli SonOfLeoCli args payload` (line 50) and asserts only `Assert.Contains(expectedError, e)`. REQ-NGUI-4.4's failure sentence ('returns the error via stderr and exits with a non-zero code') is cited only by Reports/Program.fs:52, which also discards the exit code (`let _, _, e = runCli Reports args payload`, line 70). The exit-code checks that do exist (SonOfLeoCli/Program.fs:25 and Reports/Program.fs:35) cite only REQ-NGUI-1.3 and use a different input (`"{}"`). Under the test-name linkage rule, half of each REQ has no citing assertion. If an error printed to stderr exited 0, the 3.7 and 4.4 tests would still pass.

**Action:** In SonOfLeoCli/Program.fs:50 and Reports/Program.fs:70, keep the exit code and add `Assert.Equal(1, exitCode)` next to the stderr assertion.

**Why:** A REQ with two clauses is covered only when its citing test asserts both. Linkage is by test name, so an assertion in a test citing a different REQ does not count.

---

## EFF-NGUI-4 — test-gap
- **Location:** REQ-NGUI-3.10; Tests/Tests.Integrated/SonOfLeoCli/FileArgumentPosition.fs:72-77
- **Summary:** The Reports-CLI check that `--file` straight after the report name is read as the payload is a lone `Assert.NotEqual(0, fromFileExit)`. That is an exit-code-only check with an inequality (Specimens 2 and 5).
- **Resolution:** fix-test

The Reports leg of the REQ-NGUI-3.10 theory proves the file is read when `--file` follows the report name only through `let fromFileExit, _, _ = runCli Reports [ "TrialBalance"; "--file"; path ] stdinPayload` followed by `Assert.NotEqual(0, fromFileExit)`. It discards stderr and accepts any non-zero code. `File.ReadAllText(filePath)` in Src/Ui.ReportCli/Program.fs:16 runs outside any Result, so a crash while reading the file (an uncaught exception exits with a runtime code, not 1) would pass as 'the file was read'. So would any other failure unrelated to the file's contents. Startup.run returns exactly 1 for a typed failure. This leg is the only Reports-CLI coverage of the positive `--file` rule in REQ-NGUI-3.10 / 4.3, so the half of the REQ that says 'the contents of the specified file replace the stdin payload' rests on this one assertion for the Reports CLI.

**Action:** Assert `Assert.Equal(1, fromFileExit)` and check that stderr holds the deserialisation error produced for the file's contents. Better still, give the Reports CLI a valid file payload that can be told apart from stdin (e.g. a different asOf) and assert that the stdout payload reflects the file.

**Why:** An exit-code-only check with an inequality cannot tell 'the file was read and rejected' from 'something else went wrong'. That is exactly the gap Specimens 2 and 5 describe.

---

## EFF-NGUI-5 — test-gap
- **Location:** REQ-NGUI-1.5; Tests/Tests.Integrated/InterfaceBridge/AccountRoutes.fs:62-89
- **Summary:** The sad-path Create test never records the ID of an account it might create, so its `finally` cleanup can never run. This breaks the Tests/README Form 4 cleanup rule.
- **Resolution:** fix-test

`REQ-NGUI-1.5 Account Create fails with invalid parent code` declares `let mutable accountIdToCleanup: AccountId option = None` and calls `cleanUpAccountId accountIdToCleanup` in `finally`. Nothing ever assigns it. The assertion goes through `isCorrectError ... (Some "This may cause other tests to fail.")`. Its Ok arm only returns a TestingError, so if the route wrongly succeeded the created account (code `genericAccountCodeString`) would leak. Tests/README.md Form 4 requires capturing the ID 'the instant the create succeeds — including on the `| Ok _ -> Assert.Fail "expected failure"` arm of a sad-path test'. The README names 'a test with no cleanup at all where its form requires one' as reportable, not as the accepted contamination risk. `let context = Context.create NoTransaction FetchOnly` (line 66) is also unused.

**Action:** Replace `isCorrectError` with an explicit match. On the `Ok payload` arm, deserialise the AccountReturn, look up its ID and set `accountIdToCleanup` before failing. Otherwise, remove the dead mutable and the unused context, so the code does not appear to clean up when it cannot.

**Why:** A cleanup that cannot fire looks like protection and gives none. If REQ-NGUI-1.5 ever regresses for Create, the leaked account would show up as count failures in unrelated later tests and hide the real cause.

---

