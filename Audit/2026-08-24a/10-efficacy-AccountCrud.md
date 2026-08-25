# AccountCrud Test Efficacy Auditor

## SPEC8-AC-3.3 — test-gap
- **Location:** Tests/Tests.Integrated/Model/Ledger/Account.fs line 314, REQ-AC-3.3
- **Summary:** REQ-AC-3.3 fetchById test is Specimen 8: locates by ID then asserts only that the returned ID matches.
- **Resolution:** fix-test

The test at Account.fs:314-322 fetches an account via `Account.fetchById context expectedId` and makes a single assertion: `Assert.Equal(expectedId, account |> Account.accountId)`. The locator and the assertion overlap on the same field. If fetchById returned a fabricated Account echoing the requested ID with garbage in every other property (wrong code, wrong name, wrong type, wrong dates), this test passes. The behavior IS adequately covered by REQ-AC-2.14 (Account.fs:62), which performs a full-property create-fetch roundtrip asserting code, name, type, activity period, subtype, parent, external reference, createdAt, and modifiedAt. But the REQ-AC-3.3 test itself contributes no unique verification beyond what railroadWrapper already proves (that the call succeeded without error).

**Action:** Assert at least one non-locator property from the fixture. For example, derive the expected code or name for mortgage2210Id from fixture.Data.accounts and assert it on the returned account, so the test proves the right record was returned rather than just that fetchById echoes back its input.

**Why:** Specimen 8 tests verify that the WHERE clause returns a row, not that the function returns the right record. The assertion is tautological and would pass with a function that manufactures responses containing the requested ID.

---

## AQ-AC-4.1 — test-gap
- **Location:** Tests/Tests.Integrated/ModelOrchestrator/AccountDeactivation.fs line 20, REQ-AC-4.1
- **Summary:** REQ-AC-4.1 orchestrator test name claims 'sets active end' but only asserts isActive status, not the provided Calendar Date.
- **Resolution:** fix-test

The test at AccountDeactivation.fs:20-33 passes `explicitDeactivationDate = Some(Calendar.today().PlusDays(-1))` to `deactivateAccount` and then asserts `Assert.False(deactivated |> Account.activityPeriod |> AccountActivityPeriod.isActive(Calendar.today()))`. It never asserts that the returned account's activeEnd equals the provided date. If deactivateAccount set activeEnd to any past date other than the one provided (e.g., a year ago rather than yesterday), the test passes because isActive still returns false relative to today. The route-level test at AccountRoutes.fs:168-189 DOES assert `Assert.Equal(Some endDate, accountReturn.activeEnd)`, so the exact-date behavior IS verified at the route layer. The orchestrator test's name makes two claims -- 'sets active end' and 'returns inactive account' -- but only the second is verified by its assertions.

**Action:** Add an assertion that the returned account's activeEnd equals the provided date, e.g. assert the activeEnd of the deactivated account's activity period equals `explicitDeactivationDate`. This makes the test verify what its name claims.

**Why:** A test that verifies consequences (isActive is false) rather than values (the specific date was stored) can mask bugs where the correct outcome occurs for the wrong reason. The smell test: if deactivateAccount ignored the provided date and hard-coded a different past date, this test passes while the route test fails. The function under test lives at the orchestrator layer, yet the value verification only exists at the route layer above it.

---
