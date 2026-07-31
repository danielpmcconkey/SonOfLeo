# Verify Before Claiming Missing

**Source:** Audit 2026-07-06a — ML-6/ORCH-4 overrules, skill improvement items #56a, #72a

Before reporting that a requirement's enforcement is missing, grep the full repo for the REQ ID. Before citing a requirement as justification for a finding, quote the relevant text and verify the finding matches what the requirement actually says.

**Partly superseded (2026-07-31).** Source and migrations no longer carry REQ annotations, so "this requirement is unannotated" is not a defect and is never a finding — see [[no-req-annotations-in-source]] (`../architecture/no-req-annotations-in-source.md`). The examples below predate that decision and describe annotations as though they were load-bearing. The rule they teach — look before you claim — is unchanged; only the annotation half is void.

## What went wrong

- ML-6 claimed REQ-JE-2.8, 1.12, and 1.13 were unenforced. All three were already enforced and annotated — the agent just didn't look in the orchestrator.
- ORCH-4 claimed fetchByPeriod was missing a REQ-JE-3.3 annotation. The annotation was correctly placed in the CLI routing file. The agent didn't read the requirement to understand where enforcement belongs.
- ORCH-3 cited REQ-SYS-2.1.1 without reading it. The requirement says "entity's own properties" — the finding was about cross-line composite checks, which aren't entity properties.

## The rule

1. Grep `REQ-{ID}` across `Tests/` before claiming a requirement is unverified
2. Read the requirement text before citing it as justification
3. If the requirement says something different from what your finding assumes, the finding is wrong
4. Never raise a missing source annotation as a finding — there is no such defect
