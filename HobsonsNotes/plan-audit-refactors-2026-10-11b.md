# Plan — Audit 2026-10-11b refactors (Src and tests)

Written 2026-10-11 by Hobson for one Claude Code session that can see this
repository and nothing else, working unattended overnight. Branch
`audit-refactors-2026-10-11b`, cut from `main`. Where this plan and
`Specs/Behavioral/` disagree, the spec wins — tell Dan.

## 0. Start here

Audit `2026-10-11b` (architecture and F#/DDD panels) is ruled in
`Audit/2026-10-11b/99-disposition.md` on main. **This plan is every row
owned by `impl-agent` or `test-agent` there**, plus 11a #054 from
`Audit/2026-10-11a/99-disposition.md`. Items cite rows as `#NNN` (11b unless
marked 11a). Read the row before starting an item: the ruling is the
instruction, except where §3 says otherwise. §3 was written after checking
every ruling against the code on 2026-10-11, and where they disagree §3
decides.

R-labels (R0–R8) group the rows; they appear in the rulings.

1. Read §1 and §2.
2. Create the branch, rebuild `sonofleo_test` from scratch
   (`Tests/Tests.Integrated/setup-throwaway-test-db.sh`), and record the
   baseline: build warnings, `Tests.Isolated` and `Tests.Integrated` counts,
   `Checks/run-all.sh`. Put them in §5.1 straight away.
3. Do Parts A–E **in order**. Later parts depend on earlier ones.
4. **Commit and push after every numbered item**, and append that item's
   entry to §5 in the same commit. This is a long session; if your context
   is compacted, §5 and `git log` are how you find your place. Re-read §2
   and the item you are on after any compaction.
5. Finish with §4.

**Never stop for a question.** Nobody will answer until morning. If an item
cannot be done as written, do the closest thing that keeps every rule in §2,
record exactly what you did and why under §5.6, and move on. If an item
would need a database migration, do not write one: skip that item, say so
in §5.6, and carry on with the rest.

## 1. Read before you touch anything

- `README.md`, `Specs/README.md`, `Src/README.md`, `Tests/README.md`.
- `Skills/SonOfLeoSrcDeveloper/SKILL.md` — how Src is shaped. Follow it.
- `Skills/ArchReviewer/SKILL.md` — run it over your own diff before §4.
- `Skills/SonOfLeoRequirementsAudit/resolved-findings.md`, especially
  LOUD-FAILURE, ENTRYDATE-IO and REPORT-RECORDS.
- `CompoundedLearnings/articles/architecture/type-taxonomy.md` (component
  and composite types, rewritten 2026-10-11) and `validation-layers.md`.
- The model's Type conventions, Encapsulation and Error handling
  constraints (`Architecture/SonOfLeo.archimate`, Motivation folder). In
  particular: "Component types are defined in a components module in the
  lowest tier of their business domain" (and its documentation),
  "Naked primitives are discouraged in Entity or component types", and the
  error-module rule.
- Both disposition records named in §0.

## 2. Standing rules

- **You write `Src/`, `Checks/`, `Architecture/` (drift fixes only),
  `Src/README.md` (item B1 only) and `Tests/`; never `Specs/`.** No migration
  in this slice (§0).
- `git pull --rebase --autostash` before every commit. Only Dan merges to
  `main`.
- **Public repository.** No institution names, real tickers, account numbers,
  people's names or real amounts anywhere: code, comments, fixtures, commit
  messages.
- **Dan is the authority.** If a ruling looks wrong, make the case once in
  §5.6; don't edit a spec, and don't quietly do something else.
- **No REQ annotations in source.**
- **Loud beats bubblewrap.** No new guard, error case or test for a failure
  that already aborts loudly with nothing written.
- **Code never breaks a tie** unless a requirement names the tie-break.
- **Counts only alongside values.**
- **Every operation uses its context's initiation instant** (REQ-SYS-3.4).
- Commit subjects describe the behaviour or the shape change, not the edit.

### What this slice is, and what it must not change

These are refactors. Business-tier types stop carrying persisted names and
codes; they carry IDs and domain types, and **names are resolved in
`Ui.InterfaceBridge`** (converters and report writers) through the
LookupCaches. What the operator sees does not change:

- **Route payloads and report output stay byte-for-byte the same**: the same
  fields, the same values, the same order. The interface contracts keep
  their string fields.
- So **the route tests (`Tests/Tests.Integrated/InterfaceBridge/`) are the
  oracle for this slice.** Their assertions on payloads and output should
  pass without edits. Setup code in them that calls a Business function
  whose signature changed (e.g. a cache accessor in B1) may change; an
  assertion may not. If an assertion needs changing, that is a behaviour
  change: stop that item's approach, find one
  that keeps the route test unchanged, and if there is none, record it in
  §5.6 before changing the test.
- Business-tier tests that read name or code fields change to compare IDs
  (from the fixture) or domain values. A changed assertion must still be
  seen failing before it passes (perturb Src or the expected value).
- Every **order** a requirement names must survive. Where the ruling moves
  a sort (into SQL, or into a converter after name resolution), perturb the
  sort and watch the existing order test fail.

### Name resolution rules (apply in every item)

1. **A LookupCache sees only committed rows, and it lives for the whole
   process** (`App.DataAccessLayer.LookupCache`). Inside a route that wrote
   a name, the cache can return the old one (11a #094).
   - When the converter or writer has the entity in hand (it was returned,
     or just written), take the name **from the entity**, not the cache.
   - Use a cache only for **referenced** records the route did not write
     (owners, linked ledger accounts, a holding's security, dimension
     values).
   - Do not make the cache transaction-aware and do not add invalidation.
2. **In tests, the cache outlives a test.** A test that renames a record
   and then reads a report naming it can see the old name if an earlier
   test loaded that cache. If you hit this, change the test data so the
   report test does not depend on a renamed record; do not change the
   cache. Record each case in §5.6.
3. Converters and report writers may take a `Context` (or the resolved
   names) as a new parameter. That is expected; routes already have a
   context.
4. A resolution failure is a loud error (an `IAppError`), never a blank or
   a placeholder name.

## 3. The work

File references were checked on 2026-10-11 and **may have drifted**;
verify before editing.

### A. Moves that need no new caches

1. **Domain types out of the error modules** (#002–#005, #071). In each
   project the error module compiles before its component module, so the
   component can define the type.
   - #002 `ActivityShapeProblem` (`PositionsError.fs` ~10) to
     `PositionsComponent` with a `toString` carrying today's message text;
     `PositionsActivityShapeInvalid` carries `string * LocalDate * string *
     string`; delete the compile-order todo. Construction sites in
     `InvestmentActivity.fs`. Tests reading the fourth field:
     `PositionsSliceTwoRoutes.fs` ~246, `Tests.Isolated/.../LotsAndActivity.fs`
     ~194 compare it to `ActivityShapeProblem.toString <case>`.
   - #003 `OrphanedLineReason` (`CashFlowError.fs` ~8) to `CashFlowComponent`
     with a `toString`; the case carries `(Guid * Guid * string) list`.
     Built in `CashFlowOps.fs` ~542–549; tests in `InvoiceMatching.fs`,
     `LinkageAndMatching.fs`, `Cancellation.fs` compare against
     `OrphanedLineReason.toString <case>`, so each still pins one reason.
   - #004 `StageLineProtection` to `ClassificationComponent`, with
     `describeProtection` as its public `toString`; both cases carry
     `Guid * string`. `StageEntryOrchestration.fs` ~569–690 keeps the typed
     value until it raises (the `<> RecordedInClassificationRun` comparison
     at ~686 stays on the typed value). Tests in `StageEntryUpdate.fs`
     match the case, then compare the string to the `toString`.
   - #005 Delete the `IngestionRejectedRecord` record from
     `DataIngestionError.fs`. `IngestionFileRejected` carries
     `string * (int list * string option * IAppError) list`. Where
     `StageEntryOrchestration.fs` (~449–506) and `IngestionRoutes.fs` (~37)
     want a named shape while building it, a private record local to that
     module is fine. Tests change their destructuring only.
2. **RulePriority** (R8: #041, #042). New `RulePriority` in
   `ClassificationComponent`: `private RulePriority of int`, `create`
   accepting any int (total: returns the value, not a Result), `value`, and
   a function that says which of two priorities wins (lower wins), used by
   `Classifier.fs` ~42 instead of the bare `List.min`/comment. Copy the
   shape of `DateInMonthNumber` (`Business.General/Cadence.fs` ~119) minus
   the validation. `ClassificationRule.priority` and
   `PrioritizedMatch.priority` take it; the DAL binding and read unwrap and
   wrap at the column; `FieldUpdate<int>` becomes `FieldUpdate<RulePriority>`.
   The contracts stay `int`; `ClassificationFieldConverters` wraps.
   `PrePostingReview.fs` ~80 and the SQL ordering at
   `ClassificationOrchestration.fs` ~136 keep their order. Tests: the test
   helper `createClassificationRuleForTest` may keep an `int` parameter and
   wrap inside.
3. **Single-domain rows to their components modules** (#008, #012, #020;
   the #079 accept list).
   - #008 `AccountActivityDetail` (`AccountActivity.fs` ~15) to
     `JournalEntryComponent` after `JournalEntryLineMemo`.
     `AccountActivity` stays.
   - #020 `PeriodActivityLine` (`PeriodActivity.fs` ~15) to
     `JournalEntryComponent`. `PeriodActivityAccount` stays.
   - #012 `AgreementFilter` (`FetchFilterAndSort.fs` ~90) to
     `CashFlowComponent`; `FetchStageEntrySort` (~80) to
     `StageEntryComponent`; `IngestionContracts.fs`' open of `FetchFilters`
     becomes `StageEntryComponent`.
   - **Known clash:** `FetchSort` and `FetchStageEntrySort` share the cases
     `EntryDateAsc`/`EntryDateDesc`. After the move, open order makes the
     unqualified cases resolve to `FetchSort` in `StageEntryOrchestration.fs`
     (~903) and `StageEntryFetching.fs` (~606, ~630). Qualify them
     (`FetchStageEntrySort.EntryDateAsc`).
   - **Inference risk:** `JournalEntryComponent` is opened widely. New
     public record fields there (`amount`, `memo`, `entryDate`, …) can
     change what an unannotated `x.amount` resolves to. Where the build
     breaks this way, annotate the lambda's parameter type; don't rename
     fields.
4. **Privacy** (#065, #067, #068, #069, #070). One commit per row; #067 last
   and on its own.
   - #065 `InvoiceCompositeUpdate` and `InstanceCompositeUpdate`
     (`InstanceOrchestration.fs` ~412) become private with a `create` and
     accessors. **Correction to the ruling:** they are built in
     `CashFlowOps.fs` (~329, ~702, ~880) as well as in
     `CashFlowFieldConverters.fs` (~458–530); both call `create`. Tests that
     use `{ u with … }` copy-and-update stop compiling: rebuild those values
     with `create`, or add a test helper in `Tests.Helpers`; do not add
     `with…` functions to Src for tests' sake.
     `PaymentAgreementClassificationResult` stays public.
   - #068 `InvoiceDecision` and `PaymentPostingTransition`
     (`CashFlowComponent.fs` ~386) become private with `create` and
     accessors. (`PaymentPostingTransition` changes again in item C3; do
     #068 first.)
   - #069 Merge `BaseStageEntryGroupId` and `BaseStageRawRow` into
     `StageEntryComponent.fs` (it needs opens for NodaTime, Money,
     AccountComponent and JournalEntryComponent; its project already
     references Ledger). `BaseStageRawRow` goes private with a `create`
     that `IngestionFieldConverters.fs` (~184) calls **and accessors**:
     `StageEntryOrchestration.fs` reads its fields (~153–497) and tests
     build and read it (`StageEntryIngestion.fs` ~46, ~203, ~882;
     `StageEntryFetching.fs` ~677). Delete `BaseStageEntry.fs` and remove it
     from the `.fsproj` (`check-compile-order.sh` checks this); fix opens.
     The model's `BaseStageEntry` component goes too (drift).
   - #070 `StoredFieldMatch` and `StoredRuleGroup` (`ClassificationRule.fs`
     ~67): use `type private …` (private type), **not**
     `= private { … }` (private representation), which can hide the fields
     from the JSON serializer. Run `ClassificationRuleCrud.fs` and confirm a
     rule with rule groups round-trips. If serialization breaks either way,
     leave them public and say so in §5.6.
   - #067 The seven classification component records
     (`ClassificationComponent.fs`: `MoneySearchPattern` ~59,
     `MatchCandidate` ~133, `PrioritizedMatch` ~147, `ClassificationResult`
     ~159, `ClassificationRun` ~164, `PaymentAgreementClaimCluster` ~169,
     `PaymentAgreementDecision` ~193) become private with same-named
     accessors and `create`. Expect roughly 66 field reads in Src, 110+ in
     tests, and about 40 construction sites (many in `Tests.Isolated`).
     Mechanical; one commit.
5. **Ledger functions take domain types** (R7: #039, #040, #077).
   - Delete `Account.updateAccountNameById` and
     `Account.updateExternalReferenceById` (`Account.fs` ~295–314).
     `Account.update` takes three positional `FieldUpdate`s (name, external
     reference, activity period); there is no updates record and you need
     not add one. `AccountRoutes.fs` (~71, ~81) converts in
     `AccountFieldConverters` and calls `Account.update` with
     `SetTo`/`NoChange`. There is a reference converter already; add an
     AccountName converter beside it (and use it in `accountCreate`, which
     calls `AccountName.create` inline at ~26).
     Tests in `Tests.Integrated/Model/Ledger/Account.fs` (~323–374) call
     `Account.update` instead.
   - `FiscalPeriod.fetchIdByKey` takes `FiscalPeriodKey` and unwraps only at
     the parameter binding and in its not-found error.
     `EntryDate.create` (`JournalEntryComponent.fs` ~102) stops unwrapping.
     Tests: `DalTests.fs` ~145, `Model/Ledger/FiscalPeriod.fs` ~45,
     `FiscalPeriodCreation.fs` ~62 build keys with `FiscalPeriodKey.fromString`.

### B. LookupCaches (R0)

1. **Typed accessors over private caches** (#081). In each entity module,
   the raw `Cache<…>` binding becomes private, and the module exposes typed
   functions, e.g.
   `Account.idToCode : Context -> AccountId -> Result<AccountCode, IAppError>`,
   `Account.codeToId : Context -> AccountCode -> Result<AccountId, IAppError>`.
   The accessor wraps and unwraps the domain types (a stored value that
   fails its constructor is a loud error).
   - Existing caches: `Account` (codeToId, idToCode, idToName),
     `FiscalPeriod.keyToId`, `MasterAgreement` and `PaymentAgreement`
     (nameToId, idToName). Callers are in `AccountFieldConverters`,
     `FiscalPeriodFieldConverters`, `SharedContractConverters`,
     `CashFlowLookupConverters`, `StageEntryLine.fs`,
     `ClassificationOrchestration.fs`, `StageEntryOrchestration.fs`,
     `AgreementOrchestration.fs`, and tests (`AccountRoutes.fs`,
     `Model/Ledger/Account.fs`, `FiscalPeriodRoutes.fs`,
     `JournalEntryFetching.fs`). `DalTests.fs` tests the raw factories and
     stays as it is.
   - New caches, **id → name only** unless a later item needs name → id:

     | Module | Column | ID → name type |
     |---|---|---|
     | `Business.General/Person.fs` | `general.person.person_name` | `PersonId → PersonName` |
     | `Positions/InvestmentAccount.fs` | `positions.investment_account.account_name` | `InvestmentAccountId → InvestmentAccountName` |
     | `Positions/Security.fs` | `positions.security.security_name` | `SecurityId → SecurityName` |
     | `Positions/DimensionValue.fs` | `positions.dimension_value.value_name` | `DimensionValueId → DimensionValueName` |
     | `Positions/Property.fs` | `positions.property.property_name` | `PropertyId → PropertyName` |
     | `Classification/ClassificationRule.fs` | `classification.classification_rule.rule_name` | `ClassificationRuleId → ClassificationRuleName` |
     | `DataIngestion/IngestionSource.fs` | `ingestion.source.source_name` | `IngestionSourceId → JournalRefFinancialInstitution` |

   - **Never a name → ID cache for DimensionValue**: `value_name` is unique
     only within its dimension.
   - A ticker or a security's dimension values are not cached: where a
     converter needs them, it fetches the `Security` entity.
   - Tests: one isolated-or-integrated test per new accessor is not
     required (they are thin wrappers over tested machinery); the R1–R6
     route tests exercise them. Do test any accessor whose wrap can fail
     silently (none expected).
2. **`Src/README.md`**: replace the LookupCache row (~36) with this text:

   > `App.DataAccessLayer.LookupCache` | The generic route-lifetime column ↔
   > ID cache machinery (`stringToIdCache`, `idToStringCache`). Each entity
   > module that needs a lookup binds a private cache to its own table and
   > column and exposes typed accessors over it (`Account.idToCode :
   > Context -> AccountId -> Result<AccountCode, IAppError>`,
   > `Person.idToName`, …), so codes, names and IDs never leave the Business
   > tier as bare primitives. A cache reads committed rows only: inside a
   > route that wrote a name, take the name from the entity in hand. |
   > Hand-writing a name-to-ID lookup query, or exposing a raw
   > `Cache<string, Guid>`. Bind a private cache and add a typed accessor.

### C. Ledger, ingestion and cash flow rows (R4, R5, R6)

1. **R4: ledger report rows carry AccountId** (#055–#058, #075 ledger half).
   `TrialBalanceRowNested`/`Flattened` (`TrialBalance.fs` ~11, ~20),
   `ReconciliationRow` (`Reconciliation.fs` ~13), `PeriodActivityAccount`
   (`PeriodActivity.fs` ~23), `DeactivatedAccountWithBalance`
   (`BalanceSheetIntegrity.fs` ~13, whose field is `code`) replace code and
   name with `accountId`.
   - Order is unaffected: the REQ-RPT-1.6 crawl sorts `Account` entities
     (`childrenInTrialBalanceOrder` ~29), and so does the deactivated list
     (~48).
   - `Reconciliation.fs` ~67 matches on AccountId.
   - `TrialBalanceRowFlattened` is also used by the shadow-post in
     `StageEntryOrchestration.fs` (~882) and by `IngestionRoutes.fs`.
   - Resolution: `ReportConverters.fs` and the writers
     (`TrialBalanceWriter`, `PeriodActivityWriter`,
     `BalanceSheetIntegrityWriter`) resolve code and name through
     `Account.idToCode`/`idToName`.
2. **R5: pre-posting review** (#021, #037, #054, #075 ingestion half, #076).
   In `PrePostingReview.fs`:
   - `PrePostingPayment` fields become `PaymentAmount`, `InvoiceDate`,
     `DueDate`, `InvoiceAmount`; `instanceDate` stays a date. The sort at
     ~176 compiles unchanged: these single-case unions already compare by
     value (**the ruling's worry about DueDate comparability is moot**).
   - `PrePostingAgreement` becomes `paymentAgreementId` and
     `masterAgreementId`.
   - `PrePostingLine` carries `accountId` and `classificationRuleId`
     (option) in place of account code/name and rule name. The rule
     tie-break (`ruleNameForLine` ~79, priority then name) stays in the
     function with the rules in hand.
   - `PrePostingEntry` carries `ingestionSourceId`; the source-name sort
     (~96, ~212) stays in the function, using the `IngestionSource` entity.
   - Then move `PrePostingPayment` and `PrePostingAgreement` to
     `CashFlowComponent` (#021).
   - `ReportConverters` and `PrePostingReviewWriter` resolve and unwrap.
3. **R6: cash flow** (#061, #062).
   - Remove `masterAgreementName` from `Instance` (field, accessor, `create`
     parameter, the `agreement_name` read and the join in `fetchAny`).
   - **REQ-CF-14.12 tie-break** (`InstanceOrchestration.fs` ~391, in
     `fetchOpenComposites`): the fetch does not have the master agreements
     in hand. Keep the order in the Business function by fetching the
     master agreements in one batch query there
     (`MasterAgreement` already has a by-ID-batch fetch) and sorting on
     their names. Do not move it to the converter: the `fetchOpenInstances`
     route relies on the order the orchestration gives.
   - `BillToChase`, `ProjectedInvoice`, `PaymentPostingTransition` carry
     `masterAgreementId` and `paymentAgreementId` in place of names;
     `ProjectedAccount` drops code and name (it has `accountId`).
   - `CashFlowFieldConverters` resolve names; the three sorts it does on
     names (~317, ~353, ~358) sort after resolution.
   - **Stale-name trap:** `convert [Agreement] to [AgreementReturn]` (~269)
     deliberately avoids the cache (11a #094) and calls
     `convert [Instance] to [InstanceReturn]` (~287). That inner conversion
     must take the master agreement name from the Agreement in hand, not
     from `MasterAgreement.idToName`, or UpdateAgreement returns the old
     name after a rename. The 11a #094 route test guards this; it must stay
     green unchanged.

### D. Positions (R1, R2, R3), in that order

1. **R1: holdings-as-of, wealth history, roll-forward** (#014, #017, #026,
   #027, #036, #038, #043, #044, #053, #063, #072, #073 R1 half).
   - `HoldingsAsOf.fs`: the query selects investment account, ledger
     account, security, dimension value and owner person IDs instead of
     names. `reconstitute` turns every column into its domain type; the raw
     row is not passed on. Rows group by `InvestmentAccountId`, so
     same-named accounts no longer merge.
   - **Order (REQ-POS-8.4): correction to #044.** The final select has no
     ORDER BY today; the order is F# sorts on names (~143, ~208). Add an
     ORDER BY to the query (account name, account ID, security name, while
     selecting IDs) and drop the F# name sorts; `List.groupBy` keeps first
     occurrence, so the order survives grouping. The existing order tests
     (`HoldingsAsOf.fs` ~176) must fail when you perturb the ORDER BY.
   - `HoldingsAsOfAccount`: `investmentAccountId`, `institution:
     Institution`, `accountGroup: AccountGroup`, `ownerIds: Set<PersonId>`,
     `ledgerAccountId: AccountId option`, `contributionBasis:
     ContributionBasis option` (#027). It crosses into Account, so it
     stays in orchestration.
   - `HoldingsAsOfLine` (#014, which **supersedes #027 for the line**): it
     carries `securityId` in place of name, ticker and dimension-value
     names; lots as `(LocalDate * Quantity * Money option) list`
     (acquired date, quantity, reported cost basis); then it moves to
     `PositionsComponent`. The converter fetches the `Security` for ticker
     and dimension values and resolves names through the R0 caches.
   - `InvestmentWealthHistory`: `ByDimension` (~32) now needs each
     security's `DimensionValueId`; fetch the `Security` entities (one
     batch) rather than reading names off the line.
   - `WealthGroup` (#038, #063) keys on typed values: investment account ID,
     `AccountGroup`, `TaxTreatment`, `DimensionValueId`, `Set<PersonId>`,
     and `Unassigned`. The two `List.sortBy fst` calls
     (`InvestmentWealthHistory.fs` ~52, ~76) would now order by Guid:
     remove them. Order (by name, owners by owners' names, unassigned last,
     REQ-RPT-9.2) is applied after resolution in `ReportConverters`
     (~203) or the writer, whichever holds the resolved labels. The order
     tests in `InvestmentWealthHistory.fs` (~239–322) and
     `PositionsReportRoutes.fs` must stay green and fail under a perturbed
     sort. Then move `WealthPoint` to `PositionsComponent` (#017).
   - `UnitRollForward.fs` (#026, #036, #053): `RollForwardRow.securityId`,
     `RollForward.investmentAccountId`; `startQuantity`/`endQuantity`
     become `Quantity` (an absent line is `Quantity.fromDecimal 0M`);
     `unitsIn`, `unitsOut`, `expected`, `difference` stay `decimal`. Rows
     stay sorted by Security name inside `rollForward` using the entities
     already fetched. `accountName` stays for error cases. Then move both
     records to `PositionsComponent`.
2. **R2: positions views** (#013, #016, #023, #029–#033, #035, #046–#050,
   #052, #059 part, #060, #074).
   - Snapshots (#046, #029): `SnapshotLineView` carries `securityId` in
     place of the name; `SnapshotView` drops `investmentAccountName`;
     `viewSnapshot` loses its `accountName` parameter (and `fetchHeader`
     its string half). Lines stay sorted by Security name in the Business
     function, which keeps fetching each line's Security to sort.
     **Gap in the rulings:** `ReplacedLineLots.security` is a name too; it
     becomes `securityId`, in the same Security-name order, and the
     bridge resolves it. `RecordedSnapshot` stays public (#066).
   - Holdings (#013, #030, #047): `viewHolding` goes; `listHoldings`
     returns `Holding.Holding list`, **still sorted by account name then
     security name in the Business function** (it fetches the accounts and
     securities to sort; the ruling leaves the order unplaced and this
     keeps it where it was). `HoldingView` is deleted. `accountNameOf` and
     `securityNameOf` stay for error cases.
   - Activity (#016, #031, #048): **ruling between #016 and #048:** the
     entity holds a `HoldingId`, not a `SecurityId`, and there is no
     Holding cache. Keep `ActivityView` as
     `{ activity; securityId: SecurityId option }` in orchestration (it
     holds an entity, so it is a composite), dropping
     `investmentAccountName`. `RecordedRange` carries
     `investmentAccountId` in place of the name and moves to
     `PositionsComponent`. Delete `securityNamesById` once its callers
     (`recordActivity`'s error at ~74 can use the fetched Security;
     `listActivity`) no longer need it.
   - Investment accounts (#032, #049): return the `InvestmentAccount`;
     delete `InvestmentAccountView`; the converter resolves owners (Person
     cache, sorted by name) and the ledger code (Account cache).
   - Properties (#033, #050): return the `Property`; delete `PropertyView`;
     the converter resolves owners and account codes and names, and sorts
     the account lists by code (moved from `RealEstateOrchestration.fs`
     ~263).
   - Securities (#023, #035, #052): return the `Security`; delete
     `SecurityView` and `dimensionValueNamesById`; the converter resolves
     dimension value names (DimensionValue cache). The shared
     `convert [Map<Dimension,string>] to …` converter is also used by
     holdings-as-of (R1); keep both callers working.
   - `ledgerAccountCodeAndName` (#060): **delete it outright** in this
     item. Its error-case use (`RealEstateOrchestration.fs` ~100) switches
     to `codeOf` on the fetched account; it has no pre-ledger use (the
     ruling's deferral wording is moot).
   - `personNamesOf` (#059): its third caller is NetWorth (R3). Leave it
     until D3.
   - Converters that were pure now take a `Context`.
3. **R3: net worth** (#028, #073 R3 half, 11a #054).
   - `NetWorthInvestmentAccount`: `investmentAccountId`, `ownerIds:
     Set<PersonId>`, `accountGroup: AccountGroup`; then move it to
     `PositionsComponent` (#079 list).
   - `NetWorthProperty`: `propertyId`, `ownerIds`; it holds
     `LedgerAccountBalance`, so it stays in orchestration.
   - `NetWorth.investmentsByAccountGroup`: `(AccountGroup * Money) list`
     (the sort still orders by the group's string).
   - **Deferred, do not touch:** `LedgerAccountBalance`'s code/name/
     balanceDate reshape and everything pre-ledger (#045, #034, #051, #066
     part, 11a #065, 11a #069).
   - 11a #054: move `NetWorthComponent` (with its `toString`) and
     `NetWorthDate` to `PositionsComponent` beside `WealthGrouping`;
     `classifyDate` stays in orchestration.
   - `NetWorthWriter` and `ReportConverters` resolve names; properties stay
     ordered by name and investment accounts by name (REQ-RPT-8.5): sort
     after resolution if the Business function can no longer do it from
     entities in hand.
   - Delete `personNamesOf` (`PersonOrchestration.fs` ~59) now that no
     caller is left.

### E. Model

1. `model_drift.py` will report moved and deleted files (at least
   `BaseStageEntry.fs`) and changed serving edges. Fix the model until it
   reports NO DRIFT; `validate.py` VALID. Function documentation that
   names a deleted view or a moved sort gets corrected in the same commit.

### Out of scope

- Every row ruled overruled or deferred; everything pre-ledger (D40).
- Spec edits, waivers, and rows owned by Hobson alone.
- Any change to what a route returns.

## 4. When you finish

- Build: 0 warnings, 0 errors.
- `Tests.Isolated` and `Tests.Integrated` pass, the latter against a
  `sonofleo_test` rebuilt from scratch. If you can't get the database, say
  so; don't skip or fake.
- `bash Checks/run-all.sh` green.
  `Skills/SonOfLeoRequirementsAudit/traceability-audit.sh .` Invariants 1
  and 2 clean (they were clean on main; this slice adds no requirement).
- `validate.py` VALID, `model_drift.py` NO DRIFT.
- ArchReviewer over `git diff main...HEAD -- Src Checks`; act on what it
  finds or say why not in §5.5.
- Route tests: confirm in §5.1 that no assertion under
  `Tests/Tests.Integrated/InterfaceBridge/` changed, or list each change
  and its reason.
- Push, and complete §5.

## 5. Report

Written by the session that did this plan. Fill it in as you go (§0 step 4).

### 5.1 Outcome
(baseline first; final counts and check outputs at the end)

**Baseline** (main at 2dcfebe, `sonofleo_test` rebuilt from scratch by
`setup-throwaway-test-db.sh` on a local PostgreSQL 16, .NET SDK 10.0.112):

- Build: 0 warnings, 0 errors.
- `Tests.Isolated`: 530 passed, 0 failed.
- `Tests.Integrated`: 1778 passed, 0 failed.
- `Checks/run-all.sh`: 12 passed, 0 failed, 1 skipped (check-traceability
  skips off main).
- `traceability-audit.sh .`: Invariants 1 and 2 clean.
- `validate.py`: VALID. `model_drift.py`: NO DRIFT.

**Final** (branch head after E1, `sonofleo_test` dropped and rebuilt by
`setup-throwaway-test-db.sh`, clean build):

- Build: 0 warnings, 0 errors.
- `Tests.Isolated`: 530 passed, 0 failed.
- `Tests.Integrated`: 1778 passed, 0 failed.
- `Checks/run-all.sh`: 12 passed, 0 failed, 1 skipped (check-traceability
  skips off main).
- `traceability-audit.sh .`: Invariants 1 and 2 clean.
- `validate.py`: VALID. `model_drift.py`: NO DRIFT.
- Every numbered item A1 to E1 is done; none skipped. No migration.

**Route tests.** No payload or route-output assertion under
`Tests/Tests.Integrated/InterfaceBridge/` changed. Nine files changed in
setup, expected-side construction or destructuring only:

- `PositionsSliceTwoRoutes.fs` REQ-POS-13.1 (A1 #002): the error case's
  fourth field is compared to `ActivityShapeProblem.toString
  QuantityMissing`, the same text the message always showed.
- `IngestionRoutes.fs` (A1 #005): `rejectionOf` destructures the
  `(lines, group, error)` tuple instead of the deleted record; same values.
- `ClassificationRuleRoutes.fs` FetchClassificationRuleById (A2): the
  expected side adds `|> RulePriority.value` because the accessor returns
  `RulePriority`; the payload and comparison are unchanged.
- `CashFlowRoutes.fs` (A3, three places): the `AgreementFilter` type
  annotation follows its move to `CashFlowComponent`.
- `AccountRoutes.fs`, `FiscalPeriodRoutes.fs` (B1): setup and cleanup
  lookups go through the typed cache accessors.
- `ReportRoutes.fs` (C1, C2): REQ-RPT-4.1 and REQ-RPT-6.4 build their
  expected side from Account entities by ID instead of reading code and
  name off the Business row; REQ-SYS-3.4 passes a context to the trial
  balance and pre-posting writers.
- `PositionsRoutes.fs` REQ-POS-8.1 (D1): the holdings-as-of expected side
  builds names from the InvestmentAccount, Security and ledger Account
  fetched by ID.
- `PositionsSliceTwoRoutes.fs` REQ-POS-14.1 (D1): the roll-forward expected
  side resolves each row's Security by ID and reads `Quantity.amount`.
- `PositionsReportRoutes.fs` REQ-RPT-9.5 (D1) and REQ-RPT-8.6 (D3): the
  data-only expected sides resolve owner, account and property names from
  entities by ID and sort owners by name.

### 5.2 Per item
(one entry per numbered item: what changed, commits, tests touched)

**A1. Domain types out of the error modules** (#002–#005, #071). Four
commits, one per row.
- #002 `ActivityShapeProblem` is in `PositionsComponent` after
  `ActivityKind`, with `toString` carrying the old message fragments;
  `PositionsActivityShapeInvalid` carries `string * LocalDate * string *
  string`; the compile-order todo is gone. Tests: `LotsAndActivity.fs`
  REQ-POS-12.6 and route `PositionsSliceTwoRoutes.fs` REQ-POS-13.1 compare
  the fourth field to `ActivityShapeProblem.toString <case>`.
- #003 `OrphanedLineReason` is in `CashFlowComponent` with a `toString`
  (the reason half of each old sentence; the message keeps the line and
  agreement IDs in front, so the text is unchanged).
  `CashflowPaymentAgreementLinksOrphaned` carries `(Guid * Guid * string)
  list`. Tests in `InvoiceMatching.fs`, `LinkageAndMatching.fs`,
  `Cancellation.fs` compare to `OrphanedLineReason.toString <case>`.
- #004 `StageLineProtection` is in `ClassificationComponent` (top of the
  file); `describeProtection` became its public `toString`. Both error
  cases carry `Guid * string`. `StageEntryOrchestration.protectionsOf`
  keeps the typed value, and the edit check still compares
  `<> RecordedInClassificationRun` on it. `StageEntryUpdate.fs` matches the
  case, then compares the string to the `toString`.
- #005 `IngestionRejectedRecord` is deleted. `IngestionFileRejected`
  carries `string * (int list * string option * IAppError) list`;
  `ingestFile` takes the tuple as each record's error, and keeps a private
  `RejectedRecord` while it sorts. `IngestionRoutes` builds the tuple.
  Tests changed destructuring only (`StageEntryIngestion.fs`,
  `RevisedRequirementsStaging.fs`, `StagingIngestionRules.fs`, route
  `IngestionRoutes.fs`).

**A2. RulePriority** (#041, #042). `RulePriority` (private int, total
`create`, `value`, and `winner`: lower wins) sits in
`ClassificationComponent` after `ClassificationRuleName`.
`ClassificationRule.priority`, `create`, `update`'s `FieldUpdate` and
`PrioritizedMatch.priority` take it; `persist`, `reconstitute` and `update`
unwrap and wrap at the column. `Classifier.classifyCandidate` reduces the
matches' priorities with `RulePriority.winner` in place of `List.min`.
`ClassificationFieldConverters` has `convert [int] to [RulePriority]`, used by
the two rule routes (the update maps it over the `FieldUpdate`), and the
return converters unwrap. `PrePostingReview`'s priority-then-name sort and
the SQL `PriorityAsc`/`PriorityDesc` ordering are untouched (the union
compares by its int). Tests: `createClassificationRuleForTest` keeps its
`int` and wraps; direct `constructNewAndPersist`/`ClassificationRule.create`
calls wrap their literal; model assertions compare `RulePriority.create n`;
where a test builds a key or a set of ints it unwraps with `value`.

**A3. Single-domain rows to their components modules** (#008, #012, #020).
`AccountActivityDetail` and `PeriodActivityLine` follow
`JournalEntryLineMemo` in `JournalEntryComponent`; `AgreementFilter` is in
`CashFlowComponent` (before `OrphanedLineReason`); `FetchStageEntrySort` ends
`StageEntryComponent`. `IngestionContracts` opens `StageEntryComponent` in
place of `FetchFilters`. `AgreementOrchestration` names
`CashFlowComponent.AgreementFilter` (it does not open the component module).
The `EntryDateAsc`/`EntryDateDesc` clash is qualified in
`StageEntryOrchestration.fetchFiltered` and `StageEntryFetching.fs` (606,
630). The new `JournalEntryComponent` fields broke no inference. Tests:
`StageEntryFetching.fs` qualification; `CashFlowRoutes.fs` setup annotations.

**A4. Private representations** (#065, #068, #069, #070, #067). Five
commits.
- #065 `InvoiceCompositeUpdate` and `InstanceCompositeUpdate` are private
  with accessors and `create` (inline tuple field types, no new type
  abbreviations). `CashFlowOps` and `CashFlowFieldConverters` call
  `create`. Tests copy-and-update through a new `Tests.Helpers/CompositeUpdates.fs`
  (`withInvoiceUpdates`, `withNewPayments`, `mapInvoiceCompositeUpdates`, …);
  no `with…` functions were added to Src.
- #068 `InvoiceDecision` and `PaymentPostingTransition` are private with
  accessors and `create`; callers in `CashFlowOps`, both field-converter
  modules and three test files use them.
- #069 `BaseStageEntryGroupId` and `BaseStageRawRow` now end
  `StageEntryComponent.fs`; `BaseStageRawRow` is private with accessors and
  `create`, which `IngestionFieldConverters` calls. `BaseStageEntry.fs` is
  deleted and out of the `.fsproj`. The model's component is handled in E.
- #070 `StoredFieldMatch` and `StoredRuleGroup` are `type private`. The
  `ClassificationRuleCrud.fs` round trip of a rule with rule groups passes.
- #067 The seven classification component records are private with
  same-named accessors and `create`. `CashFlowOps.fs` and the
  `StageEntryUpdate.fs` test open `ClassificationComponent` (see 5.6 on the
  accessor quirk). Test edits are accessor rewrites with the same expected
  values.

**A5. Ledger functions take domain types** (#039, #040, #077). One commit.
`Account.updateAccountNameById` and `Account.updateExternalReferenceById`
are gone; the two `AccountRoutes` update routes convert the payload
(`convert [string] to [AccountName]`, new beside the reference converter and
also used by `accountCreate`) and call `Account.update` with `SetTo` and
`NoChange`. `FiscalPeriod.fetchIdByKey` takes a `FiscalPeriodKey` and
unwraps it once for the parameter and the not-found error;
`EntryDate.create` passes the key straight in. Tests changed setup only:
`Model/Ledger/Account.fs` calls `Account.update`; `DalTests.fs`,
`Model/Ledger/FiscalPeriod.fs` and `FiscalPeriodCreation.fs` build keys with
`FiscalPeriodKey.fromString`. No assertion changed.

**B1. Typed accessors over private caches** (#081). One commit. Every raw
`Cache<…>` binding is private; each entity module exposes typed accessors
taking `Context`: `Account.codeToId`/`idToCode`/`idToName`,
`FiscalPeriod.keyToId`, `MasterAgreement` and `PaymentAgreement`
`nameToId`/`idToName`, and new id → name accessors on `Person`,
`InvestmentAccount`, `Security`, `DimensionValue` (no name → id),
`Property`, `ClassificationRule` and `IngestionSource` (to
`JournalRefFinancialInstitution`). A stored value that fails its type's
`create` comes back as that error. Callers (`StageEntryLine`,
`AgreementOrchestration`, `StageEntryOrchestration`,
`ClassificationOrchestration`, the account, fiscal period, shared and cash
flow lookup converters) pass and receive domain types; their not-found
mappings and messages are as before. The account converter's Guid-returning
helper folded into `fallibleConverterAccountCodeToAccountId`. Tests changed
setup only (`Model/Ledger/Account.fs`, `JournalEntryFetching.fs`, route
`AccountRoutes.fs` and `FiscalPeriodRoutes.fs` cleanup lookups); the
REQ-AC-1.5 resolve helper unwraps to keep its Guid assertions. `DalTests.fs`
is untouched.

**B2. `Src/README.md`.** The LookupCache row carries the plan's text.

**C1. R4 ledger report rows carry AccountId** (#055–#058, #075 ledger
half). One commit. `TrialBalanceRowNested`/`Flattened`, `ReconciliationRow`,
`PeriodActivityAccount` and `DeactivatedAccountWithBalance` carry
`accountId` in place of code and name; `Reconciliation` matches trial
balance rows on it. The orders are untouched (the crawl and the deactivated
list still sort `Account` entities). `ReportConverters` take a context and
resolve code and name through `Account.idToCode`/`idToName` (via the account
converters), returning `Result`; the three writers take a context, resolve
each row, then render as before. `ReportRoutes` and the shadow post and
shadow reconcile in `IngestionRoutes` pass the context. Tests:
`TrialBalance.fs` and `StageEntryPosting.fs` find rows by the fixture's IDs
(or `codeOfId`); `Reconciliation.fs` and `BalanceSheetIntegrity.fs` read a
row's code and name back through the lookups with the test's context;
`PeriodActivity.fs` pairs each row with its code and name once per fetch.
Route `ReportRoutes.fs`: see 5.1.

**C2. R5 pre-posting review** (#021, #037, #054, #075 ingestion half,
#076). One commit. `PrePostingPayment` carries `PaymentAmount`,
`InvoiceDate`, `DueDate` and `InvoiceAmount` (the instance-date-then-due-date
sort compiles unchanged); `PrePostingAgreement` carries
`paymentAgreementId` and `masterAgreementId`; both now end
`CashFlowComponent`. `PrePostingLine` carries `accountId` and
`classificationRuleId`; the rule tie-break stays in the function
(`ruleIdForLine`, priority then name, with the rules in hand).
`PrePostingEntry` carries `ingestionSourceId`; the entry sort pairs each
entry with its source's name from the `IngestionSource` entity and sorts
there. The review no longer fetches all accounts or the master agreements
(it only needed them for names). `ReportConverters` (now taking a context
and returning `Result`) and `PrePostingReviewWriter` (a context-taking
`write` that names every line, then renders) resolve through the caches.
Tests: `PrePostingReview.fs` compares account, rule, agreement and source
IDs, and unwraps the payment values; route `ReportRoutes.fs` passes a
context to the writer.

**C3. R6 cash flow** (#061, #062). One commit. `Instance` drops
`masterAgreementName` (field, accessor, `create` parameter, the
`agreement_name` read and the master agreement join). `fetchOpenComposites`
keeps the REQ-CF-14.12 date-then-name order by batch-fetching the Master
Agreements of the open Instances and sorting on their names.
`BillToChase` carries `masterAgreementId` and `paymentAgreementId`;
`ProjectedInvoice` and `PaymentPostingTransition` carry `masterAgreementId`
in place of the agreement name (each name became the ID it named);
`ProjectedAccount` drops code and name. `CashFlowFieldConverters` resolve
(taking a context, returning `Result`) and sort accounts, bills and
transitions on the resolved returns. `convert [Instance] to
[InstanceReturn]` resolves through the cache; the Agreement conversion
instead hands its Instances the Master Agreement's name from the Agreement
in hand. Tests: `Cancellation.fs`, `PaymentsToPosted.fs` and
`ProjectionRules.fs` compare agreement and account IDs (REQ-CF-8.7 reads
the code and name back through the lookups); `CashFlowProjection.fs` finds
F-1280 by ID. No route test changed.

**D1. R1 holdings-as-of, wealth history, roll-forward** (#014, #017, #026,
#027, #036, #038, #043, #044, #053, #063, #072, #073 R1 half). One commit.
The holdings-as-of query selects the investment account, ledger account,
security and owner IDs (owners as one aggregated text column parsed back to
`PersonId`s); `reconstitute` builds the account and line in their domain
types and the raw row goes no further. Rows group by `InvestmentAccountId`.
The query's `ORDER BY` (account name, account ID, Security name, each name
`collate "C"`) replaces both F# name sorts. `HoldingsAsOfAccount` carries
the typed fields the plan lists; `HoldingsAsOfLine` (securityId, lots as
tuples) and `WealthPoint` moved to `PositionsComponent`. `WealthGroup` keys
on investment account ID, `AccountGroup`, `TaxTreatment`, `DimensionValueId`,
`Set<PersonId>` and `Unassigned`; `computeInvestmentWealthHistory` reads
each line's dimension value from the Security (all fetched once, by
dimension only) and no longer sorts. `ReportConverters` resolves a group to
its contract and orders by `wealthGroupOrder` (names, then owner name
lists, unassigned last); the writer's context-taking `write` resolves the
same way and orders its columns by it. `RollForwardRow`/`RollForward` carry
`securityId`, `investmentAccountId` and `Quantity` start and end, sorted by
the fetched Securities' names, and live in `PositionsComponent`.
`NetWorth` resolves its investment rows' names until D3. Tests:
`HoldingsAsOf.fs`, `InvestmentWealthHistory.fs` (through the bridge
converter, which owns the order now) and `UnitRollForward.fs` resolve
names by ID; three route tests changed setup only (§5.1).

**D2. R2 positions views** (#013, #016, #023, #029–#033, #035, #046–#050,
#052, #059 part, #060, #074). One commit. `SnapshotLineView` carries
`securityId`; `SnapshotView` drops the account name; `viewSnapshot` and
`fetchHeader` lose their name halves and lines stay in Security-name order
(each line's Security fetched to sort). `ReplacedLineLots` carries
`securityId`, in the same order. `HoldingView`, `InvestmentAccountView`,
`PropertyView`, `SecurityView`, `viewHolding`, `viewInvestmentAccount`,
`viewProperty`, `viewSecurity`, `dimensionValueNamesById`,
`securityNamesById` and `ledgerAccountCodeAndName` are deleted; the list
functions return entities in the same name orders (`listHoldings` fetches
all accounts and Securities once to sort by account then Security name).
`ActivityView` is `{ activity; securityId }`; `RecordedRange` carries
`investmentAccountId` and moved to `PositionsComponent`. Every positions
converter that named something now takes a context and returns `Result`;
the Property converter sorts its account lists by code; the owners helper
sorts names. `personNamesOf` stays for D3. Tests: the seven Business-tier
maintenance and recording files resolve names by ID through new
entity-reading `PositionsLookups` helpers; no route test changed.

**D3. R3 net worth** (#028, #073 R3 half, 11a #054). One commit.
`NetWorthInvestmentAccount` carries `investmentAccountId`, `ownerIds` and
`AccountGroup` and moved to `PositionsComponent` with `NetWorthComponent`
(and its `toString`) and `NetWorthDate`; `classifyDate` stays.
`NetWorthProperty` carries `propertyId` and `ownerIds`.
`investmentsByAccountGroup` is `(AccountGroup * Money) list`, still sorted
by the group's text. `computeNetWorth` keeps both name orders from the
entities it already holds (accounts from the holdings-as-of order,
properties sorted by name). `ReportConverters` gains
`netWorthInvestmentAccountNames` and `netWorthPropertyNames`, which the
converter and the writer's new context-taking `write` share.
`personNamesOf` is deleted. Tests: `NetWorth.fs`, `NetWorthPreLedger.fs`
and `NetWorthHistory.fs` resolve names by ID (the property lookup takes the
test's transaction context); one route test changed setup only (§5.1).

**E1. Model** (drift). One commit. `BaseStageEntry` is gone from the
model: its realization of "Define the raw ingestion data shape" moved to
`StageEntryComponent`, where the raw row now lives, and on the Application
tiers view the function now sits inside the `StageEntryComponent` box
(widened; Dan owns the layout). 15 serving edges the code no longer backs
were removed (the `BaseStageEntry` ones, `PersonOrchestration` into the
three positions orchestrations, `InvestmentWealthHistory` into its writer,
and others), plus the composition and the other edges that touched
`BaseStageEntry`. 48 serving edges were added for references the slice
introduced, chiefly `LookupCache` into the seven entity modules it now
caches, `Context` into the report writers, entity modules into the
converters and writers that resolve names, and `PersonComponent` and
`PositionsComponent` into the modules that carry owner IDs and the moved
types. No function documentation named a deleted view or a moved sort.
`model_drift.py`: NO DRIFT. `validate.py`: VALID.

### 5.3 Fail-then-pass evidence
(table: perturbation → what failed)

Each perturbation was made in Src, the named tests were seen failing, and
Src was restored from a copy before the item's commit.

| Item | Perturbation in Src | What failed |
|---|---|---|
| A1 #002 | `ActivityShapeProblem.toString` output suffixed with "!" in `InvestmentActivity.confirmShape` | isolated REQ-POS-12.6 and route REQ-POS-13.1 |
| A1 #003 | orphan reason choice inverted in `CashFlowOps` | 17 of 108 in InvoiceMatching, LinkageAndMatching, Cancellation |
| A1 #004 | every protection reported as `RecordedInClassificationRun` in `StageEntryOrchestration` | 8 of 10 REQ-STG-6.5 tests |
| A2 | `RulePriority.winner` picks the higher number | isolated REQ-CR-3.5, REQ-CR-3.6 |
| A2 | `ClassificationRule.persist` binds priority + 1 | 5 of 49 ClassificationRuleCrud (REQ-CR-6.1 ×4, REQ-CR-1.22) |
| C1 | row `accountId` replaced by a fresh ID in TrialBalance, PeriodActivity, BalanceSheetIntegrity | 49 of 83 across TrialBalance, PeriodActivity, Reconciliation, BalanceSheetIntegrity, StageEntryPosting, ReportRoutes (every changed lookup) |
| C1 order | `childrenInTrialBalanceOrder` sorted descending | REQ-RPT-1.6, REQ-RPT-6.3 |
| C2 | fresh account, agreement and source IDs; payment amount and dates swapped; `tryLast` for the rule | 8 of 25 PrePostingReview |
| C2 | rule replaced by a fresh ID (and `Some` for an unrecorded line) | all 7 REQ-RPT-7.3 |
| C2 order | source name dropped from the entry sort | REQ-RPT-7.6 |
| C2 order | payments reversed after their sort | REQ-RPT-7.4 |
| C3 | fresh master and payment agreement IDs on projected invoices, bills to chase, posting transitions | 11 of 60 in Cancellation, CashFlowProjection, PaymentsToPosted, ProjectionRules |
| C3 | fresh AccountId on projected accounts | 5 (CashFlowProjection helpers, REQ-CF-8.6, REQ-CF-8.7) |
| C3 order | `fetchOpenComposites` result reversed | REQ-CF-14.12 |
| C3 order | converter account sort descending; bills reversed after their sort | REQ-CF-8.11, each time |
| D1 order | holdings-as-of ORDER BY descending | 7 of 65 incl. REQ-POS-8.4 |
| D1 order | wealth-group sort removed from the converter | REQ-RPT-9.2 (both order tests), REQ-RPT-9.5 route |
| D1 order | `wealthGroupOrder` puts unassigned first | REQ-RPT-9.2 order test, both rendered-table route tests |
| D1 order | roll-forward sort reversed | 9 of 28 incl. REQ-POS-14.2, REQ-POS-14.1 route |
| D1 | ledger account `None`, owners empty, lots reversed, group keys swapped | 10 of 31, every changed Business-tier assertion (REQ-POS-8.2, 8.3, REQ-RPT-9.2, 9.3) |
| D2 order | snapshot lines, Holdings, Investment Account, Security and Property lists reversed | 24 of 469 incl. REQ-POS-11.2, 11.3, 11.5, 11.6, 7.3, 7.5 at both tiers |
| D2 order | Property account-code sort reversed in the converter | 4 of 147 (REQ-POS-11.6 routes) |
| D2 order | owner-name sort reversed in the converter | 4 of 213 (REQ-POS-11.3, 11.6 routes) |
| D2 | activity `securityId` dropped; range account ID replaced | 11 of 42 (REQ-POS-12.1, 12.9, 13.2–13.5, 13.7) |
| D3 order | property order and group-total sorts reversed in NetWorth | 6 of 62 incl. REQ-RPT-8.5 order tests |
| D3 | owner sets emptied on investment and property rows | 2 of 62 (the REQ-RPT-8.5 owner assertions) |
| D3 order | report owner-name sort reversed | 4 of 34 (REQ-RPT-8.6 route, REQ-RPT-9.2, 9.3, 9.5) |

A4 (#067) changed test assertions only into accessor calls on the same
values, so it had nothing to see fail. B1 and B2 changed no assertion.

### 5.4 Name-resolution notes
(where a name came from the entity in hand, where from a cache; any
stale-cache test data changed under §2 rule 2)

- **Ledger reports (C1).** Converters and writers resolve account code and
  name through `Account.idToCode` and `Account.idToName`. The report routes
  write nothing, so every account is referenced.
- **Pre-posting review (C2).** The converter and writer resolve account
  code and name, rule name, payment and master agreement names and source
  name through the caches; all are referenced. The source-name order stays
  in `PrePostingReview`, which has the IngestionSource entity in hand.
- **Cash flow (C3).** `Instance` no longer carries its master agreement's
  name. `convert [Instance] to [InstanceReturn]` resolves it through
  `MasterAgreement.idToName` for the InstanceComposite routes, which write
  Instances but never the agreement's name. `convert [Agreement] to
  [AgreementReturn]` passes the master agreement's name from the Agreement
  in hand to its Instances, which is the 11a #094 stale-name trap avoided.
  Projection and posting-transition converters resolve through the caches
  (ProjectCashFlow writes nothing; the posting transition writes Payments
  only) and sort on the resolved returns.
- **Holdings, wealth, roll-forward (D1).** Account name, owners, ledger code
  and name, and dimension value names come from the InvestmentAccount,
  Person, Account and DimensionValue caches. The Security entity is fetched
  for its name, ticker and dimensions. Wealth groups resolve through the
  InvestmentAccount, DimensionValue and Person caches.
- **Positions views (D2).** Each route's own entity supplies its own name
  (the Security's from the Security, the account's from the
  InvestmentAccount, the property's from the Property). Caches serve only
  referenced records: owners (Person, sorted), linked ledger accounts
  (Account), a holding's account and Security, dimension values.
- **Net worth (D3).** Investment account, property and owner names resolve
  through the InvestmentAccount, Property and Person caches, shared by the
  converter and `NetWorthWriter`. The route writes nothing. Order stays in
  the Business function, which has the entities.
- Test-side lookups (`PositionsLookups.*NameOf` and the like) read entities,
  not caches.
- **No stale-cache case was hit**, so no test data changed under §2 rule 2.

### 5.5 ArchReviewer

Run over `git diff origin/main...HEAD -- Src Checks` (76 files; local
`main` was stale, see §5.6). Verdict: CONDITIONAL, with no real defect
found: no wrong output, no stale name, no lost order. It confirmed names
come from the entity in hand where the route has one, caches serve only
referenced records with no invalidation, every resolution failure is an
`IAppError`, the orders hold (`wealthGroupOrder`, the `collate "C"` ORDER
BY, owner and property-account sorts, the REQ-CF-14.12 batch fetch), and
no REQ annotation was added. Findings and what I did:

- **Check 12, reinvention: owner-name resolution written twice**
  (`ReportConverters` and `PositionsFieldConverters`). Fixed: the
  `PositionsFieldConverters.ownerNamesOf` is public and `ReportConverters`
  uses it.
- **Check 12, reinvention: pre-posting names resolved twice** (the
  converter and the writer). Fixed: `PrePostingNamedLine`,
  `PrePostingNamedEntry` and ``name [PrePostingEntry]`` moved from the
  writer to `ReportConverters`, and the converter and the writer both use
  them. The reviewer also pointed at the ledger writers' `idToCode` plus
  `idToName` pair; I left those, since each is two calls on one ID and a
  helper would not remove any logic.
- **Check 7, a comment restating `RulePriority.winner`.** Deleted.
- **Check 6, `matchesAtLowest`.** Renamed `matchesAtWinningPriority`.
- **Check 5, `type private RejectedRecord` mid-file in
  `StageEntryOrchestration`.** Left: the plan (#005) places it there, next
  to the one function that uses it.
- **Observation: holdings-as-of fetches each line's Security by ID.** Left:
  the plan prescribes it; noted in case the report grows.

After the fixes, the build had 0 warnings, Isolated 530 and Integrated 1778
passed, Checks 12 passed with 1 skipped, and the model had its serving edges
moved to match (3 added, 7 removed): NO DRIFT, VALID.

### 5.6 Disagreements, deviations, skipped items, things for Dan

No item was skipped and no plan step contradicted Specs/Behavioral/.

- **An F# resolution quirk (A4 #065, #067).** When a private record type and
  its same-named accessor module sit side by side inside an orchestration
  module (`InstanceOrchestration.InvoiceCompositeUpdate`), the fully
  qualified `InstanceOrchestration.InvoiceCompositeUpdate.invoiceUpdates`
  resolves to the type's private field and fails with "Field is not
  static". `create` resolves, and with the module opened the short form
  reaches the accessor. No Src caller outside the module needs it today;
  tests go through `Tests.Helpers.CompositeUpdates`, and two test files
  open `ClassificationComponent` for the same reason. Worth knowing before
  the next private record nested in an orchestration module.
- **Behaviour change (B1).** The period-key temporal filter in
  `SharedContractConverters` validated the trimmed key, then looked up the
  raw string. It now looks up the trimmed `FiscalPeriodKey`, so a key with
  surrounding spaces resolves, as it already did through
  `FiscalPeriodFieldConverters`. The error text on a miss is unchanged.
- **Holdings-as-of query (D1).** The query still selects account and
  security names, only to label `AccountSnapshotLine.confirmFigures` errors
  and for its ORDER BY; nothing returned carries them. The ORDER BY uses
  `collate "C"` so the order matches the ordinal F# sort it replaced
  whatever the database collation. The dimension-value joins the plan
  describes were dropped: the line carries `securityId` (#014) and the
  converter reads dimensions from the Security, so nothing would consume
  those IDs.
- **Wealth history (D1)** fetches all Securities once per call, only when
  grouping by dimension, because Security has no fetch-by-ID-list.
- **Cash flow IDs (C3).** Projected invoices, bills to chase and posting
  transitions carry master and payment agreement IDs and account IDs in
  place of names; I read the plan's "agreement" fields that way for each
  row type. The 11a #094 route test (CashFlowRoutes REQ-CF-14.2 and
  REQ-CF-14.8) stays green unchanged but asserts no Instance's master
  agreement name, so the stale-name trap it was written for is guarded by
  the converter's design, not by a test. A follow-up test is worth adding.
- **Property account order (D2).** The account-code order of a Property's
  linked accounts now lives only in the converter; the Business-tier
  PropertyMaintenance summary sorts its own resolved list, and the route
  tests assert the order (seen failing).
- **Small D2 consequences.** The out-of-scope pre-ledger balance converter
  builds its `LedgerAccountReturn` inline now that the string-pair
  converter is gone. A Security not held that `recordActivity` names in its
  error is fetched by ID (loud if missing) instead of falling back to its
  Guid text.
- **Opens after a type move (D3).** `NetWorthHistoryWriter` needed
  `PositionsComponent` opened when `NetWorthDate` moved. Without it, its
  two matches compiled as variable patterns (warnings FS0049 and FS0026);
  the zero-warning build caught it. Any later type move should watch for
  the same thing.
- **Model (E1).** The `StageEntryComponent` box on the Application tiers
  view was widened to take the function `BaseStageEntry` realized; the
  layout is yours to redo.
- **Local `main` was stale** in this container. Every comparison here used
  `origin/main` (2dcfebe).
- No stale-cache cases (§5.4).
