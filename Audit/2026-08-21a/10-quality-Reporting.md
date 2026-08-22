# spec-audit-reporting

## AMB-RPT-1 — ambiguity
- **Location:** Specs/Behavioral/Reporting.md, REQ-RPT-2.2
- **Summary:** REQ-RPT-2.2 introduces the term "level" for the boundary-type row field without mapping it to "generation," the term defined and used everywhere else in the spec for the same concept.
- **Resolution:** fix-spec

REQ-RPT-1.3 defines the trial balance row as containing "hierarchical depth (generation)." REQ-RPT-1.7 establishes the numbering: generation 0 for top-level accounts, incrementing by 1 per nesting level. REQ-RPT-3.3 reuses "hierarchical depth (generation)" for CSS class assignment. But REQ-RPT-2.2, defining the boundary-type serialization shape, calls the same field "level (int)" without stating that level equals generation or referencing the concept from section 1. The code confirms they are the same value -- ReportConverters.fs line 20 maps `level = flattenedRow.generation` -- but the spec never states this equivalence. The Specs README principle ("Pick the word and keep it") supports using one term consistently. A competent developer would almost certainly infer level = generation from context, so implementation divergence is unlikely, but the unexplained term switch is a spec-quality issue in a document that otherwise uses terminology precisely.

**Action:** Either replace "level (int)" in REQ-RPT-2.2 with "generation (int)" to match the rest of the spec, or add a parenthetical noting that level corresponds to the hierarchical depth (generation) defined in REQ-RPT-1.7.

**Why:** A spec that defines a concept with one name and then silently renames it in a neighboring section forces the reader to infer equivalence. The inference is easy here, but unnecessary -- the fix is a single-word change.

---

