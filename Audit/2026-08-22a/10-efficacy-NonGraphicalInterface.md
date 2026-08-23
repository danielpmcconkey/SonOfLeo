# ngui-test-efficacy-auditor

## NGUI-AQ-2 — test-gap
- **Location:** Tests/Tests.Integrated/SonOfLeoCli/Program.fs, line 55, REQ-NGUI-3.6
- **Summary:** Hard-wired expected value "Money Market" in the REQ-NGUI-3.6 stdout payload test instead of deriving from fixture data (Specimen 1).
- **Resolution:** fix-test

The test at line 44-60 asserts `Assert.Equal("Money Market", fetched.name)` where "Money Market" is the fixture name for account code "F-1270". The test class receives `fixture: TestDataFixture` via its constructor (line 12) but never references it. The expected name should be derived: `let expectedName = fixture.Data.accounts |> List.find(fun a -> a |> Account.code |> AccountCode.value = "F-1270") |> Account.accountName |> AccountName.value`. The fixture code "F-1270" is an appropriate stable identifier (F- prefix convention), but the name is a mutable property that should be looked up, not assumed. Per Specimen 1: the hard-wired value encodes what the fixture contained the day the test was written. If the fixture account is renamed, this test breaks for a fixture-mismatch reason rather than a behavioral regression, which is exactly Specimen 1's disease. None of the 7 tests in this file use the fixture at all despite receiving it.

**Action:** Derive the expected name from `fixture.Data.accounts` by looking up the account with code "F-1270" and extracting its AccountName value, replacing the hardcoded string.

**Why:** Specimen 1 violations create tests whose failures diagnose fixture drift rather than behavioral regressions. The test has the fixture available but ignores it, so the fix is trivial and the test becomes fixture-change-resilient.

---

