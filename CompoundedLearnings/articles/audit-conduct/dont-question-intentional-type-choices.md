# Don't Prescribe Structure for External Data

**Source:** Audit 2026-07-06a — AMB-JE-3a overrule

When a field represents data from an external system (FI reference IDs, merchant strings, external account numbers), do not flag its type as under-elaborated or suggest it should be more structured. External systems don't conform to our type model. A free-text string with validation constraints (not null, max length) is the correct design when the data's shape is outside our control.

## Example

AMB-JE-3a questioned how an external reference's target is identified, implying the value should have more structure. Dan pointed to REQ-JE-1.44 (value cannot be null or whitespace) and REQ-JE-1.45 (value max 100 characters). External FI reference IDs have no universal structure — every financial institution formats them differently. Asking for structure here is asking us to standardize something we don't own.

## The line

Internal data SHOULD use domain types — flagging "this should be a Money instead of a decimal" is a valid finding. External data that crosses the system boundary in someone else's format should not be forced into a domain type it doesn't fit.
