# SonOfLeo Requirements Audit

Periodic multi-agent audit of the whole project: spec hygiene, code truthfulness,
and a five-lens expert panel (customer, GAAP, F#/DDD, architecture,
AI-maintainability). Rewritten 2026-07-05 to be state-free and re-runnable.

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
| `Runs/<date>/` | Per-run reports: `00-`–`03-` baseline artifacts, `10-*` one per auditor, `99-synthesis.md` |

## Running it

From a Claude Code session (Hobson or BD), invoke the `Workflow` tool:

```
Workflow({
  scriptPath: "<repo>/Skills/SonOfLeoRequirementsAudit/requirements-audit.workflow.js",
  args: {
    repoRoot: "<absolute path to this clone>",        // Hobson: /media/dan/fdrive/ai-sandbox/workspace/SonOfLeo
    runDir:   "<repoRoot>/Skills/SonOfLeoRequirementsAudit/Runs/<YYYY-MM-DD[a]>",
    danStatement: "<Dan's where-I-think-we-are paragraph, verbatim>",
    runTests: false                                    // optional; true = audit builds and runs the suites itself
  }
})
```

`danStatement` is **required** — the run refuses to start without it. Get it from
Dan fresh each time; do not recycle an old one.

`runTests` defaults to false — Dan runs the suites himself in Rider, and the
audit reads Rider's session logs (`~/.cache/JetBrains/Rider*/log/UnitTestLogs/Sessions/`,
UTF-16) as evidence of recency and scope instead of re-running. Those logs prove
completion, not per-test outcomes. Set `runTests: true` only when independent
execution is wanted (the Integrated suite needs a reachable test DB; without one
the agent reports an environment limitation, not a failure).

The run is read-only against the repo except for `runDir`. ~20 subagents.

## After the run

1. Dan reads `99-synthesis.md`; Hobson walks him through it item by item
   (Saturday-exception style).
2. Every ruling Dan makes gets appended to `resolved-findings.md` with date,
   scope, and status (`overruled` / `deferred` + revisit trigger).
3. Accepted actions go wherever they belong (spec edit, code fix, BD task) —
   the audit itself changes nothing.
