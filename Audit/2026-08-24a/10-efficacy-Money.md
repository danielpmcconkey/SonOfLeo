# money-test-efficacy-auditor

_No findings._

## Reasoning

Audited all 28 test methods in Tests/Tests.Isolated/Model/Money.fs against the 22 active testable REQs in Specs/Behavioral/Money.md (27 total active minus 4 waived minus 1 unenforceable). Checked all four criteria:

1. BEHAVIORAL COVERAGE: Every active testable REQ (1.2, 1.3, 1.4, 2.2, 2.2.1, 2.3, 2.3.1, 2.3.2, 2.4, 2.4.1, 2.4.2, 2.4.3, 2.4.4, 2.4.5, 2.4.6, 2.5, 2.5.1, 2.6, 2.6.1, 2.8, 2.9, 2.9.1) has at least one citing test that exercises the described behavior. Applied the smell test to each: happy-path tests assert on extracted decimal values (not shapes or counts alone), so garbage of the right shape would fail. Sad-path tests match typed DU error cases, so the wrong error would fail.

2. ASSERTION QUALITY: Checked all 28 tests against all 10 bullshit-test specimens. No matches. Specifically: (a) the only count assertion (splitByN returns 3 shares) uses the input parameter 3, not a fixture snapshot (not Specimen 1); (b) all assertions use Assert.Equal, no cowardly inequalities (not Specimen 2); (c) every count assertion is accompanied by value assertions — fromDecimalList asserts length AND per-element value equality, splitByN asserts count AND sum (not Specimen 3); (d) all 11 sad-path tests use typed DU case matching with both escape arms (Error-wrong and Ok-unexpected), never Result.isError or string Contains (not Specimen 4); (e) no CLI tests in scope (not Specimen 5); (f) all expected values derive from test literals or plain .NET arithmetic (List.sum, decimal addition), never from functions in the call chain of the function under test — sumList is used in 2.4.1 but is not in splitByN's call chain (not Specimen 6); (g) every test body contains Assert.Equal or a typed match with Assert.Fail arms (not Specimen 7); (h-j) not applicable to isolated tests.

3. NEGATIVE COVERAGE: All REQs with explicit rejection behavior have corresponding negative tests: 2.4.2 (zero-ways), 2.4.3 (one-way), 2.4.6 (negative-ways) each have a test matching MoneyImproperSplit. Section 1 validations (precision, max, min) are tested through 2.2.1 and 2.3.1. Boundary violations on add (2.5.1), subtract (2.6.1), and sumList (2.9.1) each have both max-exceeded and below-min tests with typed error matching. No rejection behavior in the spec lacks a test.

4. UNCITED BEHAVIOR: Two public functions in Src/Model/Money.fs lack REQ backing: toCurrencyString (used by TrialBalanceWriter.fs, arguably covered by REQ-RPT-3.6 in the Reporting spec) and toAccountingString (defined but unused anywhere in the codebase — dead code). Neither represents a missing REQ in Money.md: toCurrencyString is a presentation concern owned by the reporting domain, and toAccountingString is unused scaffolding. No tests exercise logic outside any spec's scope. No REQ describes multiple distinct behaviors where only some are tested — multi-rule validations (2.2.1 citing all of section 1, 2.5.1/2.6.1/2.9.1 citing section 1) are fully decomposed into separate tests.

Also verified against resolved-findings.md: MON-2 (sum intermediate overflow) and MON-3 (split count type) were both overruled and do not apply. No finding in this audit matches any resolved entry's scope.
