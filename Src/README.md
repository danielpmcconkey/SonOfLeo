# Src — what already exists

Read this before writing a helper. Most of what you are about to write is here already,
and reinventing any of it is a review rejection.

This file is an **inventory**, not a rule book. The code is the authority for how each
item behaves; this tells you the item exists and roughly what it is for.

## Projects

Three tiers, `App` ← `Business` ← `Ui`:

- `App.Utility`, `App.DataAccessLayer`, `App.Operation`, `App.Session`
- `Business.General`, `Business.FinancialServices`,
  `Business.FinancialServices.{Ledger,DataIngestion,CashFlow,Classification}`,
  `Business.CrossDomainOrchestration`
- `Ui.InterfaceBridge`, `Ui.OperatorCli`, `Ui.ReportCli`

What may reference what, and the order of project references, `<Compile Include>` lists and
`open`s, is defined by "Dependencies Build From the Base Up" in
`Architecture/SonOfLeo.archimate`. `Checks/check-compile-order.sh` guards the hand-maintained
`<Compile Include>` order in each `.fsproj` — a new file goes at its correct position, never
appended blindly.

## Infrastructure inventory

| Module | Use it for | Never |
|---|---|---|
| `App.Utility.IAppError` | The error interface (`ToMessage`, `DomainName`, `CaseName`). Each container defines its own error DU implementing it (`UtilityError`, `DalError`, `LedgerError`, `DataIngestionError`, `CashFlowError`, `BridgeError`, …); its `ToMessage` is the only place that DU's strings live. | Building an error string anywhere else. Adding a wildcard arm to a `ToMessage`. Using `TestingError` in `Src/`. |
| `App.Utility.Result` | `result { }`, `convertListOfResultsToResultsList`, `convertOptionToDesiredTypeWithFallibleConverter` | Hand-rolling a fold or loop over `Result` values. Adding FsToolkit. |
| `App.Utility.FieldUpdate` | `NoChange \| SetTo`. Converters: `map`, `mapNoChangeToOptionWithConversion`, `convertFieldUpdateToNewTypeFallible`, `convertFieldUpdateOptionToNewTypeOption`, `convertFieldUpdateOptionToNewTypeOptionFallible` | Writing FieldUpdate plumbing by hand. Using an option/flag to mean "don't update this field". |
| `App.Utility.Clock` / `App.Utility.Calendar` | `Clock.now()`, `Calendar.today()`, `dateFromInstant` | `DateTime.Now`, `DateTimeOffset.UtcNow`, `SystemClock`. Enforced by `Checks/check-clock.sh`. |
| `App.DataAccessLayer` | `QueryParameterValue`, `AcceptableExpectedRows`, `buildReadQuery`, `RowReader`, the execute functions, `DbTransaction` | Touching Npgsql anywhere else. Enforced by `Checks/check-npgsql.sh`. Interpolating a value into SQL — structural fragments only. |
| `App.DataAccessLayer.LookupCache` | Account code ↔ ID and fiscal period key ↔ ID | Hand-writing a code-to-ID lookup query. It exists. |
| `Business.FinancialServices.Money` | All money arithmetic — `add`, `subtractVal1FromVal2`, `sumList`, `splitByN` | Arithmetic on raw `decimal` money values. |
| `App.Session.Context` / `App.Operation.AuditEnvelope` | One context per user action — its transaction and its single instant — created at the route handler (`Context.create`) and threaded down. `Ui.InterfaceBridge.CommandRoute.runFuncAndAutoRollback` is the rolled-back bracket. | A fresh `Clock.now()` inside a mutating operation. |
| `App.Utility.Json` | `Json.fromJson<'T>` / `Json.toJson<'T>` | Constructing your own `JsonSerializerOptions`. |

## Two conventions the code follows silently

The code obeys both of these everywhere, which means you cannot tell from reading it
whether they are deliberate. They are.

- **Parameter order: context first, subject last.** The context, then the subject — so the
  subject rides the pipeline: `accountId |> Account.fetchById context`. Functions operating on several subjects may
  group the subjects before the context arguments.
- **Private by default.** Obvious interface functions are public without argument. A
  function whose analogs in other domains are private must be private, unless it carries a
  documented, Dan-approved rationale at the definition site.

## Where the rest of the rules live

- Behavioral requirements: `Specs/Behavioral/`
- Terms with a SonOfLeo-specific meaning: `Specs/Definitions.md`
- Judgment — layering, validation location, naming, temporal and money handling:
  `CompoundedLearnings/` (start at its catalogs)
- Anything mechanically checkable: `Checks/` — a script, not a paragraph
