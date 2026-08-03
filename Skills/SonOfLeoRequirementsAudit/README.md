# SonOfLeo Requirements Audit

Periodic multi-agent audit of the whole project: spec hygiene, code truthfulness,
and a five-lens expert panel (customer, GAAP, F#/DDD, architecture,
AI-maintainability). Rewritten 2026-08-03 per FT-1/2/3/4/8 rulings: sequential
auditors, no synthesis, no severity, conduct catalog wired in, progressive writes.

## Design rule

**Nothing in the workflow script may describe the state of the codebase.**
State is derived fresh each run by a scout agent; Dan's *belief* about the state
arrives as a required argument, and the gap between the two is itself an audit
output (the statement-delta section).

## Contents

| File | What |
|---|---|
| `requirements-audit.workflow.js` | The workflow script (Claude Code `Workflow` tool) |
| `traceability-audit.sh` | Mechanical REQ traceability check (also runnable standalone) |
| `resolved-findings.md` | Precedent ledger — Dan's prior rulings. Precedent, not law: agents suppress only exact matches, re-raise anything ambiguous. The audit also vets this file for staleness each run. |
| `Audit/<date>/` (repo root) | Per-run reports: `00-`–`03-` baseline artifacts, `10-*` one per auditor, `99-disposition.md` |

## Running it

From a Claude Code session (Hobson or BD), invoke the `Workflow` tool:

```
Workflow({
  scriptPath: "<repo>/Skills/SonOfLeoRequirementsAudit/requirements-audit.workflow.js",
  args: {
    repoRoot: "<absolute path to this clone>",
    runDir:   "<repoRoot>/Audit/<YYYY-MM-DD[a]>",
    danStatement: "<Dan's where-I-think-we-are paragraph, verbatim>",
    runTests: false,
    batch: 1,
    batchSize: 5
  }
})
```

`danStatement` is **required** — the run refuses to start without it. Get it from
Dan fresh each time; do not recycle an old one.

`runTests` defaults to false — Dan runs the suites himself in Rider. Set `true`
only when independent execution is wanted (the Integrated suite needs a reachable
test DB).

`batch` (default 1) and `batchSize` (default 5) control batched execution. Batch
1 runs baseline (scout + traceability) and caches results to `.baseline-cache.json`
in the run folder, then runs auditors 1–5. Subsequent batches load the cache and
run the next slice. The final batch also writes the disposition template. Invoke
with `batch: 2`, `batch: 3`, etc. to continue — the return value tells you the
next batch number and total count.

The run is read-only against the repo except for `runDir`. Auditors run
sequentially within each batch — ~20 auditors total, split across batches, plus
baseline and writer agents.

## After the run

1. Hobson walks Dan through each auditor's report one at a time
   (obligation-review style — one finding, discuss, next finding).
2. Dan's rulings go into `99-disposition.md` in the run folder.
3. Rulings that establish reusable precedent also go into `resolved-findings.md`.
4. Accepted actions go wherever they belong (spec edit, code fix, BD task) —
   the audit itself changes nothing.
