# code-truthfulness-dal

## DAL-CACHE-TX-1 — other
- **Location:** Src/App.DataAccessLayer/LookupCache.fs:25-41 (Cache.fetch), 54-65 (fetchAll), 76 and 81 (`fun _ -> fetchAll ...`); hit by Src/Ui.InterfaceBridge/Routes/CashFlowRoutes.fs:72-90 (updateAgreement) via Src/Ui.InterfaceBridge/BoundaryConverters/CashFlowFieldConverters.fs:210-218 and :127
- **Summary:** A lookup cache's full load ignores the caller's transaction and reads only committed rows on a separate connection. A route that renames a master or payment agreement and then converts IDs back to names in the same transaction therefore returns the old name.
- **Resolution:** fix-code

`Cache.fetch context key` takes the caller's DbTransaction, but only `loadOne` uses it. `loadAll` is built as `fun _ -> fetchAll table keyColumn`. `fetchAll` calls `createDbTransaction()`, which takes a new pooled connection and transaction, and reads `select unique_id, {keyColumn} from {table}` there. Under PostgreSQL's default READ COMMITTED isolation, that read cannot see the caller's uncommitted writes. `loadOne` (which does use the caller's transaction) runs only when the key is missing from the full map. So a key that exists in committed state but was renamed in the current transaction resolves to its committed value, which is stale.

Concrete path: `CashFlowRoutes.updateAgreement` runs in one transaction (`runCommandRouteAndAutoCompleteTransaction`). (1) It resolves the agreement through `MasterAgreement.nameToId`. (2) `AgreementOrchestration.updateAgreement` (AgreementOrchestration.fs:432) applies `agreementNameUpdate = SetTo "B"` inside that transaction. (3) `convert [Agreement] to [AgreementReturn]` maps every payment agreement through `convert [PaymentAgreement] to [PaymentAgreementReturn]`, which calls `convert [MasterAgreementId] to [AgreementNameString]` (CashFlowFieldConverters.fs:215-218), that is, `MasterAgreement.idToName.fetch`. The idToName cache is empty or was loaded before the rename. Either way, `loadAll` reads the committed row on its own connection, finds the ID mapped to the old name "A", and returns it without consulting `loadOne`. The payload therefore carries `masterAgreement.agreementName = "B"`, taken from the entity, next to `paymentAgreements[i].masterAgreementName = "A"`, taken from the cache. The same thing happens to an invoice's `paymentAgreementName` (CashFlowFieldConverters.fs:127, `PaymentAgreement.idToName`) when the same update renames a payment agreement (REQ-CF-14.8).

The LookupCache header comment covers staleness across the cache's lifetime ("no invalidation by design"). It does not say the cache reads outside the caller's transaction, and the `fetch context` signature suggests the opposite. Only the new-key case is handled, because a missing key falls through to `loadOne`. I found no test that renames an agreement through the route and asserts the payment-agreement or invoice names in the return; the rename tests found (RevisedRequirementsCashFlow.fs:262, MasterAgreementDataStates.fs:317) don't check this field.

**Action:** Have the full load run on the caller's transaction when it has one: pass `context` into `loadAll` and call `executeReaderQuery context ...`, opening its own transaction only for a NoTransaction context. Separately, decide whether `fetch` should bypass or refresh the cache for a key the current transaction may have written. Add a route test that renames a master agreement that has payment agreements and asserts each returned `masterAgreementName` is the new name.

**Why:** A command's return payload is the operator's confirmation of what was written (REQ-NGUI-3.6). If it disagrees with itself (new name on the master agreement, old name on its legs), it reports a state that never existed in the database. Tests miss it because they check the entity rather than the converted payload.

---

## STALE-DAL-ART-1 — stale-reference
- **Location:** CompoundedLearnings/articles/architecture/dal-errors-are-backstops.md:52-53 ("What works"); Src/App.DataAccessLayer/ExecuteReader.fs:14-23
- **Summary:** The DAL-errors-are-backstops learning still says DalNoOp is raised for an "ExactlyOne or OneOrMany expectation", but OneOrMany was deleted from AcceptableExpectedRows.
- **Resolution:** fix-spec

ExecuteReader.fs:14-16 now defines `AcceptableExpectedRows = ExactlyOne | AnyQuantityIsAcceptable`, and confirmNumRows (lines 18-23) raises `DalNoOp` only for `ExactlyOne when numRows = 0`. `Zero` and `OneOrMany` were deleted under audit 2026-10-04a disposition #002 (applied in 2dbe95c). The article's 'What works' section still reads: "The DAL reports 'no rows came back' as its own case, `DalNoOp`, for an `ExactlyOne` or `OneOrMany` expectation." A grep for `OneOrMany` outside Audit/ and HobsonsNotes/ finds only this article. CompoundedLearnings ranks above code, so an agent following the article could write `OneOrMany` and fail to compile, or reason about a DalNoOp path that no longer exists.

**Action:** Edit dal-errors-are-backstops.md line 53 to say DalNoOp is raised for an `ExactlyOne` expectation only.

**Why:** CompoundedLearnings is the guidance agents code from. Naming a deleted DU case re-teaches dead surface that disposition #002 deliberately removed.

---

