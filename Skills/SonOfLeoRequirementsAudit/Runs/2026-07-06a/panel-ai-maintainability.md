# AI-Maintainability Panel — SonOfLeo (Phase 4, 2026-07-06a)

**Scope:** all of `Specs/`, the `Src/` F# tree, `Tests/`, `DbMigrations/`, `Skills/SonOfLeoRequirementsAudit/` and `Skills/TestWriter/`, live `traceability-audit.sh` output, `resolved-findings.md`. BdsNotes unscanned.

**Headline:** the spec system is a genuine, well-formed constitution. The risk to BD isn't missing rules — it's that enforcement has holes exactly where a plausible-but-wrong edit does the most damage to the books, and the mechanical safety net currently cries wolf. Ranked by how badly BD could hurt the ledger before anyone noticed.

---

## AIM-1 — Test fixture silently TRUNCATEs whatever DB the env var points at; no runtime environment guard
- **Category:** architecture / failure-amplification
- **Severity:** high
- **Location:** `Tests/Tests.Integrated/_TestDataStage.fs:351-364` (Dispose TRUNCATE), `Tests/Tests.Integrated/Tests.Integrated.fsproj` `CopyEnvConfig` target, `Src/Utilities/DAL.fs:47-90`
- **Summary:** BD's sanctioned lane (tests) is wired to unconditionally `TRUNCATE … CASCADE` every ledger table on a database chosen entirely by an env-var name, with no code-level assertion it's the dev DB.
- **Detail:** `TestDataFixture.Dispose` runs `TRUNCATE ledger.journal_entry_comment, …, ledger.account, ledger.fiscal_period CASCADE` against whatever `DAL.dataSource` resolves. Resolution reads `appsettings.json`'s `ConnectionStringEnvVar`; an MSBuild target copies `appsettings.Development.json`→`appsettings.json` yielding `SONOFLEO_DEV_CONNSTR`. No runtime environment assertion exists anywhere in `Src/` — a full grep for `isDebug|IsProduction|prod|environment|assert` returns only build artifacts. So `BuildAndEnvironment`'s "debug may NEVER access prod" and `REQ-DAL-3.3` are enforced solely by an env-var value plus a copied JSON file. Point that var at prod, edit the copy target, or run the Integrated suite where the dev var resolves to prod, and the run wipes the books — build green, tests pass. The connstr sniffer (`DAL.fs:63`, `Contains(";") || Contains("Host=")`) doesn't compare DB names, so a dev/prod pair on one host is indistinguishable.
- **Suggested action:** Add a runtime guard (DAL or fixture) that refuses to connect/TRUNCATE without a positive dev signal — e.g. require `SONOFLEO_ENV=dev` and assert it before destructive DDL.
- **Why (AI-specific):** The single most catastrophic, irreversible action in the system sits inside BD's first territory, gated only by ambient config an agent can't see is load-bearing. Textbook "passes build+tests, destroys everything."
- **Resolution owner:** dan-decides → fix-code
- **Prior ruling:** none (`environment-isolation-plan-2026-06-14.md` is a plan, not an enforced control).

---

## AIM-2 — The no-update / no-delete guardrails are invisible to the documented code-first navigation
- **Category:** enforcement-gap
- **Severity:** high
- **Location:** `REQ-JE-4.1, 6.1, 6.2`, `REQ-AC-4.22, 5.1`, `REQ-FP-4.3, 5.1`; `Specs/README.md` "star chart" linkage rules
- **Summary:** Negative-existence guardrails exist only as spec prose — unannotated, waived — yet README trains agents to navigate code-first ("find the requirement… grep its ID"), which returns nothing for exactly these rules.
- **Detail:** Invariant 3 confirms `REQ-JE-4.1/4.2/6.1/6.2/5.5/5.6`, `REQ-AC-4.22/5.1`, `REQ-FP-4.3/5.1` carry no code annotation. Correct as a testing decision (waivers already ruled sound). AI-maint problem: for these guardrails the grep is empty, reading as "no rule exists." An agent told "add a way to correct a JE amount" or "add delete-account" implements it — type system, tests, annotations, star-chart all silent. Only reading `JournalEntryCrud.md §4/§6` / `AccountCrud.md §4/§5` *before* coding surfaces the prohibition, and nothing routes an agent there first.
- **Suggested action:** Add a code-resident marker for forbidden operations (a `Doctrine_ForbiddenOperations.md` or "deliberately-absent" annotations at the orchestrator surface enumerating forbidden verbs + REQ IDs), and note in README that negative-existence REQs are guarded by review, not grep.
- **Why (AI-specific):** These guardrails' violation silently corrupts/destroys audit data; an agent gets zero mechanical resistance and the documented navigation method actively misleads it.
- **Resolution owner:** fix-spec (+ optional fix-annotation)
- **Prior ruling:** touches the 4.22/5.1/6.x waivers but does not re-raise them — raises *discoverability* of the guardrails, which no ruling addresses.

---

## AIM-3 — The mechanical two-state check counts stricken/withdrawn requirements as active, burying real gaps
- **Category:** maintainability / traceability
- **Severity:** high
- **Location:** `Skills/SonOfLeoRequirementsAudit/traceability-audit.sh` (active-ID parse + Invariant 2); specs leaving inline `- **REQ-…** stricken` stubs alongside Withdrawn rows (`AccountCrud.md:63`, `SystemWide.md:21,35`, `DataAccessLayer.md` DAL-1.1-1.13, `JournalEntryCrud.md` JE-1.43/2.10/5.4)
- **Summary:** Invariant 2 reports 88 "active, untested, unwaived" REQs, but ~55 are stricken/withdrawn IDs the script still parses as active — so the constitution's central promise (every active REQ tested-or-waived) is neither true nor mechanically checkable as written.
- **Detail:** "active" = `grep '- **REQ-…'` above the Withdrawn/Waived headings. Specs kill reqs by writing an inline `stricken` bullet *and* a Withdrawn-table row; the inline stub stays in the numbered list, so it's counted active. Withdrawn is subtracted for Invariant 1 (phantoms) but not Invariant 2. Confirmed: REQ-AC-2.1 at `:63` (stricken) and `:175` (Withdrawn); REQ-SYS-2.2/4.1 likewise. The 88-line output is mostly noise, hiding genuine gaps: `REQ-DAL-1.14-1.19`, `REQ-DAL-2.1/2.2/2.3/3.1-3.6`, `REQ-AC-1.40/3.3/4.6`, `REQ-JE-1.56/3.9.1/3.9.3` (several already CONFIRMED-but-open in `Runs/2026-07-06a/action-items.md` #6/#54/#60-61). Also one stale waiver: `REQ-SYS-6.1` is both waived and test-annotated (script flags it).
- **Suggested action:** (1) fix-code: subtract `$tmp/withdrawn` from `active` before Invariants 2/3. (2) fix-spec: one canonical death-state — delete inline stricken bullets (README already says a dead req "moves to" the table) or make the parser drop them. Then triage residual real gaps.
- **Why (AI-specific):** This script is BD's coverage oracle. ~55 false positives train an agent to ignore it wholesale, at which point genuine untested requirements ship unnoticed. A mechanical check is only maintainable if trustworthy without human interpretation.
- **Resolution owner:** fix-code (script) + fix-spec (hygiene)
- **Prior ruling:** none — action-items #61/#71 note specific DAL gaps but never diagnose the stricken-counting behavior.

---

## AIM-4 — 109 interchangeable bare-`Guid` parameters make argument-order mistakes compile clean and pass tests
- **Category:** failure-amplification
- **Severity:** medium
- **Location:** `Src/Model/Ledger/JournalEntryComment.fs:39-180` (`updateComment`/`validate…` take `uniqueId: Guid`, `primaryJournalEntryId: Guid`, `secondaryJournalEntryId: Guid option` side by side); pervasive — 109 bare-`Guid` params across `Src/Model` + `Src/ModelOrchestrator`
- **Summary:** Entity identity is raw `Guid` everywhere, so transposing two same-shaped ID args (account vs entry vs comment) type-checks and can pass loosely-scoped tests while mislinking ledger rows.
- **Detail:** FSDDD-01 already found a live instance (comment PK passed where a JE ID belongs). That's the expected failure mode of a 109-site surface, not a one-off. Where two `Guid`s that both reference real rows are swapped (e.g. primary/secondary JE link), FK constraints pass and the books are silently wrong. Single-case DU wrappers (`AccountId`, `JournalEntryId`, `CommentId`) would make transpositions compile errors at zero runtime cost — the same "illegal states unrepresentable" philosophy already applied to `Money`, `FieldUpdate`, and calendar dates.
- **Suggested action:** Introduce single-case ID wrappers for ledger entity IDs, incrementally; start with the JE composite where same-typed IDs sit adjacent.
- **Why (AI-specific):** Argument-order transposition is among the most common LLM edit errors, here invisible to compiler and often tests. Wrappers convert BD's likeliest silent-corruption mistake into a loud build failure — highest-leverage type change for agent safety.
- **Resolution owner:** dan-decides (appetite) → fix-code
- **Prior ruling:** overlaps FSDDD-01; raised here as a *class* finding, not the single call site.

---

## AIM-5 — No repo-level entry point; migration workflow and environment-enforcement rules live only in Dan's head / memory
- **Category:** missing-requirement / maintainability
- **Severity:** medium
- **Location:** repo root (no `README`/`AGENTS.md`/`CLAUDE.md`), `DbMigrations/` (10 `.sql`, no runner, no README)
- **Summary:** A fresh agent given only the repo can't infer that `Specs/` is the constitution, how migrations are authored/applied/reviewed, or how environment isolation is meant to hold.
- **Detail:** No top-level orientation file — only `Specs/README.md`, `Specs/Conventions/README.md`, the audit-skill README, and two one-line Tests READMEs. `DbMigrations/` holds timestamped SQL (`YYYYMMDDHHMM-Name.sql`) with no runner, no ordering doc, and no statement of the "review before prod" gate (that rule lives only in Hobson's cross-project memory). `RecreateAccountTable`/`RemoveAccountType` show the schema is edited destructively in dev, making the review gate load-bearing yet invisible to BD, who will own schema changes. F# `.fsproj` compile-order discipline is undocumented, though the compiler catches violations loudly (least of the three).
- **Suggested action:** Add a one-page root `AGENTS.md`/`README.md`: name `Specs/README.md` as the constitution/entry point; state the migration authoring convention, how migrations are applied, and the pre-prod human review gate; point to `Doctrines.md` and the two skills.
- **Why (AI-specific):** Spec authority is inferable; the migration lifecycle and prod-review gate are not — precisely the operations where an uninformed agent does irreversible damage.
- **Resolution owner:** fix-spec
- **Prior ruling:** none.

---

## AIM-6 — Load-bearing test doctrine lives in a skill file the Tests tree never points to
- **Category:** maintainability
- **Severity:** medium
- **Location:** `Skills/TestWriter/SKILL.md` + `references/`; `Tests/Tests.Integrated/README.md`, `Tests/Tests.Isolated/README.md` (one line each)
- **Summary:** The rules BD must follow to extend tests safely (two-state rule, fixture-only setup, consumable-victim pattern, reserved-empty +4-month period, TRUNCATE-cleanup contract) all live in `TestWriter/SKILL.md`, but the Tests dirs carry only a one-sentence README and no pointer to it.
- **Detail:** `TestWriter/SKILL.md` is good and in-repo, so discoverable by reading — only if the agent knows to look. An agent in `Tests/Tests.Integrated/` sees one sentence and wouldn't learn it must not create setup entities inline, that the +4-month period must stay empty (the `fetchByPeriod` empty-list test depends on it), or that orchestrator-committing tests self-clean via `_Cleanup.fs`. Violations produce the flaky/order-dependent tests already logged as TT-02/TT-03/#65.
- **Suggested action:** Expand both Tests READMEs to point at `Skills/TestWriter/SKILL.md` and inline the three or four hard invariants.
- **Why (AI-specific):** BD's first territory is tests. Doctrine loaded only when the harness happens to trigger the skill isn't reliably in-context for an agent editing a `.fs` test file; co-locate a pointer with the work.
- **Resolution owner:** fix-spec (docs)
- **Prior ruling:** none (distinct from fixture-fix action items #58-59/#65).

---

## AIM-7 — DAL error paths surface `ex.StackTrace` instead of the message, blinding an agent's debug loop
- **Category:** enforcement-gap
- **Severity:** medium
- **Location:** `Src/Utilities/DAL.fs:64, 97, 110, 122, 168, 199` (`| ex -> Error $"… {ex.StackTrace}"`), annotated `REQ-NGUI-1.3.1`
- **Summary:** Boundary catch sites interpolate `ex.StackTrace` (omits the message, can be null pre-throw) rather than `ex.Message`/`ex.ToString()`, so the Result-railway error text an agent reads to self-correct often won't say what went wrong.
- **Detail:** `REQ-NGUI-1.3.1` requires message *and* stack trace; these give only the trace. Run 2026-07-06a flagged the sibling AMB-1 (#25, CONFIRMED) with fix "`ex.Message`→`ex.ToString()`"; the DAL does neither — it uses `ex.StackTrace`, the worst of the three for legibility.
- **Suggested action:** Replace `{ex.StackTrace}` with `{ex.ToString()}` at these sites, matching AMB-1.
- **Why (AI-specific):** An agent debugging works from the error string; StackTrace-only (often null-message) errors make BD's observe-and-fix loop slower and guess-driven.
- **Resolution owner:** fix-code
- **Prior ruling:** related to action-items #25 (AMB-1), CONFIRMED, not in `resolved-findings.md` (no suppression). Re-raised because DAL still ships `ex.StackTrace`, not the ruled `ex.ToString()`.

---

## AIM-8 — Precedent ledger has no linkage back to REQ IDs
- **Category:** maintainability
- **Severity:** low
- **Location:** `Skills/SonOfLeoRequirementsAudit/resolved-findings.md`, `Runs/2026-07-06a/action-items.md`
- **Summary:** Rulings are keyed by ad-hoc slugs (CV-2, AMB-JE-1) with no back-reference to the REQ IDs they concern, so an agent can only apply precedent by reading all ~30 entries each run.
- **Detail:** The "precedent, not law" discipline is sound, but entries like AMB-JE-3a reference `REQ-JE-4.9` only in prose. No REQ→rulings index exists; "suppress only exact matches" forces a full linear read every audit, and the risk of missing an on-point precedent rises as the ledger grows. The action-items file duplicates this with a third ID scheme, unlinked.
- **Suggested action:** Add a `**Requirements:** REQ-…` tag line per ledger entry so tooling can surface "prior rulings for this REQ"; optionally have the traceability script emit matching entries per flagged REQ.
- **Why (AI-specific):** Precedent stays cheap only if retrievable by the REQ being audited, not by re-reading the whole ledger; free-text slugs make matching a judgment call that will drift.
- **Resolution owner:** fix-annotation (ledger format)
- **Prior ruling:** none.

---

### Credit where due
- Authority hierarchy is real and self-describing: `Specs/README.md` + `Definitions.md` + `Doctrines.md` let a fresh agent infer layer ownership, the `validateThenConstruct` persistence gate, and the two-state rule.
- Withdrawn/Waived tables preserve *why* a requirement died.
- The read path already routes every row through `constructFromRaw` reconstitution (`DAL.executeReaderQuery`) — persistence gate for reads is implemented, not just written.
- Precedent-ledger + statement-delta is a strong anti-relitigation pattern.

**Through-line (AIM-1/2/3):** this codebase documents its rules better than most at this stage, but leans on *review and convention* to enforce the rules whose violation is most expensive — and BD's first lane (tests) sits atop the most destructive of those. Converting three from review-enforced to mechanically-enforced — env guard (AIM-1), forbidden-op discoverability (AIM-2), ID wrapper types (AIM-4) — de-risks the handover more than any added documentation.
