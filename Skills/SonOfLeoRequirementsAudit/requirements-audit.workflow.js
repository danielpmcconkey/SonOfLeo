export const meta = {
  name: 'sonofleo-audit',
  description: 'SonOfLeo audit in batches: traceability + spec quality + code truthfulness + expert panel',
  phases: [
    { title: 'Baseline', detail: 'Scout repo state (batch 1) or load cached baseline (batch 2+)' },
    { title: 'Auditors', detail: 'Run this batch of auditors — reports written as they complete' },
    { title: 'Wrap', detail: 'Write disposition template (final batch only)' },
  ],
}

const input = typeof args === 'string' ? JSON.parse(args) : args
if (!input || !input.repoRoot || !input.runDir || !input.danStatement) {
  throw new Error('Required args: repoRoot, runDir, danStatement')
}
const REPO = input.repoRoot
const RUN_DIR = input.runDir
const DAN_STATEMENT = input.danStatement
const AUDIT_SCRIPT = `${REPO}/Skills/SonOfLeoRequirementsAudit/traceability-audit.sh`
const LEDGER_PATH = `${REPO}/Skills/SonOfLeoRequirementsAudit/resolved-findings.md`
const CONDUCT_PATH = `${REPO}/CompoundedLearnings/catalogs/audit-conduct.md`
const BATCH = input.batch || 1
const BATCH_SIZE = input.batchSize || 5
const CACHE_FILE = `${RUN_DIR}/.baseline-cache.json`

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
          category: { type: 'string', enum: ['ambiguity', 'insufficient-elaboration', 'contradiction', 'stale-reference', 'missing-requirement', 'enforcement-gap', 'test-gap', 'customer-gap', 'gaap-gap', 'idiom', 'architecture', 'maintainability', 'statement-delta', 'stale-ruling', 'other'] },
          location: { type: 'string', description: 'File path and/or REQ ID' },
          summary: { type: 'string', description: 'One sentence' },
          detail: { type: 'string', description: 'Full explanation with evidence' },
          suggestedAction: { type: 'string', description: 'Atomic action to resolve' },
          why: { type: 'string', description: 'Why this matters. For idiom findings, teach the underlying FP principle.' },
          resolutionOwner: { type: 'string', enum: ['fix-spec', 'fix-code', 'fix-test', 'dan-decides'], description: 'Who/what resolves this' },
          priorRuling: { type: 'string', description: 'If this re-raises a resolved-findings.md entry: which one and why. Omit otherwise.' },
        },
        required: ['id', 'category', 'location', 'summary', 'detail', 'suggestedAction', 'why', 'resolutionOwner'],
      },
    },
    noFindingsReasoning: { type: 'string', description: 'Required when findings is empty. What you checked, what you considered, and why nothing rose to finding level. "No findings" with no explanation is indistinguishable from a shallow run.' },
  },
  required: ['agentName', 'findings'],
}

const SCOUT_SCHEMA = {
  type: 'object',
  properties: {
    stateSummary: { type: 'string', description: 'Dense factual summary of current repo state for downstream auditors: branch, HEAD, domain inventory (specs, code, tests), REQ counts (active/withdrawn/waived/unenforceable) per spec, migration list. Facts only.' },
    behavioralSpecs: { type: 'array', items: { type: 'string' }, description: 'Repo-relative paths of every file in Specs/Behavioral/' },
    srcFiles: { type: 'array', items: { type: 'string' }, description: 'Repo-relative paths of every .fs file under Src/ (exclude obj/ and bin/)' },
  },
  required: ['stateSummary', 'behavioralSpecs', 'srcFiles'],
}

const CACHE_SCHEMA = {
  type: 'object',
  properties: {
    stateSummary: { type: 'string' },
    behavioralSpecs: { type: 'array', items: { type: 'string' } },
    srcFiles: { type: 'array', items: { type: 'string' } },
    traceability: { type: 'string' },
  },
  required: ['stateSummary', 'behavioralSpecs', 'srcFiles', 'traceability'],
}

// ---------------------------------------------------------------------------
// Shared prompt blocks — process and vision, never state
// ---------------------------------------------------------------------------
const AUTHORITY_HIERARCHY = `
AUTHORITY HIERARCHY (highest to lowest):
1. Dan's explicit decisions — stated verbally or written in specs
2. Specs/Definitions.md — terms whose meaning changes which requirements apply
3. Specs/Behavioral/ — testable requirement statements with REQ- IDs
4. CompoundedLearnings/ — judgment, interpretation, coding guidance
5. Actual code and config

When a lower authority contradicts a higher one, that is a finding.
`

const PRECEDENT_RULES = `
PRECEDENT LEDGER — ${LEDGER_PATH}
Read it before reporting. Prior rulings on audit findings. Treat as precedent, NOT law:
- Suppress ONLY when a ruling matches exactly — same requirement, same point.
- If matching takes any squinting, RE-RAISE and explain why in priorRuling.
`

const CONDUCT_RULES = `
AUDIT CONDUCT CATALOG — ${CONDUCT_PATH}
Read the catalog AND every article it lists BEFORE reporting findings. These are hard-won
rules about what constitutes a legitimate finding. Findings that violate conduct rules
will be discarded.
`

const VISION = `
PRODUCT VISION (Dan, 2026-07-05):
- SonOfLeo replaces LeoBloom. Import mechanism moves INTO the codebase as a first-class
  domain: staging, rules engine, dedup in staging. Current Python importers are scaffolding.
- Long term, data feeds an ML-adjacent retirement-planning engine. The ledger must capture
  data with the shape and fidelity that engine will need.
- System: cash-basis GAAP, USD-only, F# on .NET 10, PostgreSQL, NodaTime, xUnit.
`

const HYGIENE = `
RULES OF ENGAGEMENT:
- Read-only. Change NOTHING in the repo. Findings only.
- BdsNotes/ is archaeological — never scan, never cite as current.
- Evidence over vibes: every finding cites file paths / REQ IDs / line-level specifics.
- "Nice to have" is not a finding.
- Do NOT assign severity — that is Dan's prerogative, not the auditor's.
- If you have NO findings, you MUST populate noFindingsReasoning: what you checked, what you considered, and why nothing rose to finding level.
`

function formatReport(result) {
  if (!result || !result.findings || result.findings.length === 0) {
    const name = (result && result.agentName) || 'Unknown'
    const reasoning = (result && result.noFindingsReasoning)
      ? `\n\n## Reasoning\n\n${result.noFindingsReasoning}`
      : ''
    return `# ${name}\n\n_No findings._${reasoning}`
  }
  let md = `# ${result.agentName}\n\n`
  for (const f of result.findings) {
    md += `## ${f.id} — ${f.category}\n`
    md += `- **Location:** ${f.location}\n`
    md += `- **Summary:** ${f.summary}\n`
    md += `- **Resolution:** ${f.resolutionOwner}\n`
    if (f.priorRuling) md += `- **Prior ruling:** ${f.priorRuling}\n`
    md += `\n${f.detail}\n\n`
    md += `**Action:** ${f.suggestedAction}\n\n`
    md += `**Why:** ${f.why}\n\n---\n\n`
  }
  return md
}

// ============================================================================
// Phase 1 — Baseline
// ============================================================================
phase('Baseline')
const RUN_TESTS = !!input.runTests
let scout, traceability, testRun

if (BATCH === 1) {
  log('Scouting repo state and running traceability script...')

  const baselineTasks = [
    () => agent(
      `You are the state scout for a SonOfLeo audit. Repo: ${REPO}.

Derive the CURRENT state mechanically. Read:
- git: current branch, HEAD sha, last ~15 commit subjects
- Specs/README.md, Specs/Definitions.md
- Every file in Specs/Behavioral/ — count active, withdrawn, waived, and unenforceable
  REQ IDs per file
- The Src/ tree (every .fs, excluding obj/ and bin/) and each .fsproj's compile order
- The Tests/ tree — file list and approximate test counts ([<Fact>] and [<Theory>])
- DbMigrations/ — list migrations in order

Produce a dense, factual stateSummary — it becomes shared context for downstream
auditors. Include what they need to avoid flagging phantoms: which domains have specs,
which have code, which have tests.`,
      { label: 'scout', phase: 'Baseline', schema: SCOUT_SCHEMA }
    ),
    () => agent(
      `Run the mechanical traceability audit for SonOfLeo.

Execute: bash ${AUDIT_SCRIPT} ${REPO}

Capture the complete raw stdout regardless of exit code. Return it verbatim.`,
      { label: 'traceability-script', phase: 'Baseline' }
    ),
  ]

  if (RUN_TESTS) {
    baselineTasks.push(() => agent(
      `Build and test SonOfLeo at ${REPO}.

1. Find the .fsproj files, dotnet build.
2. Run Tests.Isolated and Tests.Integrated with dotnet test.
   Integrated needs a PostgreSQL test database — report an environment limitation if it
   cannot connect, not a test failure.
3. Report: build success/failure, per-suite counts, runtime, full text of failures.

Read-only except for build output.`,
      { label: 'test-run', phase: 'Baseline' }
    ))
  }

  const baselineResults = await parallel(baselineTasks)
  scout = baselineResults[0]
  traceability = baselineResults[1]
  testRun = baselineResults[2]
  if (!scout) throw new Error('Scout failed — cannot proceed without derived state')

  const baselineWriteFiles = [
    { name: '00-scout-state.md', content: `# Scout — Derived Repo State\n\n${scout.stateSummary}` },
    { name: '01-traceability.md', content: `# Traceability Script Output\n\n\`\`\`\n${traceability}\n\`\`\`` },
    { name: '02-test-run.md', content: `# Build & Test Run\n\n${testRun || (RUN_TESTS ? '(agent failed)' : '(skipped)')}` },
    { name: '03-dan-statement.md', content: `# Dan's Statement of Position\n\n${DAN_STATEMENT}` },
    { name: '.baseline-cache.json', content: JSON.stringify({
      stateSummary: scout.stateSummary,
      behavioralSpecs: scout.behavioralSpecs,
      srcFiles: scout.srcFiles,
      traceability: traceability || '',
    }) },
  ]
  await agent(
    `Create ${RUN_DIR} (mkdir -p) if needed, then write these files exactly as given:\n\n` +
    baselineWriteFiles.map(f => `FILE: ${RUN_DIR}/${f.name}\nBEGIN_CONTENT\n${f.content}\nEND_CONTENT`).join('\n\n'),
    { label: 'baseline-writer', phase: 'Baseline' }
  )
} else {
  log(`Batch ${BATCH}: loading cached baseline...`)
  const cached = await agent(
    `Read the file ${CACHE_FILE}. It contains a JSON object. Parse it and return the object exactly as structured.`,
    { label: 'cache-reader', phase: 'Baseline', schema: CACHE_SCHEMA }
  )
  if (!cached) throw new Error('Could not load baseline cache — run batch 1 first')
  scout = { stateSummary: cached.stateSummary, behavioralSpecs: cached.behavioralSpecs, srcFiles: cached.srcFiles }
  traceability = cached.traceability
}

const STATE_BLOCK = `
CURRENT REPO STATE (derived by scout this run — verify specifics yourself when load-bearing):
${scout.stateSummary}
`
const DAN_BLOCK = `
DAN'S STATEMENT (verbatim, this run):
${DAN_STATEMENT}

This is his mental model, not ground truth. Where the repo disagrees, flag it
(category: statement-delta).
`
const CONTEXT = `${AUTHORITY_HIERARCHY}\n${VISION}\n${DAN_BLOCK}\n${STATE_BLOCK}\n${PRECEDENT_RULES}\n${CONDUCT_RULES}\n${HYGIENE}`

// ============================================================================
// Phase 2 — Auditors (this batch only)
// ============================================================================
phase('Auditors')

const areaOf = f =>
  f.startsWith('Src/DataAccessLayer') ? 'dal'
  : f.startsWith('Src/InterfaceBridge') || f.startsWith('Src/SonOfLeoCli') ? 'interface'
  : f.startsWith('Src/Model') ? 'model'
  : f.startsWith('Src/ModelOrchestrator') ? 'orchestrator'
  : 'infrastructure'
const areas = {}
for (const f of scout.srcFiles) {
  const a = areaOf(f)
  areas[a] = areas[a] || []
  areas[a].push(f)
}

const auditors = []

// --- Ledger vet ---
auditors.push({
  label: 'ledger-vet',
  filename: '10-ledger-vet.md',
  prompt: `You are auditing the audit's own precedent ledger for staleness.

Read ${LEDGER_PATH}. For each ruling, check it against CURRENT specs in
${REPO}/Specs/ (Behavioral/, Definitions.md) and, where it concerns code, the current
code in ${REPO}/Src/.

Flag (category: stale-ruling) any ruling that:
- references a requirement that has been withdrawn, renumbered, or materially rewritten
- was scoped to a project phase that has passed
- is so broadly worded it could suppress findings Dan never intended to rule on

For each, say whether it should be kept, rewritten (propose the rewrite), or retired.
${CONDUCT_RULES}
${HYGIENE}`,
})

// --- Statement delta ---
auditors.push({
  label: 'statement-delta',
  filename: '10-statement-delta.md',
  prompt: `You compare Dan's mental model against the actual SonOfLeo repo state.

${DAN_BLOCK}
${STATE_BLOCK}

Go claim by claim. Verify each against the repo at ${REPO} yourself — do not take the
scout's summary on faith for anything load-bearing.

A finding is a FACTUAL CONTRADICTION: Dan said X, the repo says not-X. Things Dan
did not mention are NOT findings — his statement is a summary, not an inventory.
Do not infer gaps from omissions or stretch-read a claim into something it doesn't say.
If you have to argue that a statement "could be interpreted as" something problematic,
it is not a finding.

Every factual contradiction is a finding (category: statement-delta). Confirmations are
not findings, but list them briefly in a summary finding so Dan sees what checked out.
${CONDUCT_RULES}
${HYGIENE}`,
})

// --- Spec quality: one per behavioral spec ---
for (const specPath of scout.behavioralSpecs) {
  const specName = specPath.split('/').pop().replace('.md', '')
  auditors.push({
    label: `quality:${specName}`,
    filename: `10-quality-${specName}.md`,
    prompt: `You are a requirements-quality auditor for SonOfLeo, a personal-finance
double-entry ledger in F#.

YOUR SCOPE: ${REPO}/${specPath}

${CONTEXT}

CROSS-REFERENCE: Specs/Definitions.md, Specs/Behavioral/SystemWide.md, and any spec
your scope document cites by REQ ID.

CHECK:
1. Terms used consistently with Definitions.md?
2. Internal contradictions within the spec?
3. Contradictions with SystemWide.md or other behavioral specs it references?
4. Requirements ambiguous enough that two developers would implement differently?
5. Requirements insufficiently elaborated — WHAT is clear but not enough to implement?
6. Withdrawn table: are withdrawal reasons sound? Did any leave an uncovered gap?
7. Waived and Unenforceable tables: are reasons sound? Does the three-state rule hold
   (every active requirement tested, waived, or unenforceable)?

DO NOT flag: missing requirements (the panel owns gaps), style preferences.`,
  })
}

// --- Code truthfulness: one per area ---
for (const [area, files] of Object.entries(areas)) {
  auditors.push({
    label: `truthfulness:${area}`,
    filename: `10-truthfulness-${area}.md`,
    prompt: `You are a code-truthfulness auditor for SonOfLeo.

YOUR SCOPE — these source files (plus ${REPO}/DbMigrations/ where relevant):
${files.map(f => `- ${REPO}/${f}`).join('\n')}

${CONTEXT}

TRACEABILITY SCRIPT OUTPUT (mechanical — go beyond it):
${traceability}

LINKAGE: source code carries NO REQ annotations (retired 2026-07-31). Test names are
the only spec linkage. Your job is to read the code and specs independently, then check:

1. CORRECTNESS: does the code implement what the behavioral specs require for this area?
2. CONTRADICTION: code that behaves differently from what a spec requires.
3. PRACTICE: does code follow the practices in CompoundedLearnings/ catalogs?
4. SCHEMA: for migrations touching this area, do schema, nullability, defaults, and FK
   direction match spec requirements?

DO NOT flag: unimplemented specs (spec precedes code), style preferences.`,
  })
}

// --- Test efficacy: one per behavioral spec ---
const SPECIMENS_PATH = `${REPO}/Skills/TestWriter/references/bullshit-test-specimens.md`
const TESTS_README_PATH = `${REPO}/Tests/README.md`

for (const specPath of scout.behavioralSpecs) {
  const specName = specPath.split('/').pop().replace('.md', '')
  auditors.push({
    label: `efficacy:${specName}`,
    filename: `10-efficacy-${specName}.md`,
    prompt: `You are a test-efficacy auditor for SonOfLeo, a personal-finance double-entry
ledger in F#.

YOUR SCOPE: tests that cite REQ IDs from ${REPO}/${specPath}.
Find them by grepping Tests/ for the REQ prefix (e.g. REQ-AC, REQ-JE, REQ-STG).

REQUIRED READING (read ALL before reporting):
- The behavioral spec: ${REPO}/${specPath}
- Bullshit-test specimens: ${SPECIMENS_PATH}
- Test standards: ${TESTS_README_PATH}

${CONTEXT}

CHECK THESE FOUR THINGS. Every finding must cite the REQ ID, the test file and line,
and the specific assertion(s) that fail the criterion.

1. BEHAVIORAL COVERAGE — For each active REQ in this spec, does a citing test actually
   exercise the behavior described? A test that touches the right function but checks the
   wrong property is not coverage. Read the REQ text and the test body side by side. Apply
   the smell test: "if the function under test returned garbage of the right shape, would
   this test fail?" If no, the behavior is untested regardless of the citation.

2. ASSERTION QUALITY — Apply the six specimen patterns from the bullshit-test specimens
   doc. Specifically:
   - Hard-wired counts instead of derived expected values (Specimen 1)
   - Cowardly inequalities: >=, >, <> instead of exact equality (Specimen 2)
   - Count-only assertions that never inspect values (Specimen 3)
   - Untyped failure: Result.isError or string Contains instead of typed match (Specimen 4)
   - Exit-code-only as the sole assertion (Specimen 5)
   - Fox guarding the hen house: expected value derived from a function in the call chain
     of the function under test (Specimen 6)
   If an assertion matches a specimen pattern, cite which specimen and show the assertion.

3. NEGATIVE COVERAGE — For REQs that define rejection criteria (validation rules, illegal
   state transitions, boundary violations, "must reject when"), is there a test that sends
   the bad input and asserts the typed error? Not every REQ needs a negative test — only
   those with an explicit rejection behavior. Flag missing negative tests only when the REQ
   says something must fail and no test proves it does.

4. UNCITED BEHAVIOR — Test or code behavior with no REQ backing it:
   - Tests exercising logic not described in any spec (potential missing REQ)
   - REQs that describe multiple distinct behaviors (e.g. "validate X, Y, and Z") where
     only some are tested — the REQ is cited but coverage is partial
   - Code paths visible in Src/ that implement behavior not captured by any REQ in this
     spec or any other spec (potential missing REQ — check other specs before flagging)

DO NOT: run or modify anything. DO NOT flag style. DO NOT flag tests for REQs outside
your spec's scope — another efficacy auditor owns those.`,
  })
}

// --- Panel: customer ---
auditors.push({
  label: 'panel:customer',
  filename: '10-panel-customer.md',
  prompt: `You audit SonOfLeo AS ITS CUSTOMER — Hobson, Dan's comptroller agent, the primary
operator of the predecessor (LeoBloom) and intended primary user of this one.

${CONTEXT}

READ FIRST:
- Weekly routine: /home/dan/.claude/skills/leobloom:saturday/SKILL.md
- Real usage data: ${REPO}/HobsonsNotes/cli-requirements-from-leobloom-usage.md
- Current CLI: ${REPO}/Src/SonOfLeoCli/ and ${REPO}/Src/InterfaceBridge/InterfaceContracts/
- ${REPO}/Specs/Behavioral/NonGraphicalInterface.md

JUDGE AGAINST THREE HORIZONS:
1. NEAR — replacing LeoBloom's ledger operations: walk the Saturday routine and ask what
   today's CLI can and cannot do. For every query, judge the RETURN TYPE against what the
   consumer actually needs.
2. MID — imports as a first-class domain: does anything in the current design pre-commit
   us wrongly?
3. FAR — retirement/ML engine: is the ledger capturing what that engine needs?

Findings are capability gaps (category: customer-gap), ranked by how much of the
customer's week they block.`,
})

// --- Panel: GAAP ---
auditors.push({
  label: 'panel:gaap',
  filename: '10-panel-gaap.md',
  prompt: `You are a GAAP domain expert auditing SonOfLeo — a CASH-BASIS, USD-only, personal
double-entry ledger.

${CONTEXT}

READ: Specs/Behavioral/ (JournalEntryCrud.md, AccountCrud.md, FiscalPeriodCrud.md,
SystemWide.md, Money.md, DataIngestion.md), Specs/Definitions.md, and corresponding
code in Src/Model/Ledger/, Src/Model/DataIngestion/, and Src/ModelOrchestrator/.

EVALUATE:
1. JOURNAL MODEL: entry/line/reference/comment design, balanced-entry invariant,
   positive-amounts + entry-type model, period assignment.
2. VOIDING & CORRECTION: void-as-soft-delete, closed-period corrections. Sound under
   cash-basis GAAP?
3. PERIOD DISCIPLINE: fiscal-period model, is_open gating, date-inside-period rule.
4. FORWARD READINESS: trial balance and period close are next. Does the current model
   give closure everything it needs? Flag anything that requires RESHAPING, not adding.
5. CHART OF ACCOUNTS: types/hierarchy/activation rules.
6. DATA INGESTION: staging-to-ledger posting path, balanced-entry enforcement at
   ingestion, account code resolution at posting time, external reference construction.
   Anything that touches the ledger is within your mandate.

Cash-basis simplifications Dan chose deliberately are decisions, not findings.`,
})

// --- Panel: F#/DDD ---
auditors.push({
  label: 'panel:fsharp-ddd',
  filename: '10-panel-fsharp-ddd.md',
  prompt: `You are an F# and domain-driven-design expert reviewing SonOfLeo. Dan is hand-writing
this to TEACH HIMSELF to think functionally. For every finding, the "why" must articulate
the FP/DDD principle at stake.

${CONTEXT}

READ: all of ${REPO}/Src/ (skip obj/ and bin/), the .fsproj files (compile order is
load-bearing in F#), and skim Tests/ for how the API reads from a caller's seat.

EVALUATE:
1. TYPES: are illegal states unrepresentable? Smart-constructor discipline?
2. COMPOSITION: Result/error handling through ResultCE — consistent railway style?
3. MODULE BOUNDARIES: domain modules own type+validation+persistence; orchestrators
   compose across domains. Is it holding?
4. IDIOM: pattern matching, pipelines, partial application, F#-native signatures.
5. DDD: do types speak the accounting domain language? Boundary contracts held?

Genuine idiom violations only — not F# golf.`,
})

// --- Panel: architecture ---
auditors.push({
  label: 'panel:architecture',
  filename: '10-panel-architecture.md',
  prompt: `You are a software-architecture reviewer auditing SonOfLeo for corner-painting:
structural decisions that will hurt when the system grows.

${CONTEXT}

READ: .fsproj files, ${REPO}/Src/ layout, ${REPO}/Src/InterfaceBridge/InterfaceContracts/,
${REPO}/Src/DataAccessLayer/, DbMigrations/.

EVALUATE AGAINST WHAT'S COMING (staging domain, trial balance, period close, reporting,
reconciliation, analytics):
1. PROJECT STRUCTURE: will the current split hold?
2. GOD TYPES: modules accreting unrelated responsibilities.
3. DATABASE: schema shape risks for staging, closing, analytics.
4. BOUNDARIES: boundary-type strategy — does it scale to reporting?
5. COUPLING: anything the import domain will need that is currently private or hardcoded.

Every finding must name the FUTURE COST concretely.`,
})

// --- Panel: AI maintainability ---
auditors.push({
  label: 'panel:ai-maintainability',
  filename: '10-panel-ai-maintainability.md',
  prompt: `You are an expert in AI-agent-maintained codebases. SonOfLeo is hand-written by Dan
today, but the coding agent ("BD") will progressively take over — currently tests only,
later code proper.

${CONTEXT}

READ: Specs/ in full, Src/, Tests/ (BD's current territory),
Skills/SonOfLeoRequirementsAudit/.

EVALUATE:
1. SELF-SUFFICIENCY: could a fresh agent, given only the repo, infer the rules?
2. GUARDRAILS: what stops an agent from doing the wrong thing plausibly?
3. TRACEABILITY: the test-name linkage system — can an agent maintain it mechanically?
4. FAILURE AMPLIFICATION: small wrong edit passes build+tests but corrupts semantics.
5. SLICING: is work decomposable into agent-sized, verifiable slices?

Ranked by how badly BD could hurt the books before anyone noticed.`,
})

// --- Slice for this batch ---
const batchStart = (BATCH - 1) * BATCH_SIZE
const batchEnd = Math.min(batchStart + BATCH_SIZE, auditors.length)
const batchAuditors = auditors.slice(batchStart, batchEnd)
const isLastBatch = batchEnd >= auditors.length

if (batchAuditors.length === 0) {
  log(`Batch ${BATCH}: no auditors remaining (all ${auditors.length} covered).`)
} else {
  log(`Batch ${BATCH}: auditors ${batchStart + 1}–${batchEnd} of ${auditors.length} (parallel)`)

  await pipeline(
    batchAuditors,
    (aud, _, i) => {
      log(`[${batchStart + i + 1}/${auditors.length}] ${aud.label}`)
      return agent(aud.prompt, { label: aud.label, phase: 'Auditors', schema: REPORT_SCHEMA })
    },
    (result, aud) => {
      const content = formatReport(result)
      if (result && result.findings && result.findings.length > 0) {
        log(`${aud.label}: ${result.findings.length} finding(s)`)
      }
      return agent(
        `Create ${RUN_DIR} (mkdir -p) if needed, then write this file exactly as given. The delimiters BEGIN_CONTENT and END_CONTENT are structural markers — do NOT include them in the file.\n\nFILE: ${RUN_DIR}/${aud.filename}\nBEGIN_CONTENT\n${content}\nEND_CONTENT`,
        { label: `writer:${aud.label}`, phase: 'Auditors' }
      )
    }
  )
}

// ============================================================================
// Phase 3 — Wrap (final batch only)
// ============================================================================
if (isLastBatch) {
  phase('Wrap')
  log('Final batch. Building disposition template from all auditor reports...')

  await agent(
    `Read every file matching the pattern ${RUN_DIR}/10-*.md. For each file that contains
findings (indicated by ## headings with Location/Summary/Resolution fields), extract
each finding.

Write ${RUN_DIR}/99-disposition.md with this exact format:

# Disposition Record

| # | Auditor | ID | Summary | Owner | Status | Ruling | Date |
|---|---------|----|---------|----- -|--------|--------|------|
(one row per finding across ALL 10-*.md files, sequentially numbered starting at 001,
Auditor = the H1 heading of the source file, ID = the finding's ## heading ID,
Summary = the Summary field value, Owner = the Resolution field value, Status = pending,
Ruling and Date left empty)

If no findings exist across any file, write a single row:
| --- | --- | --- | No findings | --- | --- | --- | --- |

End with:

## Statuses
- **pending** — not yet reviewed
- **accepted** — finding valid, action assigned
- **overruled** — finding rejected with reason
- **deferred** — acknowledged, not acting now (add revisit trigger)`,
    { label: 'disposition-writer', phase: 'Wrap' }
  )
}

return {
  runDir: RUN_DIR,
  batch: BATCH,
  batchSize: BATCH_SIZE,
  totalAuditors: auditors.length,
  totalBatches: Math.ceil(auditors.length / BATCH_SIZE),
  rangeThisBatch: batchAuditors.length > 0 ? `${batchStart + 1}–${batchEnd}` : 'none',
  isLastBatch,
  nextBatch: isLastBatch ? null : BATCH + 1,
}
