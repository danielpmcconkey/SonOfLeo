# money-test-efficacy-auditor

## MON-SPEC4-1 — test-gap
- **Location:** Tests/Tests.Isolated/Model/Money.fs, lines 36, 42, 48, 60, 66, 72, 115, 126, 137, 199, 220, 256, 265
- **Summary:** All 13 sad-path tests in Money.fs use Assert.True(result.IsError) instead of typed DU matching, which is Specimen 4 verbatim.
- **Resolution:** fix-test

Every rejection test in this file asserts failure with `Assert.True(result.IsError)` rather than matching the typed AppError DU case. The codebase has five distinct Money error cases -- MoneyFailedToConvertImproperPrecision, MoneyFailedToConvertExceededMax, MoneyFailedToConvertBelowMin, MoneyImproperSplit, MoneySplitFailedReconciliation -- and none of the 13 sad-path tests discriminates among them. Tests/README.md is explicit: "match the typed DU case ... Never Result.isError." The bullshit-test specimens doc (Specimen 4) calls out this exact pattern: "`isError` passes for *any* failure -- the wrong validation firing, a broken DB connection, a typo'd column name."

Affected tests and the typed case each should match:
- REQ-MON-2.2.1/1.4 (line 36): should match MoneyFailedToConvertImproperPrecision
- REQ-MON-2.2.1/1.2 (line 42): should match MoneyFailedToConvertExceededMax
- REQ-MON-2.2.1/1.3 (line 48): should match MoneyFailedToConvertBelowMin
- REQ-MON-2.3.1 rounding (line 60): should match MoneyFailedToConvertImproperPrecision
- REQ-MON-2.3.1 max (line 66): should match MoneyFailedToConvertExceededMax
- REQ-MON-2.3.1 min (line 72): should match MoneyFailedToConvertBelowMin
- REQ-MON-2.4.2 (line 115): should match MoneyImproperSplit
- REQ-MON-2.4.3 (line 126): should match MoneyImproperSplit
- REQ-MON-2.4.6 (line 137): should match MoneyImproperSplit
- REQ-MON-2.5.1 (line 199): should match MoneyFailedToConvertExceededMax
- REQ-MON-2.6.1 (line 220): should match MoneyFailedToConvertBelowMin
- REQ-MON-2.9.1 max (line 256): should match MoneyFailedToConvertExceededMax
- REQ-MON-2.9.1 min (line 265): should match MoneyFailedToConvertBelowMin

Additionally, none of these tests includes the mandatory `| Ok _ -> Assert.Fail "Expected failure; got success"` arm. These are isolated tests (Form 1) so cleanup is not a concern, but the guard arm is still required by the test standard -- without it, a silently-succeeding operation passes a failure test.

**Action:** Replace each Assert.True(result.IsError) with a match expression that names the expected error case, fails on wrong errors with Assert.Fail reporting what was caught, and fails on Ok with Assert.Fail. The canonical form from Tests/README.md: match result with | Error (MoneyFailedToConvertImproperPrecision _) -> () | Error e -> Assert.Fail $"Wrong error. {AppError.toMessage e}" | Ok _ -> Assert.Fail "Expected failure; got success".

**Why:** An untyped failure assertion cannot distinguish the intended rejection from an unrelated error. If a refactor introduced a bug where fromDecimal threw a different error (or fromDecimalList started failing on the first element instead of the offending one), these tests would still pass green. Typed matching proves the system rejects the input for the right reason, which is the behavior the REQ describes.

---
