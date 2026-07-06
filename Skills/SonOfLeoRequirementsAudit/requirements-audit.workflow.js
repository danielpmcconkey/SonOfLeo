export const meta = {
  name: 'sonofleo-audit',
  description: 'Full SonOfLeo audit: mechanical traceability + spec quality + code truthfulness + five-lens expert panel + synthesis',
  phases: [
    { title: 'Baseline', detail: 'Scout the repo state, run traceability script, vet the precedent ledger, run the test suites' },
    { title: 'Quality', detail: 'Per-spec requirements quality review + statement-vs-reality delta' },
    { title: 'Truthfulness', detail: 'Verify REQ annotations and conventions against actual code, per code area' },
    { title: 'Panel', detail: 'Five expert lenses: customer, GAAP, F#/DDD, architecture, AI-maintainability' },
    { title: 'Synthesis', detail: 'Merge, dedupe, prioritize; write the run reports' },
  ],
}

// ============================================================================
// Inputs. NOTHING in this script may describe the state of the codebase —
// state is derived fresh each run by the Scout, and Dan's view of the state
// arrives via args. That is what keeps this skill re-runnable.
//
// args = {
//   repoRoot:     absolute path to the SonOfLeo repo clone being audited
//   runDir:       absolute path for this run's report output (dated)
//   danStatement: Dan's "where I think we are" paragraph, verbatim, REQUIRED
// }
// ============================================================================
const input = typeof args === 'string' ? JSON.parse(args) : args
if (!input || !input.repoRoot || !input.runDir || !input.danStatement) {
  throw new Error('Required args: repoRoot, runDir, danStatement (Dan must state where he thinks the project is before every run)')
}
const REPO = input.repoRoot
const RUN_DIR = input.runDir
const DAN_STATEMENT = input.danStatement
const AUDIT_SCRIPT = `${REPO}/Skills/SonOfLeoRequirementsAudit/traceability-audit.sh`
const LEDGER_PATH = `${REPO}/Skills/SonOfLeoRequirementsAudit/resolved-findings.md`

const REPORT_SCHEMA = {
  type: 'object',
  properties: {
    agentName: { type: 'string' },
    findings: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          id: { type: 'string', description: 'Short unique slug e.g. GAAP-3 or IDIOM-JE-1' },
          category: { type: 'string', enum: ['ambiguity', 'insufficient-elaboration', 'contradiction', 'stale-annotation', 'incorrect-annotation', 'missing-annotation', 'convention-violation', 'missing-requirement', 'enforcement-gap', 'test-gap', 'customer-gap', 'gaap-gap', 'idiom', 'architecture', 'maintainability', 'statement-delta', 'stale-ruling', 'other'] },
          severity: { type: 'string', enum: ['high', 'medium', 'low'] },
          location: { type: 'string', description: 'File path and/or REQ ID' },
          summary: { type: 'string', description: 'One sentence' },
          detail: { type: 'string', description: 'Full explanation with evidence' },
          suggestedAction: { type: 'string', description: 'Atomic action to resolve' },
          why: { type: 'string', description: 'Why this matters. For idiom findings, teach the underlying FP principle — Dan is using this project to learn to think functionally.' },
          resolutionOwner: { type: 'string', enum: ['fix-spec', 'fix-code', 'fix-annotation', 'fix-test', 'dan-decides'], description: 'Who/what resolves this' },
          priorRuling: { type: 'string', description: 'If this touches a resolved-findings.md entry: which one, and why you are re-raising anyway (ambiguous scope / direction change). Omit otherwise.' },
        },
        required: ['id', 'category', 'severity', 'location', 'summary', 'detail', 'suggestedAction', 'why', 'resolutionOwner'],
      },
    },
  },
  required: ['agentName', 'findings'],
}

const SCOUT_SCHEMA = {
  type: 'object',
  properties: {
    stateSummary: { type: 'string', description: 'Dense factual summary of current repo state: branch, HEAD, what domains exist in Specs/Behavioral, what code exists in Src, what tests exist, REQ counts (active/withdrawn/waived) per spec, migration list. Facts only, no judgment. This text is injected into every downstream auditor prompt.' },
    behavioralSpecs: { type: 'array', items: { type: 'string' }, description: 'Repo-relative paths of every file in Specs/Behavioral/' },
    srcFiles: { type: 'array', items: { type: 'string' }, description: 'Repo-relative paths of every .fs file under Src/ (exclude obj/ and bin/)' },
  },
  required: ['stateSummary', 'behavioralSpecs', 'srcFiles'],
}

// ---------------------------------------------------------------------------
// Shared prompt blocks. These describe PROCESS and VISION, never state.
// ---------------------------------------------------------------------------
const AUTHORITY_HIERARCHY = `
AUTHORITY HIERARCHY (highest to lowest):
1. Dan's explicit decisions — anything in Specs/Decisions.md or stated verbally
2. Specs/Definitions.md — terms whose meaning changes which requirements apply
3. Specs/Conventions/ — developer-facing rules enforced by review
4. Specs/Behavioral/ — testable requirement statements with REQ- IDs
5. Actual code and config

When a lower authority contradicts a higher one, that's a finding. When two items at the
same level contradict each other, that's also a finding.
`

const PRECEDENT_RULES = `
PRECEDENT LEDGER — ${LEDGER_PATH}
Read it before reporting. It records Dan's prior rulings on audit findings. Treat it as
precedent, NOT law:
- Suppress a finding ONLY when it matches a prior ruling exactly — same requirement, same
  point, zero interpretation required.
- If matching a ruling takes any squinting, RE-RAISE the finding and set priorRuling to
  the ledger entry name plus why you re-raised (ambiguous scope, or the project direction
  appears to have changed since the ruling).
Dan has changed direction several times without updating docs. A stale ruling silently
suppressing a live problem is worse than re-asking him.
`

const VISION = `
PRODUCT VISION (stated by Dan, 2026-07-05 — judge foundations against ALL of this):
- SonOfLeo replaces LeoBloom, but better. The import mechanism moves INTO the codebase as
  a first-class generalized domain: standardized staging, a standardized rules engine for
  classifying import lines, and deduplication living in staging — NOT in the ledger. The
  current external Python importers are scaffolding to be demolished, not integrated.
- Long term, SonOfLeo's data feeds an ML-adjacent retirement-planning engine (successor to
  Dan's Monte Carlo simulator): retirement prep, withdrawal strategies. The app will
  eventually manage Dan's entire financial life. Ask whether the ledger is capturing data
  with the shape and fidelity that engine will need, or discarding signal that cannot be
  recovered later.
- System facts: cash-basis GAAP, USD-only, F# on .NET 10, PostgreSQL, NodaTime, xUnit.
`

const DAN_BLOCK = `
DAN'S STATEMENT OF WHERE HE THINKS THE PROJECT IS (verbatim, this run):
${DAN_STATEMENT}

This is his mental model, not ground truth. Where the repo disagrees with it, that
disagreement is itself valuable data — flag it (category: statement-delta) rather than
silently trusting either side.
`

const HYGIENE = `
RULES OF ENGAGEMENT:
- Read-only. You change NOTHING in the repo. Findings only.
- BdsNotes/ is an archaeological record — never scan it, never cite it as current.
- Evidence over vibes: every finding cites file paths / REQ IDs / line-level specifics.
- "Nice to have" is not a finding. Unenforceable or untestable requirements are legitimate
  (they get waived — that is a valid state).
`

// ============================================================================
// Phase 1 — Baseline (four independent agents, barrier: everything downstream
// needs the scout's state summary)
// ============================================================================
phase('Baseline')
const RUN_TESTS = !!input.runTests
log(`Scouting repo state, running traceability script, vetting precedent ledger${RUN_TESTS ? ', running tests' : ' (test run skipped — pass runTests: true to include it)'}...`)

const baselineTasks = [
  () => agent(
    `You are the state scout for a SonOfLeo audit. Repo: ${REPO} (audit whatever branch is
currently checked out — report which one it is).

Derive the CURRENT state of the project mechanically. Read:
- git: current branch, HEAD sha, last ~15 commit subjects
- Specs/README.md, Specs/Definitions.md, Specs/Decisions.md (titles/topics only)
- Every file in Specs/Behavioral/ — count active, withdrawn, and waived REQ IDs per file
- The Src/ tree (every .fs, excluding obj/ and bin/) and each .fsproj's compile order
- The Tests/ tree (Tests.Isolated and Tests.Integrated) — file list and approximate test
  counts (count [<Fact>] and [<Theory>] attributes)
- DbMigrations/ — list migrations in order

Produce a dense, factual stateSummary (facts only, no judgment) — it becomes the shared
context for a dozen downstream auditors, so include the things an auditor needs to avoid
flagging phantoms: which domains have specs, which have code, which have tests.`,
    { label: 'scout', phase: 'Baseline', schema: SCOUT_SCHEMA }
  ),
  () => agent(
    `Run the mechanical traceability audit for SonOfLeo.

Execute: bash ${AUDIT_SCRIPT} ${REPO}

The script may exit 1 (phantom references) — that is a report, not a failure; capture the
output either way. Return the complete raw stdout. Do not summarize or interpret.`,
    { label: 'traceability-script', phase: 'Baseline' }
  ),
  () => agent(
    `You are auditing the AUDIT'S OWN precedent ledger for staleness.

Read ${LEDGER_PATH}. For each ruling, check it against the CURRENT specs in
${REPO}/Specs/ (Behavioral/, Conventions/, Definitions.md, Decisions.md) and, where the
ruling concerns code, the current code in ${REPO}/Src/.

Flag (category: stale-ruling) any ruling that:
- references a requirement that has since been withdrawn, renumbered, or materially rewritten
- was scoped to a project phase that has since passed (e.g. "tests don't exist yet")
- appears overtaken by a later entry in Specs/Decisions.md
- is so broadly worded it could suppress findings Dan never intended to rule on

For each, say whether the ruling should be kept, rewritten (propose the rewrite), or
retired. ${HYGIENE}`,
    { label: 'ledger-vet', phase: 'Baseline', schema: REPORT_SCHEMA }
  ),
]
if (RUN_TESTS) {
  baselineTasks.push(() => agent(
    `Build and test the SonOfLeo solution at ${REPO}.

1. Locate the solution/projects (there may be no .sln at root — find the .fsproj files).
2. dotnet build the source and test projects.
3. Run Tests/Tests.Isolated and Tests/Tests.Integrated with dotnet test.
   The Integrated suite needs a PostgreSQL test database and environment configuration —
   if it cannot connect from this host, that is an ENVIRONMENT LIMITATION: report it as
   such, do not report the tests as failing.
4. Report: build success/failure, per-suite pass/fail/skip counts, total runtime, and the
   full text of any genuine failures.

Return a plain factual report. You may build/test but change no source files.`,
    { label: 'test-run', phase: 'Baseline' }
  ))
}

const [scout, traceability, ledgerVet, testRun] = await parallel(baselineTasks)

if (!scout) throw new Error('Scout failed — cannot brief downstream auditors without derived state')

const STATE_BLOCK = `
CURRENT REPO STATE (derived mechanically by the scout this run — trust this over any
assumption, and verify specifics yourself when a finding hinges on them):
${scout.stateSummary}
`
const CONTEXT = `${AUTHORITY_HIERARCHY}\n${VISION}\n${DAN_BLOCK}\n${STATE_BLOCK}\n${PRECEDENT_RULES}\n${HYGIENE}`

// ============================================================================
// Phases 2-4 — one big fan-out. Every agent below depends only on the
// baseline, so they all run concurrently; phase labels group the display.
// ============================================================================
phase('Quality')
log(`Fanning out: statement-delta, ${scout.behavioralSpecs.length} spec reviewers, conventions, code truthfulness, 5-lens panel...`)

// --- Statement vs reality ---------------------------------------------------
const statementDeltaTask = () => agent(
  `You compare Dan's mental model of the SonOfLeo project against its actual state.

${DAN_BLOCK}
${STATE_BLOCK}

Go claim by claim through Dan's statement. For each claim, verify it against the repo at
${REPO} yourself (read specs, code, tests — do not take the scout's summary on faith for
anything load-bearing). Also check the reverse direction: significant things that are true
of the repo but absent from Dan's statement (work he may have forgotten, or work BD/Dan
did that he hasn't registered).

Every disagreement — in either direction, at any size — is a finding
(category: statement-delta). Confirmations are not findings, but list them briefly in your
final summary finding so Dan sees what checked out. Pay particular attention to "tests for
all of that are solid" — that claim deserves scrutiny, not a nod: look for REQs without
tests (net of the Waived tables), tests that assert less than their REQ tag claims, and
whole layers with thin coverage.
${HYGIENE}`,
  { label: 'statement-delta', phase: 'Quality', schema: REPORT_SCHEMA }
)

// --- Requirements quality: one reviewer per behavioral spec, discovered ------
const specQualityTasks = scout.behavioralSpecs.map(specPath => () => agent(
  `You are a requirements-quality auditor for SonOfLeo, a personal-finance double-entry
ledger in F#.

YOUR SCOPE: ${REPO}/${specPath}

${CONTEXT}

CROSS-REFERENCE: Specs/Definitions.md, Specs/Decisions.md, Specs/Behavioral/SystemWide.md,
and any spec your scope document cites by REQ ID.

CHECK:
1. Terms used consistently with Definitions.md?
2. Internal contradictions within the spec?
3. Contradictions with SystemWide.md, Decisions.md, or other behavioral specs it references?
4. Requirements ambiguous enough that two reasonable developers would implement them
   differently?
5. Requirements insufficiently elaborated — WHAT is clear but not enough to implement or
   verify?
6. Withdrawn table: are withdrawal reasons sound? Did any withdrawal leave an uncovered gap?
7. Waived-from-testing table: are waiver reasons sound? Does the two-state rule hold
   (every active requirement either tested or waived)?

DO NOT flag: missing requirements (the panel owns gaps), style preferences.`,
  { label: `quality:${specPath.split('/').pop().replace('.md', '')}`, phase: 'Quality', schema: REPORT_SCHEMA }
))

const conventionsQualityTask = () => agent(
  `You are a requirements-quality auditor for SonOfLeo.

YOUR SCOPE: every file in ${REPO}/Specs/Conventions/ — internal consistency and
consistency with Definitions.md, Decisions.md, and the behavioral specs.

${CONTEXT}

CHECK:
1. Conventions contradicting behavioral requirements, Definitions.md, or Decisions.md
   (watch for stale conventions that predate a decision override)?
2. Conventions contradicting each other?
3. Ambiguous or insufficiently elaborated conventions?
4. Conventions that IMPLY a behavioral requirement with no REQ- ID?
5. Gaps between what conventions promise and what behavioral specs require?

DO NOT flag: whether code follows conventions (another agent owns that), style preferences.`,
  { label: 'quality:conventions', phase: 'Quality', schema: REPORT_SCHEMA }
)

// --- Code truthfulness: partition Src/ dynamically from the scout's file list
const areaOf = f =>
  f.startsWith('Src/Model/Ledger') ? 'model-ledger'
  : f.startsWith('Src/Model') ? 'model-core'
  : f.startsWith('Src/ModelOrchestrator') ? 'orchestrator'
  : f.startsWith('Src/SonOfLeoCli') ? 'cli'
  : 'utilities'
const areas = {}
for (const f of scout.srcFiles) {
  const a = areaOf(f)
  areas[a] = areas[a] || []
  areas[a].push(f)
}

const truthfulnessTasks = Object.entries(areas).map(([area, files]) => () => agent(
  `You are a code-truthfulness auditor for SonOfLeo.

YOUR SCOPE — these source files (plus ${REPO}/DbMigrations/ where relevant to them):
${files.map(f => `- ${REPO}/${f}`).join('\n')}

${CONTEXT}

THE TRACEABILITY SCRIPT'S MECHANICAL OUTPUT (do not re-derive it; go beyond it):
${traceability}

ANNOTATION CONVENTIONS (Specs/Conventions/Traceability.md — read it first):
- Enforceable requirements are annotated at the point of enforcement, at ALL enforcement
  points; spec documents never name source files — linkage lives at the destination.

FOR YOUR FILES, READ THE CODE AT EVERY ANNOTATION SITE and check:
1. TRUTHFULNESS: does the code actually enforce what the annotated REQ says? Check the
   spec text, not the annotation's vibe.
2. INCORRECT: annotated but not enforced, or enforced differently than specced.
3. MISSING: code that clearly enforces a requirement without annotating it.
4. STALE: annotations referencing withdrawn/renumbered REQs (cross-check the Withdrawn
   tables).
5. CONVENTION ENFORCEMENT: does this code follow Specs/Conventions/ (Temporal, Money,
   Naming, BuildAndEnvironment as applicable)? Cite the convention line and the deviation.
6. For migrations touching your area: do schema, nullability, defaults, and FK direction
   match what the specs and Temporal convention require (e.g. no DB-originated temporal
   values)?

DO NOT flag: unimplemented specs (spec precedes code here), style preferences.`,
  { label: `truthfulness:${area}`, phase: 'Truthfulness', schema: REPORT_SCHEMA }
))

const testTruthfulnessTask = () => agent(
  `You are a test-truthfulness auditor for SonOfLeo.

YOUR SCOPE: ${REPO}/Tests/ (both Tests.Isolated and Tests.Integrated).

${CONTEXT}

TEST RUN RESULTS FROM THIS AUDIT:
${testRun || `(no test run this audit — Dan runs the suites himself in Rider. For evidence of
recency, check Rider's session logs: the newest file in
~/.cache/JetBrains/Rider*/log/UnitTestLogs/Sessions/ — UTF-16LE, decode with
"iconv -f UTF-16LE -t UTF-8". Extract session timestamp, scope, and element count
("Got N elements to run") and cite them. CAVEAT: "elements" counts leaf tests PLUS their
container type/module nodes (e.g. 345 elements = 323 [<Fact>]s + 22 containers on
2026-07-05) — cross-check against a grep count of [<Fact>]/[<Theory>] attributes and cite
the leaf count. These logs prove the suite ran to completion, NOT per-test outcomes — do
not overclaim. Beyond that, audit statically; do not build or
run tests yourself.)`}

CHECK:
1. REQ-tagged tests: does the test body actually verify what the tagged requirement says?
   Prioritize the journal-entry tests (newest), but sample every area.
2. Tests asserting weaker properties than their REQ claims (e.g. "doesn't throw" standing
   in for "returns the specced error").
3. Active requirements with neither a test nor a Waived-table entry (the two-state rule).
4. Shared fixture/staging code (GenericTestProperties, _TestDataStage, _Cleanup,
   CliExecutor): hidden ordering dependencies, cleanup gaps, anything that would make the
   suites flaky or order-dependent as they grow.

DO NOT: run or modify anything. DO NOT flag style.`,
  { label: 'truthfulness:tests', phase: 'Truthfulness', schema: REPORT_SCHEMA }
)

// --- The five-lens expert panel ----------------------------------------------
const panelTasks = [
  () => agent(
    `You audit SonOfLeo AS ITS CUSTOMER. You represent Hobson, Dan's comptroller agent —
the primary operator of the predecessor system (LeoBloom) and the intended primary user
of this one. Dan is the PO; you are the user whose week runs through this tool.

${CONTEXT}

READ FIRST:
- The weekly routine this app must eventually absorb:
  /home/dan/.claude/skills/leobloom:saturday/SKILL.md (host path, outside the repo)
- Real usage data from the predecessor: ${REPO}/HobsonsNotes/cli-requirements-from-leobloom-usage.md
- The current CLI surface: ${REPO}/Src/SonOfLeoCli/ and ${REPO}/Src/Model/UI/InterfaceContractTypes.fs
- ${REPO}/Specs/Behavioral/NonGraphicalInterface.md

JUDGE AGAINST THREE HORIZONS:
1. NEAR — replacing LeoBloom's ledger operations: walk the Saturday routine phase by phase
   (imports aside) and ask what today's CLI can and cannot do: posting multi-line JEs,
   voiding with reasons, corrections, account activity review, balance queries,
   reconciliation-supporting reads. For every query that exists, judge the RETURN TYPE
   against what the consumer actually needs to look at — a query with the right name and
   the wrong fields is a gap (this exact mistake was made once already; see the withdrawn
   REQ-JE-3.4).
2. MID — imports as a first-class domain (staging, rules engine, dedup in staging): does
   anything in the CURRENT ledger design pre-commit us wrongly? (e.g. external-reference
   semantics, source field, comment linkage — are they shaped so a staging layer can dock
   cleanly?)
3. FAR — the retirement/ML engine: is the ledger capturing what that engine will need
   (dates, categorization fidelity, entry linkage, void history), or discarding
   unrecoverable signal?

Findings are capability gaps and mis-shaped surfaces (category: customer-gap), ranked by
how much of the customer's week they block. "It works but is awkward from a shell" is
also a finding if the Saturday routine would trip on it weekly.`,
    { label: 'panel:customer', phase: 'Panel', schema: REPORT_SCHEMA }
  ),
  () => agent(
    `You are a GAAP domain expert auditing SonOfLeo — a CASH-BASIS, USD-only, personal
double-entry ledger. Judge the whole ledger domain as built, not a wishlist for domains
that don't exist yet.

${CONTEXT}

READ: Specs/Behavioral/ (JournalEntryCrud.md, AccountCrud.md, FiscalPeriodCrud.md,
SystemWide.md, Money.md), Specs/Definitions.md, Specs/Decisions.md, and the corresponding
code in Src/Model/Ledger/ and Src/ModelOrchestrator/.

EVALUATE:
1. JOURNAL MODEL: entry/line/reference/comment design, the balanced-entry invariant,
   positive-amounts + entry-type model, period assignment derived from entry date.
2. VOIDING & CORRECTION: void-as-soft-delete excluded from balances, no reversal
   mechanism (offsetting entry + comment), closed-period corrections. Sound under GAAP
   for a cash-basis personal ledger? Any audit-trail hole?
3. PERIOD DISCIPLINE: fiscal-period model, is_open gating, date-inside-period rule.
4. FORWARD READINESS — this is the load-bearing part: trial balance and PERIOD CLOSE are
   the next slices. Does the current model give closure everything it needs (normal
   balance / account-type semantics for computing net income, retained-earnings landing
   spot in the account taxonomy, reopening policy, closing-entry representation)? Flag
   anything close will need that would require RESHAPING (not just adding to) what exists.
5. CHART OF ACCOUNTS: types/hierarchy/activation rules — accounting-sound?

Flag gaps only where they would cause accounting problems (category: gaap-gap or
missing-requirement). Cash-basis simplifications Dan chose deliberately (see Decisions.md)
are decisions, not findings.`,
    { label: 'panel:gaap', phase: 'Panel', schema: REPORT_SCHEMA }
  ),
  () => agent(
    `You are an F# and domain-driven-design expert reviewing SonOfLeo. Dan — a career C#
developer — is hand-writing this to TEACH HIMSELF to think functionally. Your findings
are teaching material: for every finding, the "why" must articulate the FP/DDD principle
at stake, not just the mechanical fix.

${CONTEXT}

READ: all of ${REPO}/Src/ (skip obj/ and bin/), the .fsproj files (compile order is
load-bearing in F#), and skim Tests/ for how the API reads from a caller's seat.

EVALUATE:
1. TYPES: are illegal states unrepresentable, or merely validated? Smart-constructor
   discipline — can invalid values be constructed by bypassing the constructors? Are
   primitives obsessively wrapped where it pays, and NOT where it doesn't?
2. COMPOSITION: Result/error handling through the ResultCE — consistent railway style, or
   does imperative C# thinking leak through (exceptions for domain errors, mutation,
   null-adjacent patterns)?
3. MODULE BOUNDARIES: the two-layer decision (domain modules own type+validation+
   persistence; orchestrators compose across domains — see Decisions.md 2026-06-06). Is it
   holding? Any domain logic leaking into orchestrators or the CLI, or cross-domain reach
   inside an entity module?
4. IDIOM: pattern matching vs if-chains, pipelines, partial application, function
   signatures that read as F# rather than transliterated C#.
5. DDD: do the types speak the accounting domain's language? Anemic records where behavior
   belongs? Boundary contracts (codes at the UI edge, UUIDs internal) held cleanly?

Rank by what teaches Dan the most. Genuine idiom violations only — not F# golf; where his
C#-accented F# is actually fine, do not manufacture sophistication.`,
    { label: 'panel:fsharp-ddd', phase: 'Panel', schema: REPORT_SCHEMA }
  ),
  () => agent(
    `You are a software-architecture reviewer auditing SonOfLeo for corner-painting:
structural decisions that will hurt when the system grows into its vision. STRUCTURE, not
plumbing — performance tuning and test mechanics are out of scope unless genuinely
alarming.

${CONTEXT}

READ: the .fsproj files and project graph, ${REPO}/Src/ layout, Src/Model/UI/
InterfaceContractTypes.fs (named god-type suspect — is everything-in-one-contracts-file
sustainable as domains multiply?), Src/Utilities/DAL.fs, DbMigrations/ (the schema as it
stands), Specs/Decisions.md, and HobsonsNotes/architecture-decisions-2026-06-19.md.

EVALUATE AGAINST WHAT'S COMING (staging domain + rules engine, trial balance, period
close, reporting, reconciliation, the analytics feed):
1. PROJECT STRUCTURE: is the Model / ModelOrchestrator / Cli / Utilities split going to
   hold, or does the next domain force an awkward wedge? F# compile order makes
   restructuring expensive later — flag it now if it's coming.
2. GOD TYPES: types or modules accreting unrelated responsibilities; single files that
   every change must pass through.
3. DATABASE: will the schema bite? Think: staging tables docking onto the ledger, closing
   entries, period-close bookkeeping, analytics extraction, migration re-runnability, the
   ledger under 10+ years of personal financial data (scale is small — the risk is shape,
   not volume).
4. BOUNDARIES: the boundary-type strategy (codes at edge, enriched boundary-only return
   types like REQ-JE-3.9's) — does it scale to a real reporting layer or does it breed a
   parallel shadow model?
5. COUPLING: anything the import/staging domain or the close process will need to reach
   into that is currently private, hardcoded, or single-purpose.

Every finding must name the FUTURE COST in concrete terms ("when you build X, this forces
Y").`,
    { label: 'panel:architecture', phase: 'Panel', schema: REPORT_SCHEMA }
  ),
  () => agent(
    `You are an expert in AI-agent-maintained codebases. SonOfLeo is hand-written by Dan
today, but the coding reins will progressively hand over to an AI agent ("BD" — currently
allowed to touch only tests, later the code proper). Audit whether this codebase is one
an AI agent can maintain safely and well WITHOUT the context that lives only in Dan's
head or in past conversations.

${CONTEXT}

READ: Specs/ in full (this is the agent's constitution), the Src/ tree, Tests/ (BD's
current territory — how legible is the test architecture he must extend?), and
Skills/SonOfLeoRequirementsAudit/ itself.

EVALUATE:
1. SELF-SUFFICIENCY: could a fresh agent, given only the repo, correctly infer the rules
   of the road? What load-bearing knowledge is undocumented (compile-order discipline,
   which layer owns what, why UUIDs stay internal, the two-state test rule, migration
   workflow, environment isolation)?
2. GUARDRAILS: what stops an agent from doing the WRONG thing plausibly — e.g. adding an
   update path for posted-JE fields, hard-deleting audit data, originating temporal values
   in the DB, bypassing smart constructors? Are the negative-existence requirements
   (REQ-JE-4.1, 6.1, 6.2 style) discoverable enough that an agent won't violate them
   innocently?
3. TRACEABILITY REGIME: is the REQ-annotation system one an agent can maintain
   mechanically, or does it depend on judgment that will drift?
4. FAILURE AMPLIFICATION: places where a small wrong edit passes build+tests but corrupts
   semantics (the expensive kind of AI mistake in a ledger).
5. SLICING: is work naturally decomposable into agent-sized, verifiable slices, or do
   changes fan out across files in ways that invite half-done edits (F# compile order both
   helps and hurts here — assess which).

Findings ranked by how badly BD could hurt the books before anyone noticed.`,
    { label: 'panel:ai-maintainability', phase: 'Panel', schema: REPORT_SCHEMA }
  ),
]

const fanout = await parallel([
  statementDeltaTask,
  ...specQualityTasks,
  conventionsQualityTask,
  ...truthfulnessTasks,
  testTruthfulnessTask,
  ...panelTasks,
])

// ============================================================================
// Phase 5 — Synthesis
// ============================================================================
phase('Synthesis')
const structuredResults = [ledgerVet, ...fanout].filter(Boolean)
const allFindings = structuredResults.flatMap(r => (r && r.findings) || [])
const droppedAgents = 1 + fanout.length - structuredResults.length + (ledgerVet ? 0 : 0)
log(`${allFindings.length} raw findings from ${structuredResults.length} auditors${droppedAgents > 0 ? ` (${droppedAgents} agent(s) failed and are missing from synthesis)` : ''}. Synthesizing...`)

const synthesis = await agent(
  `You are the synthesis agent for a SonOfLeo multi-lens audit. Merge the findings of
${structuredResults.length} specialist auditors into one prioritized, deduplicated report
for Dan (the project owner) and Hobson (his comptroller agent, who will walk Dan through
it item by item).

${DAN_BLOCK}

RAW FINDINGS (JSON):
${JSON.stringify(allFindings, null, 2)}

BASELINE FACTS:
- Test run: ${typeof testRun === 'string' ? testRun.slice(0, 3000) : 'unavailable'}
- Traceability script output is filed separately in the run directory; preserve its
  phantom-reference findings if any auditor echoed them.

PRODUCE A MARKDOWN REPORT:
1. **Executive summary** — 5-8 sentences: overall health, the 3-5 findings that matter
   most, and a one-line verdict per lens (customer / GAAP / F#-DDD / architecture /
   AI-maintainability / spec hygiene).
2. **Statement vs reality** — its own section, first-class: where Dan's stated position
   and the repo disagree, in both directions.
3. **Findings by lens**, high severity first within each: ID, severity, location, the
   atomic action, the why. Merge duplicates found by multiple lenses (note the
   convergence — two lenses hitting the same spot raises confidence). Preserve
   resolutionOwner and any priorRuling notes.
4. **Precedent-ledger maintenance** — the stale-ruling findings: keep / rewrite / retire
   recommendations.
5. **Dan-decides docket** — every finding tagged dan-decides, phrased as a crisp decision
   question with the options and your recommendation.
6. **Counts** — total, by severity, by lens, by resolutionOwner.

Atomic action means completable in one edit or one decision. Do not pad; do not soften.
Return the full markdown text.`,
  { label: 'synthesizer', phase: 'Synthesis' }
)

// --- Write the run reports ----------------------------------------------------
const reports = [
  { name: '00-scout-state.md', content: `# Scout — Derived Repo State\n\n${scout.stateSummary}` },
  { name: '01-traceability-script.md', content: `# Traceability Script Output\n\n\`\`\`\n${traceability}\n\`\`\`` },
  { name: '02-test-run.md', content: `# Build & Test Run\n\n${testRun || (RUN_TESTS ? '(test-run agent failed)' : '(skipped — runTests not set; Dan runs the suites himself)')}` },
  { name: '03-dan-statement.md', content: `# Dan's Statement of Position (audit input)\n\n${DAN_STATEMENT}` },
]
structuredResults.forEach(r => {
  const slug = (r.agentName || 'agent').toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '')
  reports.push({
    name: `10-${slug}.md`,
    content: `# ${r.agentName}\n\n${(r.findings || []).map(f =>
      `## ${f.id} — ${f.severity.toUpperCase()} (${f.category})\n- **Location:** ${f.location}\n- **Summary:** ${f.summary}\n- **Resolution owner:** ${f.resolutionOwner}${f.priorRuling ? `\n- **Prior ruling:** ${f.priorRuling}` : ''}\n\n${f.detail}\n\n**Suggested action:** ${f.suggestedAction}\n\n**Why:** ${f.why}\n`
    ).join('\n---\n\n') || '_No findings._'}`,
  })
})
reports.push({ name: '99-synthesis.md', content: typeof synthesis === 'string' ? synthesis : JSON.stringify(synthesis, null, 2) })

await agent(
  `Create the directory ${RUN_DIR} (mkdir -p) and write the following files into it,
each with exactly the content given. Change nothing else anywhere.

${reports.map(r => `FILE: ${RUN_DIR}/${r.name}\nCONTENT:\n${r.content}\n===END FILE===`).join('\n\n')}`,
  { label: 'file-writer', phase: 'Synthesis' }
)

return {
  runDir: RUN_DIR,
  auditors: structuredResults.length,
  totalFindings: allFindings.length,
  bySeverity: {
    high: allFindings.filter(f => f.severity === 'high').length,
    medium: allFindings.filter(f => f.severity === 'medium').length,
    low: allFindings.filter(f => f.severity === 'low').length,
  },
  danDecides: allFindings.filter(f => f.resolutionOwner === 'dan-decides').length,
}
