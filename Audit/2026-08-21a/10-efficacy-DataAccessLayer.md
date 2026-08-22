# dal-efficacy

_No findings._

## Reasoning

Scope: tests citing REQ IDs from Specs/Behavioral/DataAccessLayer.md. Grepped Tests/ for REQ-DAL — zero hits. The DAL spec has 19 active requirements, all of which are either waived from testing (16, Dan-approved 2026-07-06 through 2026-08-02) or classified as unenforceable (3, Dan-approved 2026-08-02). There are literally no tests in scope to audit.

What I checked against the four audit criteria:

1. BEHAVIORAL COVERAGE: All 19 active REQs are waived or unenforceable. No test cites any REQ-DAL ID. The waiver reasons are sound: REQ-DAL-1.3 through 1.19 describe connection-string validation behaviors that cannot be provoked without corrupting the test environment; REQ-DAL-1.20 is a build-configuration fact; REQ-DAL-2.1/2.3 are negative-existence claims (parameterization) enforced by code review; REQ-DAL-2.2 is exercised by DalTests but waived from REQ-ID citation by design; REQ-DAL-3.1/3.2/3.4/3.5/3.7 are architectural or schema facts verified outside the test suite.

2. ASSERTION QUALITY: No citing tests exist, so no assertions to evaluate.

3. NEGATIVE COVERAGE: Several REQs define rejection criteria (1.3, 1.14-1.18), but all are waived with the justification that they are impossible to provoke from the test harness. The DalTests.fs Theory test exercises 28 DAL error paths (unboxing nulls, type mismatches, bad SQL, row-count violations) with typed error matching via isCorrectErrorString, but does so without citing REQ-DAL IDs — consistent with REQ-DAL-2.2's waiver note.

4. UNCITED BEHAVIOR: DalTests.fs (Tests.Integrated/DataAccessLayer/DalTests.fs) exercises DAL error surfacing for 28 error variants without citing any REQ-DAL ID. The REQ-DAL-2.2 waiver explicitly acknowledges this: "Behavior proven; waived from REQ-ID citation because the test exercises the mechanism, not the requirement by name." This is known, acknowledged, and by design.

Precedent: DAL-EFFICACY (resolved-findings.md, 2026-08-20, overruled) directly states: "A test-efficacy auditor scoped to the DAL will always return 'no findings' because there are no REQ IDs to audit against. This is by design, not a gap. Do not flag the absence of DAL-specific efficacy findings." This ruling matches my situation exactly.
