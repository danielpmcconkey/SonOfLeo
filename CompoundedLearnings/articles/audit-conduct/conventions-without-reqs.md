# Not Every Rule Has a REQ ID

**Source:** Audit 2026-07-06a — CQ-3 ruling, DEC-1 overrule, skill improvement item #27a

Operational rules and domain guidance can exist as learnings without a corresponding REQ ID in the behavioral specs. A testable rule gets extracted to a REQ ID only when the business domain it applies to has been specced and built. Until then, the learning stands on its own.

## Example

Temporal guidance says sub-second precision instants from external systems should be rejected, and middleware must convert inbound data to the system's standard. These are real rules — but the import/staging domain doesn't exist yet. Dan: "How can I have a requirement for an entire domain I haven't yet conceived of?"

The DB-origination rule ("no now() in defaults/triggers") was extracted to REQ-DAL-3.7 because the DAL domain exists. The other two stayed as guidance because their domain doesn't exist yet.

## The rule

When auditing, do not flag the absence of a REQ ID for a rule whose domain hasn't been built. REQ extraction happens during domain design, not during reviews of operational guidance.
