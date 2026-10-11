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

### 5.3 Fail-then-pass evidence
(table: perturbation → what failed)

### 5.4 Name-resolution notes
(where a name came from the entity in hand, where from a cache; any
stale-cache test data changed under §2 rule 2)

### 5.5 ArchReviewer

### 5.6 Disagreements, deviations, skipped items, things for Dan
