# dal-efficacy-auditor

_No findings._

## Reasoning

Scope: tests citing REQ IDs from Specs/Behavioral/DataAccessLayer.md (REQ-DAL prefix).

What I checked:
1. Read the full DataAccessLayer.md spec. It contains 19 active requirements across three sections: connection string handling (REQ-DAL-1.3, 1.14-1.20), query execution (REQ-DAL-2.1-2.3), and database/data access architecture (REQ-DAL-3.1-3.7). Fourteen additional REQs are withdrawn.
2. Grepped the entire Tests/ directory for "REQ-DAL" -- zero matches in any .fs file, comment, or test name.
3. Grepped all .fs and .md files outside Specs/ for "REQ-DAL" -- zero matches anywhere in source or test code.
4. Read Tests/Tests.Integrated/DataAccessLayer/DalTests.fs (the only DAL-scoped test file, containing one Theory with 28 InlineData rows). It exercises DAL error surfacing but cites no REQ IDs whatsoever.

Why nothing rises to finding level:
All 19 active REQ-DAL requirements are accounted for: 16 are waived from testing (Dan-approved, 2026-07-06 and 2026-08-02) and 3 are classified as unenforceable. Zero are in the "tested" state. There are no REQ-DAL-citing tests to audit for behavioral coverage, assertion quality, negative coverage, or uncited behavior.

The resolved finding DAL-EFFICACY (overruled 2026-08-20, verbiage corrected 2026-08-22) explicitly addresses this: "all 19 are either waived from testing or classified as unenforceable. [...] A test-efficacy auditor scoped to the DAL will always return 'no findings' because there are no tested REQ IDs to audit against. This is by design, not a gap."

The DalTests.fs file exercises DAL error paths without citing REQ IDs. The waiver for REQ-DAL-2.2 explicitly acknowledges this: "Behavior proven; waived from REQ-ID citation because the test exercises the mechanism, not the requirement by name." This is not uncited behavior -- it is behavior described by a spec requirement that is deliberately waived from REQ-ID citation.

Statement-delta check: Dan's statement says "No new features since the remediation" and "code-to-ID migration was the largest structural change." The two most recent migrations (RebuildClassificationRule, RebuildStageEntryLine) are staging/ingestion domain, not DAL infrastructure. The DAL source files (DbConnections, DbTransaction, QueryParameters, ExecuteReader, ExecuteNonQuery, ExecuteScalar) are unaffected by the account-ID pivot. No delta between the statement and the repo state for DAL scope.
