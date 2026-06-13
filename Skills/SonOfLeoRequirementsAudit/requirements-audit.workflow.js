export const meta = {
  name: 'sonofleo-requirements-audit',
  description: 'Multi-agent requirements audit: quality, truthfulness, GAAP gaps, synthesis',
  phases: [
    { title: 'Traceability', detail: 'Run the mechanical traceability script' },
    { title: 'Requirements Quality', detail: 'Vet specs for ambiguity, insufficiency, contradictions' },
    { title: 'Code Truthfulness', detail: 'Verify annotations match reality and conventions match code' },
    { title: 'GAAP Coverage', detail: 'Domain expert evaluates requirement completeness' },
    { title: 'Synthesis', detail: 'Merge all findings into atomic action list' },
  ],
}

const REPO = '/workspace/SonOfLeo'
const AUDIT_SCRIPT = `${REPO}/Skills/SonOfLeoRequirementsAudit/traceability-audit.sh`
const RUN_DIR = args.runDir

const REPORT_SCHEMA = {
  type: 'object',
  properties: {
    agentName: { type: 'string' },
    findings: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          id: { type: 'string', description: 'Short unique slug e.g. AMB-AC-1.47' },
          category: { type: 'string', enum: ['ambiguity', 'insufficient-elaboration', 'contradiction', 'stale-annotation', 'incorrect-annotation', 'missing-annotation', 'convention-violation', 'missing-requirement', 'enforcement-gap', 'other'] },
          severity: { type: 'string', enum: ['high', 'medium', 'low'] },
          location: { type: 'string', description: 'File path and/or REQ ID' },
          summary: { type: 'string', description: 'One sentence' },
          detail: { type: 'string', description: 'Full explanation with evidence' },
          suggestedAction: { type: 'string', description: 'Atomic action to resolve' },
          why: { type: 'string', description: 'Why this matters' },
        },
        required: ['id', 'category', 'severity', 'location', 'summary', 'detail', 'suggestedAction', 'why'],
      },
    },
  },
  required: ['agentName', 'findings'],
}

const AUTHORITY_HIERARCHY = `
AUTHORITY HIERARCHY (highest to lowest):
1. Dan's explicit decisions — anything in Specs/Decisions.md or stated verbally
2. Specs/Definitions.md — terms whose meaning changes which requirements apply
3. Specs/Conventions/ — developer-facing rules enforced by review
4. Specs/Behavioral/ — testable requirement statements with REQ- IDs
5. Actual code and config

When a lower authority contradicts a higher one, that's a finding. When two items at the same
level contradict each other, that's also a finding.
`

const WHAT_BAD_MEANS = `
WHAT COUNTS AS A BAD REQUIREMENT:
- Ambiguous: you cannot determine what compliance looks like
- Insufficiently elaborated: the "what" is clear but there isn't enough to implement or verify
- Contradicts another requirement or contradicts a higher authority (see hierarchy)

WHAT IS NOT BAD:
- Unenforceable requirements are fine (some exist deliberately)
- Untestable requirements are fine (they get waived, and that's a legitimate state)
`

const ANNOTATION_RULES = `
ANNOTATION CONVENTIONS (from Specs/Conventions/Traceability.md):
- All enforceable requirements must be annotated at the point of enforcement in code
- Requirements enforced in multiple places must be annotated at ALL enforcement points
- Tests must annotate which requirements they verify
- Format: // REQ-XX-N.N in code comments
- Spec documents NEVER name source files — all linkage is at the destination (code/tests)
`

const PROJECT_STATE = `
PROJECT STATE:
- This is a hand-written F# rewrite of LeoBloom. Very early — under active construction.
- Branch: main
- Completed: Account CRUD model (create, read, update/deactivate), DAL, utility modules
- NOT completed: journal entries, obligations, interface/CLI, reporting
- Journal-related enforcement is explicitly deferred (REQ-AC-4.4 and REQ-AC-4.6 have todo comments)
- Tests exist in skeleton form only — 102 of 105 active requirements have no tests yet
- This is EXPECTED — spec precedes code in this waterfall approach, and tests haven't been the focus yet
`

// ============================================================================
// Phase 1: Traceability (mechanical)
// ============================================================================
phase('Traceability')
log('Running traceability audit script...')

const traceabilityResult = await agent(
  `Run the traceability audit script and capture its output.

Execute: bash ${AUDIT_SCRIPT} ${REPO}

Capture the FULL output. The script will exit 1 if there are phantom references — that's expected.
Return the complete stdout as your response text. Do not summarize or interpret — just return the raw output.`,
  { label: 'traceability-script', phase: 'Traceability' }
)

// ============================================================================
// Phase 2: Requirements Quality (3 agents in parallel)
// ============================================================================
phase('Requirements Quality')
log('Launching requirements quality reviewers...')

const qualityResults = await parallel([
  // Agent A: AccountCrud spec reviewer
  () => agent(
    `You are a requirements quality auditor for the SonOfLeo project, a personal finance
double-entry ledger system written in F#.

YOUR SCOPE: Review Specs/Behavioral/AccountCrud.md for requirements quality.

${AUTHORITY_HIERARCHY}

${WHAT_BAD_MEANS}

CROSS-REFERENCE THESE FILES:
- Specs/Definitions.md — authoritative term definitions (Money, Price, Entity, Instant, Date, etc.)
- Specs/Decisions.md — Dan's structural decisions (append-only log)
- Specs/Behavioral/SystemWide.md — cross-cutting policies that AccountCrud requirements must not contradict

${PROJECT_STATE}

SPECIFIC THINGS TO CHECK:
1. Does every requirement use terms consistently with Definitions.md?
2. Are there requirements that contradict each other within AccountCrud?
3. Are there requirements that contradict SystemWide.md or Decisions.md?
4. Are any requirements ambiguous enough that two reasonable developers would implement them differently?
5. Are any requirements insufficiently elaborated — you know WHAT but not enough to implement or verify?
6. The withdrawn table — are the withdrawal reasons sound? Did any withdrawal create an uncovered gap?
7. The waived-from-testing table — are the waiver reasons sound?
8. The promotion candidates section — any issues there?

DO NOT flag:
- Missing tests (tests haven't been written yet, that's expected)
- Requirements that are unenforceable or untestable (that's legitimate)
- Missing requirements (that's a different agent's job)

Read the files in ${REPO}/Specs/ to do your analysis. Be thorough but precise.`,
    { label: 'quality:account-crud', phase: 'Requirements Quality', schema: REPORT_SCHEMA }
  ),

  // Agent B: DAL + SystemWide spec reviewer
  () => agent(
    `You are a requirements quality auditor for the SonOfLeo project, a personal finance
double-entry ledger system written in F#.

YOUR SCOPE: Review Specs/Behavioral/DataAccessLayer.md and Specs/Behavioral/SystemWide.md.

${AUTHORITY_HIERARCHY}

${WHAT_BAD_MEANS}

CROSS-REFERENCE THESE FILES:
- Specs/Definitions.md — authoritative term definitions
- Specs/Decisions.md — Dan's structural decisions
- Specs/Behavioral/AccountCrud.md — entity-specific requirements that depend on these cross-cutting specs

${PROJECT_STATE}

SPECIFIC THINGS TO CHECK:
1. Does every requirement use terms consistently with Definitions.md?
2. Are there requirements that contradict each other within or between these specs?
3. Are there requirements that contradict Decisions.md?
4. Are there requirements that contradict AccountCrud.md? (e.g., SystemWide says X, but AccountCrud implies not-X)
5. Are any requirements ambiguous enough that two reasonable developers would implement them differently?
6. Are any requirements insufficiently elaborated?
7. SystemWide.md has a "todo" comment about logging audit activities — flag it, but understand it's a known gap.
8. REQ-SYS-5.1 says "perfectly reconstituted" — is this sufficiently precise given temporal precision constraints?

DO NOT flag:
- Missing tests
- Requirements that are unenforceable or untestable (legitimate)
- Missing requirements (different agent's job)

Read the files in ${REPO}/Specs/ to do your analysis.`,
    { label: 'quality:dal-syswide', phase: 'Requirements Quality', schema: REPORT_SCHEMA }
  ),

  // Agent C: Convention-vs-spec consistency
  () => agent(
    `You are a requirements quality auditor for the SonOfLeo project, a personal finance
double-entry ledger system written in F#.

YOUR SCOPE: Review all files in Specs/Conventions/ for internal consistency and consistency
with the behavioral specs and definitions.

${AUTHORITY_HIERARCHY}

${WHAT_BAD_MEANS}

FILES TO READ:
- All files in ${REPO}/Specs/Conventions/ (Traceability.md, Temporal.md, Money.md, Naming.md, BuildAndEnvironment.md, README.md)
- ${REPO}/Specs/Definitions.md
- ${REPO}/Specs/Decisions.md
- ${REPO}/Specs/Behavioral/AccountCrud.md (to check for contradictions)
- ${REPO}/Specs/Behavioral/DataAccessLayer.md
- ${REPO}/Specs/Behavioral/SystemWide.md

SPECIFIC THINGS TO CHECK:
1. Do conventions contradict any behavioral requirements?
2. Do conventions contradict Definitions.md?
3. Do conventions contradict Decisions.md? (Watch for stale conventions that predate a decision override)
4. Do conventions contradict each other?
5. Are any conventions ambiguous or insufficiently elaborated?
6. Do any conventions IMPLY a behavioral requirement that doesn't have a REQ- ID?
   (e.g., Money.md says "half-up rounding" — is that covered by a REQ or only by convention?)
7. Are there gaps between what conventions promise and what behavioral specs require?

DO NOT flag:
- Whether code follows conventions (different agent's job)
- Missing tests
- Style preferences

Read the files to do your analysis. Be thorough but precise.`,
    { label: 'quality:conventions', phase: 'Requirements Quality', schema: REPORT_SCHEMA }
  ),
])

// ============================================================================
// Phase 3: Code Truthfulness (2 agents in parallel)
// ============================================================================
phase('Code Truthfulness')
log('Launching code truthfulness reviewers...')

const codeResults = await parallel([
  // Agent D: Annotation auditor
  () => agent(
    `You are a code annotation auditor for the SonOfLeo project, a personal finance
double-entry ledger system written in F#.

YOUR JOB: Verify that REQ- annotations in code are truthful and correct.

${ANNOTATION_RULES}

${AUTHORITY_HIERARCHY}

The traceability audit script already found these mechanical problems:
${traceabilityResult}

Your job goes BEYOND the mechanical — you must READ THE CODE at each annotation site and
determine whether the code actually does what the requirement says.

FILES TO READ:
- ${REPO}/Src/Model/Ledger/Account.fs — the main Account module
- ${REPO}/Src/Model/Ledger/AccountComponent.fs — component types (AccountCode, AccountName, etc.)
- ${REPO}/Src/Model/Money.fs — Money type
- ${REPO}/Src/Model/Audit.fs — AuditEnvelope type
- ${REPO}/Src/Utilities/DAL.fs — Data Access Layer
- ${REPO}/Src/Utilities/Clock.fs — Clock utility
- ${REPO}/DbMigrations/2026-06-01-07-48-CreateAccountTable.sql — the migration
- ${REPO}/Specs/Behavioral/AccountCrud.md — the requirements being annotated
- ${REPO}/Specs/Behavioral/DataAccessLayer.md
- ${REPO}/Specs/Behavioral/SystemWide.md

SPECIFIC THINGS TO CHECK:
1. WITHDRAWN ANNOTATIONS: Code references REQs that have been withdrawn. For each one,
   identify the correct surviving requirement (usually in the "Reason" column of the
   Withdrawn table) and recommend updating the annotation.
2. TRUTHFULNESS: For each annotation, does the code at that site actually enforce the
   requirement? Example: if code annotates REQ-AC-1.3 (code max 10 chars), does the code
   actually check the length against 10?
3. MISSING ANNOTATIONS: Code that clearly enforces a requirement but doesn't annotate it.
4. INCORRECT ANNOTATIONS: Code that annotates a requirement but doesn't actually enforce it,
   or enforces it incorrectly.
5. The migration has DEFAULT now() on active_begin, created_at, modified_at — does this
   contradict the Temporal convention that the persistence layer may never originate temporal values?
6. The comment block in validateParentChildRelationship claims circular ancestry checks are
   unnecessary because IDs are generated at insertion — verify this reasoning.

DO NOT:
- Recommend writing tests (that's a separate concern)
- Flag missing annotations for requirements that have no code implementation yet (spec precedes code)
- Touch anything outside the Audit output directory`,
    { label: 'truthfulness:annotations', phase: 'Code Truthfulness', schema: REPORT_SCHEMA }
  ),

  // Agent E: Convention enforcement checker
  () => agent(
    `You are a convention enforcement auditor for the SonOfLeo project, a personal finance
double-entry ledger system written in F#.

YOUR JOB: Check whether the actual code and configuration follow the conventions
defined in Specs/Conventions/.

FILES TO READ:
- All convention docs in ${REPO}/Specs/Conventions/
- All source code in ${REPO}/Src/ (Account.fs, AccountComponent.fs, Money.fs, Audit.fs, DAL.fs, Clock.fs, ResultCE.fs, Program.fs)
- The migration: ${REPO}/DbMigrations/2026-06-01-07-48-CreateAccountTable.sql

CONVENTIONS TO CHECK:

1. TEMPORAL (Temporal.md):
   - All temporal values use NodaTime Instant, not DateTime/DateTimeOffset (except at I/O edge)
   - DB persists instants as timestamptz
   - DB persists dates as Postgres date type only
   - Persistence layer never originates temporal values (no now() in defaults/triggers)
   - Required temporal columns carry no defaults
   - Temporal arithmetic with instants never uses years or months

2. MONEY (Money.md):
   - Money type wraps decimal with penny precision
   - No multiplication/division on Money records directly
   - Unpack to decimal, operate, repack through constructor
   - Half-up rounding (MidpointRounding.AwayFromZero), never default banker's rounding
   - Allocation sums exactly to original (residual forced into one part)
   - Postgres numeric(12,2) for money columns

3. NAMING (Naming.md):
   - create vs fromString naming convention for constructors

4. BUILD & ENVIRONMENT (BuildAndEnvironment.md):
   - Separate databases per environment
   - Debug mode never accesses production DB
   - Environment managed through env vars
   - Production password distinct from dev

5. TRACEABILITY (Traceability.md):
   - Annotation format and placement

${PROJECT_STATE}

For each violation, explain specifically what the convention says and what the code does differently.

DO NOT:
- Flag things that are simply not yet implemented (project is early)
- Recommend tests
- Touch anything outside the Audit output directory`,
    { label: 'truthfulness:conventions', phase: 'Code Truthfulness', schema: REPORT_SCHEMA }
  ),
])

// ============================================================================
// Phase 4: GAAP Coverage
// ============================================================================
phase('GAAP Coverage')
log('Launching GAAP specialist...')

const gaapResult = await agent(
  `You are a GAAP domain expert auditing the requirements of SonOfLeo, a personal finance
double-entry ledger system. Your job is to evaluate whether the COMPLETED domains have
sufficient requirement coverage.

IMPORTANT CONTEXT — READ THIS FIRST:
${PROJECT_STATE}

This means:
- The Account CRUD model is "complete" (minus journal-related enforcement which is explicitly deferred)
- There is NO journal entry system yet
- There is NO interface/CLI yet
- There is NO reporting yet
- DO NOT write a wishlist for the entire system
- DO NOT flag missing requirements for domains that don't exist yet (journals, reporting, CLI)

YOUR SCOPE: Evaluate ONLY whether the Account domain — as completed — has sufficient
requirements for a GAAP-aligned personal finance ledger. Think about:

1. CHART OF ACCOUNTS:
   - Are the account types (Asset/Liability/Equity/Revenue/Expense) sufficient?
   - Are the subtypes adequate for personal finance? Are any critical subtypes missing?
   - Is the parent-child hierarchy sufficient for how a personal chart of accounts works?
   - Account activation/deactivation — are the rules sound from an accounting perspective?

2. ACCOUNT PROPERTIES:
   - Are there properties a GAAP-aligned account should have that aren't specced?
   - Is the normal balance (debit/credit) handling correct for each account type?

3. DATA INTEGRITY:
   - Are the uniqueness constraints sufficient?
   - Are the validation rules accounting-sound?
   - The spec says no system-wide deletion policy (per-entity) — is REQ-AC-5.1 (no hard delete for accounts) the right call?

4. WHAT'S EXPLICITLY DEFERRED (acknowledge, don't flag):
   - Journal entry validation (REQ-AC-4.4 balance check, REQ-AC-4.6 post-date check)
   - These are correctly deferred until the journal domain exists

ALSO READ:
- ${REPO}/Specs/Definitions.md — especially Money, Price, Quantity, Rate definitions
- ${REPO}/Specs/Decisions.md — especially USD-only, cash-basis, balance-invariant, rounding decisions
- ${REPO}/Specs/Behavioral/AccountCrud.md
- ${REPO}/Specs/Behavioral/SystemWide.md
- ${REPO}/HobsonsNotes/cli-requirements-from-leobloom-usage.md — real-world usage data from LeoBloom

The Hobson notes are from the CLI's primary user (the comptroller agent). They document what
was actually used vs. never used in the predecessor system. This is relevant because it tells
you what the REAL accounting workflows look like.

Flag findings as missing-requirement only when the gap would cause accounting problems in the
Account domain specifically. "Nice to have" is not a finding.`,
  { label: 'gaap-specialist', phase: 'GAAP Coverage', schema: REPORT_SCHEMA }
)

// ============================================================================
// Phase 5: Synthesis
// ============================================================================
phase('Synthesis')
log('Synthesizing all findings...')

const allResults = [
  ...(qualityResults || []),
  ...(codeResults || []),
  gaapResult,
].filter(Boolean)

const allFindings = allResults.flatMap(r => r.findings || [])

const synthesisResult = await agent(
  `You are the synthesis agent for a SonOfLeo requirements audit. Your job is to merge
findings from 6 specialist agents into a single, deduplicated, prioritized action list.

HERE ARE ALL THE FINDINGS:
${JSON.stringify(allFindings, null, 2)}

YOUR TASK:
1. Deduplicate: multiple agents may have found the same issue from different angles. Merge them.
2. Prioritize: high severity first, then medium, then low.
3. Group by category.
4. For each unique finding, produce ONE atomic recommended action with a brief "why."
5. An "atomic" action is one that can be completed in a single edit or decision — not "review all requirements."
6. If a finding is a judgment call that needs Dan's input, say so explicitly.

IMPORTANT CONSTRAINTS:
- Neither you nor any agent changes anything outside the Audit output directory
- The traceability script already identified withdrawn-annotation phantoms mechanically — do not lose those
- Distinguish between "fix the spec" vs "fix the code" vs "fix the annotation" vs "Dan needs to decide"

Produce a markdown document with:
1. Executive summary (3-5 sentences)
2. Findings by category, each with: ID, severity, location, action, why
3. A count: total findings, by severity, by category
4. Items requiring Dan's decision (separated out)

Return the full markdown text as your response.`,
  { label: 'synthesizer', phase: 'Synthesis' }
)

// Write all reports to the audit run directory
const reports = [
  { name: '00-traceability-script-output.md', content: `# Traceability Script Output\n\n\`\`\`\n${traceabilityResult}\n\`\`\`` },
]

const agentNames = [
  'quality-account-crud',
  'quality-dal-syswide',
  'quality-conventions',
  'truthfulness-annotations',
  'truthfulness-conventions',
  'gaap-specialist',
]

allResults.forEach((r, i) => {
  const name = agentNames[i] || `agent-${i}`
  reports.push({
    name: `0${i + 1}-${name}.md`,
    content: `# ${r.agentName || name}\n\n${r.findings.map(f =>
      `## ${f.id}\n- **Category:** ${f.category}\n- **Severity:** ${f.severity}\n- **Location:** ${f.location}\n- **Summary:** ${f.summary}\n\n${f.detail}\n\n**Suggested action:** ${f.suggestedAction}\n\n**Why:** ${f.why}\n`
    ).join('\n---\n\n')}`,
  })
})

reports.push({
  name: '07-synthesis.md',
  content: typeof synthesisResult === 'string' ? synthesisResult : JSON.stringify(synthesisResult, null, 2),
})

// Write reports via a file-writing agent
await agent(
  `Write the following files to the directory ${RUN_DIR}. Create the directory first with mkdir -p.

${reports.map(r => `FILE: ${RUN_DIR}/${r.name}\nCONTENT:\n${r.content}\n\n===END FILE===`).join('\n\n')}

Write each file exactly as specified. Do not modify content.`,
  { label: 'file-writer', phase: 'Synthesis' }
)

return {
  runDir: RUN_DIR,
  totalFindings: allFindings.length,
  bySeverity: {
    high: allFindings.filter(f => f.severity === 'high').length,
    medium: allFindings.filter(f => f.severity === 'medium').length,
    low: allFindings.filter(f => f.severity === 'low').length,
  },
}
