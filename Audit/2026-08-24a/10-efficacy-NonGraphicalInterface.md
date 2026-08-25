# NGUI Test Efficacy Auditor

_No findings._

## Reasoning

Audited all 19 tests across 4 files citing REQ-NGUI IDs against the 10 active non-waived requirements in NonGraphicalInterface.md. Checked all four criteria:

BEHAVIORAL COVERAGE: All 10 testable REQs (1.3, 1.3.1, 1.5, 3.6, 3.7, 3.8, 3.9, 4.2, 4.4, 4.5) have citing tests that exercise the described behavior. The 17 waived REQs were reviewed against the waiver table and all have Dan-approved waiver reasons. The WAIVE-1 resolved finding explicitly overrules questioning the "too broadly scoped" waiver pattern used for REQ-NGUI-3.1 through 3.5.

ASSERTION QUALITY: Applied all 10 specimen patterns. (1) No hard-wired counts -- expected values are derived from fixture data where applicable (e.g., SonOfLeoCli/Program.fs line 52-55 derives expectedName from fixture.Data.accounts). (2) No cowardly inequalities. (3) No count-only assertions lacking value checks. (4) No untyped failures -- route-level tests use isCorrectError/isCorrectErrorString which match typed DU cases with both escape arms (wrong error and unexpected success); process-level tests appropriately use exit codes and exact message strings per Specimen 5 guidance. (5) Exit-code-only assertions appear in SonOfLeoCli/Program.fs for REQ-NGUI-1.3 (failure and success) and REQ-NGUI-3.8 (case sensitivity); these are process-level plumbing tests (Form 5) where exit-code-only is the documented correct practice. (6) No fox-guarding-hen-house -- considered the Reports/Program.fs stderr test (line 45-64) which derives its expected message from FileIO.writeTextFile, but that test is verifying process-level stderr serialization, not the error-producing logic itself; the function under test is the CLI process boundary, not the file I/O. (7) No assertion-free tests. (8) No tautological locators. (9) No observation-from-inside-mechanism. (10) No count-of-truth-table issues. The Assert.Contains usage on main CLI stderr was reviewed and overruled by NGUI-AQ-1; not re-raised.

NEGATIVE COVERAGE: All REQs with rejection criteria have negative tests. REQ-NGUI-1.5 has five route-level tests across different Account operations (Create with bad parent code, FetchByParentCode/Deactivate/UpdateName/UpdateExternalReference with bad account codes), each using typed error matching. REQ-NGUI-3.8 has two tests proving wrong-case domain and verb both fail (contrasted with the working-case success test at line 24). REQ-NGUI-3.9 and 4.5 both test unknown route rejection. REQ-NGUI-4.5 has layered coverage: typed error at route level (ReportRoutes.fs line 117) and exact string at process level (Reports/Program.fs line 92).

UNCITED BEHAVIOR: No tests in these files exercise NGUI behavior without REQ backing. No code paths in SonOfLeoCli/Program.fs or Reports/Program.fs implement behaviors outside the NGUI spec scope (the routing logic, stdin/stdout/stderr handling, and exit code returns are all covered by cited REQs or waived requirements).
