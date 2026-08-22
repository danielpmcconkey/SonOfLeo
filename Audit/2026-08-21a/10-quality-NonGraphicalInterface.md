# ngui-spec-auditor

## AMB-NGUI-1 — ambiguity
- **Location:** Specs/Behavioral/NonGraphicalInterface.md, REQ-NGUI-3.9 vs REQ-NGUI-4.5
- **Summary:** REQ-NGUI-3.9 and REQ-NGUI-4.5 describe the same error scenario for parallel CLIs but use different error-qualification language: "appropriate error" vs "typed error."
- **Resolution:** fix-spec

REQ-NGUI-3.9 (main CLI, unsupported domain/verb): "the CLI application must exit with an appropriate error." REQ-NGUI-4.5 (Reports CLI, unsupported report name): "the Reports CLI must exit with a typed error." These are parallel requirements covering the same situation — an actor provides an unrecognized route — for two CLIs governed by the same spec. "Typed error" is a stronger, more specific constraint: the error must be a specific case in the AppError discriminated union (confirmed by the ReportRoutes test at line 117, which verifies against the ReportingUnknownReportName case). "Appropriate error" is weaker and vaguer — it says the error must be fitting but does not mandate a specific error type. The implementation already uses typed errors for both: CliUnknownCommand (AppError.fs line 38) and ReportingUnknownReportName (AppError.fs line 163). The spec language diverges where the implementation does not. The term "typed error" also appears in REQ-STG-3.2 (DataIngestion.md), confirming it carries specific meaning in this spec corpus.

**Action:** Align REQ-NGUI-3.9 to say "typed error" to match REQ-NGUI-4.5 and the actual implementation, or if "appropriate error" is deliberately weaker, document why the main CLI's unknown-route error has a different constraint than the Reports CLI's.

**Why:** Parallel requirements describing the same scenario for sibling CLIs should use the same language. A future implementer of a third CLI following this spec's patterns could reasonably read "appropriate error" as permitting a non-typed error (e.g., a raw format string) while "typed error" forbids it. Consistent terminology prevents that divergence.

---
