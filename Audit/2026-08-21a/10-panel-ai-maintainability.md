# agentic-readiness

## STALE-HOOK-1 — stale-reference
- **Location:** CompoundedLearnings/articles/process/a-check-verdict-is-evidence-not-truth.md (line 50), CompoundedLearnings/articles/process/checks-read-the-tree-not-the-commit.md (line 14-15)
- **Summary:** Two CompoundedLearnings articles state the pre-commit hook is uninstalled, but it was re-enabled on 2026-07-31 and is currently installed and working.
- **Resolution:** fix-spec

a-check-verdict-is-evidence-not-truth.md line 50 says: "Current status (2026-07-26): the pre-commit hook is switched off by Dan's decision." checks-read-the-tree-not-the-commit.md line 14-15 says: "Note: the hook is currently uninstalled (see a-check-verdict-is-evidence-not-truth.md), so nothing enforces this today." However, Checks/install-hooks.sh lines 7-9 document: "History: this hook was disabled 2026-07-26 because check-format produced intermittent false FAILs. Fantomas was dropped 2026-07-31 and check-format.sh deleted with it, which removed the only unreliable check. Re-enabled the same day." I verified the hook IS installed at .git/hooks/pre-commit and Checks/run-all.sh --quick passes (7 passed, 0 failed, 2 skipped). The articles were never updated when the hook was re-enabled. This matters because the CodeReviewer skill (Skills/CodeReviewer/SKILL.md line 42) explicitly directs agents to read a-check-verdict-is-evidence-not-truth.md when handling check failures. An agent following that article's instructions would read that the hook is off and that 'Checks/install-hooks.sh now requires --force' (also stale -- the current script requires no flag). The articles are load-bearing process documentation in the system that Specs/README.md calls 'the material nothing executes' -- exactly the material that 'just sits there being wrong.'

**Action:** Update both articles to reflect the current state: the hook was re-enabled 2026-07-31 when Fantomas was dropped and the unreliable check-format.sh was deleted. Remove the 'currently uninstalled' / 'switched off' language and the stale '--force' reference.

**Why:** CompoundedLearnings is the jurisprudence agents read before acting (CompoundedLearnings/README.md). Stale factual claims in load-bearing articles -- especially ones the CodeReviewer skill directly references -- undermine an agent's ability to self-orient from the repo alone. The operational risk is low (the hook IS on, so agents get caught regardless), but the self-sufficiency cost is real: a fresh agent cannot reconcile 'the hook is off' (article) with 'the hook just refused my commit' (reality) without external context.

---
