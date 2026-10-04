# code-inward-coverage-ui-interfacebridge-cli

## COV-UI-1 — test-gap
- **Location:** Src/Ui.OperatorCli/Program.fs:15-22 (commandRoutes, route); Tests/Tests.Helpers/RouteResolver.fs:21-33
- **Summary:** The operator CLI's own route table, which decides which domains the shipped executable can reach, is never tested for anything except the Account domain. Route tests go through a hand-copied duplicate of the table in Tests.Helpers instead.
- **Resolution:** fix-test

Ui.OperatorCli/Program.fs builds `commandRoutes` by joining eight lists: accountDomainCommandRoutes @ fiscalPeriodDomainCommandRoutes @ journalEntryDomainCommandRoutes @ ingestionDomainCommandRoutes @ classificationDomainCommandRoutes @ cashFlowDomainCommandRoutes @ personDomainCommandRoutes @ positionsDomainCommandRoutes. Its `route` function looks up (domain, verb) in that list.

Every in-process route test (InterfaceBridge/*Routes.fs, PersonRoutes.fs, PositionsRoutes.fs and others) calls `routeUiCommandForTesting` in Tests/Tests.Helpers/RouteResolver.fs. That file declares its own `commandRoutes` with the same join written out again. It does not reference Program.commandRoutes, and nothing in Tests or Src references it either (grep for `commandRoutes` hits only these two definitions).

The only tests that run the real executable go through CliExecutor.runCli (SonOfLeoCli/Program.fs, FileArgumentPosition.fs, UsageMessages.fs). All 19 runCli calls use the Account domain (Create, FetchAll, FetchByCode, plus mis-cased Account variants) or a bogus domain ("Ropa" "Interior").

As a result, nothing in the suite checks that the shipped CLI can reach the Person, DimensionValue, Security, InvestmentAccount, Holding, AccountSnapshot, Property, Valuation, FiscalPeriod, JournalEntry, Ingestion, Classification or CashFlow routes. For example, deleting `@ personDomainCommandRoutes @ positionsDomainCommandRoutes` from Program.fs would leave every test green while `SonOfLeoCli Person Create` returned "Unknown command: Person Create".

This matters because Dan's statement for this slice is "Every operation has its own CLI route, so this data can be entered and reported on from the command line". That claim holds today only by inspection. The Reports CLI does not have this gap: Ui.ReportCli/Program.fs uses `reportingRoutes` directly, the same list RouteResolver uses.

**Action:** Add a test that ties the executable's dispatch table to the route modules. Either expose Program.commandRoutes (or move the joined list into Ui.InterfaceBridge so Program.fs and RouteResolver.fs share one definition), or add a runCli test that calls at least one verb from each of the eight route lists and checks the result is not CliUnknownCommand.

**Why:** A route that exists in the route module but is missing from the executable's table is invisible to the user and the tests do not detect it. Duplicating the composition in a test helper tests the copy, not the shipped code.

---

## SD-UI-1 — statement-delta
- **Location:** Src/Ui.InterfaceBridge/Routes/PositionsRoutes.fs:304-459 (positionsDomainCommandRoutes); Src/Ui.InterfaceBridge/Routes/PersonRoutes.fs:43-61; Specs/Behavioral/Positions.md REQ-POS-11.1-11.8; Specs/Behavioral/Person.md 'Deletion' note
- **Summary:** Dan's statement says the Positions slice 'implements all the CRUD operations', but there is no delete for Dimension Value, Security, Investment Account, Holding or Property; only Account Snapshot and Valuation have one. Unlike Person, the Positions spec never says the missing deletes are deliberate.
- **Resolution:** dan-decides

Dan (verbatim): "This slice implements all the CRUD operations for dealing with such data."

The Positions route table has these verbs:
- DimensionValue: Create / Rename / List
- Security: Create / Update / List
- InvestmentAccount: Create / Update / List
- Holding: Create / UpdateBasisMethod / List / FetchAsOf
- AccountSnapshot: Record / Delete / Fetch / ListDates
- Property: Create / Update / List
- Valuation: Record / Delete / List

The orchestration layer has the same gaps. The only public delete functions are AccountSnapshotOrchestration.deleteSnapshot and RealEstateOrchestration.deleteValuation, and InvestmentOrchestration has no delete at all.

The spec matches the code: REQ-POS-11.1-11.8 ask for create/update/list (plus delete only for snapshots in REQ-POS-7.4 and valuations in REQ-POS-11.8). The code is therefore compliant, and the gap is between Dan's description and the repo. Person.md states its deliberate exclusion explicitly ("There is no means to delete a Person. Owners are referenced by investment accounts and property..."). Positions.md has no matching note for the five entity types above. A reader cannot tell whether those deletes were left out on purpose (referenced history, like the ledger's deactivate-not-delete) or were missed.

**Action:** Dan to confirm whether the missing deletes for Dimension Value, Security, Investment Account, Holding and Property are intentional. If they are, add a 'Deletion' design note to Positions.md like the one in Person.md. If not, add delete REQs and routes.

**Why:** Dan's description of the slice is the authority auditors measure against. When 'all CRUD' disagrees with a spec that deliberately omits deletes for most entities, the next reader cannot tell a design choice from an oversight. Person.md already shows the pattern for recording the choice.

---

