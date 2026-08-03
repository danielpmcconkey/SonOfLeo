# SystemWide Auditor

## STALE-SYS-1 — stale-reference
- **Location:** Specs/Behavioral/SystemWide.md, Promotion candidates section (line 80)
- **Summary:** Promotion candidate REQ-AC-2.13 has a revisit trigger that was met weeks ago and two additional entities now confirm the pattern, satisfying the section's own admission criterion.
- **Resolution:** fix-spec

The Promotion candidates section states: "REQ-AC-2.13 (IDs are system-generated UUIDs; new UUIDs may not be passed in) -- revisit when journal entry creation is specced." The section's preamble defines the holding criterion: "Rules that look general but stay entity-specific until a second entity confirms them."

Journal entry creation has been specced and built. The same pattern now appears in all three entity domains:
- REQ-AC-2.13: "the creation function must generate a unique UUID for the ID (new UUIDs may not be passed in)"
- REQ-FP-2.1: "the system must generate a unique UUID for the ID (new UUIDs may not be passed in)"
- REQ-JE-2.1: "the system must generate a unique UUID for the header ID (new UUIDs may not be passed in)"
- REQ-JE-2.2: "the system must generate a unique UUID for each line ID (new UUIDs may not be passed in)"

The stated trigger (JE creation specced) has been met. The section's own criterion (a second entity confirms the pattern) is met by two additional entities (FP and JE). The candidate is stale and should be evaluated for promotion or explicitly dismissed.

**Action:** Evaluate REQ-AC-2.13 for promotion to a system-wide requirement (e.g., REQ-SYS-7.1). If promoted, the three entity-level REQs become instances of the system-wide rule. If deliberately kept entity-specific, remove the candidate entry with a note explaining why.

**Why:** The Promotion candidates section is a self-imposed tracking mechanism. A stale entry with a met trigger means the spec is not following through on its own commitments, and the tracking mechanism loses credibility as a tool for surfacing cross-cutting patterns.

---
