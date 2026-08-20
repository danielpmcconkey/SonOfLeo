# NGUI Spec Auditor

## CON-NGUI-1 — contradiction
- **Location:** Specs/Behavioral/NonGraphicalInterface.md, REQ-NGUI-1.1 (line 7) vs REQ-NGUI-4.2 (line 41)
- **Summary:** REQ-NGUI-1.1's universal quantifier "All interface use cases" claims every use case follows the domain+verb+payload trigger pattern, but Section 4's Reports CLI accepts only a report name and payload -- no domain, no verb.
- **Resolution:** fix-spec
- **Prior ruling:** WAIVE-1 covers only the soundness of the "too broadly scoped" waiver reason for REQ-NGUI-3.1-3.5. It does not address whether 1.1's "All" quantifier contradicts Section 4's distinct routing model. Different scope.

REQ-NGUI-1.1 states: "All interface use cases must be triggered by the actor providing a domain, a verb, and a payload." Section 4 defines the Reports CLI as a separate executable (REQ-NGUI-4.1) whose actor provides a report name as the first argument (REQ-NGUI-4.2) and a payload (REQ-NGUI-4.3). No domain argument is provided and no verb argument is provided. The code confirms the structural divergence: CommandRoute.fs defines CommandRoute with explicit `domain` and `verb` fields, while ReportRoute has only a `name` field. The main CLI (SonOfLeoCli/Program.fs) destructures args into `domain :: verb :: rest`, while the Reports CLI (Reports/Program.fs) destructures into `name :: rest`. The word "All" in 1.1 is an unqualified universal that the Reports CLI does not satisfy. A generous reading could treat the domain as implicit in executable selection and the report name as a verb equivalent, but the spec does not state this interpretation, and the code's type model treats them as structurally distinct patterns.

**Action:** Either scope REQ-NGUI-1.1 to the main CLI (e.g., "All main CLI use cases...") or add a qualifying clause acknowledging that specialized executables may satisfy the pattern with the domain implicit in executable selection and the report name serving as the verb equivalent.

**Why:** An LLM agent implementing a new CLI or extending the Reports CLI might take 1.1 at face value and add domain+verb arguments where only a name is needed. An auditor checking 1.1 compliance across all interfaces would flag the Reports CLI as non-compliant. The universal claim should match the actual architecture so that both human and agent readers get a consistent picture of the interface model.

---

