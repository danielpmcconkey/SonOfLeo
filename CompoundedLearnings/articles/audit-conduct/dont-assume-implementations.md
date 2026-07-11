# Don't Assume Implementations

**Source:** Audit 2026-07-06a — MON-2 overrule, skill improvement item #19a

Spec quality auditors must not assume how code implements a requirement. Either verify against the actual code or confine the finding to the spec text as written.

## Example

MON-2 flagged that sum-of-a-list validation was ambiguous about intermediate overflow. The auditor assumed summation was implemented as a fold over the add function (which would validate intermediates). It wasn't — the implementation uses `List.sumBy` on the decimal projection and validates once via `fromDecimal`. The "ambiguity" existed only in the invented implementation.

## The rule

If your finding depends on a specific implementation approach, check whether the code actually uses that approach. If it doesn't, your finding is about a hypothetical, not the system.
