# InterfaceBridge Auditor

## IDIOM-IB-1 — idiom
- **Location:** Src/InterfaceBridge/InterfaceContracts/AccountContracts.fs line 3 and line 89
- **Summary:** FetchSort, an orchestrator-layer DU from ModelOrchestrator.FetchFilters, is imported directly into the AccountContracts interface contract, bypassing boundary conversion entirely.
- **Resolution:** fix-code

AccountContracts.fs opens ModelOrchestrator.FetchFilters (line 3) and uses FetchSort directly in the AccountActivityFetchInput record (line 89: `sort: FetchSort option`). In the route (AccountRoutes.fs line 137), `input.sort` passes through from the deserialized contract to the orchestrator's `fetchFiltered` with no boundary converter intercepting it. This is architecturally inconsistent with how every other field in the same contract is handled -- the AccountActivityFilterInput fields (accountCode, accountType, source, etc.) all go through explicit boundary converters in OrchestrationConverters.fs that translate from primitive types to domain types. FetchSort alone skips this step. The type-taxonomy article in CompoundedLearnings states: 'Interface contracts: DTOs at the CLI boundary, owned by InterfaceBridge. These use primitives (string, decimal, Guid, LocalDate, Instant) -- not domain types.' FetchSort is defined in the orchestrator layer (ModelOrchestrator/FetchFilterAndSort.fs), making it a domain type per this taxonomy. The fix would be to accept a string in the contract and add a boundary converter (parallel to how AccountType uses a string in the contract and `convert AccountTypeString Option to AccountType Option` handles the conversion).

**Action:** Replace `sort: FetchSort option` in AccountActivityFetchInput with `sort: string option`, and add a boundary converter function (e.g., `convert SortString Option to FetchSort Option`) in OrchestrationConverters.fs that validates and maps the string to the FetchSort DU.

**Why:** Interface contracts are the system's external boundary. They should depend only on primitives so the contract shape is self-evident from JSON and decoupled from internal type evolution. When an orchestrator type leaks into a contract, the actor's JSON payload shape is dictated by an internal F# DU's serialization behavior (via JsonFSharpConverter), and any rename or restructure of the orchestrator type silently changes the external API. The boundary converter pattern exists precisely to insulate these two concerns from each other.


## IDIOM-IB-2 — idiom
- **Location:** Src/InterfaceBridge/InterfaceContracts/FiscalPeriodContracts.fs lines 15-18; Src/InterfaceBridge/Routes/FiscalPeriodRoutes.fs lines 18, 28, 47, 57
- **Summary:** FiscalPeriodInput is a single interface contract type shared across four semantically different operations (Create, FetchByKey, Close, Reopen), violating the type-taxonomy convention that input types are never shared across semantically different operations.
- **Resolution:** fix-code

FiscalPeriodContracts.fs defines one type `FiscalPeriodInput = { periodKey: string }` with an explicit doc comment acknowledging it is 'multi-purpose' (line 15). This single type is deserialized as the input for four distinct routes in FiscalPeriodRoutes.fs: Create (line 18), FetchByKey (line 28), Close (line 47), and Reopen (line 57). The type-taxonomy article in CompoundedLearnings states: 'Input types are never shared across semantically different operations, even when they happen to have the same primitive shape (e.g., a string input for "fetch JE by external reference" is not the same contract as a string input for "fetch account by name").' Create, read, close, and reopen are semantically different operations by any standard. The codebase itself follows the one-type-per-operation convention everywhere else: Account has AccountCreateInput, AccountFetchByCodeInput, AccountDeactivationInput, AccountUpdateNameInput, etc. Journal entries have JournalEntryInput, JournalEntryFetchByIdInput, JournalEntryVoidInput, etc. FiscalPeriod is the sole exception.

**Action:** Split FiscalPeriodInput into four types: FiscalPeriodCreateInput, FiscalPeriodFetchByKeyInput, FiscalPeriodCloseInput, and FiscalPeriodReopenInput. Each contains `periodKey: string`. Update the four route handlers and their CommandRoute entries to reference the specific types.

**Why:** The one-contract-per-operation convention exists so each operation's input can evolve independently. Today all four operations need only a periodKey, but if Create later adds a field (e.g., an initial open/closed flag override) or Close adds a field (e.g., a reason string), the shared type forces an unrelated operation to carry a field it does not use. Splitting now costs four trivial type aliases; splitting later means changing every caller and updating tests. The convention front-loads the trivial cost to avoid the compound cost later.

