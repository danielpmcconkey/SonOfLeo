# fp-efficacy-auditor

## FP-AQ-1 — test-gap
- **Location:** Tests/Tests.Integrated/InterfaceBridge/FiscalPeriodRoutes.fs, line 76 (REQ-FP-3.2)
- **Summary:** REQ-FP-3.2 FetchByKey happy-path test asserts only the lookup criterion, matching Specimen 8 (tautological locator).
- **Resolution:** fix-test

The test fetches a fixture period's key via fetchById, sends that key to the FetchByKey route, then asserts only `Assert.Equal(existingKey, returned.periodKey)`. The returned `periodKey` is the same value the route used for its lookup (the route's WHERE clause filters on period_key). The assertion can only fail if the route returns a row whose key differs from the one used to find it -- which is a WHERE-clause test, not a behavioral test. The FiscalPeriodReturn type carries startDate, endDate, isOpen, createdAt, and modifiedAt, none of which is asserted. If the route returned a fabricated FiscalPeriodReturn with the requested key but garbage for every other field, this test would pass. Smell-test confirmation: (1) garbage of the right shape passes; (2) a route that echoes the key without a DB lookup passes; (3) the assertion field IS the locator field. This is the only test anywhere in the suite that covers FetchByKey happy-path behavior -- there is no model-level fetchByKey test to compensate.

**Action:** Assert at least one non-lookup property (e.g. startDate or isOpen) against fixture-derived data. The fixture holds the full FiscalPeriod in memory (`fixture.Data.fiscalPeriods`); derive the expected value from there, not from a re-fetch, per Specimen 6 guidance. Example: `let expected = fixture.Data.fiscalPeriods |> List.find ...` then `Assert.Equal(expected |> FiscalPeriod.startDate, returned.startDate)`.

**Why:** A test whose only assertion overlaps with its lookup criterion tests the query's WHERE clause, not the system's behavior. Per the Specimen 8 rule: 'the locator and the assertion must not overlap.' Because this is the sole test for FetchByKey's happy path across all layers, no other test compensates for the gap.

---

## FP-MR-1 — missing-requirement
- **Location:** Specs/Behavioral/FiscalPeriodCrud.md (no REQ exists); tests at Tests/Tests.Integrated/InterfaceBridge/FiscalPeriodRoutes.fs lines 157, 170, 183
- **Summary:** Three route-level tests exercise non-existent-key rejection for FetchByKey, Close, and Reopen, but no FP REQ (or any REQ) describes this behavior.
- **Resolution:** fix-spec

Tests citing REQ-FP-3.2, REQ-FP-4.1, and REQ-FP-4.2 assert that sending a non-existent period key ('1850-01') to FetchByKey, Close, and Reopen produces the typed error FiscalPeriodNoPeriodMatchingKey. Those three REQs describe the happy-path capability ('must be able to retrieve/close/reopen'), not the error behavior when the key has no match. The rejection behavior exists in code (FiscalPeriodFieldConverters.fs line 17 and FiscalPeriod.fs line 140 raise FiscalPeriodNoPeriodMatchingKey), is correctly tested with typed error matching via isCorrectError, and is meaningful -- but no REQ in FiscalPeriodCrud.md or any other spec describes what the system must do when a caller provides a period key that does not match any existing fiscal period. Per Tests/README.md: 'Do not write tests unless you have a behavioral REQ to cite.'

**Action:** Add REQs to FiscalPeriodCrud.md for the not-found error behavior. One option is per-operation REQs under the relevant sections (e.g. 'REQ-FP-3.2.1 When retrieving by key, if no period matches the provided key, the system must produce a typed error' and analogous for 4.1 and 4.2). Alternatively, a single FP-level REQ could cover all three since the error originates from the same boundary converter function.

**Why:** Tests without REQ backing create a traceability illusion: the audit counts REQ-FP-3.2 as having two tests, but the sad-path test verifies behavior that REQ-FP-3.2 does not describe. If the error semantics change (e.g., returning None vs. a typed error), no requirement guides the developer on correct behavior, and the test citation misleads future auditors about what REQ-FP-3.2 actually covers.

---

