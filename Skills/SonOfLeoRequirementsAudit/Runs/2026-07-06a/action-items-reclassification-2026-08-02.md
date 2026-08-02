# Action-Item Reclassification — 2026-08-02

Hobson's post-refactor review of `action-items.md`. Every open item (CONFIRMED or
DEFERRED) was verified against the repo as of `daf2084`, after reading the full commit
history since 2026-07-06. Items already RESOLVED or OVERRULED were left alone unless the
refactor changed their truth.

Classification buckets, per Dan's framing:

1. **Aligned — still needs doing**
2. **Aligned — met by the refactor**
3. **Aligned — partially met**
4. **Partially aligned — needs Dan's re-adjudication**
5. **Not well enough understood — needs discussion**
6. **Invalidated by the refactor**

---

## 1. Aligned — still needs doing

| # | Item | Evidence |
|---|---|---|
| 6 | Tests for REQ-AC-1.40 (parent exists), REQ-AC-3.3 (fetch-by-ID) | **Done 2026-08-02 (P2).** Tests written by BD; REQ-AC-3.3.1 classified unenforceable |
| 38 | Consolidate REQ-AC-1.19 / 1.19.1 | **Done 2026-08-02 (P1).** 1.19.1 withdrawn; five tests renamed by BD to cite 1.19 |
| 60 | REQ-JE-3.4 verification | **Done 2026-08-02 (P1).** Spec note corrected — no test cites it, capability covered by JE-3.9 |
| 69 | Defensive-parameterization REQ | **Disposed 2026-08-02 — overruled; see Dispositions below.** REQ-DAL-2.3 amended to clarify when interpolation is acceptable |
| 99a | As-of balance tests (REQ-JE-3.6.2) | **Done 2026-08-02 (P2).** Two tests by BD; uncovered a bug in AccountBalance.fs (null check on as-of filtered entries, `830b5c8`) |
| 100a | Amount/description filter tests | **Done 2026-08-02 (P2).** Tests by BD; uncovered a bug in AccountActivity.fs (description filter not unwrapping DU value, `c54fe0c`) |
| 101a | Signed-balance normal-orientation test (REQ-JE-3.6.1) | **Done 2026-08-02 (P2).** Test by BD |
| 102a | Counter-account revisit | Not addressed in `cli-requirements-from-leobloom-usage.md` |
| 115 | Next-month period auto-create check | Nothing in code |
| 119a | fetchHeadersFromFilter dedup-then-enforce | Still passes `expectedRows` to the DAL before deduping; the warning comment the item wanted removed is alive at `JournalEntryOrchestration.fs:312-316` |
| 120a | `unwrap` test helper | Open; now ~178 `Result.defaultWith` sites, home is `Tests.Helpers` |
| 121a | Isolated test-file mismatches | Banner at `JournalEntryComponent.fs:181` still names nonexistent `JournalEntryLine.validateAmount`; tests named `validateAmount` call `confirmAmountIsPositive` |
| 122a | Comment fetch secondary-match | The todo is still in `JournalEntryComment.fs:159` |
| 124a | LookupCache lifetime doc | No comment on the module; the `failwith` init is there but undocumented |
| FT-1 | Sequential audit flow | Workflow still `parallel()` fan-out (`workflow.js:540`) |
| FT-3 | Remove severity rankings | `severity: high/medium/low` still in the finding schema (`workflow.js:45`) |
| FT-9 | Re-run the audit | The capstone, once the last test session lands |

## 2. Aligned — met by the refactor

| # | Item | How |
|---|---|---|
| 12 | REQ-SYS-6.1 enforcement | Per-entity no-op instances specced (SYS-6.1.1 added) and tested in routes + voiding; the annotation half is moot (see §6) |
| 25, 92a | Stack traces / ex.Message at DAL catch sites | AppError DAL cases carry the raw `exn`; `toMessage` emits Message + StackTrace uniformly — the straggler is gone |
| 40 | 1.48 vs 1.50 fight | Both rewritten with inclusive boundaries, "inactive" pinned as synonym; they now agree |
| 46, 47 | Doctrines reframing | Doctrines.md deleted in the disposition; salvage moved to learnings |
| 54 | REQ-JE-1.56 tests | BD's first story, `57b4c51` |
| 58, 65 | Consumable-fixture-victim pattern | Eliminated. Void tests post the entry they void in-test (`JournalEntryVoiding.fs:40-44`); the fixture's one voided JE is staged already-voided as a read-only archetype (`TestDataStage.fs:336`); `Tests/README.md` bans mutating fixtures where the function owns its transaction |
| 59 | TRUNCATE pre-stage | Truncation is the fixture constructor's first act; fixture no longer IDisposable |
| 66 | NodaTime clock | `89beb82` |
| 84, 90, 117a | Typed AppError system | Done wall-to-wall |
| 88 | Standardize constructor naming | `validateThenConstruct` has zero occurrences; `create` is the blessed name per `descriptive-naming.md` |
| 92, 93a, 118a | Transaction ownership, batch seam | Routes own transactions, wrapper currency, bracket rollback holes plugged (`c18566c`, `4138a01`) |
| 94 | Period key at CLI boundary | `TemporalFilterInput = PeriodKey of string` |
| 95a | Split contract types per domain | Account/FiscalPeriod/Journal/Shared contract files |
| 104a | PersonalFinance review | `HobsonsNotes/montecarlo-constraints-from-personalfinance.md` |
| 109a | ID wrapper DUs | AccountId, JournalEntryHeaderId, FiscalPeriodId, component IDs — done |
| 110a, FT-5 | Compounded learnings skill | `CompoundedLearnings/` + `Skills/CreateLearning` |
| 111a | Git-traceability discussion | Superseded by a stronger decision: annotations retired outright, test names + tooling are the linkage |
| 116a | DevDataStage out of Src | `b02a0ff` |
| 9a, 17c, 19a, 20a, 27a, 33a, 48a, 56a, 72a, 105a, 106a | The 11 auditor-prompt fixes | All eleven exist as articles in `CompoundedLearnings/catalogs/audit-conduct.md` — but see FT-4 in §3 for the wiring gap |

## 3. Aligned — partially met

| # | Item | Done / remaining |
|---|---|---|
| 2 | REQ-AC-4.6 test | The behavior **is** tested — `AccountRoutes.fs:420` "Deactivate rejects when JEs dated after deactivation date" — but the test cites REQ-AC-4.1, so 4.6 is still untested by linkage. One rename fixes it |
| 18 | Rate definition / Rate×Money | Definition wording updated ("scales a Money or Quantity value"); the design session on projections (loan balance in N months) never happened |
| 85 | FSDDD-04 trio | Sub-2 (transaction mechanics) met; sub-3 kept-as-is by Dan's ruling; sub-1 (LookupCache init) still open under #91 |
| 96a, 105, 106 | Period-close design | **Disposed 2026-08-02 — see Dispositions below.** Hobson misread this: closing entries are a *planned enhancement*, not abandoned. The design session stays owed, sequenced to when the closing slice is scheduled. Near-term work: a deferred entry in `resolved-findings.md` so auditors stop re-flagging it |
| 103a | Reclass verb | Problem scoped in the CLI-requirements doc (`ledger reclass` = atomic void+repost, weekly use); no REQs written |
| 107a | Env-guard defense vetting | Mostly resolved by Dan's 2026-08-02 attestation under #115a (see Dispositions): container has no prod password AND is network-blocked from the prod DB. Residue: confirm test appsettings point at dev/test only and the release-config gate, then record all four backstops in `debug-release-access.md` |
| 123a | confirmX rename sweep | `check-confirm-naming.sh` blocks new `validateX` with an 8-entry allowlist "awaiting the #123a sweep" — the sweep itself pending |
| 125a | Every AppError case tested | `check-apperror-coverage.sh` exists; the 07b error-trace gap-filling is this item in progress; BD's waiver candidates pending Dan's ruling |
| FT-2 | Per-phase output to run folder | The 07-05 workflow rewrite writes per-auditor `10-*` files to `runDir`; whether it writes as-it-goes is unconfirmed from the script alone |
| FT-4 | Prompt meta-review | Substance fully landed as audit-conduct articles — **but the workflow script never tells auditors to read the catalog**. It wires in `resolved-findings.md` only. One prompt edit closes it |
| FT-8 | Finding-disposition system | `resolved-findings.md` + the schema's `priorRuling` field exist; per-run finding IDs and a clean disposition record per run — partially there |

## 4. Partially aligned — needs Dan's re-adjudication

| # | Item | The tension |
|---|---|---|
| 17b | REQ-JE-4.9 void/closed posture | **Disposed 2026-08-02 — see Dispositions below.** The July ruling is reversed: updates ARE allowed on voided entries. Remaining work: amend the 4.9 spec text |
| 29, 41 | "Unenforceable" tables | **Disposed 2026-08-02 — see Dispositions below.** Unenforceable stays as a distinct third state. Remaining work: three-state rule in Specs/README.md, tables in the 7 specs, move REQ-MON-1.1, teach `traceability-audit.sh` the third table |
| 61 | DAL requirements coverage | **Disposed 2026-08-02 — see Dispositions below.** None of the offered outs: DAL requirements get **direct tests**. Remaining work: a DAL test suite driving the DAL functions with crafted bad inputs, asserting the typed AppError case |
| 108a | Negative-existence guardrail discoverability | **Disposed 2026-08-02 — closed as met.** See Dispositions below: the Checks suite + `Src/README.md`'s "Never" column IS the mechanism |
| 112a | Minimal CLAUDE.md at repo root | **Disposed 2026-08-02 — VETOED (repeat veto; do not re-raise).** See Dispositions below |
| 113a | Traceability cross-reference | **Disposed 2026-08-02 — see Dispositions below.** Hand cross-reference retired; Invariant 2 becomes a commit gate, with a placeholder-test policy for new REQs |
| 115a | Config/env/connection-guard hook | **Disposed 2026-08-02 — no hook. See Dispositions below.** Container isolation is physical (no password, network block); Hobson's prod access is sanctioned Saturday-comptroller work a hook would break |

## 5. Not well enough understood — needs discussion

| # | Item | Why |
|---|---|---|
| 74 | `validateNoNewVoidedEntries` REQ | **Disposed 2026-08-02 — see Dispositions below.** Mystery solved: the check guarded against creating an already-voided JE; superseded by structural enforcement (no creation contract exposes `voidedAt`) and REQ-JE-2.14 now exists. Remaining work: one waiver row for 2.14 |
| 89 (+83, 87, 96/ARCH-2) | Validate-on-read | **Disposed 2026-08-02 — closed.** Settled by the reconstitute functions; see Dispositions below |
| 91 | LookupCache architecture | **Disposed 2026-08-02 — closed by assumption.** Current shape stands; the next audit (FT-9) is the designated check on the assumption. #124a (document the lifetime posture) stays open in §1 |

## 6. Invalidated by the refactor

| # | Item | Why |
|---|---|---|
| 64 | Consistent means-to annotations | REQ annotations in source retired 2026-07-31; settled, do-not-relitigate |
| 70 | Dangling placeholder annotation | The placeholder went with the annotation sweep; the "next audit names the REQ" mechanism no longer exists |
| 75 | Git-based annotation mapping | Moot — no annotations to map; `traceability-audit.sh` maps test names |
| 76 | NGUI-1.3.1 annotation sweep | The annotation task is moot; the substance (stack traces survive the railway) is met by AppError |
| FT-6 | Minor-before-major sequencing | The major surgery is done; the constraint is spent |

---

## Housekeeping in action-items.md itself

- #116a, #117a, #118a still read CONFIRMED though long done; likewise the met-in-substance items in §2 need flipping.
- The "Phase 4 — GAAP Panel" section appears verbatim twice (lines 184–203 and 232–251).
- #127a (Fantomas) is marked DONE but was deliberately reversed on 07-31 — worth a note so the record doesn't lie.
- Unrelated find: `AccountRoutes.fs:311` carries `// todo: ask claude to provide the correct REQ #` on the FetchBalances happy path.

## Dispositions (from the 2026-08-02 review session)

### #17b — REQ-JE-4.9: reference updates ARE allowed on voided entries (July ruling reversed)

**Ruling (Dan, 2026-08-02, on Hobson's recommendation):** an external reference's FI and
value may be updated regardless of whether the entry is voided or its fiscal period is
closed. The principle: **void seals the money, never the metadata.**

**Rationale:**

1. *Operational (from LeoBloom comptroller experience):* reclass = void-and-repost is a
   weekly routine — voided entries are a large, permanent population, not a pathology.
   And the external reference is the importer dedup key ("dedup is authoritative" — the
   Synchrony $232.01 incident is the scar). A JE voided *without* repost (duplicate or
   fraudulent charge reversed outright) is the tombstone telling the importer "this feed
   line is already accounted for." A fat-fingered reference on that voided entry silently
   breaks dedup with no balance impact to betray it — the only clean fix is correcting
   the reference on the voided entry itself. A wrong ref on a voided JE is arguably
   *more* dangerous than on a live one.
2. *GAAP:* void immutability protects the economic record — accounts, amounts, dates,
   the void itself. An external reference is cross-linkage metadata (the check number in
   the memo line), not financial content.
3. *Spec consistency:* comments may be added to voided entries (REQ-JE-5.5 — the reclass
   link depends on it); references may be appended to voided entries (REQ-JE-4.10);
   write-once references were withdrawn (REQ-JE-1.47, "we'll fat-finger this someday").
   The fat-finger rationale does not expire at void. One consistent rule beats a special
   case, per the one-word-one-meaning doctrine: "voided" means *financially* negated.
4. *Counterargument, weighed and accepted:* editing a voided entry's reference can
   rewrite which real-world event it claims to represent. In a multi-user shop that
   weighs heavily; here there is a single actor, audit timestamps on every mutation, and
   the alternative failure mode (permanent wrong tombstone) does concrete recurring harm.

**Work item:** amend REQ-JE-4.9 with a clause mirroring 4.10 — *"The FI and value may be
updated regardless of whether the entry is voided or its fiscal period is closed
(mirrors REQ-JE-4.10 and REQ-JE-5.5)."* Route-level sad/happy coverage of the voided
case then follows the normal error-trace process.

### #29/#41 — "Unenforceable" becomes a third requirement state

**Ruling (Dan, 2026-08-02):** unenforceable stays as a concept distinct from waived. Every
active requirement is in exactly one of **three** states: tested, waived, or unenforceable.

**Definitions (the line that keeps the two tables honest):**

- **Waived from testing** — the system (or its structure: types, schema, review) *does*
  enforce the requirement, but we deliberately don't verify it with a test. The
  negative-existence family (REQ-AC-4.22, AC-5.1, JE-4.1, JE-6.1/6.2, MON-2.1, MON-2.7…)
  stays here: enforced by code review, unprovable by unit test.
- **Unenforceable** — nothing in the system enforces or could enforce it; it is a
  declaration binding humans, not code. Canonical specimen: **REQ-MON-1.1** ("Money values
  are always denominated in US Dollars") — nothing tracks currency at all. REQ-MON-2.2.1
  already flags it: "(Except 1.1, which is unenforceable)".

**Work items:**

1. `Specs/README.md` — replace the two-state rule with the three-state rule; define
   "unenforceable" per above. Same fix to the "Two-state rule" boilerplate sentence that
   heads each spec's waived table.
2. Add an `## Unenforceable` table to each of the 7 behavioral specs (ID, why it cannot be
   enforced, Dan's approval date). Empty tables are fine — they signal we didn't forget
   (original CQ-5(b) intent).
3. Move REQ-MON-1.1 from Money.md's waived table to its unenforceable table. Sweep the
   remaining waived rows for other movers using the definition line: *does anything —
   code, type, schema, or review — actually enforce this?* No → it moves.
4. `traceability-audit.sh` — the section-boundary awk at line 29 and the `section_ids`
   calls hardcode "Withdrawn" / "Waived from testing"; add "Unenforceable" to both, and
   Invariant 2 becomes `active − (tested ∪ waived ∪ unenforceable)`. Add the consistency
   check: **unenforceable-but-tested is a contradiction** (if a test verifies it,
   something was enforcing it).

### #61 — DAL requirements get direct tests, not waivers

**Ruling (Dan, 2026-08-02):** rejected all three offered outs (wholesale waiver /
citations on existing tests / carve-out). The DAL requirements are easy to test directly:
hand-craft "bad" SQL or bad inputs, send them at the DAL functions (`executeNonQuery`,
`executeReaderQuery`, the scalar/unboxing paths, the transaction bracket), and assert the
expected typed `AppError` case comes back.

**Shape of the work:**

- A dedicated DAL test suite — `Tests.Integrated/DataAccessLayer/` — sitting *below*
  `Tests.Integrated.Model` in the layer hierarchy (`Tests/README.md` hierarchy gains a
  rung). Per the once-at-the-lowest-layer rule, failure vectors proven here come OFF the
  upper layers' books.
- Test vectors are direct DAL invocations: malformed SQL → the DAL error case; row-count
  expectation violations → `DalResultantRowsDidntMatchExpectation`; unboxing type
  mismatches → the `DalErrorDuring*Unboxing` family; connection-string env var absent /
  containing a literal connection string → `DalConnectionStringEnvVarNotFound` /
  `DalConnectionStringEnvVarContainsConnectionString`; bracket rollback-on-throw behavior.
- `TestingError` (the AppError case fenced out of `Src/` by `check-testingerror.sh`)
  exists for exactly this kind of harness work — use it where a synthetic error is needed.
- Test names cite REQ-DAL IDs like every other test; walk all ~30 active DAL requirements.
  Any row that genuinely can't be provoked this way gets an individual waiver (or
  unenforceable row, per the #29/#41 three-state rule) — decided one at a time, not
  wholesale.
- Done means: Invariant 2 in `traceability-audit.sh` reports zero uncovered REQ-DAL IDs.
- Overlap note: this largely delivers #125a's remaining DAL slice too — the
  `check-apperror-coverage.sh` gaps for `Dal*` cases should close as a side effect.

### #74 — `validateNoNewVoidedEntries`: superseded by structural enforcement; REQ-JE-2.14 needs a waiver row

**Dan's answer (2026-08-02):** the check guarded the intent *"you can't create an
already-voided journal entry."* It is now enforced structurally — no creation input in the
interface contracts exposes a `voidedAt` field; `voidedAt: Instant option` appears only on
`JournalEntryHeaderReturn` (`JournalContracts.fs:53`). The runtime check removed in
`149c170` had nothing left to do.

**Verified 2026-08-02:** the requirement half of the original action item is also done —
**REQ-JE-2.14** ("The system must not allow the creation of a new journal entry that is
already voided") exists at `JournalEntryCrud.md:87`.

**Remaining work (small):** REQ-JE-2.14 is currently neither tested nor waived — a
two-state-rule violation. Add it to JournalEntryCrud.md's waived table with the structural
rationale: *enforced at the interface-contract boundary — no creation input type carries a
`voidedAt` field, so the state cannot be expressed; Model-layer reconstitution from the DB
legitimately carries `voidedAt` but reconstitution is not creation. Enforced by contract
shape + code review* (same family as the negative-existence waivers).

### #89/#91 — validate-on-read closed by the reconstitute pattern; LookupCache closed by assumption

**Ruling (Dan, 2026-08-02):** both design discussions are closed. #89 is *certainly*
closed — the answer lives in the reconstitute functions. #91 (LookupCache) is closed by
assumption; **the next audit (FT-9) is the designated check on whether we erred.**

**The settled validate-on-read posture** (worth stating precisely, since ARCH-2 asked
"do historical rows break if validation tightens?"), as embodied in e.g.
`Account.fs:63-69`:

- **Field-level rules are re-proven on every read** — reconstitution routes raw
  primitives through the same smart constructors as creation (`AccountCode.create`,
  `AccountType.fromString`, …), so a domain type can never exist un-proven, and a
  historical row that violates a *tightened field rule* surfaces as a typed error at
  read time rather than a corrupt value downstream.
- **Collective/cross-entity facts are trusted, not re-proven** — per the reconstitute
  doc comment: "zero validation at the collective level. All fields are assumed to have
  come from a trusted source (e.g. the database) where such validation occurred at the
  time of writing." No cascading per-row DB lookups on the read path (the ARCH-2
  complaint); additionally, no DB lookups are *possible* inside reconstitute because it
  runs inside an open database reader.
- This also closes **#83** (FSDDD-02: impure constructors — creation validation moved to
  orchestration, reconstitution is pure) and **#87** (FSDDD-07: two construction sites
  are now the deliberate pair: validated-create vs trusted-reconstitute).

**LookupCache (#91):** current shape stands — generic `Cache` type, process-lifetime,
loud `failwith` on init load (Dan's explicit preference for a CLI process), lazy
fill-on-miss. #85 sub-issue 1 closes with it. **#124a remains open**: write the
process-lifetime assumption into the module doc so the posture Dan just blessed is
discoverable — that item is the paper trail for this very ruling.

### #96a/#105/#106 — period close: deferred enhancement, not abandoned; silence the auditors, keep the design session queued

**Dan's clarification (2026-08-02):** FP closing (GAAP closing entries) is a **planned
enhancement**, not something being abandoned — the reclassification's earlier "no closing
entries by design" framing was Hobson's misreading of the LeoBloom operational note
(which says closing is deferred until the process is trusted, not never). Dan knows FP
closing is incomplete; the problem is auditors repeatedly telling him so.

**What this means for the items:**

- **The design session (#96a/#105) stays owed but re-sequenced:** "what is period close —
  posting lock vs GAAP closing entries, annual grain, retained earnings" gets settled
  *when the closing-entries slice is scheduled*, not during the current wrap-up. The
  posting lock (`is_open`) is the whole of "close" until then.
- **#106 (close atomicity)** stays deferred into that same future session, as originally
  ruled.
- **Near-term work item (the actual ask):** add a **deferred** entry to
  `Skills/SonOfLeoRequirementsAudit/resolved-findings.md`: *"FP closing is a posting lock
  only; GAAP closing entries are a planned, unscheduled enhancement. Do not flag closing
  as incomplete, missing, or un-specced. Revisit when: Dan schedules the closing-entries
  slice."* That's the designed mechanism for stopping repeat findings — auditors must
  respect deferred entries unless the revisit trigger has fired.

### #113a — hand cross-reference retired; Invariant 2 becomes a commit gate with a placeholder-test policy

**Ruling (Dan, 2026-08-02):** the manual cross-reference of the Phase-1 untested/unwaived
lists is dead. Invariant 3 (unannotated REQs) is moot — annotations were retired.
Invariant 2 (every active requirement tested, waived, or — per #29/#41 — unenforceable)
is now mechanical in `traceability-audit.sh`, and **it should run as a commit check**,
now that the Checks suite is canonized.

**The placeholder-test policy:** no new REQ is committed without at least a citing test.
Where the real test isn't written yet, a placeholder satisfies the gate:

```fsharp
member _.``REQ-XY-N <behavior>``() = Assert.Fail "Not yet implemented"
```

Two properties, both deliberate:

- The gate vets **existence only** — a test named `REQ-XY-N` exists. Content quality
  remains the job of review and the audit's test-truthfulness phase. Dan stated this
  limitation explicitly; it is accepted, not overlooked.
- A placeholder is **loud**: it fails on every run until the real test replaces it, so an
  unimplemented requirement is visible in the suite itself rather than in a backlog doc.
  A red suite between REQ-commit and test-commit is the feature.

**Work items (sequencing matters):**

1. **Clear the existing Invariant 2 backlog first** — the DAL suite (#61 disposition),
   the §1 behavioral-coverage list, REQ-JE-2.14's waiver (#74), and the three-state
   migration (#29/#41). The gate cannot be turned on while the repo fails it wholesale.
2. Then **promote Invariant 2 from report to failure**: exit non-zero on violations (today
   only Invariant 1 phantoms fail) and wire the script into `Checks/run-all.sh` — it is
   grep-speed, fine for `--quick` and the pre-commit hook.
3. Write the placeholder rule **once**, in `Specs/README.md`'s requirement-anatomy
   section: *a new requirement ships in the same commit as a citing test; a placeholder
   `Assert.Fail "Not yet implemented"` is acceptable; the commit gate enforces existence.*

### #112a — repo-root CLAUDE.md: vetoed, permanently; the imagined problem doesn't exist

**Ruling (Dan, 2026-08-02, a repeat veto):** no CLAUDE.md at the repo root. The
mechanical reason: Hobson launches from `/home/dan/penthouse-pete/`, BD from
`/media/dan/fdrive/ai-sandbox/workspace/` — a repo-root CLAUDE.md sits outside both
launch chains and **would never be read**.

**Re-examining AIM-5's premise:** the "cold-entering agent with no entry point" does not
exist in this project's operating model. Every agent that touches SonOfLeo arrives warm —
Dan's agents via the wakeup discipline (wakeups name the reading list: `Specs/README.md`,
`Tests/README.md`, the catalogs), audit subagents via the workflow script's prompts,
ad-hoc subagents via task prompts authored by someone already oriented. The entry point
is the **wakeup discipline**, not a file, and it is already load-bearing.

**Work item (the one real fix):** add an **overruled** entry to
`Skills/SonOfLeoRequirementsAudit/resolved-findings.md`: *"No repo-level agent entry
point (CLAUDE.md or equivalent) — by design. Agents enter via wakeups/prompts that carry
the reading list; a root file would sit outside every launch chain. Vetoed repeatedly.
Do not re-flag."* That ends the recurring veto cycle the same way #96a's entry ends the
closing-entries nag.

### #115a — no config/env/connection-guard hook; isolation is physical, and Hobson's prod access is sanctioned

**Ruling (Dan, 2026-08-02):** no hook. Two reasons:

1. **BD's container cannot reach prod even if compromised or confused:** the prod
   password is not present in the container, and networking guards prevent the container
   from connecting to the prod DB at all. The boundary is physical, not honor-system —
   a harness hook would add ceremony to a door that's already bricked up.
2. **Hobson's prod access is a feature, not a hole.** As Dan's LeoBloom Saturday
   comptroller, Hobson occasionally *needs* prod connectivity. A connection-guard hook on
   the host harness would block sanctioned work.

**Knock-on — #107a shrinks (see its §3 row):** Dan's statement here attests two of the
four AIM-1 backstops (no password in container; network block). Remaining residue for
#107a: confirm the test appsettings point only at dev/test and the release-config gate
works as described, then record all four backstops with dates in
`CompoundedLearnings/articles/architecture/debug-release-access.md` so the next audit
verifies against a written baseline instead of re-deriving the threat model.

**Deliberately NOT added to resolved-findings.md:** unlike #96a and #112a, future
auditors *should* re-verify environment isolation — drift in networking or secrets
placement is exactly what an audit is for. The suppression mechanism is for settled
design arguments, not for standing security posture.

### #69 — defensive parameterization: overruled; amend REQ-DAL-2.3 instead

**Ruling (Dan, 2026-08-02):** the proposed origin-agnostic parameterization REQ is
overruled. The existing REQ-DAL-2.3 ("originating from user input") is deliberately
scoped: the only non-parameterized SQL values are type-safe by construction (e.g. `limit`
is `int option` — F# enforces that at compile time, so injection is structurally
impossible). A blanket "parameterize everything" rule would demand ceremony where the type
system already provides the guarantee.

**Work item:** amend REQ-DAL-2.3's language to explain *when* interpolation is acceptable
— specifically, when the value's type makes injection structurally impossible (e.g.
compiler-enforced `int`). The requirement should teach, not just mandate.

### #108a — negative-existence guardrail discoverability: closed as met; the Checks suite is the mechanism

**Ruling (Dan, 2026-08-02):** agreed with Hobson's assessment — closed, nothing to build.

**The reasoning, for the record.** AIM-2 observed that "never do X" rules (never touch
Npgsql outside the DAL, never `DateTime.Now`, never build an error string outside
`AppError.toMessage`) are invisible to code-first navigation — a prohibition has no code
to find, so an agent discovers it only by breaking it. The auditor's proposed fix
(an index document of never-rules) was docs-of-docs and was rejected; #108a asked for a
better mechanism. The refactor then built it without naming it:

1. **`Checks/*.sh`** — six of the eight scripts are never-rules made executable
   (`check-npgsql`, `check-clock`, `check-testingerror`, `check-tomessage-wildcard`,
   `check-hardwired-dates`, `check-confirm-naming`), running in the pre-commit hook. An
   agent that reads no documentation and violates a prohibition is stopped at commit —
   the rule enforces itself rather than waiting to be discovered.
2. **`Src/README.md`'s "Never" column** — every infrastructure module paired with its
   prohibition, covering the rules a grep can't mechanize.

The "better mechanism" is the repo's own governing rule applied to prohibitions: prefer
the executable form. Future never-rules follow the same pattern — a Checks script where
mechanizable, a "Never" cell where not.

## Open questions for Dan

1. ~~**#17b** — does "no reference updates after void" still hold, given 4.10 now allows appends on voided entries?~~ **Disposed — see above.**
2. ~~**#29/#41** — keep the two-state rule and fold "unenforceable" into waiver reasons, or add the third table?~~ **Disposed — see above.**
3. ~~**#61** — DAL coverage: wholesale waiver, citations, or carve-out?~~ **Disposed — see above.**
4. ~~**#74** — what did `validateNoNewVoidedEntries` guard, and did the consolidation preserve it?~~ **Disposed — see above.**
5. ~~**#89/#91** — are the validate-on-read and LookupCache discussions closed by the refactor's choices?~~ **Disposed — see above.**
6. ~~**#96a** — canonize "no closing entries by design" into FiscalPeriodCrud or a learning, so it stops living only in HobsonsNotes?~~ **Disposed — see above (the question was mis-premised; closing is deferred, not abandoned).**
7. **#112a / #115a / #108a** — the three §4 infrastructure calls are Dan's.
8. ~~The test items in §1 (#6, #99a–101a, #2's rename) look like natural cargo for the final test-writing session — fold them into the 07b work list?~~ **Disposed (Dan, 2026-08-02): no.** The 07b sheet is a specific, bounded exercise — route *input-contract validation* tracing — and stays that way. The §1 test items are behavioral-coverage gaps (as-of balance, filter permutations, signed balance, REQ-AC-1.40/3.3, the 4.6 test rename) and become their **own separate work list**, not 07b cargo.
