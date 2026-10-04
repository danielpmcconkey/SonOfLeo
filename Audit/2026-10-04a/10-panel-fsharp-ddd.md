# fsharp-ddd-reviewer

## FSDDD-POS-1 — statement-delta
- **Location:** Src/Business.CrossDomainOrchestration/PositionsLedgerLinks.fs:15-49 (LinkingRecord, confirmNotLinkedElsewhere); used by InvestmentOrchestration.fs (createInvestmentAccount, updateInvestmentAccount) and RealEstateOrchestration.fs (createProperty, updateProperty); REQ-POS-4.8, REQ-POS-4.9 (amended 2026-10-04), REQ-POS-9.7; Architecture/SonOfLeo.archimate line 1804
- **Summary:** Investments and real estate are coupled in one helper that both peers' orchestrators sit on top of. The only thing that helper adds is a cross-peer check that the amended REQ-POS-4.9 says can never fire.
- **Resolution:** fix-code

Dan's statement and the archimate (line 1804) say investments and real estate are unordered peers: "Neither may use the other; anything that combines them (net worth, investable wealth) is orchestration." NetWorth.fs follows that, because it sits above both and combines them. PositionsLedgerLinks.confirmNotLinkedElsewhere is different. It fetches both InvestmentAccount.fetchByLedgerAccountId and Property.fetchByLedgerAssetAccountId, and its comment (line 28) says the two link kinds "share one pool". InvestmentOrchestration and RealEstateOrchestration both compile after it and call it through confirmInvestmentAccountLink and confirmPropertyAssetLink. So each peer's orchestrator depends on a module that knows the other peer's entity. That is a combining module placed below the peers rather than above them.

The cross-peer branches are also unreachable. The HEAD commit amended REQ-POS-4.9 to strike "or one of each", on the reasoning that an Investment Account links only to an 'Investment' subtype (4.8), a Property only to a 'FixedAsset' subtype (9.7), and an Account's subtype never changes (REQ-AC-4.22). The code still runs and pattern-matches the cross-peer arms: `| None, Some linked, _ ->` when an investment account links, and `| Some linked, _, _` when a property links (lines 42-47). The amendment proves these arms dead. What the helper still really checks is the same-kind uniqueness of each link (the DB backs it with the investment_account_ledger_account_id_key and property_ledger_asset_account_id_key unique constraints), and that check needs only the linking peer's own table.

A smaller point of the same kind: `LinkingRecord` carries `InvestmentAccountId option` / `PropertyId option`, and `confirmMortgageLink` takes `self: PropertyId option`. Every caller passes `Some` (creates pass the freshly minted id). The `None` case cannot occur, yet the `when Some(...) = self` guards are written to handle it.

**Action:** Split the check by peer. confirmInvestmentAccountLink checks only InvestmentAccount.fetchByLedgerAccountId, and confirmPropertyAssetLink checks only Property.fetchByLedgerAssetAccountId. Delete LinkingRecord and the 'share one pool' comment, and make `self` a plain id rather than an option. Keep the shared ledger-account lookup (fetchLedgerAccount, codeOf, describeType) as the only common part, since it touches the Ledger, not the other peer.

**Why:** DDD bounded contexts stay separate only when the dependency graph says so, not when the documentation does. A helper beneath both peers that imports both peers' entities is a back channel: the next agent adding a 'shared' rule will put it there, and the peers will drift into one context. The FP point: once a rule changes so that a match arm can no longer be reached, change the match so the dead case is gone, rather than keeping branches that the spec now proves impossible. The same holds for an `option` parameter that is never None: it asks every reader to reason about a case that cannot happen.

---

## FSDDD-POS-2 — enforcement-gap
- **Location:** Src/Business.FinancialServices.Positions/AccountSnapshotLine.fs:112-128 (reconstitute) vs :52-80 (checkFigures); InvestmentAccount.fs:140-162 (reconstitute); Property.fs:146-166 (reconstitute); REQ-SYS-2.1; REQ-POS-4.5, 4.6, 6.7, 6.8, 9.3; CompoundedLearnings/articles/coding/validation-layers.md layer 5
- **Summary:** Three Positions `reconstitute` functions skip legal-data-state rules that can be decided from the row alone. A stored snapshot line with a zero quantity, a negative market value or an out-of-tolerance product, or an account or property with no owners, is returned on read as if valid.
- **Resolution:** fix-code
- **Prior ruling:** FSDDD-3 (2026-10-03, overruled) covers entity `create` being infallible, with validation in orchestrators. This finding does not ask `create` to validate. It is about `reconstitute`, which validation-layers.md (rewritten under that same ruling's #200) explicitly makes responsible for row-local rules, and which REQ-SYS-2.1 names. Not the same point.

REQ-SYS-2.1 says every operation that reconstitutes an entity must enforce that entity's legal data-state rules. validation-layers.md layer 5 makes this concrete: "`reconstitute` validates ... every cross-field constraint that can be decided from the row alone."

- AccountSnapshotLine.reconstitute (112-128) builds Quantity, Price and Money and stops there. REQ-POS-6.7 (quantity > 0, market value >= 0, reported cost basis >= 0) and REQ-POS-6.8 (|quantity*price - market value| <= 0.05) depend only on columns of this one row. They are enforced only by `checkFigures`, which the write path alone calls (AccountSnapshotOrchestration.buildLine). The migration does not back them either: account_snapshot_line has no CHECK constraints, and its header comment says "every business rule (... the snapshot tolerance ...) is the application's."
- InvestmentAccount.reconstitute (140-162) receives owner_ids in the same row (a scalar subquery in fetchAny) and turns an absent value into `[]` without complaint. REQ-POS-4.5 (one or more owners) and REQ-POS-4.6 (a non-Taxable account has exactly one owner) are row-local but are not re-checked. It does re-check REQ-POS-4.7 through ActivityPeriod.create.
- Property.reconstitute (146-166) does the same with owner_ids and REQ-POS-9.3. It does re-check 9.4 through OwnedPeriod.create and 9.5 through PurchaseBasis.create.

The slice already shows the remedy. ContributionBasis, PurchaseBasis and ValuationValue are wrappers whose `create` holds the rule, so their reconstitutes (AccountSnapshotHeader.fs:97-100, Property.fs:153, Valuation.fs:99) re-enforce it on read at no extra cost. The snapshot line's market value and cost basis express the same kind of rule ("Money, not negative") as a free-standing check, so the read path loses it. Account.reconstitute in the Ledger re-runs confirmTypeAndSubtypeAreValid, so the Ledger already sets the precedent.

**Action:** Have AccountSnapshotLine.reconstitute run the same figure rules that checkFigures applies, with a read-side error that needs no account or security labels, or by restructuring checkFigures so both paths can call it. Have InvestmentAccount.reconstitute and Property.reconstitute reject an empty owner list, and have InvestmentAccount.reconstitute reject more than one owner on a non-Taxable account.

**Why:** A smart-constructor discipline protects only the paths that go through the constructor. When an invariant lives in a write-side check function instead of in a type, every other way in (here, reading from the database) bypasses it, and the system can hand out an entity its own spec calls illegal. Putting the rule in a wrapper type, or calling one shared validation from both `create`-side orchestration and `reconstitute`, makes 'parse, don't validate' hold in both directions. That is why ContributionBasis is enforced on read and MarketValue is not.

---

## FSDDD-POS-3 — contradiction
- **Location:** DbMigration/Scripts/202610041020-CreatePositionsTables.sql (positions.account_snapshot_line); Src/Business.FinancialServices.Positions/AccountSnapshotLine.fs:15-23; REQ-SYS-3.1; Specs/Definitions.md (Entity; Insert-only log records)
- **Summary:** Account snapshot lines are persisted rows with their own UUID that a user action inserts, but they carry no created_at or modified_at. Definitions.md classifies no such record as a non-entity, so REQ-SYS-3.1 applies and is not met.
- **Resolution:** dan-decides
- **Prior ruling:** DB-STAGE-1 (2026-08-20) exempted staged entries and lines from REQ-SYS-3.1 because Definitions.md explicitly says they are not entities. Snapshot lines have no such entry, so that ruling does not cover them.

Definitions.md's Entity litmus test is "does any user action ever insert or update a row? Yes → entity." Recording a snapshot inserts account_snapshot_line rows, and replacing one deletes and re-inserts them (AccountSnapshotOrchestration.recordOne). The only carve-outs Definitions.md grants are 'Staged entry/Staged line (not an entity)' and 'Insert-only log records', which it limits by name to classification.rule_match and ingestion.staged_entry_audit. A snapshot line fits neither: it is not a log, and it is deleted and replaced. REQ-SYS-3.1 nonetheless requires "Every persisted entity must carry a 'created at' and a 'modified at' timestamp", and both the table and the AccountSnapshotLine record lack them. By contrast, every other Positions table with its own unique_id, and ledger.journal_entry_line (the closest analogue: a line under a header), carries both. AccountSnapshotLineId is minted once (AccountSnapshotOrchestration.fs:129) and never used to address a line.

**Action:** Dan decides which model is intended. (a) The line is a member of the Snapshot aggregate, never addressed on its own, so Definitions.md classifies it (like staged lines) as not an entity, and the separate line identity may go too. Or (b) it is an entity, so add created_at/modified_at to the table and the type.

**Why:** In DDD terms the question is whether a snapshot line is an entity with its own lifecycle or a part of the snapshot aggregate whose identity and timestamps belong to the root. The code already treats it as the second: it is replaced wholesale with the header, never fetched by id, and has no update. The spec treats it as the first, by omission. If the glossary stays silent, the system-wide invariant and the model disagree, and the next audit or agent cannot tell which one is wrong.

---

## FSDDD-POS-4 — maintainability
- **Location:** Src/Business.FinancialServices.Positions/Valuation.fs:49-57 (valueOn); Src/Business.CrossDomainOrchestration/NetWorth.fs:144-153 (source); REQ-POS-10.4
- **Summary:** The REQ-POS-10.4 rule ('latest Valuation dated on or before the date, else the purchase basis') is coded twice: once to get the value, and again in NetWorth to work out where the value came from.
- **Resolution:** fix-code

Valuation.valueOn filters the valuations to this property and to dates on or before the as-of date, sorts them descending by date, takes the head, and falls back to the purchase basis. NetWorth.computeNetWorth calls valueOn for the value, then repeats the same filter (`Valuation.propertyId v = Property.propertyId property && Valuation.valuationDate v <= asOf`), sorts descending, takes the head, and maps to `ValuationDated`, falling back to `PurchaseBasisValue`. The two are separate encodings of one domain rule. If one is changed (for example to tie-break same-day records, or to exclude valuations after disposal) and the other is not, the report would show a value with the wrong label for where it came from. PropertyValueSource is declared in NetWorth.fs, but it references only LocalDate.

**Action:** Have one function return the value together with its source, for example `valueOn : LocalDate -> Property -> Valuation list -> Money * PropertyValueSource`. A small result type in the Positions tier carries the two cases, either `FromValuation of Valuation` or `FromPurchaseBasis of PurchaseBasis`. NetWorth then pattern-matches on it instead of recomputing.

**Why:** A function that returns a value but drops the reason for it forces any caller that needs the reason to re-derive it, so the rule now lives in two places. Returning a small sum type that carries both the answer and its provenance keeps one source of truth, and makes the 'value came from purchase basis' case something the type system tracks rather than a parallel computation.

---

## FSDDD-POS-5 — idiom
- **Location:** Src/Business.FinancialServices.Positions/InvestmentAccount.fs:20 (owners: PersonId list), :72, :287; Property.fs:26-27, :73-74, :296, :303; REQ-POS-4.5, 9.3, 9.8, 11.3, 11.6
- **Summary:** Owners and mortgage accounts are modelled as lists, and the code de-duplicates and sorts them each time they are built or saved. The spec describes them as sets ('the complete new set'), so `Set<_>` is the type that says what they are.
- **Resolution:** dan-decides

InvestmentAccount.create does `owners |> List.distinct |> List.sortBy PersonId.value`. Property.create does the same for owners and mortgageAccountIds. Then InvestmentAccount.update and Property.update call `List.distinct` again before persistOwners/persistMortgageAccounts. Persistence relies on a composite primary key (investment_account_owner_pkey, property_owner_pkey, property_mortgage_account_pkey) to reject duplicates. An infallible entity `create` that quietly rewrites its input is a sign that the parameter type admits values the entity does not mean: repeated members, and an order that carries no meaning. REQ-POS-11.3 and 11.6 say owners and mortgage accounts are given "as the complete new set". The boundary still needs a list (`PersonName list`), so that REQ-POS-4.5/9.3's 'same Person twice is rejected' can be detected and reported, and InvestmentOrchestration.resolveOwners and RealEstateOrchestration.resolveOwners already do that before converting to ids.

**Action:** Change the `owners` field and the matching `...FieldUpdates`/`ownersUpdate` payload to `Set<PersonId>`, and `mortgageAccountIds` to `Set<AccountId>`, everywhere past the orchestrator's duplicate check. Drop the List.distinct/List.sortBy calls in create and update. Keep `PersonName list` at the boundary so a repeated name can still be reported.

**Why:** Choosing the collection type is a modelling decision. A set states 'unique and unordered' in the type, so structural equality, de-duplication and a canonical order come free, and no function has to remember to normalise. When normalisation is repeated at several call sites, the type is too permissive, and the next call site that forgets will persist a duplicate and fail on the primary key with a DAL error instead of a domain one.

---

## FSDDD-POS-6 — idiom
- **Location:** Src/App.Utility/Result.fs:15-22 (convertListOfResultsToResultsList); used over effectful functions at Src/Business.CrossDomainOrchestration/AccountSnapshotOrchestration.fs:207-211 (recordSnapshots), :193 (line persists); InvestmentAccount.fs:418-425 (persistOwners); Property.fs:84-97 (persistChildren)
- **Summary:** `List.map f |> convertListOfResultsToResultsList` runs `f` on every element before looking at any result. When `f` writes to the database, processing does not stop at the first failure: every later snapshot, owner or line is still written, and only then is the first error returned.
- **Resolution:** dan-decides

convertListOfResultsToResultsList is a foldBack over an already-evaluated `Result list`. The `List.map` before it is eager, so every effect has run by then. recordSnapshots maps recordOne over all inputs. If input 1 fails validation, inputs 2..n still fetch, delete lines, replace or insert headers and insert lines, and then input 1's error is returned. Today the command-route transaction (CommandRoute.runCommandRouteAndAutoCompleteTransaction) rolls all of it back, so the operator sees the right error and nothing persists. The behaviour is correct only because the caller holds a transaction, not because the railway stops on failure. Src/README forbids hand-rolling folds over Result, so the remedy belongs in App.Utility.Result. Prior audits (2026-08-24a truthfulness) judged the leftmost-error behaviour reasonable. This finding is about evaluation order, not about which error is returned.

**Action:** Add a short-circuiting traversal to App.Utility.Result (for example `traverseResultSequentially : ('a -> Result<'b, IAppError>) -> 'a list -> Result<'b list, IAppError>`, a fold that binds element by element) and use it where the element function performs I/O. Keep convertListOfResultsToResultsList for lists of results that are already pure.

**Why:** Result's railway promises that nothing after a failure runs. Mapping first and sequencing afterwards is the applicative way to traverse, which is right for independent pure validations. For effectful steps the monadic way is right, binding each step before starting the next. Mixing the two means the code keeps working only while every caller wraps it in a transaction. A future caller with a non-transactional effect, such as a file write, an audit log append or a cache, would see side effects from work that was supposed to have been abandoned.

---

## FSDDD-POS-7 — stale-reference
- **Location:** Src/README.md 'Projects' section (lines 11-17)
- **Summary:** Src/README.md's project inventory does not list Business.FinancialServices.Positions. It still names `Business.FinancialServices.{Ledger,DataIngestion,CashFlow,Classification}`.
- **Resolution:** fix-spec

README.md calls itself the inventory agents must read before writing code ("Read this before writing a helper"), and its Projects list is how an agent learns which containers exist. The Positions project exists and is referenced by Business.CrossDomainOrchestration.fsproj line 18, and the archimate (line 1805) places it as Ledger < DataIngestion < CashFlow < Positions < Classification. The README list omits it, so the list order also no longer matches the rung order.

**Action:** Change the Business line to `Business.FinancialServices.{Ledger,DataIngestion,CashFlow,Positions,Classification}`.

**Why:** Agents write the code, and the README is how they find out where things go. If the inventory leaves out a bounded context, a new positions type may land in a sibling project or in orchestration, which is exactly the placement drift the type-placement article warns about.

---

