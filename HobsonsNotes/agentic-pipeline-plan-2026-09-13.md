# Agentic Development Pipeline — Plan

Written 2026-09-13. Dan and Hobson, conversationally. Dan is pivoting SonOfLeo to
full agentic development. Simian's velocity on the CashFlow build proved the model;
this plan formalises and automates it.

## Design principles

1. **State machine lives in SonOfLeo, not Nightshift.** Nightshift was built to be
   general-purpose. SonOfLeo's domain knowledge (SrcDeveloper skill, CRUD shape,
   naming canon, compile-order rules, authority hierarchy, CompoundedLearnings catalogs)
   is too specific to shove through DSWF addenda. A bespoke engine that lives alongside
   the code it builds can read the skills and specs directly.

2. **Artifact-driven.** Every step (agent or state machine) writes an artifact for the
   next step. Postgres for state machine state (card status, dependencies, blocker log,
   run history). Files for agent artifacts (specs, plans, code review findings, audit
   reports). State is what the engine reads to decide what to do next; artifacts are what
   agents read to do their work.

3. **Three orchestration modes.** Layers 1–4 are conversational — Dan in Claude Code
   with each agent, human-in-the-loop. Layer 5 is the autonomous build pipeline — cards
   through the kanban, Foreman for judgment, blocker protocol for Dan. Layer 6 is
   Hobson-led integration — Hobson owns the quality gate and assigns remediation work.

4. **Run slowly.** The engine can work while Dan sleeps or while he's at his day job.
   Configurable daily agent-invocation budget. Deliberate delay between cards. The
   priority is correctness, not speed.

5. **Dumb orchestrator, smart agents, fresh contexts.** Proven in OGRE. The engine is a
   deterministic state machine with zero LLM in it. All intelligence lives in the agent
   blueprints. Every agent gets a fresh context — no conversation history, no context rot.

---

## The full pipeline

### Layer 0 — Branch setup

**Actor:** Dan, manual.

Dan creates a feature branch for the slice off `main`.

### Layer 1 — Strategy and planning

**Actors:** Dan + Hobson (Opus 4.6), conversational in Claude Code.

1.1. Dan and Hobson dive deep on the slice — what it does, why it matters, how it fits
     the roadmap, what constraints apply.

1.2. Hobson writes the behavioural spec for the slice. REQ IDs, valid/invalid states,
     lifecycle constraints — the same shape as the existing specs in `Specs/Behavioral/`.

1.3. Dan reviews the spec. Iterate until ratified.

**Artifact:** ratified spec committed to `Specs/Behavioral/`.

### Layer 2 — Technical architecture

**Actors:** Dan + Simian (Opus 5 or Fable), conversational in Claude Code.

2.1. Dan and Simian discuss the technical approach. Which types go in which layer. Which
     functions go in Model vs ModelOrchestrator vs InterfaceBridge. What the CRUD shape
     looks like for new entities. How the compile order changes.

2.2. Simian produces a technical plan. This includes:
   - New types and which project/folder they live in.
   - New functions and which layer they belong to.
   - Changes to existing types/functions.
   - Compile-order changes (`.fsproj` entries).
   - Migration scripts needed.
   - **A dependency-mapped breakdown of work into Nightshift-sized cards.** Each card is
     one buildable, testable, reviewable unit. Cards carry explicit dependencies: card B
     cannot start until card A completes. Cards with no dependency relationship may
     execute in any order.

2.3. Dan reviews the technical plan. Iterate until ratified.

**Artifact:** ratified technical plan, stored as a file in `HobsonsNotes/` (per the
existing rule that no `.md` agent files go in the repo root).

### Layer 3 — Test planning

**Actors:** Dan + BD (Opus 5), conversational in Claude Code.

3.1. Dan and BD discuss the testing approach.

3.2. BD writes a test plan. BD sees **only the behavioural spec from Layer 1** — not the
     technical plan. This is deliberate: the test plan validates Dan's intent (the spec),
     not Simian's interpretation of it (the architecture). BD adjusts after reading the
     code, which is the right order.

3.3. Dan reviews the test plan. Iterate until ratified.

**Artifact:** ratified test plan, stored alongside the technical plan.

### Layer 4 — Audit the plans

**Actors:** Hobson (Opus 4.6) coordinates.

4.1. Hobson runs a plan-level audit. Not a full code audit — the code doesn't exist yet.
     This checks:
   - Spec internal consistency (same checks as the `quality:*` auditors in the current
     audit workflow).
   - Spec-to-spec consistency (new spec vs existing specs — contradictions, term drift).
   - Technical plan fidelity to the spec (does the plan implement what the spec requires?
     Does it add things the spec doesn't describe?).
   - Test plan coverage (does the test plan have a strategy for every active REQ?).
   - Dependency map soundness (are the card dependencies correct? Is the ordering
     buildable?).

4.2. Dan and Hobson revise as needed.

**Artifact:** audit findings and disposition record.

### Layer 5 — Build

This is the autonomous pipeline. The state machine drives it.

#### 5.1 — Card creation

**Actor:** BD (Opus 4.6), as Nightshift Tzar.

BD takes the ratified technical plan and produces individual cards in the state machine's
Postgres kanban. Each card carries:
- Title and description (the work unit).
- The REQ IDs it implements.
- Pointers to the relevant sections of the technical plan.
- Pointers to the relevant sections of the test plan.
- Dependencies on other cards (by card ID).
- Priority (execution order within the dependency constraints).

#### 5.2 — Card review

**Actor:** Simian (Opus 5).

Simian reviews the cards for fidelity to the technical plan. Are the dependencies right?
Is each card scoped to one buildable unit? Does the card description carry enough context
for a fresh agent to build it?

**Artifact:** reviewed card set, ready for the engine.

#### 5.3 — Card execution (state machine)

The engine processes cards one at a time, skipping blocked cards and picking up the next
unblocked one. Single-threaded execution on one release branch — each card commits
sequentially. The dependency model means "don't start B until A is done," not "run A and
B in parallel."

For each card:

**5.3.1 — Build agent reads requirements.**
Agent: Opus 4.6 builder, with the SrcDeveloper skill loaded as context.
The agent reads the card's description, the referenced REQ IDs from the spec, and the
referenced sections of the technical plan. It also reads the CompoundedLearnings catalogs
relevant to the work.

**5.3.2 — Question gate.**
If the build agent has questions it cannot resolve from the available context, it stops,
writes the questions to an artifact file, and returns FAIL. The state machine routes to
the Foreman.

**5.3.3 — Foreman judgment.**
Agent: Opus 5.
The Foreman reads the build agent's questions and the card context. If the Foreman can
resolve the questions from the spec, the technical plan, and the CompoundedLearnings, it
writes clarifications and the card retries at 5.3.1 with the injected context. If the
Foreman cannot resolve the questions, the card is blocked. **Dependent cards are also
blocked.** Independent cards remain eligible for processing.

**5.3.4 — Build.**
Agent: Opus 4.6 builder.
The agent implements the card's requirements. It follows the SrcDeveloper skill's rules:
no commits, no staging, build after every change, surface blockers instead of guessing.
On success, the engine commits the changes.

**Commit point.** The engine (not the agent) runs `git add` on the changed files and
commits with a structured message: `card {id}: {title}`.

**5.3.5 — Code review.**
Agent: Opus 4.6, using a new SonOfLeo-specific code review skill (see §Build artifacts
below).
The agent reviews the diff from 5.3.4 against the card's requirements, the spec, the
SrcDeveloper skill, and the CompoundedLearnings. Returns APPROVED, CONDITIONAL, or
REJECTED.

**5.3.6 — Review dispute resolution.**
If code review returns CONDITIONAL or REJECTED, the Foreman (Opus 5) reads both the
review findings and the code. The Foreman determines who's right — the builder or the
reviewer. The builder revises. **Always another code review after revision.** On success,
the engine commits.

**Commit point** (if revision occurred).

**5.3.7 — Test planning.**
Agent: Opus 4.6 test agent.
The agent reads the card's REQs, the code from 5.3.4/5.3.6, and the test plan from
Layer 3. It identifies which tests to write for this card. Questions go to the Foreman.

**5.3.8 — Test build.**
Agent: Opus 4.6 test agent.
The agent writes the tests. Tests must pass. On success, the engine commits.

**Commit point.**

**5.3.9 — Test failure triage.**
If tests fail, the Foreman (Opus 5) reviews and determines the cause:
- Code needs revision → route back to 5.3.4.
- Tests need revision → route back to 5.3.8.
- Test data needs revision → route back to 5.3.8 with context.
- Failure is due to code outside this card's scope → block the card.

**5.3.10 — Card complete.**
All tests pass, code review approved. The engine marks the card as dev-complete.

**Commit point** (final, if any outstanding changes).

#### 5.4 — Slice integration

All cards are dev-complete.

**5.4.1 — Regression test.**
The engine runs the full test suite (`dotnet test` on all test projects). Failures go to
the Foreman for triage.

**5.4.2 — Full-slice code review.**
Agent: Opus 5.
Reviews the entire slice diff (all cards together) — both `Src/` and `Tests/`. This is
the architectural review: does the slice hang together? Are there cross-card
inconsistencies? Does the compile order hold end to end?

### Layer 6 — SIT (System Integration Testing)

**Actor:** Hobson (Opus 4.6), with first-round adjudication authority.

6.1. Hobson runs `bash Checks/run-all.sh` — the full mechanical checks suite.

6.2. Hobson runs the full audit suite (the existing `requirements-audit.workflow.js`
     pattern, or the state-machine-driven version — see §Audit integration below).

6.3. Hobson adjudicates the audit findings. First round only — Hobson catches the obvious
     issues and assigns remediation. This is a new authority expansion: previously,
     adjudication was Dan's exclusive domain.

6.4. Hobson assigns work:
   - Spec issues → Hobson fixes them himself.
   - Test issues → BD fixes them.
   - Src issues → Simian fixes them.

6.5. Agents execute their assigned remediation work.

6.6. Hobson re-runs the audit.

6.7. Hobson reviews results. If anything remains unresolved or if any finding is beyond
     Hobson's jurisdiction, Hobson brings those specific items to Dan.

6.8. Dan adjudicates the remaining findings. Repeat 6.4–6.7 until Dan is satisfied.

### Layer 7 — Merge

**Actor:** Dan, manual.

Dan merges the release branch to `main`.

---

## State machine design

### Language and location

**Python.** Not F# — the engine is not domain code and must not live in `Src/`. Python
is proven (OGRE) and Dan's second language. The engine lives in a new top-level directory
in the SonOfLeo repo: `Pipeline/`.

```
Pipeline/
├── engine.py            # main loop
├── agent_invoker.py     # claude -p subprocess wrapper
├── card_processor.py    # step handler (Nightshift's StepHandler equivalent)
├── foreman.py           # foreman invocation and response parsing
├── db.py                # Postgres access (psycopg2)
├── models.py            # Card, Step, Blocker, RunHistory dataclasses
├── config.py            # engine config, budget, delays
├── blueprints/          # agent blueprint markdown files
│   ├── builder.md
│   ├── test-agent.md
│   ├── code-reviewer.md
│   └── foreman.md
├── sql/
│   ├── 001_create_schema.sql
│   └── 002_seed_workflow.sql
└── artifacts/           # per-card artifact directories (gitignored)
    └── {card_id}/
        ├── process/     # inter-agent artifacts
        └── deliverable/ # final outputs
```

### Database schema

New schema `pipeline` in the existing SonOfLeo PostgreSQL instance. Same patterns as
Nightshift's schema, adapted for SonOfLeo's needs.

```sql
CREATE SCHEMA IF NOT EXISTS pipeline;

-- Cards
CREATE TABLE pipeline.card (
    id              serial          PRIMARY KEY,
    title           text            NOT NULL,
    description     text            NOT NULL,
    req_ids         text[]          NOT NULL DEFAULT '{}',
    plan_refs       text[]          NOT NULL DEFAULT '{}',
    test_plan_refs  text[]          NOT NULL DEFAULT '{}',
    priority        integer         NOT NULL DEFAULT 3 CHECK (priority BETWEEN 1 AND 5),
    status          text            NOT NULL DEFAULT 'queued'
                    CHECK (status IN ('queued', 'in_progress', 'blocked',
                                      'dev_complete', 'complete')),
    current_step    text,
    created_at      timestamptz     NOT NULL DEFAULT now(),
    updated_at      timestamptz     NOT NULL DEFAULT now(),
    completed_at    timestamptz
);

-- Card dependencies (DAG)
CREATE TABLE pipeline.card_dependency (
    card_id         integer         NOT NULL REFERENCES pipeline.card(id),
    depends_on_id   integer         NOT NULL REFERENCES pipeline.card(id),
    PRIMARY KEY (card_id, depends_on_id),
    CHECK (card_id <> depends_on_id)
);

CREATE INDEX ix_card_dep_depends ON pipeline.card_dependency (depends_on_id);

-- Task queue (OGRE pattern)
CREATE TABLE pipeline.task_queue (
    id              bigserial       PRIMARY KEY,
    card_id         integer         NOT NULL REFERENCES pipeline.card(id),
    step_name       text            NOT NULL,
    status          text            NOT NULL DEFAULT 'pending'
                    CHECK (status IN ('pending', 'claimed', 'complete', 'failed')),
    created_at      timestamptz     NOT NULL DEFAULT now(),
    claimed_at      timestamptz,
    completed_at    timestamptz
);

CREATE UNIQUE INDEX ix_task_queue_one_active
    ON pipeline.task_queue (card_id)
    WHERE status IN ('pending', 'claimed');

-- Blockers
CREATE TABLE pipeline.blocker (
    id              serial          PRIMARY KEY,
    card_id         integer         NOT NULL REFERENCES pipeline.card(id),
    step_name       text            NOT NULL,
    agent_response  jsonb,
    foreman_assessment jsonb,
    context         text,
    created_at      timestamptz     NOT NULL DEFAULT now(),
    resolved_at     timestamptz,
    resolution      text
);

-- Run history
CREATE TABLE pipeline.run_history (
    id              bigserial       PRIMARY KEY,
    card_id         integer         NOT NULL REFERENCES pipeline.card(id),
    step_name       text            NOT NULL,
    model           text            NOT NULL,
    started_at      timestamptz     NOT NULL DEFAULT now(),
    completed_at    timestamptz,
    outcome         text,
    notes           text,
    tokens_used     integer
);

-- Engine config (singleton)
CREATE TABLE pipeline.engine_config (
    id              integer         PRIMARY KEY DEFAULT 1 CHECK (id = 1),
    engine_enabled  boolean         NOT NULL DEFAULT true,
    daily_budget    integer         NOT NULL DEFAULT 100,
    invocations_today integer       NOT NULL DEFAULT 0,
    budget_reset_at timestamptz     NOT NULL DEFAULT now(),
    delay_seconds   integer         NOT NULL DEFAULT 60,
    updated_at      timestamptz     NOT NULL DEFAULT now()
);

INSERT INTO pipeline.engine_config (id) VALUES (1) ON CONFLICT DO NOTHING;
```

### Workflow steps (hardcoded, not DB-driven)

Nightshift stores workflow steps in the DB because it serves multiple projects with
different pipelines. SonOfLeo has one pipeline. The steps are hardcoded in the engine:

```python
STEPS = [
    Step("build_read",     model="opus",  timeout=600,
         outcomes=["SUCCESS", "QUESTIONS"],
         transitions={"SUCCESS": "build", "QUESTIONS": "foreman_build"}),
    Step("foreman_build",  model="opus5", timeout=600,
         outcomes=["RESOLVED", "ESCALATE"],
         transitions={"RESOLVED": "build_read", "ESCALATE": "BLOCKED"}),
    Step("build",          model="opus",  timeout=1800,
         outcomes=["SUCCESS", "FAIL"],
         transitions={"SUCCESS": "code_review", "FAIL": "foreman_build"}),
    Step("code_review",    model="opus",  timeout=900,
         outcomes=["APPROVED", "CONDITIONAL", "REJECTED"],
         transitions={"APPROVED": "test_plan", "CONDITIONAL": "foreman_review",
                       "REJECTED": "foreman_review"}),
    Step("foreman_review", model="opus5", timeout=600,
         outcomes=["BUILDER_REVISE", "REVIEWER_WRONG", "ESCALATE"],
         transitions={"BUILDER_REVISE": "build_revise", "REVIEWER_WRONG": "test_plan",
                       "ESCALATE": "BLOCKED"}),
    Step("build_revise",   model="opus",  timeout=1200,
         outcomes=["SUCCESS", "FAIL"],
         transitions={"SUCCESS": "code_review_2", "FAIL": "foreman_build"}),
    Step("code_review_2",  model="opus",  timeout=900,
         outcomes=["APPROVED", "CONDITIONAL", "REJECTED"],
         transitions={"APPROVED": "test_plan",
                       "CONDITIONAL": "foreman_review_2",
                       "REJECTED": "foreman_review_2"}),
    Step("foreman_review_2", model="opus5", timeout=600,
         outcomes=["BUILDER_REVISE", "REVIEWER_WRONG", "ESCALATE"],
         transitions={"BUILDER_REVISE": "BLOCKED",
                       "REVIEWER_WRONG": "test_plan",
                       "ESCALATE": "BLOCKED"}),
    Step("test_plan",      model="opus",  timeout=600,
         outcomes=["SUCCESS", "QUESTIONS"],
         transitions={"SUCCESS": "test_build", "QUESTIONS": "foreman_test"}),
    Step("foreman_test",   model="opus5", timeout=600,
         outcomes=["RESOLVED", "ESCALATE"],
         transitions={"RESOLVED": "test_plan", "ESCALATE": "BLOCKED"}),
    Step("test_build",     model="opus",  timeout=1800,
         outcomes=["SUCCESS", "FAIL"],
         transitions={"SUCCESS": "COMPLETE", "FAIL": "foreman_test_fail"}),
    Step("foreman_test_fail", model="opus5", timeout=600,
         outcomes=["CODE_REVISE", "TEST_REVISE", "DATA_REVISE", "EXTERNAL", "ESCALATE"],
         transitions={"CODE_REVISE": "build_revise",
                       "TEST_REVISE": "test_build",
                       "DATA_REVISE": "test_build",
                       "EXTERNAL": "BLOCKED",
                       "ESCALATE": "BLOCKED"}),
]
```

Note: `opus5` is a placeholder for however the Opus 5 model ID resolves. The model tier
map is a config value, same as Nightshift.

### Agent invocation

Same pattern as Nightshift. Each agent is a fresh Claude CLI subprocess:

```python
subprocess.run([
    "claude", "-p",
    "--append-system-prompt", system_prompt,
    "--output-format", "text",
    "--model", model_id,
    "--dangerously-skip-permissions",
    "--no-session-persistence",
    prompt
], cwd=REPO_PATH, env=filtered_env)
```

The system prompt is the blueprint markdown. The prompt carries the card context, prior
step artifacts, and any injected context from the Foreman.

### Commit protocol

The engine owns all git operations. Agents never commit, never stage, never push.

After a successful build or test step:
1. `git add` the specific files the agent changed (not `git add .`).
2. `git commit -m "card {id}: {title} [{step}]"`.
3. `git add` / `git commit` only when the step produces file changes. If a code review
   passes with no changes, no commit.

The agent writes a manifest of changed files to its artifact directory. The engine reads
the manifest and stages exactly those files.

### Dependency-aware card selection

When the engine polls for the next card:

```sql
SELECT c.id FROM pipeline.card c
WHERE c.status = 'queued'
  AND NOT EXISTS (
      SELECT 1 FROM pipeline.card_dependency d
      JOIN pipeline.card dep ON dep.id = d.depends_on_id
      WHERE d.card_id = c.id
        AND dep.status NOT IN ('dev_complete', 'complete')
  )
ORDER BY c.priority, c.created_at
LIMIT 1
FOR UPDATE SKIP LOCKED;
```

A card is eligible only when all its dependencies are dev-complete or complete. Blocked
dependencies block their dependents transitively.

### Budget and pacing

The engine tracks invocations per day. Before each agent call:

```python
if config.invocations_today >= config.daily_budget:
    log("Daily budget exhausted. Pausing until reset.")
    wait_for_budget_reset()

config.invocations_today += 1
time.sleep(config.delay_seconds)
```

`budget_reset_at` resets `invocations_today` to 0 at midnight ET. Dan can adjust the
daily budget and delay via direct SQL on `pipeline.engine_config`.

This is crude but predictable. It does not track actual token usage or session limits —
it counts agent invocations as a proxy. A future version could integrate with Anthropic's
usage API if one becomes available.

### Blocker protocol

Same as Nightshift. When a card is blocked:
1. Card status → `blocked`.
2. Card's current step preserved (resume from where it stopped).
3. Blocker record created with full context.
4. Engine moves to the next unblocked card.
5. **All cards that depend on the blocked card are now ineligible** (the dependency query
   handles this automatically — blocked is not `dev_complete` or `complete`).

Dan resolves blockers in a morning conversation with BD or Hobson, then unblocks via
SQL. The engine picks up unblocked cards on the next poll.

---

## Build artifacts — what needs to be created

### 1. State machine engine (`Pipeline/`)

The Python engine described above. Components:

| File | Responsibility | Nightshift equivalent |
|---|---|---|
| `engine.py` | Main loop, startup, polling | `EngineWorker.cs` |
| `card_processor.py` | Step execution, transitions | `StepHandler.cs` |
| `agent_invoker.py` | Claude CLI subprocess | `AgentInvoker.cs` |
| `foreman.py` | Foreman invocation and parsing | `ReviewLoopHandler.cs` |
| `db.py` | Postgres operations | `*Repository.cs` files |
| `models.py` | Dataclasses | `Models/*.cs` |
| `config.py` | Engine config, budget | `EngineConfigRepository.cs` |

### 2. Agent blueprints (`Pipeline/blueprints/`)

Four blueprints, adapted from Nightshift's but with SonOfLeo's domain knowledge
referenced rather than injected via DSWF:

**`builder.md`** — the build agent.
- Must read: the SrcDeveloper skill, the card's referenced REQs, the referenced
  sections of the technical plan, the CompoundedLearnings catalogs (`coding.md`,
  `architecture.md`, `process.md`).
- Verification: `dotnet build` on touched projects, `bash Checks/run-all.sh --quick`.
- Never commits. Writes a file manifest to `artifacts/{card_id}/process/manifest.json`.
- Output: structured JSON with `outcome` (SUCCESS/FAIL/QUESTIONS) and `notes`.

**`test-agent.md`** — the test-writing agent.
- Must read: `Tests/README.md`, the card's REQs, the test plan, the code from the build
  step, the CompoundedLearnings `testing.md` catalog, and
  `Skills/TestWriter/references/bullshit-test-specimens.md`.
- Verification: `dotnet test` on the test project(s) touched.
- Output: structured JSON with `outcome` (SUCCESS/FAIL) and `notes`.

**`code-reviewer.md`** — see item 3 below.

**`foreman.md`** — adapted from Nightshift's `foreman-jurisdiction.md`.
- SonOfLeo-specific jurisdiction rules:
  - Authority hierarchy: Dan > Hobson > import script > classifier. A value written by
    a higher authority is never overridden by a lower one.
  - CompoundedLearnings catalogs are domain authority for coding, architecture, and
    testing questions.
  - Spec REQs are the source of truth for behavioural questions.
  - The SrcDeveloper skill is the source of truth for structural/naming questions.
- Expanded jurisdiction compared to Nightshift: the Foreman resolves code review
  disputes (5.3.6) and triages test failures across four categories (5.3.9).
- Output: structured JSON with `outcome` and `inject_context`, written to a file path
  provided in the prompt.

### 3. Code review skill (`Skills/CodeReviewer/` — replace existing)

The existing `SonOfLeo:CodeReviewer` skill is a self-review checklist for interactive
sessions. The new version must work as both:
- A Claude Code skill (for interactive use by Simian or Hobson).
- A standalone blueprint (for the pipeline's code review step).

The skill checks:
- **Gate 0:** `bash Checks/run-all.sh` + `dotnet build`. Hard gate — any failure rejects.
- **Pass 1:** "There's already a function for that" (existing check, against
  `Src/README.md`).
- **Pass 2:** SrcDeveloper compliance — naming canon, CRUD shape, compile order, comment
  discipline, standing permissions respected.
- **Pass 3:** Spec fidelity — does the code implement what the card's REQs require?
- **Pass 4:** CompoundedLearnings compliance — does the code follow the catalogs?
- **Pass 5:** Diff hygiene — no unrelated changes, no scope creep, no files outside the
  card's remit.

Output: APPROVED / CONDITIONAL / REJECTED with specific findings (file, line, what's
wrong, what the fix is).

### 4. Audit integration

The 36+ audit agents from the existing `requirements-audit.workflow.js` can be driven by
the state machine as a separate workflow. Instead of Hobson coordinating them manually in
a Claude Code session, they become cards in a special `audit` card set:

- One card per auditor (coverage:model, coverage:orchestrator, quality:CashFlow,
  truthfulness:dal, panel:gaap, etc.).
- Cards have no dependencies — they run in series, one per engine cycle.
- Each card writes its findings to `artifacts/{card_id}/deliverable/findings.md`.
- After all audit cards complete, the engine writes a disposition template (same as the
  current workflow's Wrap phase).
- Hobson reads the findings and disposition template in a morning session.

This is Layer 6.2. The state machine runs the auditors overnight; Hobson's morning
session is review and adjudication, not coordination.

**Implementation note:** the audit workflow is a second workflow definition alongside the
build workflow. The engine selects the active workflow from `pipeline.engine_config` or
from a CLI flag. Build and audit never run simultaneously.

### 5. Foreman jurisdiction document

The load-bearing document. Adapted from Nightshift's `foreman-jurisdiction.md` with
SonOfLeo-specific rules:

**Within jurisdiction (Foreman resolves autonomously):**
- Build agent asks a question that is answered by the spec, the technical plan, or the
  CompoundedLearnings. Foreman cites the source in `inject_context`.
- Code reviewer rejects on a CompoundedLearnings violation. Foreman verifies the
  violation is real and routes to revision.
- Code reviewer rejects on a naming canon violation. Foreman verifies against the
  SrcDeveloper skill and routes to revision.
- Test failure due to a typo, wrong assertion value, or missing test data. Foreman
  identifies the cause and routes to the appropriate retry.
- Code reviewer flags a subjective style preference with no CompoundedLearnings or
  SrcDeveloper basis. Foreman overrules the reviewer.

**Outside jurisdiction (Foreman escalates to Dan):**
- A question that requires domain knowledge not in the spec or catalogs.
- A spec ambiguity — two reasonable interpretations, neither clearly right.
- Scope change — the card's work requires changes the technical plan didn't anticipate.
- Architecture question — the card reveals a design issue that affects other slices.
- Second failure at the same review gate with no improvement.
- Any change to `AppError` cases beyond the standing-permission pattern.
- Anything the Foreman isn't sure about. Default is escalate.

---

## Git model

One release branch per slice. All cards commit sequentially to this branch. No per-card
branches — the engine is single-threaded and the dependency model ensures ordering.

```
main
 └── feat/slice-name
      ├── card 1: build commit
      ├── card 1: revision commit (if any)
      ├── card 1: test commit
      ├── card 2: build commit
      ├── ...
      └── card N: test commit
```

Dan merges `feat/slice-name` to `main` at Layer 7.

---

## Session limit management

The daily budget (`pipeline.engine_config.daily_budget`) is the primary control. Dan sets
it based on his subscription's limits. The engine counts agent invocations, not tokens —
tokens are not observable from the engine's side.

Rough sizing: a card that goes through build → code review → test plan → test build is
4 invocations minimum. With one Foreman call, 5. A 20-card slice at no failures is ~80
invocations. With realistic review loops, ~120. A daily budget of 30–50 lets the engine
process 6–10 cards per night.

The `delay_seconds` config adds a pause between invocations. Default 60 seconds. This
smooths consumption and makes the engine's progress observable in logs.

**No dynamic session-limit detection.** There is no API for checking remaining session
quota. The budget is a conservative static ceiling. Dan adjusts it based on observed
usage. If Anthropic adds a usage API, the engine can switch to real-time metering.

---

## What changes about existing roles

| Role | Before | After |
|---|---|---|
| **Dan** | Writes all `Src/` by hand in Rider. Reviews every line. | Writes specs and reviews plans (Layers 1–3). Reviews audit findings that Hobson escalates (Layer 6.8). Merges to main (Layer 7). |
| **Hobson** | Owns REQs only. Everything else Dan assigns per task. | Owns REQs (Layer 1.2). First-round audit adjudication with authority to assign remediation work (Layer 6.3–6.4). Still never touches `Src/` or `Tests/` directly. |
| **Simian** | Writes `Src/` under Dan's direction in Claude Code sessions. | Technical architect (Layer 2). Reviews cards for plan fidelity (Layer 5.2). Does not build — the pipeline's builder agents do. |
| **BD** | Writes `Tests/` on own branches. | Test planner (Layer 3). Nightshift Tzar — creates cards (Layer 5.1). Test remediation from audit (Layer 6.5). |
| **Pipeline agents** | (New.) | Fresh-context Claude CLI subprocesses. Builder, test agent, code reviewer, foreman. No memory, no state — artifacts are the only handoff. |

---

## Implementation order

1. **Database schema** — `Pipeline/sql/001_create_schema.sql`. Stand up the `pipeline`
   schema on the SonOfLeo database.

2. **Engine skeleton** — `Pipeline/engine.py`, `db.py`, `models.py`, `config.py`. The
   main loop, card polling, dependency checking, budget enforcement. No agent invocation
   yet — just the state machine moving cards through steps with stub outcomes.

3. **Agent invoker** — `Pipeline/agent_invoker.py`. The `claude -p` subprocess wrapper.
   Env filtering, timeout handling, output capture.

4. **Builder blueprint** — `Pipeline/blueprints/builder.md`. The first agent that does
   real work. Test it against a single known-good card (a piece of the existing
   CashFlow build that Simian has already completed, reverted, and re-built through the
   pipeline).

5. **Code review skill** — `Skills/CodeReviewer/SKILL.md` replacement. Make it work
   both as an interactive skill and as a pipeline blueprint.

6. **Test agent blueprint** — `Pipeline/blueprints/test-agent.md`.

7. **Foreman blueprint and jurisdiction** — `Pipeline/blueprints/foreman.md`.

8. **Card processor** — `Pipeline/card_processor.py`. The step handler that wires
   agents to transitions, manages commits, and routes failures.

9. **Integration test** — run a known slice through the full pipeline end to end.

10. **Audit integration** — adapt the existing audit workflow to run as pipeline cards.

---

## Open questions (for Dan)

1. **Where does the engine run?** The Docker sandbox (BD's territory) or the host
   (Hobson's territory)? The engine needs `claude` on PATH and git access to the repo.
   If it runs in the sandbox, it needs the host's SonOfLeo clone mounted.

2. **Who builds the engine?** This is Python, not F# — it's outside Simian's usual
   domain. BD has Python experience (OGRE). Hobson can coordinate but doesn't write
   code. Dan could build it manually. Or it could go through Nightshift itself, which
   would be poetic.

3. **Test database for pipeline testing.** The builder and test agents need to run
   `dotnet build` and `dotnet test`. The test suite needs a PostgreSQL test database.
   Is the existing test database available from wherever the engine runs?

4. **Model IDs.** Opus 5 and Fable are referenced in the pipeline. What are the actual
   model IDs for the `claude -p` `--model` flag? The engine's model tier map needs
   concrete values.

5. **The first slice.** Which piece of the remaining CashFlow build is the right
   candidate to prove the pipeline? Task 8 (PA classification) is partially built and
   has the most complexity. A simpler task like Task 16 (classification gets its own CLI
   domain) is more mechanical and lower-risk for a first run.

6. **Hobson's adjudication authority (Layer 6.3) — what's the boundary?** "First round
   catches the stupid stuff" is the stated intent. Concretely: can Hobson overrule an
   audit finding and close it as `overruled`, or only as `accepted` with assigned work?
   Can Hobson defer a finding? The disposition record needs clear status semantics.

---

## Future considerations (not for now)

- **Azure deployment** for true environment isolation (Dan's concept 5).
- **Dynamic session-limit metering** if Anthropic provides a usage API.
- **Parallel card execution** if the single-threaded model becomes a bottleneck.
- **Per-card branches** if rollback needs outgrow single-branch revert.
- **Cron-driven engine** vs manual `python engine.py` invocation.
