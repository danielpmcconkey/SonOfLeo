# NGUI Test-Efficacy Auditor

## NGUI-AQ-1 — test-gap
- **Location:** Tests/Tests.Integrated/SonOfLeoCli/Program.fs, line 41 (REQ-NGUI-1.3.1, REQ-NGUI-3.7)
- **Summary:** The SonOfLeoCli stderr test uses Assert.Contains on error text, matching Specimen 4's string-matching sibling pattern, while the Reports equivalent for the same requirement uses Assert.Equal.
- **Resolution:** fix-test

Line 41 asserts `Assert.Contains(expectedError, e)` where `e` is stderr output. This is the exact assertion shape Specimen 4 labels as worthless ('String Contains is the same bug plus brittleness: reword the message and the test breaks; produce the wrong error with similar wording and it passes'). The README says 'Never string-matching on error text.' The Reports CLI test for the same requirement (Reports/Program.fs line 60) uses `Assert.Equal(expectedErrorMessage, e)`, demonstrating that exact matching is feasible at the process boundary -- it accounts for the trailing newline by appending `Environment.NewLine` to the expected value. Assert.Contains would pass if stderr contained the expected message as a substring of additional output (debug warnings, runtime messages, a different error whose message includes the same text). Assert.Equal catches all of those.

**Action:** Replace `Assert.Contains(expectedError, e)` with `Assert.Equal($"{expectedError}{Environment.NewLine}", e)` to match the pattern already established in Reports/Program.fs.

**Why:** Assert.Contains on error text masks two classes of defect: (1) stderr pollution -- the CLI starts emitting additional output and no test catches it, and (2) wrong error with overlapping wording -- a different failure path produces a message containing the expected string and the test passes on the wrong error. Assert.Equal eliminates both.

---

## NGUI-COV-1 — test-gap
- **Location:** REQ-NGUI-1.3.1 in NonGraphicalInterface.md; citing tests in SonOfLeoCli/Program.fs line 32 and Reports/Program.fs line 45
- **Summary:** REQ-NGUI-1.3.1 describes two behaviors -- error message in payload (tested) and full stack trace for system exceptions (not tested, not waived).
- **Resolution:** dan-decides

REQ-NGUI-1.3.1 states: 'In the event of an error, the payload will comprise the error message and, in cases of system exceptions, the full stack trace.' Both citing tests trigger domain errors (AccountCodeDoesntMatchAccountId, FileIO write failure) and verify the error message appears in stderr. Neither triggers a system exception (an unhandled .NET exception) to verify that stderr includes the full stack trace. The CLI source files (SonOfLeoCli/Program.fs and Reports/Program.fs) have no try/catch -- they rely entirely on the .NET runtime's default behavior to output unhandled exception stack traces. This means: (a) the stack trace behavior is load-bearing for operator debugging but untested, and (b) if someone adds global exception handling that reformats exceptions (message only, no stack trace), no test would detect the regression. The requirement is active, not waived. If the behavior is considered infrastructure-level and untestable, a waiver with that justification would close the gap.

**Action:** Either add a waiver for the stack-trace half of REQ-NGUI-1.3.1 with a justification (e.g., 'system exception path delegates to .NET runtime default behavior; verified by manual testing'), or add a process-level test that triggers an unhandled exception and asserts stderr contains the exception type and a stack trace marker.

**Why:** The requirement explicitly distinguishes domain errors (message only) from system exceptions (message plus stack trace). The tests exercise only the domain-error path. The system-exception path is a different trigger condition with a different expected output. As long as the requirement is active and un-waived, the gap between what the REQ says and what the tests verify is real.

---
