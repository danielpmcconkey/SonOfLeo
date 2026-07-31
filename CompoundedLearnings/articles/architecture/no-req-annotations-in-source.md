# No REQ Annotations in Source

**Source:** Dan's decision, argued with Hobson 2026-07-30 and restated 2026-07-31. Settled —
do not re-litigate.

Source code and migrations carry **no** `// REQ-XX-N.N` annotations. Requirement traceability
runs spec → test only. Test names begin with the requirement IDs they verify; that is the
whole linkage.

## Why

- **Nothing verifies them.** A test carrying a REQ ID executes: it passes, fails, or stops
  existing, and the audit sees all three. A comment carrying a REQ ID does none of that. It
  is exactly the "material nothing executes" that `Specs/README.md` blames for everything
  that rotted here.
- **"Every site, not just the first" was never checkable.** Deciding which sites bear load
  is a judgment call with no right answer and no checker, so annotation completeness was
  permanently unknown. An incomplete set reads as complete, which is worse than none.
- **They mark smells more than they mark contracts.** A requirement needing three annotations
  across two files is usually telling you the enforcement is scattered, not helping you find
  it.

## What works

- Test names carry the REQ IDs. `Tests/README.md` owns that convention.
- To answer "how is REQ-X enforced": grep `Tests/` for the ID, read the test, follow it into
  the code it exercises.
- `Skills/SonOfLeoRequirementsAudit/traceability-audit.sh` scans `Tests/` only.

## What doesn't

- Adding a REQ annotation to `.fs` or `.sql`, however tempting at a site that "clearly bears
  load."
- Reporting a missing source annotation as an audit finding. There is no such defect.
- Reading a surviving annotation as authority. ~373 predate this decision and are being
  removed; a stale one is noise, not a contract.

## Example

REQ-JE-1.56 (comment secondary link may be repointed or cleared), 2026-07-31. BD proposed
three annotation sites — a parameter type, a SET-clause builder, a boundary converter — and
flagged a fourth as a coin-flip. Four candidate sites for one small requirement, no way to
confirm the set was complete, and nothing that would notice if it drifted. Dan declined all
four. The two tests carrying `REQ-JE-1.56` in their names are the traceability.
