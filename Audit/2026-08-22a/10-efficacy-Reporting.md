# reporting-efficacy-auditor

## RPT-EFF-1 — test-gap
- **Location:** Tests/Tests.Integrated/InterfaceBridge/ReportRoutes.fs, line 100 (REQ-RPT-2.4)
- **Summary:** Expected date string in the REQ-RPT-2.4 test is derived via Calendar.localDateToString, which is in the call chain of the function under test (Specimen 6).
- **Resolution:** fix-test

The test computes its expected value as `nextMonth |> Calendar.localDateToString "yyyy-MM-dd"` (line 100). The function under test routes through `TrialBalanceWriter.write` (Src/InterfaceBridge/ReportWriters/TrialBalanceWriter.fs, line 265), which calls `asOf |> localDateToString "yyyy-MM-dd"` -- the same utility with the same format string. Both sides of the assertion `Assert.Contains($"-{expectedDateStr}", pathReturn.fullyQualifiedPath)` therefore agree by tautology. If the format string in the production code were changed from "yyyy-MM-dd" to, say, "MM-dd-yyyy", the expected value would change identically, and the test would pass while the requirement's explicit "in yyyy-MM-dd format" specification is violated. The Specimen 6 rule (Tests/README.md, bullshit-test-specimens.md) is absolute: no function in the call chain of the function under test may appear in the derivation of the expected value.

**Action:** Derive the expected date string from raw NodaTime properties instead of Calendar.localDateToString. For example: `let expectedDateStr = $"{nextMonth.Year:D4}-{nextMonth.Month:D2}-{nextMonth.Day:D2}"`. This makes the format independently verifiable.

**Why:** The requirement's substance -- that the date appears in yyyy-MM-dd format specifically -- is the one thing this test cannot verify in its current form. A formatting bug that changes the pattern would be masked because both the expected and actual values pass through the same formatting function with the same format string.

---

