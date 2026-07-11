# Specs Define the What, Not the How

**Source:** Audit 2026-07-06a — AMB-DAL-01 overrule

A requirement defines WHAT the system must do. The HOW — the detection heuristic, the implementation approach, the algorithm — is an implementation choice, not a spec obligation. Requirements are not implementation guides.

## Example

AMB-DAL-01 flagged REQ-DAL-1.16 ("reject a value that contains an actual connection string") as having no detection criteria. The implementation uses a reasonable heuristic (semicolons and `Host=`). The spec is not deficient for not prescribing the heuristic.

## The line

If two implementations of the WHAT would produce different observable behavior to the user, the spec is under-elaborated. If they'd produce the same behavior through different means, the spec is fine.
