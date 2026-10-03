# Process

Operational choreography — how work moves through the SonOfLeo household: reviews,
migrations, guardrails, traceability.

| Concept | Article | Read when... |
|---|---|---|
| Guardrail triage | (rule lives in `Skills/CreateLearning/SKILL.md`, Step 7) | A new problem or violation class surfaces and you're deciding where its remediation belongs |
| Checks read the tree, git records the index | `articles/process/checks-read-the-tree-not-the-commit.md` | You're writing or modifying anything in `Checks/` or `.git/hooks/`, or a commit passed its gate and you're about to trust that it was inspected |
| A check verdict is evidence, not truth | `articles/process/a-check-verdict-is-evidence-not-truth.md` | `Checks/run-all.sh` just failed — before you run a formatter, edit the named files, or reach for `--no-verify` |
| Audit batch execution | `articles/process/audit-batch-execution.md` | Modifying the audit workflow, or wondering why auditors run in parallel batches instead of sequentially |

Standing rules not yet needing articles: migration review is always Dan's/Hobson's job
before anything is applied; an agent hands work off (final report, request to merge) only
after a green build, test run and `bash Checks/run-all.sh` on its branch, with the output in
the report (see `Skills/CodeReviewer/SKILL.md`).
