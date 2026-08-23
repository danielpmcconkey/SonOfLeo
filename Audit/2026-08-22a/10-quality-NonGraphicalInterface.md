# ngui-spec-auditor

## CON-NGUI-1 — contradiction
- **Location:** Specs/Behavioral/NonGraphicalInterface.md, REQ-NGUI-1.1 vs REQ-NGUI-4.2
- **Summary:** REQ-NGUI-1.1's universal 'All interface use cases' claim is contradicted by the Reports CLI's single-argument input pattern in section 4.
- **Resolution:** fix-spec

REQ-NGUI-1.1 states: 'All interface use cases must be triggered by the actor providing a domain, a verb, and a payload.' Section 4's Reports CLI (REQ-NGUI-4.2) accepts only a report name and a payload -- two components, not the three that 1.1 mandates. The code confirms this divergence is structural, not incidental: the CommandRoute type (used by the main CLI) carries explicit `domain` and `verb` fields, while the ReportRoute type carries only a `name` field (Src/InterfaceBridge/CommandRoute.fs lines 8-24). The Reports CLI's Program.fs parses a single positional argument (name), not two (domain + verb). This is not ambiguity -- each section is individually clear -- but the universal quantifier in 1.1 creates a contradiction with section 4's deliberately different pattern.

**Action:** Scope REQ-NGUI-1.1 to acknowledge section 4 as a specialization, e.g. 'All interface use cases must be triggered by the actor providing a domain, a verb, and a payload (see section 4 for the Reports CLI variant)' or 'Unless a specific interface section defines its own input pattern, ...'.

**Why:** A general rule that uses 'All' but has an unacknowledged exception undermines the spec's reliability as an authoritative document. A future implementer building a third interface (e.g., an API) would read 1.1 and implement domain+verb+payload, then see section 4 deviates with no stated exception, and have no guidance on whether new interfaces should follow 1.1 or may deviate like section 4.

---
