# test-efficacy-auditor-reporting

## RPT-EFF-1 — test-gap
- **Location:** Tests/Tests.Integrated/InterfaceBridge/ReportRoutes.fs, line 108 (REQ-RPT-2.4)
- **Summary:** Assert.Contains verifies the interpolated date is present in the path but does not verify it appears before the file extension as the spec requires.
- **Resolution:** fix-test

REQ-RPT-2.4 says: 'the as-of date in yyyy-MM-dd format is appended to the file name, prefixed with a hyphen, before the file extension.' The test asserts `Assert.Contains($"-{expectedDateStr}", pathReturn.fullyQualifiedPath)`. This confirms the date substring exists somewhere in the path but does not confirm its position relative to the extension. A system that produced `rpt-2-4-test.html-2026-09-24` (date after the extension) would pass this assertion. All inputs are controlled (baseDir = testOutputDir, fileName = "rpt-2-4-test"), so the full expected path is constructable and Assert.Equal would verify the complete path format including position.

This is not covered by the NGUI-AQ-1 precedent. That ruling accepted Assert.Contains because 'the Contains check verifies the entire expected error message is present' -- the Contains argument was the full expected content. Here the Contains argument is a fragment of the expected path, leaving the baseDir, fileName prefix, and extension position unverified.

The Tests/README standard states: 'Asserting equality is preferred to asserting truth. You should know what you expect and you should assert the outcome is exactly what you expect.' The test knows the full expected path but asserts only a substring.

**Action:** Construct the full expected path from controlled inputs and use Assert.Equal. For example: `let expectedPath = System.IO.Path.Combine(testOutputDir, $"rpt-2-4-test-{expectedDateStr}.html")` then `Assert.Equal(expectedPath, pathReturn.fullyQualifiedPath)`. This verifies baseDir usage, filename preservation, date position before extension, and the .html extension in one assertion.

**Why:** RPT-2.4 is tested at only one layer (InterfaceBridge route), so this Assert.Contains is the sole verification of the path construction format. A weaker-than-necessary assertion at the only layer leaves the positional constraint described by the spec unverified by any test.

---
