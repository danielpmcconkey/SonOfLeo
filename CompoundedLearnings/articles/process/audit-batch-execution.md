# Audit Batch Execution

Auditors run in parallel batches of 5 via `pipeline()`, with each auditor writing its
own report file independently as it completes. Reports are not held until the batch
finishes — an auditor that finishes early writes immediately.

## Why not sequential (FT-1 revision, 2026-08-03)

The original FT-1 learning prescribed one-auditor-at-a-time execution. The 2026-08-03a
audit revealed this was the wrong design: sequential execution hid progress (no output
until each auditor finished) and risked losing an entire run if a later auditor failed —
earlier results had not yet been written.

Parallel-batched execution with independent writes solved both problems. Wall-clock time
dropped from sum-of-all-auditors to max-of-batch, and a failure in one auditor no longer
jeopardized the others' output.

## What stays sequential

Disposition review is still one auditor at a time — Dan reviews findings per auditor, not
clubbed across auditors. The parallelism is in execution and report writing, not in review.

## Provenance

- Original FT-1: sequential execution (pre-2026-08-03)
- Revised: 2026-08-03a audit, action item #10
- Workflow: `Skills/SonOfLeoRequirementsAudit/requirements-audit.workflow.js`
