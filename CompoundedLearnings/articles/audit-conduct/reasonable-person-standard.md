# Reasonable-Person Standard

**Source:** Audit 2026-07-06a — MON-3 overrule, AMB-DAL-04 overrule, Dan's directive

A requirement is ambiguous only if a competent developer with domain knowledge would genuinely implement it differently. Not if a pathological reading could be constructed. These are specs, not legal briefs.

## What works

Ask: "would two reasonable developers actually diverge here?" If the answer requires inventing a developer who has never been to a restaurant, never used a database, or never read the domain's own definitions, the answer is no.

## What doesn't

- Treating any theoretical interpretation gap as an ambiguity
- Constructing edge cases no real user would encounter
- Flagging integer-obvious parameters as "type unspecified" because the spec didn't write `int`

## Example

MON-3 flagged that the split count N's type wasn't specified. Dan: "If I asked you to split the check 3.4 ways, what would you tell me?" The sub-requirements (reject 0, reject 1, reject negative) make the integer constraint obvious. Overruled.
