# Traceability

## Requirements traceability

All business, system, behavioral, or non-functional requirements must be identified by an REQ label. 

This label for requirements IDs follows the format of REQ-{domain}-{number} 

### Domain
The domain portion of a requirements ID is an all-caps unique identifier for the domain within the system (ex: AC for Account CRUD or LOG for logging). It should be short (no more than 5 characters) to avoid cluttering up our artifacts.

### Number
The number portion of a requirements ID must be unique to the domain (only 1 AC-1.5 but you can have AC-1.5 and LOG-1.5). The numbers increment like software versions (the next number after 2.9 is 2.10, not 3.0) and any "sub dot" number is assumed to be applicable only to its parent. Example:
- REQ-AC-1.48 An Account record is considered "deactivated" (or "inactive") when its "active end" date is non-null and is earlier than a given reference date (the active-end date itself is still active — the boundary is inclusive).
- REQ-AC-1.48.1 The reference point is context-dependent: it may be the current system clock or a date specific to the operation (e.g., a transaction's entry date). Each requirement that references deactivation status must specify which reference point applies.

In the above example, 1.48.1 is only applicable when in the context of determining whether an Account is active

### Requirements enforcement
All requirements must be enforced throughout the system unless an explicitly added to the table of unenforceable requirements.

### Code annotations
All config and code in this system will annotate All enforceable requirements at the point where the requirement is enforced. Some requirements will be enforced in multiple places throughout the system and it is expected that all such enforcements are annotated. This lets future designers and developers know which pieces of code bear load or which pieces need to be changed when refining a business requirement.

## Testing enforcement
All requirements are assumed testable unless they are added to the table of untestable requirements.

All testable requirements must have at least 1 test (using the xUnit framework) that certifies that it is working as expected.

All tests must annotate which requirements they test for.

## Audits and auditability
The annotation conventions herein described should allow easy periodic audits to ensure that we're not missing any enforcements or tests.

While it is impossible to enforce an audit within the system, Dan attests that, once the system is complete, he will work with Hobson to create a SonOfLeoAudit skill and run it as part of the Saturday routine every week unless the underlying repo has not changed since prior audit.