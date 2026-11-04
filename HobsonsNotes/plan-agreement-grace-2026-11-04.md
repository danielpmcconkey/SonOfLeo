# Plan — A Master Agreement's own grace period, and two slice 3 follow-ups

Written 2026-11-04 by Hobson for one Claude Code session that can see this
repository and nothing else, working unattended. Branch
`agreement-grace-period`, which already carries the spec change and this
plan. Where this plan and `Specs/Behavioral/` disagree, the spec wins:
record it in §5.6.

## 0. Start here

Invoice matching (CashFlow §13) accepts a linked line only when its date
falls within the Invoice date minus the grace period and the due date plus
the grace period. Today the grace period comes from the cadence alone
(Monthly 7 days). Some counterparties routinely pay outside that, so every
payment of theirs fails matching as an orphan (REQ-CF-13.7) and is linked by
hand. Dan ruled (2026-11-04) that such an agreement gets a longer grace
period. The spec now gives a Master Agreement an optional grace period in
days that overrides the cadence's. Read the spec change first
(`git diff main -- Specs/`), whole requirements, not just the changed lines:

- `Specs/Behavioral/CashFlow.md`: §1 Master Agreement (one new paragraph);
  new REQ-CF-2.28; REQ-CF-13.3 amended (with REQ-CF-13.2, which it feeds,
  unchanged); REQ-CF-14.1, 14.2 and 14.11 amended.

Part B is two follow-ups from the slice 3 refactors
(`HobsonsNotes/plan-audit-refactors-2026-10-11b.md` §5.6): a route test the
slice said was missing, and a CompoundedLearnings article on an F# quirk it
found.

Then:

1. Read §1 and §2.
2. Rebuild `sonofleo_test` from scratch
   (`Tests/Tests.Integrated/setup-throwaway-test-db.sh`) and record the
   baseline in §5.1: build warnings, `Tests.Isolated` and
   `Tests.Integrated` counts, `Checks/run-all.sh`, traceability.
3. **Name first.** Before opening any Src, draft test names for every new
   or amended requirement (§3 Part C lists them) and for B1, from the spec
   and this plan alone. Run the name-quality check and commit them as
   failing placeholders (`Assert.Fail "Not yet implemented"`). Existing
   tests may be read for naming and fixture conventions.
4. Do Parts A–C **in order**. **Commit and push after every numbered item**
   and append that item's §5 entry in the same commit, so that after a
   context compaction §5 and `git log` tell you where you are. Re-read §2
   and the current item after any compaction.
5. Finish with §4.

**Never stop for a question.** Nobody will answer until Dan is back. If an
item can't be done as written, do the closest thing that keeps every rule
in §2, record exactly what and why in §5.6, and carry on.

## 1. Read before you touch anything

- `README.md`, `Specs/README.md`, `Src/README.md`, `Tests/README.md`.
- `Skills/SonOfLeoSrcDeveloper/SKILL.md` — how Src is shaped. Follow it.
- `Skills/ArchReviewer/SKILL.md` — run it over your own diff before §4.
- `Skills/SonOfLeoRequirementsAudit/resolved-findings.md`, especially
  LOUD-FAILURE.
- `CompoundedLearnings/README.md` and
  `CompoundedLearnings/articles/architecture/type-taxonomy.md` and
  `validation-layers.md`.
- `HobsonsNotes/plan-audit-refactors-2026-10-11b.md` §2 "Name resolution
  rules" and §5.6: they apply here unchanged (IDs below the bridge, names
  resolved in `Ui.InterfaceBridge`, the stale-cache rule, the F#
  private-record and open-order quirks).

## 2. Standing rules

- **You write `Src/`, `Tests/`, `Checks/`, `DbMigration/Scripts/` and
  `CompoundedLearnings/` (as B2 says); never `Specs/`.**
- **Migrations:** new scripts in `DbMigration/Scripts/` named
  `YYYYMMDDHHMM-PascalCaseDescription.sql` with a header comment saying why.
  Copy `202610111000-CreateInvestmentActivityCoverageTable.sql` for
  conventions. Apply them only to the throwaway `sonofleo_test`. **Never run
  anything against production.** Hobson reviews; Dan applies by hand.
- **Structural integrity in the schema; every business rule in the
  application** (REQ-DAL-3.6). The new column takes no check constraint for
  its range.
- `git pull --rebase --autostash` before every commit. Only Dan merges to
  `main`.
- **Public repository.** No institution names, real people's names, account
  numbers or real amounts anywhere: code, comments, fixtures, commits. The
  counterparty that prompted this is not named anywhere, and no test or
  comment describes a real one.
- **Dan is the authority.** If a requirement looks wrong, make the case once
  in §5.6; don't edit a spec, and don't quietly do something else.
- **No REQ annotations in source.** **Loud beats bubblewrap**: no new guard,
  error case or test for a failure that already aborts loudly with nothing
  written, beyond what the spec names. **Code never breaks a tie** unless a
  requirement names it. **Every operation uses its context's initiation
  instant** (REQ-SYS-3.4).
- **Every new or changed assertion is seen failing before it passes.**
  Perturb Src or the expected value, watch it fail, restore.
- **Route payloads change in this slice, and only these:**
  `CreateAgreementInput` gains `gracePeriodInDays: int option`;
  `UpdateAgreementInput` gains `gracePeriodInDaysUpdate:
  FieldUpdate<int option>`; `MasterAgreementReturn` gains
  `gracePeriodInDays: int option` (so every agreement fetch, update return
  and listing carries it). Every other route's payload stays as it is.
  Existing tests that build these records gain the field (`None`,
  `FieldUpdate.NoChange`) and nothing else.
- Commit subjects describe the behaviour, not the edit. Match the log.

## 3. The work

File references were checked on 2026-11-04; **verify before editing**.

### A. The grace period in days

1. **Migration.** One script adding a nullable `integer` column
   `grace_period_days` to `cashflow.master_agreement`
   (`202609071125-CreateCashFlowTables.sql` created the table). Existing
   rows stay null, which keeps today's behaviour; no back-fill. The header
   says so. Table-level grants already cover the column.

2. **The domain type and the entity.**
   - `GracePeriodInDays` in `CashFlowComponent.fs`, beside
     `DaysDueAfterInvoiceDate` (~337), with the same shape: private record,
     `value`, `create` validating 0–365 inclusive. Two new error cases in
     `CashFlowError.fs` beside `CashflowDaysDueAfterInvoiceDate…` (~18),
     each naming the value and the bound (REQ-CF-2.28).
   - `MasterAgreement.fs`: the record gains `gracePeriodInDays:
     GracePeriodInDays option` with its accessor; `create`, `persist`,
     `reconstitute`, `mapRawForDbRead` and `masterAgreementSelectFields`
     carry it; `MasterAgreementFieldUpdates` gains
     `gracePeriodInDaysUpdate: FieldUpdate<GracePeriodInDays option>` and
     `update` applies it. `updateCadence` (~314) leaves it alone
     (REQ-CF-14.2: a cadence change leaves the grace as it is).
   - Callers of `create` and builders of `MasterAgreementFieldUpdates`
     elsewhere in Src pass `None` / `NoChange` unless they are the create or
     update path.

3. **The bridge.** The three contract changes in §2.
   `CashFlowFieldConverters` wraps `int option` into
   `GracePeriodInDays option` (a value out of range returns the typed error
   before anything is written) and unwraps it into `MasterAgreementReturn`.
   The create and update routes need no other change.

4. **Matching reads it** (REQ-CF-13.3).
   `CashFlowOps.fs`: `gracePeriodInDaysFromCadenceType` (~301) and its use in
   `windowCovers` (~456) currently map each agreement ID to its cadence
   type. Replace that with the agreement's effective grace: its grace period
   in days when it has one, else the cadence's. The function that decides
   this reads only a Master Agreement, so it belongs in the CashFlow domain,
   not in orchestration; place it by the SrcDeveloper skill and say where in
   §5.2. The window arithmetic and everything else in §13 stay as they are.
   The comment above the old function ("how many days past an invoice's due
   date…") is wrong as well as stale (the grace widens both ends of the
   window); don't carry it over.

### B. Slice 3 follow-ups

1. **A route test for the stale-name trap on Instances.** Slice 3 moved the
   master agreement name off `Instance`; `convert [Instance] to
   [InstanceReturn]` resolves it through `MasterAgreement.idToName`, a cache,
   while the Agreement conversion hands its Instances the name from the
   Agreement in hand, so UpdateAgreement can't return a renamed agreement's
   Instances under the old name. The 11a #094 test
   (`Tests/Tests.Integrated/InterfaceBridge/CashFlowRoutes.fs` ~273) asserts
   no Instance's name, so nothing guards that design. Add one route test,
   leaving #094's untouched:
   - its own agreement, with at least two Instances;
   - **warm the cache first**: before the rename, call a route whose
     Instance conversion goes through `MasterAgreement.idToName` (the open
     Instances fetch, REQ-CF-14.12, is one) and assert it shows the old
     name, so the cache holds it;
   - rename the Master Agreement through UpdateAgreement; assert every
     `InstanceReturn` in the update's return, and in the fetch by the new
     name (REQ-CF-14.3), carries the new name.
   - See it fail: in `convert [Agreement] to [AgreementReturn]`
     (`CashFlowFieldConverters.fs` ~274), build the Instances with
     `convert [Instance] to [InstanceReturn]` (which resolves through
     `MasterAgreement.idToName`) instead of
     `instanceReturnUnderMasterAgreementName` with the name in hand; the
     test must go red, on the update's return (the cache reads committed
     rows, so inside the renaming transaction it holds the old name) and on
     the fetch (the warmed cache is never invalidated). Restore. Record what
     you saw in §5.2.
   - Name it for REQ-CF-14.2 (and 14.3 if it asserts the fetch), saying
     what would have to break for it to go red.

2. **A CompoundedLearnings article on the private-record accessor quirk.**
   From slice 3's §5.6, first bullet: when a private record type and its
   same-named accessor module sit side by side inside an orchestration
   module (`InstanceOrchestration.InvoiceCompositeUpdate`), the fully
   qualified `InstanceOrchestration.InvoiceCompositeUpdate.invoiceUpdates`
   resolves to the type's private field and fails with "Field is not
   static"; `create` resolves, and with the module opened the short form
   reaches the accessor. Reproduce it before you write it: a minimal case in
   a scratch project outside the repo is enough, and the article must say
   exactly what you saw, including the compiler's message. If it no longer
   reproduces, write that instead, with the version you tried. Put it under
   `CompoundedLearnings/articles/coding/`, in the format and with the
   catalog entry `CompoundedLearnings/README.md` asks for. Keep it to the
   fact, the workaround and how to recognise it; the existing
   `du-case-collision-across-opens.md` is the length to aim at.

### C. Tests

Name every test for what would have to break for it to go red. The
requirements, and what each needs at least:

- **REQ-CF-2.28** (isolated, on `GracePeriodInDays`, and integrated, on
  create): 0 and 365 accepted and stored; −1 and 366 rejected with the typed
  error, nothing stored; none given, stored as none. Copy the REQ-CF-3.10
  tests' shape (`PaymentAgreementDataStates.fs` ~471–510).
- **REQ-CF-13.3** (integrated, `LinkageAndMatching.fs` ~1165, beside the
  cadence theories, which stay unchanged): on an agreement whose grace
  period in days differs from its cadence's, a line dated exactly that grace
  before the Invoice date and exactly that grace after the due date is paid
  to the Invoice; one day beyond either is an orphan with the
  no-covering-Invoice reason. Include a grace of 0 on a Monthly agreement,
  so the override narrows as well as widens. A grace set and then cleared
  (REQ-CF-14.2) gives the cadence's window again, and a grace changed
  between two matching runs governs the second run's window for an Invoice
  that already existed.
- **REQ-CF-14.1** (route): created with a grace returns it; created without
  returns none.
- **REQ-CF-14.2** (integrated and route): an update sets it, and sets it to
  none; an out-of-range update is rejected with the typed error and the
  agreement is unchanged; a cadence change leaves it as it was.
- **REQ-CF-14.11** (route): the listing carries each agreement's grace, and
  none for one without.
- **REQ-SYS-3.3**: if the modified-at theories enumerate Master Agreement
  updates by field, add the grace; if not, add nothing.
- **B1** as above.

## 4. When you finish

- Build: 0 warnings, 0 errors.
- `Tests.Isolated` and `Tests.Integrated` pass, the latter against a
  `sonofleo_test` rebuilt from scratch with the new migration. If you
  can't get the database, say so; don't skip or fake.
- `bash Checks/run-all.sh` green.
  `Skills/SonOfLeoRequirementsAudit/traceability-audit.sh .` Invariants 1
  and 2 clean: REQ-CF-2.28 has tests, and the amended 13.3, 14.1, 14.2 and
  14.11 have tests of the new clauses.
- `validate.py` VALID, `model_drift.py` NO DRIFT. A new field needs no model
  change; if drift says otherwise, record it in §5.6 and don't edit
  `Architecture/`.
- No `Not yet implemented` placeholder left.
- ArchReviewer over `git diff main...HEAD -- Src Checks DbMigration`; act on
  what it finds or say why not in §5.5.
- Push, and complete §5.

## 5. Report

Written by the session that did this plan. Fill it in as you go (§0 step 4).

### 5.1 Outcome
(baseline first; final counts and check outputs at the end)

**Baseline** (branch at 78c6b15, `sonofleo_test` rebuilt from scratch, .NET SDK 10.0.114
installed from the Ubuntu archive because the container had none):

- Build: 0 warnings, 0 errors.
- `Tests.Isolated`: 536 passed, 0 failed. `Tests.Integrated`: 1794 passed, 0 failed.
- `bash Checks/run-all.sh`: 12 passed, 0 failed, 1 skipped (check-traceability, not main).
- `traceability-audit.sh .`: Invariant 1 clean; Invariant 2 lists REQ-CF-2.28 only (the new
  requirement, no tests yet).

**Final** (`sonofleo_test` dropped and rebuilt from scratch with the new migration, at the
last commit):

- Build: 0 warnings, 0 errors.
- `Tests.Isolated`: 539 passed, 0 failed (536 + 3). `Tests.Integrated`: 1823 passed, 0 failed
  (1794 + 29).
- `bash Checks/run-all.sh`: 12 passed, 0 failed, 1 skipped (check-traceability, not main).
  AppError coverage report: 323/407 (both new cases referenced).
- `traceability-audit.sh .`: Invariant 1 clean, Invariant 2 clean (REQ-CF-2.28 now tested;
  13.3, 14.1, 14.2 and 14.11 have tests of the new clauses).
- `validate.py`: VALID, 0 findings. `model_drift.py`: NO DRIFT.
- No `Not yet implemented` placeholder left.

### 5.2 Per item
(A1–A4, B1, B2, and the test names; where the effective-grace function
lives and why; what you perturbed for each new assertion)

**Test names (§0 step 3).** Drafted from the spec and this plan before opening Src; graded
by an independent agent (TestNameReview) given only the verbatim requirements, the waived
table and the names. What it raised and what I did:

- Isolated 2.28 name joined accept and reject: split in two, and the reject name now names
  each value with its own bound (-1 with 0, 366 with 365).
- "No grace period" at create could be met by an explicit null: the integrated create name
  says "left out", and the route create theory runs 12, explicit null and left out.
- Listing name said "as stored" (circular): now "carries 12 … and none for one created
  without one".
- Cleared-grace name: now says separate runs against an Invoice that existed before the
  clear (an orphan rolls the whole run back, REQ-CF-13.7).
- Out-of-range update: starts from 12, and the payload also changes the memo, so
  "unchanged" can't be met by dropping the field.
- Clearing by update: the route clearing payload names only the grace period, set to null,
  so an explicit null is shown to count as a named field (REQ-SYS-6.1).
- Cadence change: now a theory over 12 and none, so a cadence change that froze the old
  cadence's grace into the field goes red.
- B1 name rewritten to say what returns the Instances. Not taken: asserting the open
  Instances fetch under the new name afterwards. That fetch resolves through the process-wide
  cache, which the plan's own design leaves stale in a test process (rule 1, no
  invalidation); in production each CLI run is a fresh process.
- Not taken: a per-cadence fallback test (the existing four cadence theories create agreements
  with no grace, so they are that test); a REQ-CF-13.1 overlap test (the oldest-Invoice rule
  is unchanged and already tested; the grace only makes overlap easier to reach); a separate
  route-level create rejection (the integrated create tests already go through the
  CreateAgreement route, as the REQ-CF-3.10 ones do); "neither agreement nor Payment
  Agreements stored" (a Payment Agreement cannot exist without its agreement row).
- REQ-SYS-3.3: the modified-at theories enumerate by entity, not by Master Agreement field,
  so nothing is added there.

Names committed (placeholders): Isolated `MasterAgreementDataStates.fs` 2;
`CrossDomainOrchestration/MasterAgreementDataStates.fs` 3; `LinkageAndMatching.fs` 6;
`AgreementUpdate.fs` 2; `CashFlowListings.fs` 1; `InterfaceBridge/CashFlowRoutes.fs` 4
(including B1).

**A1 migration.** `DbMigration/Scripts/202611041000-AddMasterAgreementGracePeriodDays.sql`
adds nullable `integer` `grace_period_days` to `cashflow.master_agreement`, no check
constraint, no back-fill; the header says why (and cites no REQ IDs, per the no-annotations
rule). Applied to the throwaway `sonofleo_test` only; `\d cashflow.master_agreement` shows
the column.

**A2 domain type and entity.** `GracePeriodInDays` sits in `CashFlowComponent.fs` directly
after `DaysDueAfterInvoiceDate`, same shape (private record, `min`/`max`, `value`, `create`
checking 0–365). AppError cases added: `CashflowGracePeriodInDaysBelowMin of int * int` and
`CashflowGracePeriodInDaysExceededMax of int * int` (value, bound), with messages in the
days-due style. They are slotted alphabetically (after `CashflowExternalInvoiceIdTooLong`),
per the AppError convention, rather than literally beside the days-due cases.
`MasterAgreement.fs`: field, accessor, `create` parameter (after memo), `persist`,
`reconstitute`, `mapRawForDbRead`, select fields, `gracePeriodInDaysUpdate` in
`MasterAgreementFieldUpdates`, applied in `update`; `updateCadence` passes `NoChange`.
`AgreementOrchestration.constructNewAndPersist` takes `GracePeriodInDays option` after memo
(it is the create path), and `isThereAMasterAgreementUpdate` counts the new field, so an
update naming only the grace is not a no-op. The bridge passes `None`/`NoChange` in this
commit; A3 wires it. Existing test callers of `constructNewAndPersist`/`MasterAgreement.create`
and builders of `MasterAgreementFieldUpdates` gained `None`/`NoChange` and nothing else
(26 files, mechanical).

**A3 bridge.** The three contract changes in §2, nothing else. `convert [MasterAgreement] to
[MasterAgreementReturn]` unwraps the grace; `convert [UpdateAgreementInput] to
[MasterAgreementFieldUpdates]` wraps `FieldUpdate<int option>` with
`convertFieldUpdateOptionToNewTypeOptionFallible GracePeriodInDays.create`, so an out-of-range
value returns the typed error before anything is written. On create, the route wraps
`int option` with `convertOptionToDesiredTypeWithFallibleConverter GracePeriodInDays.create`
on the line after memo, exactly as it already wraps memo, and passes it to
`constructNewAndPersist`; that one extra binding is the only other route change. Existing tests
that build these records gained `gracePeriodInDays = None` / `gracePeriodInDaysUpdate =
NoChange` (8 files, 17 lines). Full integrated run after A3: 1794 passed, only the 30
placeholders failing.

**A4 matching.** `MasterAgreement.effectiveGracePeriodInDays : MasterAgreement -> int` in
`MasterAgreement.fs`: the agreement's grace period in days when it has one, else the cadence's
(the cadence table moved there as a private helper). It reads only a Master Agreement, so by
the SrcDeveloper placement rule (lowest tier that sees everything it references) it belongs in
the entity module, not `CashFlowOps.fs`; `CashFlowComponent.fs` can't see `MasterAgreement`, and
nothing above the entity needs to decide it. `CashFlowOps.fs` now maps each agreement ID to
that effective grace instead of its cadence type; the window arithmetic and the `None -> 0`
arm are unchanged. The old comment ("how many days past an invoice's due date…") is gone, not
carried over. The four existing cadence theories and the rest of `LinkageAndMatching` /
`InvoiceMatching` pass unchanged (85 passed; the 14 failures are this slice's placeholders).

**B1 stale-name route test** (`InterfaceBridge/CashFlowRoutes.fs`, "REQ-CF-14.2 REQ-CF-14.3
renaming an agreement whose Instances the open Instances fetch has already shown under the old
name returns every Instance in the UpdateAgreement return under the new name, and the fetch by
the new name reads every one back under the new name"). Its own Daily agreement, two Instances
(today, tomorrow) created through CreateInstance; FetchOpenInstances is read first and asserted
to show both under the old name, which loads the cache; then UpdateAgreement renames; both the
update's return and FetchAgreementSummary by the new name must carry exactly those two Instance
IDs under the new name. The #094 test is untouched. Seen failing three ways, each restored:

- Src perturbed as the plan says (`convert [Agreement] to [AgreementReturn]` built its Instances
  with `convert [Instance] to [InstanceReturn]`): red on the update's return, `Expected: [(…,
  "CF-14.2 instance renamed …"), …] Actual: [(…, "CF-14.2 instance rename …"), …]`.
- Same perturbation with the update-return assertion commented out: red on the fetch by the
  new name, same shape (old name from the warmed cache).
- The warm-up assertion, expected changed to the new name: red, old name actual.

**B2 article.** `CompoundedLearnings/articles/coding/record-and-accessor-module-qualified-access.md`,
with its row in `catalogs/coding.md`, in the CreateLearning format. Reproduced first in a
scratch class library outside the repo (.NET SDK 10.0.114, FSharp.Core 10.0.114): a private
record `Update` and `module Update` with a same-named accessor, side by side in `module
Outer.Orchestration`. The fully qualified accessor call gave `FS0728 Field 'invoiceUpdates' is
not static` and `FS1093 … not accessible`; `open` plus the short form compiled; `create` by its
qualified path compiled. Two findings beyond slice 3's note, both in the article: with the
record public, FS0728 still fires on its own (so privacy is not the cause), and an accessor
named differently from the field compiles qualified.

**Part C tests.** Two names renegotiated at step 9, out loud: REQ-CF-14.1's "left out" can't
be met with the contract as §2 fixes it (`int option` is a required JSON field; §5.3), so
"an agreement created with the grace period in days left out is stored with none" became
"… with a null grace period in days …", and the route theory dropped its "left out" case
(now "for each of a grace period in days of 12 and null …"). No test asserts the omission
refusal: it fails loudly and writes nothing (LOUD-FAILURE). The rest are as committed.

What each was seen failing against (each perturbation restored, `git status` clean after):

- Isolated 2.28 accept/reject: `GracePeriodInDays` bounds moved to 1/364 (both accept cases
  red; reject red on -1 naming bound 1), then max alone to 364 (366 assertion red).
- Integrated create, 0/365 and -1/366: same bound move (accepts red with the bound message,
  rejects red "Wrong error … maximum allowable value of 364" / "minimum … of 1"). Create with
  null: `persist` writing 7 (red, `Actual: Some({ graceDays = 7 })`).
- Matching theories and the widen test: `effectiveGracePeriodInDays` ignoring the agreement's own
  grace. Red: both paid edges for Monthly 20 and Daily 5, both orphan edges for Monthly 0, and the
  widen test (7 of 14). The other 7 are the ones that hold under the cadence's window, as they
  should: that is what the three rows exist to split.
- Cleared and widen tests: `update` writing `coalesce(grace_period_days, @value)` (a stored grace
  can't be cleared or changed). Cleared red "Expected the run to fail on orphaned lines; it
  succeeded"; widen red on the second run's orphan.
- Integrated update set/clear and the cadence theory: cadence clause also writing
  `grace_period_days = 7` (both theory cases red, `Actual: Some(7)`); `update` writing null for
  the grace (set red); the coalesce one (clear red, `Actual: Some(12)`).
- Listing: the converter returning `None` for the grace (12 red), then inventing `Some 0` (none
  red).
- Route create and set/clear: converter returning `None` (12 red on create, set, and the refused
  test's stored 12), then `Some 0` (null red on create and on clear); the update converter turning
  `SetTo null` into `NoChange` (clear red: an update naming only the grace becomes a no-op).
- Route refused update: the converter swallowing the range error as `NoChange` (red, "Expected
  failure; got success"); its after-state assertion by expected value (13 and a memo, red).

REQ-SYS-3.3 got nothing new (the modified-at theories enumerate entities, not Master Agreement
fields). No `Not yet implemented` placeholder remains.

### 5.3 Route payloads
(every contract that changed, field by field; whether an operator payload
that omits `gracePeriodInDays` or `gracePeriodInDaysUpdate` is accepted by
the operator CLI, tried, not assumed. Dan's ops scripts send these
payloads, so either answer is fine, but it must be known)

Contracts changed, and only these:

- `CreateAgreementInput`: `gracePeriodInDays: int option`, after `memo`.
- `UpdateAgreementInput`: `gracePeriodInDaysUpdate: FieldUpdate<int option>`, after
  `memoUpdate`.
- `MasterAgreementReturn`: `gracePeriodInDays: int option`, after `memo`. So it appears in
  CreateAgreement, UpdateAgreement and FetchAgreement returns (via `AgreementReturn`) and in
  ListAgreements (via `AgreementListingReturn`).

**Omitting the field: rejected, both verbs.** Tried with the built operator CLI
(`Ui.OperatorCli CashFlow CreateAgreement --file …`) against the throwaway `sonofleo_test`:

- CreateAgreement without `gracePeriodInDays`: exit 1, `Failed to deserialize JSON string into
  type …CreateAgreementInput. Missing field for record type …CreateAgreementInput:
  gracePeriodInDays`. Nothing written.
- UpdateAgreement without `gracePeriodInDaysUpdate`: exit 1, same message naming
  `gracePeriodInDaysUpdate`.
- With `"gracePeriodInDays": null`, create succeeded and returned `"gracePeriodInDays":null`; an
  update sending only `"gracePeriodInDaysUpdate":{"Case":"SetTo","Fields":[null]}` succeeded and
  returned null. The probe agreement was deleted afterwards.

So every existing ops script that sends CreateAgreement or UpdateAgreement must add the field
(`null` / `{"Case":"NoChange"}`); it fails loudly, naming the field, until it does. This is the
library's rule for every option field in every contract, not something new here. See §5.6 for
REQ-CF-14.1's "left out".

### 5.4 Stale-cache cases
(any test that needed its data changed under the stale-cache rule)

None. B1 warms the master agreement name cache on purpose and uses names unique to its run;
no other test reads a renamed record through a cache.

### 5.5 ArchReviewer
(verdict, findings, what you did about each)

Run by this session over `git diff main...HEAD -- Src Checks DbMigration` (9 files, +78/−19;
nothing under `Checks/`), Gate 0 green first. **APPROVED, no findings.** Notes per check:

- 1 Domain ownership: the column, type, errors and effective-grace rule are all CashFlow's,
  on CashFlow's table.
- 2/5 Layer and placement: `GracePeriodInDays` is a domain primitive in `CashFlowComponent.fs`
  beside its twin; `effectiveGracePeriodInDays` reads one entity so it sits in that entity's
  module; the orchestration file lost a function rather than gaining one; contracts stay in the
  bridge.
- 6 Naming: `GracePeriodInDays` mirrors `DaysDueAfterInvoiceDate`; the AppError cases follow
  `<Prefix><Concept><Failure>`; converters reused, none added.
- 7 Comments: none added; the stale "past an invoice's due date" comment removed. The
  unchanged `None -> 0` arm in `windowCovers` is unreachable in practice (every open Invoice's
  agreement is fetched) and was left as it was.
- 9 Scope: only the plan's contract fields; the one route line beyond passing the value is the
  create wrapping (A3).
- 11/12: `create` stays as before; validation through the existing `convert…Fallible` helpers.
- 13 Once-only: candidate selection untouched; only the window's width changed.

### 5.6 Disagreements, deviations, skipped items, things for Dan

No item was skipped.

- **REQ-CF-14.1 "left out" vs the §2 contract (the one for Dan).** The spec says the grace
  "may be given or left out; left out, it is null". With `gracePeriodInDays: int option`, as §2
  fixes it, a CreateAgreement payload that omits the key is refused by the deserializer, naming
  the field (§5.3, tried with the CLI). Sending `null` works. I kept the §2 contract, read "left
  out" as "not given, i.e. null", and renegotiated two committed names to say null (§5.2). If
  Dan wants a payload without the key to be accepted (so existing ops scripts keep working
  unchanged), the narrow way I found is the library's `Skippable<int option>` on that one field
  (tried in a scratch script: missing → `Skip`, null → `Include None`); it changes the contract
  type, so I didn't take it. A type-level `JsonFSharpConverter` override was tried and rejected:
  with `WithAllowOverride` it broke null handling for every option field of the type. Until then,
  every ops script that sends CreateAgreement or UpdateAgreement needs the new key added.
- **AppError placement.** The two cases are slotted alphabetically (after
  `CashflowExternalInvoiceIdTooLong`), per the SrcDeveloper AppError rule, not literally beside
  the days-due cases as A2 says.
- **Create-route wrapping.** A3 says the converters wrap `int option`; on create the route
  already wraps memo inline, so the grace is wrapped the same way on the next line rather than
  through a new converter. The update path goes through the converter as A3 says.
- **Pushing.** The SrcDeveloper skill says never push; this plan says push after every item.
  I followed the plan.
- **Name-grader suggestions not taken**, with reasons, are in §5.2 (notably a REQ-CF-13.1
  overlap test: the oldest-Invoice rule is unchanged and already tested).
- **A perturbation artefact worth knowing.** Making a cadence update also write the grace
  (to see the cadence-change theory fail) also changes the fixture, whose Instance creation
  calls `updateCadence`; the set/clear test then failed on its "starts with none" assertion.
  Its own assertions were seen failing by the separate perturbations listed in §5.2.
- **Environment.** The container had no .NET SDK; I installed `dotnet-sdk-10.0` (10.0.114)
  from the Ubuntu archive. The direct dotnet-install script was refused by the sandbox.
