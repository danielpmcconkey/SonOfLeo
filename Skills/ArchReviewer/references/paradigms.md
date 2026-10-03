# SonOfLeo Architecture Paradigms

Extracted from the full `Src/` at commit `005bef5` (2026-09-10); revised 2026-09-27 for the
re-tiered layout (`Src/App.*`, `Src/Business.*`, `Src/Ui.*`). This is what right looks like. A diff that deviates from these
patterns is a finding.

---

## 1. The Entity Shape

Every entity is a private record with a companion module. No exceptions.

```fsharp
type Entity = private {
    entityId: EntityId
    field1: DomainType1
    field2: DomainType2
    createdAt: Instant
    modifiedAt: Instant
}

let entityId e = e.entityId
let field1 e = e.field1

let create (entityId: EntityId) (field1: DomainType1) ... (createdAt: Instant) (modifiedAt: Instant) : Entity =
    { entityId = entityId; field1 = field1; ... }
```

- **`private`** record. Always. No public construction.
- **Accessors** are standalone `let` functions, one per field.
- **`create` is infallible** — returns the type directly, never `Result`. Takes already-validated
  domain types, never raw strings.
- **Fields are domain types**, never raw primitives. `AccountCode`, not `string`. `Money`, not
  `decimal`. `Instant`, not `DateTime`.

**Joined non-persisted fields:** Some entities carry read-convenience fields from SQL JOINs
(e.g., `Instance.masterAgreementName`). `create` accepts them, `persist` ignores them,
`reconstitute` populates them from the query.

---

## 2. The CRUD Function Inventory

Every persisted entity has these functions, named exactly this way:

| Function | Returns | Purpose |
|---|---|---|
| `persist` | `Result<unit, IAppError>` | INSERT. Never returns the entity. |
| `reconstitute` | `Result<Entity, IAppError>` | Private. Builds from a raw DB-row tuple via `create`. |
| `mapRawForDbRead` | `rawTuple` | Private. Maps `RowReader` to the tuple `reconstitute` expects. |
| `query` | `Result<Entity list, IAppError>` | The entity's basic SELECT; its fetch functions route through it. Usually private, but may be called by functions in the same business domain (`StageEntryHeader.query` is public for deduplication). Not the only SELECT: a cross-domain fetcher may issue its own (`CashFlowCompositeFetcher`). |
| `fetchById` | `Result<Entity, IAppError>` | Public. `query` + `ExactlyOne`. |
| `fetchByX` | `Result<Entity list, IAppError>` | Public. `query` + predicate. Named after the filter field. |
| `fetchByXIdList` | `Result<Entity list, IAppError>` | Public. Bulk fetch by ID list. |
| `update` | `Result<Entity, IAppError>` | Takes `FieldUpdates`. Builds SET dynamically. Re-fetches and returns. |

- Table aliases are globally unique (`a` for account, `je` for journal_entry, `ma` for
  master_agreement, `inv` for invoice).
- Parameters use typed `QueryParameterValue` cases — never string interpolation of values.
- `update` rejects no-ops: if all fields are `NoChange`, return `UpdateNoOp` error.
- `update` always re-fetches and returns the entity after writing.

---

## 3. Domain Primitives

Every "small value" gets its own wrapper type. Three shapes:

**ID wrappers (Guid-based):**
```fsharp
type EntityId = private EntityId of Guid
module EntityId =
    let create () = EntityId(Guid.NewGuid())
    let fromGuid g = EntityId g
    let value (EntityId g) = g
```
Always this triple: `create`, `fromGuid`, `value`.

**Bounded string wrappers:**
```fsharp
type AccountCode = private AccountCode of string
module AccountCode =
    let maxLength = 10
    let value (AccountCode ac) = ac
    let create (raw: string) : Result<AccountCode, IAppError> =
        // trim, check empty, check length
```
Fallible — returns `Result`. Dedicated error cases per type in the domain's error DU.

**DU enums:**
```fsharp
type AccountType = Asset | Liability | Equity | Revenue | Expense
module AccountType =
    let fromString (s: string) : Result<AccountType, IAppError> = ...
    let toString (t: AccountType) : string = ...
```
Always `fromString`/`toString`. Never .NET enum types.

**Semantic wrappers (no validation):**
```fsharp
type DebitAccount = DebitAccount of AccountId
type CreditAccount = CreditAccount of AccountId
```
Tags an already-validated type to prevent mixing.

---

## 4. Component File Convention

One `*Component.fs` per domain slice, holding all shared primitives:
- `AccountComponent.fs`, `JournalEntryComponent.fs`, `StageEntryComponent.fs`,
  `CashFlowComponent.fs`, `ClassificationComponent.fs`

**What goes in:** ID wrappers, bounded strings, DU enums, semantic wrappers, small composite
value types (`InvoiceLifeCycleState`, `MoneySearchPattern`).

**What does not go in:** entity types, persistence, composite types, business logic beyond
constructors.

Within its domain, the component file compiles before the entity files. An entity file may
reference value types from its own domain and lower ones, never a higher domain — see "Domains
Build Upward" in `Architecture/SonOfLeo.archimate`.

---

## 5. Composite Type Shape

Composites live in `Src/Business.CrossDomainOrchestration`, defined as private records next to their orchestration
functions:

```fsharp
type InstanceComposite = private {
    instance: Instance.Instance
    invoiceComposites: InvoiceComposite list
}
```

- Always `private` record with accessors.
- Defined at the top of the orchestration file — not in a separate types file.
- Composites can nest (`InstanceComposite` → `InvoiceComposite list`).
- Only composite types are allowed in orchestration files. Simple types belong in Component files.

---

## 6. constructNewAndPersist Shape

```
result {
    do! confirmPreconditions ...    // validate
    let id = EntityId.create()      // generate ID
    let now = context |> Context.getInitiationInstant
    let entity = Entity.create id ...fields... now now    // infallible
    do! entity |> Entity.persist context                  // write
    return entity
}
```

For composites: create parent → create children using parent's ID → validate the composite →
return. Children are persisted BEFORE composite validation (validation needs all children to
exist).

**Child collections are passed as tuple lists, not named types.** The pattern deliberately
avoids a third type between the composite and the interface contract. Children arrive as
tuples of domain primitives:

```fsharp
let constructNewAndPersist
    (context: Context.Context)
    (description: JournalEntryDescription)
    (source: JournalEntrySource option)
    (entryDate: EntryDate)
    (lines: (AccountId * Money * JournalEntryLineType * JournalEntryLineMemo option) list)
    (references: (JournalRefFinancialInstitution * JournalExternalReferenceText) list)
    (comments: (JournalEntryHeaderId option * CommentText) list)
    : Result<JournalEntry, IAppError> =
```

Each tuple element is an already-validated domain primitive. The function unpacks them to call
entity `create` functions. This keeps the type count down — there is no
`JournalEntryLineCreateInput` record type.

---

## 7. Validation Gauntlet

`confirm*` functions (not `validate*` — retired). Each returns `Result<unit, IAppError>`.
Composed in a `result { }` block of `do!` bindings.

**Ordering:** cohesion checks first (does this child belong to this parent?), then structural
checks (amounts balance, minimum counts), then each child composite.

```fsharp
let confirmInstanceComposite context instanceComposite =
    result {
        do! invoices |> List.map (confirmInvoiceIsUnderInstance instanceId) |> convert...
        do! confirmDiamond context instance invoices
        do! confirmFulfilledInstanceHasInvoices instance invoices
        do! confirmFulfilledInstanceInvoicesAreFullyPaid instance invoices
        do! invoiceComposites |> List.map (confirmInvoiceComposite context) |> convert...
    }
```

---

## 8. Update Door Shape

Persist-then-validate. The caller doesn't send the full composite — changes are applied
individually, then the whole thing is re-fetched and validated.

```
1. Confirm there's actually an update (reject NoOp)
2. Confirm cohesion (children belong to this parent)
3. Apply individual field updates (each writes to DB)
4. Re-fetch the full composite from DB
5. Run the validation gauntlet
6. Return the composite
```

Inner mutations (create an invoice on an instance) go through a "door" function that creates
the inner entity, re-validates the parent composite, and returns the parent.

---

## 9. Composite Fetch: compileFromSubLists

Composites are assembled by fetching each level separately, then pairing by parent ID:

```fsharp
let compileFromSubLists headers lines references comments =
    headers |> List.map (fun header ->
        let headerId = header |> JournalEntryHeader.journalEntryHeaderId
        let linesForHeader = lines |> List.filter (fun x ->
            x |> JournalEntryLine.journalEntryHeaderId = headerId)
        { header = header; jeLines = linesForHeader; ... })
```

**Never positional matching.** Always by business key (parent ID). Where lines need to be
paired by content (e.g., stage lines to journal lines), match on account + lineType + amount
with a depleting pool.

---

## 10. Error Model

`IAppError` (`Src/App.Utility/IAppError.fs`) is the interface; each container defines its own
error DU implementing it (`UtilityError`, `DalError`, `BizGeneralError`, `BizFinServError`,
`LedgerError`, `DataIngestionError`, `CashFlowError`, `BridgeError`, `OperatorCliError`,
`ReportCliError`). Functions return `Result<'T, IAppError>`. No exceptions, no string errors.

- Cases carry diagnostic data as tuple elements, pre-computed.
- Each DU's `ToMessage` is the only place its error strings live.
- DAL errors are generic; orchestration/bridge layers reinterpret at their altitude, matching
  a specific case with the `AsError` active pattern (or `DalError.whenNoRows`).

```fsharp
accountId |> Account.fetchById context
|> whenNoRows (JournalEntryLineAccountDoesntExist(accountId |> AccountId.value))

match result with
| Error (AsError (LedgerError.AccountIdDoesntMatch uuid)) ->
    Error (CashFlowError.CashflowPaymentAgreementDebitAccountInvalid uuid)
| other -> other
```

---

## 11. Transaction Ownership

- Routes own transactions via `Context.create`.
- Business-tier functions receive context — they never create transactions.
- `AuditEnvelope` stamps a single `Instant` per operation. All mutations within one route
  share the same initiation timestamp.

---

## 12. InterfaceBridge Layer (`Src/Ui.InterfaceBridge`)

Three sub-areas:

**Contracts:** plain records (not private), no validation. Strings where the model has typed
wrappers. Organized by domain. Input contracts (create/update) and return contracts in the
same file.

**Boundary converters:** translate between operator strings and domain types. Named in prose
with square brackets: `convert [SourceType] to [TargetType]`. Highest error-interpretation
authority.

**Routes:** parse JSON → convert each field → call orchestration → convert result → serialize.
Private functions registered in a `CommandRoute` dispatch table with domain + verb.

---

## 13. FieldUpdate Pattern

`NoChange | SetTo of 'T`. Each updatable field in a FieldUpdates record is
`FieldUpdate<DomainType>`. The update function:
1. Maps each field through `mapNoChangeToOptionWithConversion` to produce `(SET clause, param) option`
2. `List.choose id` drops the `NoChange` fields
3. Concatenates SET clauses, collects params
4. Rejects if everything is `NoChange`

---

## 14. Compile-Order DAG

Hand-maintained, load-bearing. Dependencies flow one way through the tiers:

```
App → Business → Ui
```

The rungs within and across projects (tier, business concept order, UI layer, container
foundations) are defined by the "Dependencies Build From the Base Up" principle in
`Architecture/SonOfLeo.archimate`. That model is the source of truth; don't restate it here.

A new file goes at its correct position in `<Compile Include>`, never appended.
`Checks/check-compile-order.sh` checks membership only (every file declared, every declaration
on disk), and `dotnet build` catches only an order that breaks a reference. An order that
compiles but violates the principle — e.g. a container foundation such as `*AuditableAction.fs`
compiled after the files that should build on it — is caught by nothing but this review.
Check it.

---

## 15. Classification Engine

Pure in-memory matching:

```
FieldMatch → FieldMatchChain (AND) → ClassificationRuleGroup (AND/OR) → ClassificationRule (AND all groups)
```

`Classifier.classify` is a pure function — takes rules + candidates, returns results. No DB
access. The orchestrator handles fetch, classify, write, status update.

---

## 16. Comment Discipline (as practiced)

~10 comments across the domain projects (then `Model/`, 33 files). All follow the "looks wrong but is correct" pattern:

- `Payment.transactionPointerFromColumns`: arm ignores `stageEntryHeaderUuid` — looks like a
  missed case, is correct because a posted payment carries both IDs.
- `JournalEntryComment.fetchByJournalEntryHeaderIdList`: filters on primary header only —
  looks like it misses secondaries, deliberately out of scope.
- `LookupCache.fs`: one block explaining no-invalidation design and deliberate `failwith`.

Everything else: zero comments.

---

## 17. DU Case Collision Rule

In files that open multiple component modules, prefer full qualification over `open`:

```fsharp
CashFlowComponent.MasterAgreementId    // not just MasterAgreementId
CashFlowComponent.Posted               // not just Posted (could shadow StageEntry's Posted)
```

F#'s `open` resolves bare case names to the *last* opened module — silently. Two DUs sharing
a case name (`Posted`, `Active`, `Staged`) shadow each other with no compiler warning.
