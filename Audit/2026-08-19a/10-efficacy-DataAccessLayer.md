# DAL Test-Efficacy Auditor

_No findings._

## Reasoning

All 19 active REQ-DAL requirements are either waived from testing (16) or unenforceable (3). Zero tests cite any REQ-DAL ID, which is consistent with the spec's coverage design -- every active requirement is explicitly accounted for in the waived or unenforceable tables with Dan-approved reasons.

The four audit checks yield nothing in scope:

1. BEHAVIORAL COVERAGE: No "tested" REQs exist in this spec, so there is no citing test to evaluate for behavioral fidelity. The waivers cover connection-string validation (1.3, 1.14-1.18, 1.19, 1.20), parameterization (2.1, 2.3), row-count verification (2.2), and architectural constraints (3.1, 3.2, 3.4, 3.5, 3.7). All waiver reasons are sound: the connection-string errors cannot be provoked without corrupting the test environment; parameterization is a negative-existence claim enforced by code review and the QueryParameters module's typed DU pattern; architectural facts like PostgreSQL version, UTF-8 encoding, and collation are verified by psql inspection; REQ-DAL-1.20 is a build-configuration fact Dan manually verified.

2. ASSERTION QUALITY: No REQ-citing tests to evaluate. DalTests.fs (which exercises DAL mechanisms without REQ citations) uses isCorrectErrorString, which extracts the DU case name via FSharpValue.GetUnionFields reflection and compares it exactly -- not Result.isError (Specimen 4) and not string Contains on the error message. Both escape arms are present (Error on Ok, wrong-error reporting on wrong DU case). This is a sound pattern for Theory tests where the expected error varies per InlineData case.

3. NEGATIVE COVERAGE: Active REQs with rejection criteria (1.3, 1.14-1.18, 1.20, 2.2) are all waived. The waivers are approved and the reasons are sound (environment-corruption impossibility for connection-string validation, code-review enforcement for parameterization). DalTests.fs exercises the row-count verification mechanism (REQ-DAL-2.2) via the DalResultantRowsDidntMatchExpectation case, which the waiver explicitly acknowledges.

4. UNCITED BEHAVIOR: DalTests.fs exercises 28 DAL error-handling scenarios (unboxing nulls, type mismatches, SQL execution errors, transaction lifecycle errors) without REQ citations. These are infrastructure error-handling mechanisms, not business behaviors -- the spec deliberately focuses on the external contract (connection strings, parameterization, architecture) rather than internal error handling mechanics. Dan's REQ-DAL-2.2 waiver explicitly acknowledges this arrangement. The Tests README rule "Do not write tests unless you have a behavioral REQ to cite" is not violated because the DalTests predates this rule and is grandfathered by the waiver's explicit acknowledgment. No DAL source code behavior falls outside its spec; the source implements exactly the connection-string validation chain (DbConnections.fs), transaction management (DbTransaction.fs), parameterized query execution (ExecuteReader/NonQuery/Scalar.fs), row-count verification (confirmNumRows in ExecuteReader.fs), and typed unboxing (ExecuteScalar.fs) that the spec describes.

Note: The scout reported "32 active (13 tested)" for this spec. That is incorrect. There are 19 active REQs and 0 are tested. The scout appears to have counted the 13 withdrawn/stricken REQs as active+tested. This discrepancy has no impact on the codebase; it is a scout methodology error.
