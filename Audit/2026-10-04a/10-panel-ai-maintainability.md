# agent-maintainability-auditor

## GAAP-NW-1 — missing-requirement
- **Location:** Specs/Behavioral/Reporting.md REQ-RPT-8.2, REQ-RPT-8.5; Specs/Behavioral/Positions.md REQ-POS-4.8, REQ-POS-8.1; Src/Business.CrossDomainOrchestration/NetWorth.fs (linkedAssetIds)
- **Summary:** Net worth drops a linked ledger account's balance whenever its Investment Account contributes nothing on the date, and counts the same asset twice when an Investment Account or Property is left unlinked. In both cases nothing in the report flags it.
- **Resolution:** dan-decides

RPT-8.2 excludes every Asset account linked to an Investment Account or Property and adds market value only from holdings as of the date (POS-8.1). POS-8.1 excludes an account that is active but has no snapshot on or before the date, and any account whose active end has passed. NetWorth.fs builds linkedAssetIds from every Investment Account and Property, whether or not it contributes on the as-of date. Case (a): Dan opens a brokerage and posts the funding transfer to the linked F-1xxx Investment account. If the first snapshot comes a week later, or snapshot history starts after the ledger's first fiscal period, net worth for any date in that gap leaves out both the cost balance and the market value. The account is not listed anywhere in the RPT-8.5 output, so the reader cannot see the hole. Case (b): REQ-POS-4.8 makes the link optional. An Asset/Investment ledger account whose Investment Account was never linked is counted at cost under RPT-8.2's first bullet, and its market value is counted again from holdings. RPT-8.2's own promise, 'so no amount is counted twice', does not hold for either case. Tests (NetWorth.fs) cover only linked accounts that have a covering snapshot.

**Action:** Decide the rule for a linked account with no covering snapshot (fail, fall back to the ledger balance, or list it as 'no snapshot') and for an Investment Account or Property with no ledger link while an Investment/FixedAsset-subtype ledger account exists. Then amend RPT-8.2/8.5 so the report shows the case rather than silently dropping or double-counting.

**Why:** Net worth is the headline figure the retirement-planning engine and Dan will trust. A silent understatement or double count passes every current test and looks plausible on the page.

---

## POS-CB-1 — missing-requirement
- **Location:** Specs/Behavioral/Positions.md REQ-POS-6.4, REQ-POS-5.3; Src/Business.CrossDomainOrchestration/InvestmentOrchestration.fs updateInvestmentAccount
- **Summary:** Changing an Investment Account's tax treatment away from 'Roth' leaves its stored snapshots carrying a contribution basis, which REQ-POS-6.4 forbids on any non-Roth account.
- **Resolution:** fix-spec

REQ-POS-6.4 is a data-state rule: a contribution basis requires the account's tax treatment to be 'Roth'. REQ-POS-5.3 guards a tax-treatment change only against Holdings that would break REQ-POS-5.2. No requirement guards the change against existing snapshots. The code matches: updateInvestmentAccount calls confirmHoldingsAllowTaxTreatment and never inspects snapshot contribution bases. After a Roth account is re-classified as TaxDeferred (for example, correcting a mis-entered treatment), HoldingsAsOf and net worth (RPT-8.5) report a contribution basis on a TaxDeferred account. That is an illegal state under REQ-SYS-2.1. Re-recording any of those dates then fails, because the existing snapshot's own basis can no longer be re-sent.

**Action:** Add a requirement parallel to REQ-POS-5.3. It should reject a tax-treatment change off 'Roth' while any snapshot of the account carries a contribution basis, and name the earliest and latest offending dates. Or state explicitly what happens to those bases.

**Why:** The contribution basis exists to separate tax-free withdrawable money from earnings. A basis left on a non-Roth account is wrong tax data that a later planning engine will read as fact.

---

## ENF-RECON-1 — enforcement-gap
- **Location:** Src/Business.FinancialServices.Positions/AccountSnapshotLine.fs (create, checkFigures, reconstitute); Src/Business.FinancialServices.Positions/InvestmentAccount.fs reconstitute; Src/Business.FinancialServices.Positions/Property.fs reconstitute; Src/Business.CrossDomainOrchestration/HoldingsAsOf.fs reconstituteLine; REQ-SYS-2.1, REQ-POS-6.7, 6.8, 4.5, 4.6, 9.3
- **Summary:** Positions' read paths skip the row-local rules that CompoundedLearnings validation-layers (layer 5) requires reconstitute to re-check, and nothing in the database backstops them.
- **Resolution:** fix-code

validation-layers.md layer 5 says reconstitute 're-checks every cross-field constraint that can be decided from the row alone'. Account.reconstitute does this (confirmTypeAndSubtypeAreValid, confirmParentAndChildAreDistinct). The Positions read paths do not. (1) AccountSnapshotLine.reconstitute builds a line without checking quantity > 0, market value >= 0, cost basis >= 0, or the 0.05 tolerance (REQ-POS-6.7/6.8), all decidable from the row. Those rules exist only in the separate, optional AccountSnapshotLine.checkFigures, called from one place (AccountSnapshotOrchestration.buildLine). (2) HoldingsAsOf.reconstituteLine, the read every report and the planning extract consume, re-parses the same columns with the same omissions. (3) InvestmentAccount.reconstitute does not check owners non-empty, or exactly one owner for non-Taxable accounts (REQ-POS-4.5/4.6), though owner_ids arrives in the same row. Property.reconstitute does not check owners non-empty (REQ-POS-9.3). The migration header 202610041020-CreatePositionsTables.sql states 'every business rule ... is the application's', so the database offers no backstop either. Snapshot rules are tested only by calling checkFigures directly (Tests.Isolated/Model/Positions/Investments.fs). The one wiring check is the tolerance case inside a REQ-POS-7.1 atomicity route test, so zero quantity, negative market value and negative cost basis have no end-to-end test. A future snapshot writer, such as the in-repo import domain in Dan's vision, could call AccountSnapshotLine.create and persist without checkFigures. It would compile, every current test would stay green, and the bad lines would flow into net worth unnoticed.

**Action:** Have AccountSnapshotLine.reconstitute, InvestmentAccount.reconstitute and Property.reconstitute re-check their row-local rules and return typed errors. Make HoldingsAsOf reuse the same line checks. Add orchestration-level tests proving recordSnapshots rejects a zero-quantity, a negative-market-value and a negative-cost-basis line.

**Why:** Layer 5 exists so an illegal row cannot reach a report unnoticed. Here the only guard is one call site, so the next agent who writes snapshots through a new path inherits no protection.

---

## TEST-RPT85-1 — test-gap
- **Location:** Specs/Behavioral/Reporting.md REQ-RPT-8.5 (amended 2026-10-04, commit da96f73); Tests/Tests.Integrated/CrossDomainOrchestration/NetWorth.fs; Tests/Tests.Helpers/PositionsFixture.fs
- **Summary:** The HEAD amendment to REQ-RPT-8.5, which lists the mortgage of a no-longer-owned Property among the liabilities, has no test that exercises it.
- **Resolution:** fix-test

da96f73 changed REQ-RPT-8.5 so that a mortgage of a Property not owned on the date is listed with the other liabilities. It was a spec-only commit: NetWorth.fs already filtered liabilities by ownedMortgageIds. The fixture's only disposed property, 7 Former Example Road, is created with no mortgage accounts (PositionsFixture.fs, line 320: '200000.00M None []'). The one test whose date precedes the residence's acquisition (REQ-RPT-8.4 'no primary residence is owned') asserts only properties and investable wealth, not the liability rows. Suppose a later edit filters liabilities by every Property's mortgage instead of owned ones, the natural reading of 'listed under its Property'. A disposed property's outstanding mortgage would then appear neither under a Property nor among the liabilities, and net worth would be overstated by its balance. The traceability gate still shows REQ-RPT-8.5 as tested.

**Action:** Give the fixture's disposed property (or a not-yet-acquired one) a mortgage account with a non-zero as-of balance. Add a REQ-RPT-8.5 test asserting it appears in liabilityAccounts and in totalLiabilities.

**Why:** An amended clause with no test is exactly the case where an agent's plausible 'cleanup' changes the books and every gate stays green.

---

## SD-CRUD-1 — statement-delta
- **Location:** Specs/Behavioral/Positions.md §11 (REQ-POS-11.1–11.9); Src/Ui.InterfaceBridge/Routes/PositionsRoutes.fs route table
- **Summary:** Dan's statement says the slice implements 'all the CRUD operations'. In fact there is no delete for Properties, Investment Accounts, Securities, Holdings or Dimension Values, so a mistaken Property counts in net worth permanently.
- **Resolution:** dan-decides

The route table (PositionsRoutes.fs lines 305-455) offers Delete only for AccountSnapshot and Valuation. Person has no delete by design (Person.md 'Deletion'). Positions.md says nothing on deleting the other five records, and every FK is ON DELETE RESTRICT. Concrete harm: a Rental entered twice (REQ-POS-9.6 blocks only duplicate primary residences) is owned from its acquisition date and counted at purchase basis in every later net worth (RPT-8.2). The only remedy is to fake a disposal date, which falsifies history. A Holding or Investment Account created against the wrong account likewise stays in every listing permanently.

**Action:** Dan to confirm whether deletion of these records was deliberately left out. If so, say so in Positions.md as Person.md does, and state the remedy for a mis-entered Property. If not, specify the delete operations and their guards.

**Why:** Dan's mental model of 'all CRUD' assumes a mistake can be undone. For Property it cannot, and the mistake lands directly in net worth.

---

## SYS31-POS-1 — contradiction
- **Location:** Specs/Behavioral/SystemWide.md REQ-SYS-3.1; Specs/Definitions.md (Entity, Insert-only log records); DbMigration/Scripts/202610041020-CreatePositionsTables.sql (account_snapshot_line, investment_account_owner, property_owner, property_mortgage_account)
- **Summary:** Four user-written Positions tables have no created_at/modified_at, and no Definitions exemption covers them the way staged lines and insert-only logs are covered.
- **Resolution:** dan-decides

Under Definitions 'Entity', a record type the system creates or mutates on behalf of the user is an entity, and REQ-SYS-3.1 says every persisted entity carries both timestamps. account_snapshot_line rows are inserted and replaced by REQ-POS-7.1/7.2. The owner and mortgage-link rows are deleted and re-inserted on every owners or mortgage update (InvestmentAccount.update; Property.update). None of the four tables has timestamp columns. Precedent cuts against the omission: ledger.journal_entry_line and cashflow.payment_agreement_link (a link table) both carry created_at. The only exemptions in Definitions are Staged entry, Staged line and Insert-only log records, the last added 2026-10-03 precisely to legitimise a timestamp gap. REQ-SYS-3.1 is waived from testing, so no test can catch this.

**Action:** Either add a Definitions entry (for example, parts of an aggregate replaced wholesale with their parent) that exempts these tables from REQ-SYS-3.1, or add the timestamp columns.

**Why:** When a term's scope and the schema disagree, the next agent cannot tell which tables owe timestamps and will pattern-match either way.

---

## SYS61-DV-1 — contradiction
- **Location:** Src/Business.CrossDomainOrchestration/InvestmentOrchestration.fs renameDimensionValue / confirmDimensionValueNameFree; REQ-SYS-6.1; REQ-POS-11.1
- **Summary:** Renaming a Dimension Value to its own current name is rejected as 'already exists', contradicting REQ-SYS-6.1's rule that re-sending a field's stored value counts as named and the update succeeds.
- **Resolution:** fix-code

renameDimensionValue calls confirmDimensionValueNameFree context dimension newName with no self-exclusion. When newName equals the current name, fetchByDimensionAndName finds the record itself and returns PositionsDimensionValueAlreadyExists. The sibling checks (confirmSecurityNameFree, confirmInvestmentAccountNameFree, confirmPropertyNameFree, Person confirmNameFree) all take a 'self' argument for exactly this reason. The amended REQ-SYS-6.1 bullet says: 'A field set to the value it already holds counts as named, and the update succeeds. This is the system default for every update operation.' No REQ-POS-11.1 test covers a same-name rename.

**Action:** Pass the Dimension Value's own ID to the free-name check (as the other entities do) and add a REQ-POS-11.1/REQ-SYS-6.1 test for a same-name rename.

**Why:** Inconsistency between sibling operations is what agents copy from. The next maintenance operation could be modelled on this one.

---

## SD-JSON-1 — statement-delta
- **Location:** Src/App.Utility/Json.fs missingFieldMessage
- **Summary:** fromJson names the first record field absent from the payload, which can be an optional field the payload legitimately omitted, so it does not always name the field the payload 'actually left out'.
- **Resolution:** fix-code

missingFieldMessage returns the first field in GetRecordFields that is not among the payload's property names, without excluding option-typed fields. The doc comment says FSharp.SystemTextJson treats an unset option field as acceptable but counts it as 'not set' when naming the missing field. Suppose a payload omits an option field entirely (rather than sending null) and also omits a required field declared after it. The library names the option field, and this rewrite names the same option field too. The fix covers only the null-sent case exercised by the REQ-POS-9.5 route test, whose payload is produced by Json.toJson and so always sends None as null. Payloads that come from external parsers, which Dan's statement says live outside the repo, are the ones likely to omit optional fields.

**Action:** Skip option/voption fields when choosing the field to name. Add a route or isolated test in which an optional field is omitted (not null) alongside a missing required field.

**Why:** Dan's statement presents this as fixed for every route. An error naming the wrong field sends an operator or parser author after the wrong column.

---

## GUARD-PEER-1 — enforcement-gap
- **Location:** Architecture/SonOfLeo.archimate (principle: investments and real estate are unordered peers); Src/Business.FinancialServices.Positions/Business.FinancialServices.Positions.fsproj compile order; Src/Business.CrossDomainOrchestration fsproj (InvestmentOrchestration before RealEstateOrchestration)
- **Summary:** Nothing mechanical enforces the rule Dan states as architecture, that investments and real estate are peers that may not reference each other. Compile order lets real estate reference investments.
- **Resolution:** fix-code

In the Positions fsproj, Property.fs and Valuation.fs compile after Security/InvestmentAccount/Holding/AccountSnapshot*. In the orchestration fsproj, RealEstateOrchestration compiles after InvestmentOrchestration and AccountSnapshotOrchestration. So `open Business.FinancialServices.Positions.Security` in Property.fs, or `open ...InvestmentOrchestration` in RealEstateOrchestration.fs, builds cleanly. Src/README.md concedes that 'nothing mechanical catches an order that compiles but breaks the archimate principle', and check-compile-order.sh checks membership only. Today no such reference exists (verified by the opens in every Positions file). The rule rests on agents reading the archimate documentation.

**Action:** Add a Checks/ script that fails when a real-estate file (Property.fs, Valuation.fs, RealEstateOrchestration.fs) opens or names an investment module, or the reverse. PositionsLedgerLinks, HoldingsAsOf and NetWorth would be allowlisted as the sanctioned combining points.

**Why:** Specs/README's own rule is 'prefer the executable form of a rule'. A paragraph in the archimate model does not stop an agent writing a convenience helper.

---

## TRACE-GATE-1 — enforcement-gap
- **Location:** Skills/SonOfLeoRequirementsAudit/traceability-audit.sh (test_refs scan); Specs/README.md 'Linkage rules'
- **Summary:** The traceability gate counts any REQ ID appearing anywhere in a tracked test file (comments, helper strings) as a test citation, though the documented linkage is 'test names begin with the requirement IDs they verify'.
- **Resolution:** fix-code

traceability-audit.sh greps ID_RE over every git-tracked Tests/*.fs line. Section comments already cite IDs: '// REQ-AC-3.13 — balances' (AccountCreateActivityBalance.fs line 633), '(* REQ-SYS-1.4 cases ...' (Tests.Helpers/LiteralSearch.fs), and a failure-message string in ConnectionLeakGuard.fs line 21. Today every ID found this way also appears in a backtick test name (verified by set comparison at HEAD), so nothing is currently mis-reported. But an agent who deletes or renames the last test for a requirement and leaves a section comment behind gets Invariant 2 'clean' for an untested requirement.

**Action:** Restrict the scan to IDs inside test method names (the ``...`` identifier on [<Fact>]/[<Theory>] members), or at least to lines that are not comments or strings.

**Why:** The gate is the one mechanical guarantee that every requirement is tested. It should measure the linkage the README defines.

---

## STALE-POS-1 — stale-reference
- **Location:** Src/README.md 'Projects' and inventory; CompoundedLearnings/articles/gaap-domain/numeric-type-taxonomy.md; README.md slice loop step 13 paragraph
- **Summary:** Three agent-facing documents contradict the slice as built: Src/README omits the Positions project, the numeric taxonomy says Price x Quantity = Money, and README says check-traceability exits 0 off main.
- **Resolution:** fix-spec

(1) Src/README.md lists `Business.FinancialServices.{Ledger,DataIngestion,CashFlow,Classification}`, with no Positions. Its inventory also lacks Quantity/Price and the shared PersonOrchestration.fetchPersonByName, so an agent reading 'what already exists' will not find them. (2) numeric-type-taxonomy.md states 'Price x Quantity = Money' and places Price/Quantity in 'portfolio, obligations' domains. REQ-QP-3.5 (higher authority) says the product 'is not Money', and the domain is Positions. (3) README.md says `Checks/check-traceability.sh` 'exits 0 on any branch that is not main'. The script exits 2 (SKIP), as Specs/README.md correctly says.

**Action:** Add Positions, Quantity, Price and the Person lookup helper to Src/README.md. Rewrite the taxonomy's arithmetic line to match REQ-QP-3.5 and the domain names. Correct README's exit-code sentence.

**Why:** Agents treat these files as the map. A learning that says the product is Money invites the next agent to wrap Price.multiplyQuantity in Money.fromDecimal, which would reject legitimate sub-cent products.

---

## AMB-RPT92-1 — ambiguity
- **Location:** Specs/Behavioral/Reporting.md REQ-RPT-9.2, REQ-RPT-9.5; Src/Business.CrossDomainOrchestration/InvestmentWealthHistory.fs groupedValues; Src/Ui.InterfaceBridge/ReportWriters/InvestmentWealthHistoryWriter.fs
- **Summary:** REQ-RPT-9.2 does not say whether a group whose account's latest snapshot is empty appears at a point with a zero total or not at all. The slice agent raised this, and da96f73 left it unruled.
- **Resolution:** dan-decides

The slice report (845d88b, §8.6, 'Note, not a finding') records that an account whose latest snapshot has no lines contributes no group entry, rather than a zero entry. The rendered table therefore shows a blank cell (WealthHistoryWriter: Option.defaultValue ""), not 0.00, for that group at that point. REQ-RPT-9.4 says only that a point with no holdings at all has a zero total. Two reasonable implementers would diverge on the per-group case (a fully liquidated account shown as 0.00 versus blank), and the difference is visible in the report Dan reads.

**Action:** Rule whether a group present among included accounts but with no lines reports 0.00 or is absent, and amend REQ-RPT-9.2 accordingly.

**Why:** A blank cell and a zero mean different things to a reader judging whether money left an account or whether the data is missing.

---

