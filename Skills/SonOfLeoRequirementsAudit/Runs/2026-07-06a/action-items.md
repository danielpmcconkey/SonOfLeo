# Audit Action Items — 2026-07-06a

Phase-at-a-time manual audit. Findings reviewed with Dan one at a time, highest severity first.

## Status key
- **CONFIRMED** — finding accepted, action pending
- **RESOLVED** — action completed
- **OVERRULED** — Dan reviewed and rejected
- **DEFERRED** — acknowledged, not acting now

---

## High

| # | ID | Finding | Action | Status |
|---|-----|---------|--------|--------|
| 1 | SD-01 | REQ-AC-4.4 (zero-balance deactivation guard) coded but untested and unwaived | Dan wrote the test | RESOLVED |
| 2 | SD-02 | REQ-AC-4.6 (no JE after deactivation date) coded but untested and unwaived | Confirm finding and fix if confirmed | CONFIRMED |
| 3 | CQ-1 | Conventions/README.md does not index Doctrines.md | Added index entry | RESOLVED |

## Medium

| # | ID | Finding | Action | Status |
|---|-----|---------|--------|--------|
| 4 | SD-03 | FiscalPeriod is a complete 4th domain omitted from Dan's statement | ACK — FP is supporting infrastructure like Money, not a first-class domain | OVERRULED |
| 5 | SD-04 | Withdrawn REQ-JE-3.4 still ships as CLI verb FetchLinesByAccount | Reinstated REQ-JE-3.4 | RESOLVED |
| 6 | SD-05 | ~15 Account CRUD two-state-rule violations (unwaived type-guaranteed REQs) | Waived 6 type-guaranteed REQs. Confirm and write tests for REQ-AC-1.40 (parent must exist) and REQ-AC-3.3 (fetch-by-ID) | CONFIRMED |
| 7 | AMB-AC-1 | Traceability.md contradicts AccountCrud on inclusive/exclusive active_end | Fixed Traceability.md | RESOLVED |
| 8 | IE-AC-1 | REQ-AC-3.9 instant source for reads unspecified (AuditEnvelope vs clock) | Added to resolved-findings.md | OVERRULED |
| 9 | AMB-AC-2 | REQ-AC-4.4 balance reference date unfixed | Added to resolved-findings.md | OVERRULED |
| 9a | — | Audit skill quality-reviewer prompts lack GAAP domain literacy | Update prompts: standard accounting terms (balance, posting, debit, credit, void, close) have precise meanings; do not flag as ambiguous unless usage conflicts with the GAAP definition | CONFIRMED |
| 10 | WV-AC-1 | Type-guaranteed null REQs active but unwaived (inconsistency with siblings) | Duplicate of SD-05 — already resolved | RESOLVED |
| 11 | SYS-CLK-1 | REQ-SYS-3.3 says "system clock" — should say AuditEnvelope | Added to resolved-findings.md | OVERRULED |
| 12 | SYS-2STATE-1 | REQ-SYS-6.1 two-state rule via delegated instances | Dan waived from testing. Add REQ-SYS-6.1 code annotations at enforcement sites (no-op error checks) | CONFIRMED |
| 13 | AMB-DAL-01 | REQ-DAL-1.16 "contains an actual connection string" undetectable | Added to resolved-findings.md | OVERRULED |
| 14 | CON-DAL-02 | REQ-DAL-2.2 "verify rows affected" conflicts with flexible read pattern | Added to resolved-findings.md | OVERRULED |
| 15 | AMB-JE-1 | REQ-JE-1.11 is vacuous (always true by construction) | Added to resolved-findings.md — auditor's premises were wrong | OVERRULED |
| 16 | GAP-JE-2 | External references missing audit timestamps (violates REQ-SYS-3.1) | Added to resolved-findings.md — SYS-3.1 covers it | OVERRULED |
| 17a | AMB-JE-3a | REQ-JE-4.9 target reference identification unspecified | Added to resolved-findings.md — identifying by PK is obvious | OVERRULED |
| 17b | AMB-JE-3b | REQ-JE-4.9 void/closed-period posture unspecified | Should NOT be allowed after void. Review in a future session with LeoBloom context and Saturday routine to confirm, then spec it | CONFIRMED |
| 17c | — | Audit skill prompts produce findings that ask for obvious inferences | Update prompts: do not flag entity identification by primary key as under-elaborated | CONFIRMED |
| 18 | MON-1 | Definitions Rate "scales a Money value" contradicts MON-2.7 prohibition | Three sub-issues: (a) Rate def says "scales Quantity" — probably wrong, Rate*Quantity is nonsensical in this domain; (b) design session needed on how Rate*Money will work for future projections (loan balance in N months); (c) Rate definition wording blocked on outcome of (b) | CONFIRMED |
| 19 | MON-2 | Sum intermediate-overflow behavior undefined | Auditor assumed a bad implementation (fold over add) instead of checking the code (List.sumBy) | OVERRULED |
| 19a | — | Audit skill: auditors assume implementation details without verifying | Update prompts: spec quality auditors must not assume implementation details. Either verify against the code or confine the finding to the spec text as written | CONFIRMED |
| 20a | — | Audit skill: ambiguity bar is too low — auditors flag any theoretical divergence | Update prompts: apply a reasonable-person standard. A requirement is ambiguous only if a competent developer with domain knowledge would genuinely implement it differently, not if a pathological reading could be constructed. These are specs, not legal briefs | CONFIRMED |
| 27a | — | Audit skill: conventions flagged for missing REQ IDs when no domain exists to receive them | Update prompts: conventions can exist as prose without REQ IDs when the business domain they apply to hasn't been specced yet. REQ extraction happens when the domain is built, not before | CONFIRMED |
| 33a | — | Audit skill: auditors must check migrations/schema before questioning waiver soundness | Update prompts: before flagging a waiver as unsound, verify against the DB schema (NOT NULL constraints, types) and the F# type system — not just the prose | CONFIRMED |
| 48a | — | Audit skill: requirements may be stricter than conventions | Update prompts: a behavioral requirement that is narrower than its underlying convention is not a contradiction — requirements elaborate conventions for specific domains | CONFIRMED |
| 20 | MON-3 | Split count N type/integrality unspecified | Added to resolved-findings.md | OVERRULED |
| 21 | AMB-FP-1 | Design note says closing tooling deferred but close/reopen are active tested REQs | The is_open toggle and the accounting close process are different things | OVERRULED |
| 22 | XREF-1 | REQ-NGUI-2.4 cites Definitions for terms that don't exist there | "(See Definitions)" references "interface layer", which IS defined — auditor misread the pointer | OVERRULED |
| 23 | CONTRA-1 | REQ-NGUI-1.4 "option for code" is looser than Decision "codes exclusively" | Dan updated the Decision to match intent — no contradiction | OVERRULED |
| 24 | WAIVE-1 | REQ-NGUI-3.1-3.5 waiver reason unsound for narrow testable reqs | Added to resolved-findings.md — waiver reason is fine | OVERRULED |
| 25 | AMB-1 | REQ-NGUI-1.3.1 "system exceptions" for stack trace undefined | Spec is correct, implementation is wrong. DAL catch sites use ex.Message which swallows the stack trace. Change to ex.ToString() so stack trace survives through the Result railway | CONFIRMED |
| 26 | CQ-2 | Temporal.md calls instant-to-date "rare" — it's routine now | Dan clarified "rare" in Temporal.md — means few call sites, not few invocations | RESOLVED |
| 27 | CQ-3 | Temporal.md has testable rules with no REQ IDs | DB-origination extracted to REQ-DAL-3.7 (waived). Other two are convention prose for domains that don't exist yet — conventions can exist without REQs until the domain is built | RESOLVED |
| 28 | CQ-4 | BuildAndEnvironment.md safety rules (debug-never-prod) have no REQ IDs | Dan extracted REQ-DAL-1.20 (unique ConnectionStringEnvVar per build config), waived as build-config fact. Remaining B&E rules stay as convention prose — operational domain not yet specced | RESOLVED |
| 29 | CQ-5 | Traceability.md names tables that don't exist ("unenforceable"/"untestable") | Valid. (a) Evaluate if any existing requirements are unenforceable and belong in a separate table; (b) Add an "Unenforceable" table to each behavioral spec (can be empty — signals we didn't forget); (c) Traceability.md vocabulary is correct in principle — two distinct concepts (unenforceable vs untestable) that were clubbed into "Waived from testing" | CONFIRMED |

## Low

| # | ID | Finding | Action | Status |
|---|-----|---------|--------|--------|
| 30 | SD-06 | Balance/activity reporting primitives exist despite "no real reporting" | Dan's statement already covered this ("quality of life functions") | OVERRULED |
| 31 | SD-07 | REQ-NGUI-1.4 has no annotation, test, or waiver | Dan added waiver | RESOLVED |
| 32 | SS-DAL-03 | No waived-from-testing table in DAL.md | Valid at audit time — Dan added the table this session when REQ-DAL-3.7 was extracted | RESOLVED |
| 33 | AMB-DAL-04 | Asymmetric empty vs whitespace wording (DAL-1.15 vs 1.18) | Reasonable person knows the intent | OVERRULED |
| 34 | GAP-JE-4 | REQ-JE-5.4 withdrawal left secondary link effectively fixed | Dan added REQ-JE-1.56 — secondary link is now explicitly repointable | RESOLVED |
| 35 | AMB-JE-5 | Void atomicity unspecified | Code already wraps void in a transaction with rollback on Error | OVERRULED |
| 36 | CLR-JE-6 | REQ-JE-1.29 mis-subjected ("entry ID" should say "line") | Dan fixed | RESOLVED |
| 37 | AMB-JE-7 | REQ-JE-3.9 ordering direction unspecified | Databases default to ascending. Reasonable person call | OVERRULED |
| 38 | RED-AC-1 | REQ-AC-1.19 / 1.19.1 duplicate | Determine least damaging way to consolidate | CONFIRMED |
| 39 | REF-AC-1 | REQ-AC-2.20.1 dangling reference (copy-paste artifact) | Dan deleted 2.20.1 | RESOLVED |
| 40 | AMB-AC-3 | "inactive" synonym creates tri-state ambiguity | Real issue is 1.48 and 1.50 fighting. 1.50 is correct, 1.48 may need to go. Discuss in clean context | CONFIRMED |
| 41 | MON-4 | Conversion "validate all section 1" includes unvalidatable MON-1.1 | Dan edited 2.2.1 to except 1.1. MON-1.1 is genuinely unenforceable — add to an unenforceable table when those are created (see CQ-5) | CONFIRMED |
| 42 | MON-5 | "Sort order" should say "positional order" | Dan updated to "sort / positional order" | RESOLVED |
| 43 | MON-6 | Batch conversion atomicity unspecified | Not a persistence operation — behavior is obvious from the code | OVERRULED |
| 44 | WAV-FP-1 | Period key null waiver uses value-type reasoning for a string | DB column is NOT NULL — auditor didn't check the migration | OVERRULED |
| 45 | TERM-1 | "UI domain types" terminology inconsistent with Definitions.md | NGUI is a subset of UI. Document clearly scopes itself at line 3 | OVERRULED |
| 46 | CQ-6 | Doctrines validateThenConstruct vs value-object create naming gap | Doctrines says "entity" which Definitions.md defines. Doctrines needs reframing eventually but not today | DEFERRED |
| 47 | CQ-7 | Doctrines says "create" is anti-pattern, Naming says use "create" | Same as CQ-6 — Doctrines reframing | DEFERRED |
| 48 | CQ-8 | Money.md split residual looser than REQ-MON-2.4.5 | Requirements are allowed to be stricter than conventions | OVERRULED |

## Phase 3 — Code Truthfulness

### High

| # | ID | Finding | Action | Status |
|---|-----|---------|--------|--------|
| 49 | ORCH-1 | Voided entries included in balance sums — LEFT JOIN trap | Dan fixed query (WHERE instead of JOIN condition) | RESOLVED |
| 50 | TT-01 | Balance test assertion too loose to catch ORCH-1 | Dan fixed — exact expected value assertion | RESOLVED |

### Medium

| # | ID | Finding | Action | Status |
|---|-----|---------|--------|--------|
| 51 | ML-1 | FiscalPeriod.validateThenConstruct is public (should be private) | Dan fixed | RESOLVED |
| 52 | ML-2 | updateComment can't clear secondary link to null (REQ-JE-1.56 unreachable) | Dan fixed — FieldUpdate<Guid option> wired in | RESOLVED |
| 53 | ML-3 | updateComment secondary re-pointing bypasses REQ-JE-1.53 self-link check | Dan fixed — validatePrimaryAndSecondaryRelationship runs before write | RESOLVED |
| 54 | TT-05 | REQ-JE-1.56 (repoint/clear secondary link) untested and unwaived | Write tests: repoint to different JE, clear to None | CONFIRMED |
| 55 | ORCH-2 | REQ-JE-3.9 ordering is optional, not enforced | Dan updated REQ-JE-3.9 wording | RESOLVED |
| 56 | ORCH-3 | Line-count/balance checks run after DB writes (REQ-SYS-2.1.1) | Auditor misread REQ-SYS-2.1.1 — "entity's own properties" does not apply to cross-line composite checks | OVERRULED |
| 56a | — | Audit skill: agents cite requirements without reading them | Update prompts: when citing a requirement as justification for a finding, quote the relevant text and verify the finding matches what the requirement actually says | CONFIRMED |
| 57 | TMC-1 | JE UI contract types missing NGUI-2.1/2.1.1/2.2 annotations | Dan fixed | RESOLVED |
| 58 | TT-02 | Shared fixture makes entertainment5650's balance order-dependent | Covered by #65 — no tests should mutate fixtures without rollback or self-cleanup; each should create its own data | CONFIRMED |
| 59 | TT-03 | Fixture staging commits row-by-row; mid-stage failure wedges DB | Move TRUNCATE CASCADE to constructor top (pre-stage) instead of Dispose — self-healing on dirty DB | CONFIRMED |
| 60 | TT-04 | REQ-JE-3.4 reinstated but untested/unwaived | Check git history — Dan believes tests existed. Verify | CONFIRMED |
| 61 | TT-06 | ~17 active DAL requirements neither tested nor waived | Dan wants to see the list — may be misattribution since DAL capabilities are exercised by every integration test | CONFIRMED |
| 62 | GAP-CLI-1 | REQ-NGUI-1.5 enforced but unannotated at Account code-lookup sites | Dan added ~112 annotations | RESOLVED |
| 63 | GAP-CLI-2 | Activity/balance handlers missing NGUI-2.4/3.5 marshalling annotations | Dan fixed | RESOLVED |
| 64 | INC-CLI-1 | Means-to REQ annotations applied inconsistently across CLI handlers | Hobson recommends placement, Dan adds them | CONFIRMED |
| 65 | — | Talk to BD about "consumable fixture victim" test pattern | No tests should update fixtures without rolling back. Each test needing mutable state should create its own account/JE/etc. Void victims are the known family — check for others | CONFIRMED |

### Low

| # | ID | Finding | Action | Status |
|---|-----|---------|--------|--------|
| 66 | UTIL-1 | Clock.now uses DateTimeOffset instead of NodaTime SystemClock | Discuss merits — Dan prefers NodaTime but we're at the F# boundary regardless | CONFIRMED |
| 67 | UTIL-2 | DAL parameterization missing REQ-DAL-2.1/2.3 annotations | No enforcement happens in that code block — annotation would be false | OVERRULED |
| 68 | TMC-2 | fromDecimalList missing REQ-MON-2.3.1/2.3.2 annotations | Dan fixed | RESOLVED |
| 69 | TMC-3 | REQ-DAL-2.3 incorrectly annotated on UUID lookups (not user input) | Dan removed annotation. Rethink parameterization requirements — parameterizing UUIDs is good practice but has no REQ. Need a requirement for defensive parameterization regardless of input origin | CONFIRMED |
| 70 | ML-4 | Dangling // REQ- annotation with no ID in JournalEntryLine.fs | Intentional placeholder — Dan wants the next audit to tell him which REQ belongs there | DEFERRED |
| 71 | ML-5 | fetchAll missing REQ-AC-3.7/3.9 annotations | Dan fixed | RESOLVED |
| 72 | ML-6 | Composite reqs (JE-2.8, 1.12, 1.13) — verify orchestrator enforces them | All enforced. 1.12/1.13 were already annotated. Dan added JE-2.8 to validateAccountByLine and all 3 to orchestrateCreation rollback. Only one annotation was missing — agents need to read more carefully before flagging | RESOLVED |
| 72a | — | Audit skill: truthfulness agents must verify enforcement exists NOWHERE before claiming it's missing | Update prompts: grep the full repo for the REQ ID before reporting a missing-annotation finding | CONFIRMED |
| 73 | ORCH-4 | fetchByPeriod missing REQ-JE-3.3 annotation | Wrong — fetchByPeriod takes a UUID, not a key. REQ-JE-3.3 is correctly annotated in the CLI routing file. Agent didn't read the requirement | OVERRULED |
| 73a | — | Audit skill: agent cited wrong enforcement site for ORCH-4 | Same as #56a — agents must read the requirement text before citing it | OVERRULED |
| 74 | ORCH-5 | validateNoNewVoidedEntries missing annotation | No requirement exists for this check. Dan to add one | CONFIRMED |
| 75 | ORCH-6 | fetchHeaderIdsByReference missing REQ-DAL-2.3 annotation | Dan fixed. Explore using git to map annotations instead of manual code inspection — getting unwieldy | CONFIRMED |
| 76 | TRU-CLI-1 | REQ-NGUI-1.3.1 annotation overclaims (no stack trace at that site) | Program.fs satisfies the "payload will comprise" portion. Dan added REQ-NGUI-1.3.1 annotations to DAL catch sites. Search for other try/catch boundary functions that may need it | CONFIRMED |
| 77 | TT-07 | REQ-AC-4.6 untested | Duplicate of SD-02 (#2) | RESOLVED |
| 78 | TT-08 | REQ-AC-1.40 and 3.3 untested | Duplicate of SD-05 (#6) | RESOLVED |
| 79 | TT-09 | REQ-JE-1.11 test can't exercise its named condition | By definition you can't — period is derived from the date, so the date is always within its period. The test correctly exercises the missing-period rejection path. Not mislabeled | OVERRULED |
| 80 | TT-10 | REQ-JE-2.4 test uses UUID not code — weaker than requirement | Valid for REQ-JE-1.22 (UUID reference) but not 2.4 (code resolution). Dan added a CLI-level REQ-JE-2.4 test with invalid account code | RESOLVED |
| 81 | TT-11 | SystemWide sub-clauses bookkeeping gaps | Dan added waived table entries | RESOLVED |

## Phase 4 — F#/DDD Panel

### High

| # | ID | Finding | Action | Status |
|---|-----|---------|--------|--------|
| 82 | FSDDD-01 | updateComment passes comment PK where primary JE ID belongs | Dan fixed in JournalEntryComment.fs | RESOLVED |

### Medium

| # | ID | Finding | Action | Status |
|---|-----|---------|--------|--------|
| 83 | FSDDD-02 | JE slice validateThenConstruct functions hit the DB (impure constructors) | Two action items: (a) #88 standardize entity-level module functions, (b) #89 domain-type validation on read | CONFIRMED |
| 84 | FSDDD-03 | Errors are prose not data — mid-railway string matching on error messages | Dan to design a more robust error system (#90) | CONFIRMED |
| 85 | FSDDD-04 | Result.defaultWith failwith inside Result-returning functions; hand-rolled transaction bracket | Three sub-issues: (1) LookupCache init — discuss after architecture (#91); (2) exception safety in railroad — review transaction mechanics (#92); (3) defaultWith failwith on transaction create — Dan wants loud failure, keeps as-is | CONFIRMED |
| 86 | FSDDD-06 | Option.get calls in AccountActivity | Schema guarantees non-null for those columns; Option.get is appropriate | OVERRULED |
| 87 | FSDDD-07 | Two JE construction sites, no cross-component ID check | Partly agreed — orchestrateCreation does enforce ID agreement by construction. Naming and structure depend on outcomes of #88 and #89 | CONFIRMED |

### Low

| # | ID | Finding | Action | Status |
|---|-----|---------|--------|--------|
| 88a | FSDDD-05 | All-voided accounts vanish from balance; line_types CTE derives from data | Dan rewrote query with CASE-based null handling and DU-sourced line types | RESOLVED |
| 89a | FSDDD-08 | LookupCache hardcodes SQL outside owning domain modules | LookupCache exists for a specific purpose; moving the query doesn't reduce brittleness | OVERRULED |
| 90a | FSDDD-09 | confirmAccountIsValidAndActive reimplements isActive | Dan deduplicated — confirmAccountIsActive now delegates to isActive | RESOLVED |
| 91a | FSDDD-10 | executeScalar returns boxed Object forcing unsafe casts | Dan moved all unboxing into the DAL | RESOLVED |
| 92a | FSDDD-11 | DAL exception handlers use ex.StackTrace without ex.Message | Dan fixed — one straggler at DAL.fs:115 (commit missing ex.Message) | CONFIRMED |
| 93 | FSDDD-12 | MoneyRecord/MoneyModule naming | Dan renamed to type Money / module Money | RESOLVED |

## Action Items from Phase 4

| # | Source | Action | Status |
|---|--------|--------|--------|
| 88 | FSDDD-02, FSDDD-07 | Design discussion: standardize entity-level module function names (validateThenConstruct, constructFromPreValidatedComponents, etc.) | CONFIRMED |
| 89 | FSDDD-02, FSDDD-07 | Design discussion: domain-type validation on read — should reconstitution re-prove facts the DB guarantees? | CONFIRMED |
| 90 | FSDDD-03 | Design: more robust error system — DU, error code dict, or custom Error type to replace mid-railway string matching | CONFIRMED |
| 91 | FSDDD-04 | Design discussion: LookupCache architecture — discuss after greater architecture discussion (#88/#89) | CONFIRMED |
| 92 | FSDDD-04 | Review: how transactions work with orchestrated write ops — exception safety, bracket combinator | CONFIRMED |
| 93a | ARCH-1 | Learn about DB transactions; design the batch transaction seam for atomic multi-JE posting (staging import) | CONFIRMED |
| 94 | ARCH-7 | Fix AccountActivityTemporalFilterInput: change FiscalPeriodId of Guid to FiscalPeriodKey of string — UUID leak at CLI boundary | CONFIRMED |
| 95a | ARCH-3 | Split InterfaceContractTypes.fs into per-domain files (Account, JournalEntry, FiscalPeriod) while it's still small | CONFIRMED |

## Phase 4 — Architecture Panel

### High

| # | ID | Finding | Action | Status |
|---|-----|---------|--------|--------|
| 95 | ARCH-1 | Orchestrators own transactions internally — no external-transaction path for batch posting | Action item #93a — learn transactions, design batch seam | CONFIRMED |
| 96 | ARCH-2 | Read path does cascading per-row DB lookups; historical rows break if validation tightens | Covered by #89 (validate-on-read discussion) | CONFIRMED |

### Medium

| # | ID | Finding | Action | Status |
|---|-----|---------|--------|--------|
| 97 | ARCH-3 | Boundary-type triplication per query surface; InterfaceContractTypes.fs growing | Keep contracts separate (explicit, not coupled). Split InterfaceContractTypes.fs per domain while small — dan-decides | CONFIRMED |
| 98 | ARCH-4 | LookupCache contradicts 06-19 no-memoization decision | Pragmatic compromise for CLI process. Note the override in Decisions.md. Covered by #91 | OVERRULED |
| 99 | ARCH-5 | Drop-and-recreate migration style with no tracking table | No real data yet; Dan will adopt additive-only when data exists | OVERRULED |
| 100 | ARCH-6 | Normal-balance semantics only in F# after account_type lookup dropped | No direct-DB consumers exist; app is CLI-only. Fold into #89 validate-on-read discussion | OVERRULED |
| 101 | ARCH-7 | CLI activity filter accepts fiscal period UUID no output provides | Action item #94 — change to period key | CONFIRMED |

### Low

| # | ID | Finding | Action | Status |
|---|-----|---------|--------|--------|
| 102 | ARCH-8 | No line ordinal on journal_entry_line | Line order is not semantically meaningful in double-entry accounting | OVERRULED |
| 103 | ARCH-9 | Period dates write-only, calendar-month granularity hardcoded | Calendar months are deterministic — no need to code against impossible drift | OVERRULED |
| 104 | ARCH-10 | AuditableAction DU is a flat cross-domain registry | 13 cases is fine; logging design undecided; one DU entry per new feature is negligible | OVERRULED |
