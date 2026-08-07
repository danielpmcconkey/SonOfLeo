# Process

Operational choreography — how work moves through the SonOfLeo household: reviews,
migrations, guardrails, traceability.

| Concept | Article | Read when... |
|---|---|---|
| Guardrail triage | (rule lives in `Skills/CreateLearning/SKILL.md`, Step 7) | A new problem or violation class surfaces and you're deciding where its remediation belongs |
| Checks read the tree, git records the index | `articles/process/checks-read-the-tree-not-the-commit.md` | You're writing or modifying anything in `Checks/` or `.git/hooks/`, or a commit passed its gate and you're about to trust that it was inspected |
| A check verdict is evidence, not truth | `articles/process/a-check-verdict-is-evidence-not-truth.md` | `Checks/run-all.sh` or the pre-commit hook just failed — before you run a formatter, edit the named files, or reach for `--no-verify` |
| Release-candidate merge flow | `articles/process/release-candidate-merge-flow.md` | You're about to branch for a new task, you've found a second thing worth doing mid-task, or a session is ending with unmerged branches |
| Audit batch execution | `articles/process/audit-batch-execution.md` | Modifying the audit workflow, or wondering why auditors run in parallel batches instead of sequentially |

Standing rules not yet needing articles: migration review is always Dan's/Hobson's job
before anything is applied; BD presents work only after a green `Checks/run-all.sh`,
build, and test run (see `Skills/CodeReviewer/SKILL.md`).
