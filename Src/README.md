# Src — what already exists

Read this before writing a helper. Most of what you are about to write is here already,
and reinventing any of it is a review rejection.

This file is an **inventory**, not a rule book. The code is the authority for how each
item behaves; this tells you the item exists and roughly what it is for.

## Projects

`Utilities` ← `Model` ← `ModelOrchestrator` ← `InterfaceBridge` ← `SonOfLeoCli`, plus
`DataAccessLayer`, `Context` and `Logger` as shared infrastructure. Dependencies run one
way. `Checks/check-compile-order.sh` guards the hand-maintained `<Compile Include>` order
in each `.fsproj` — a new file goes at its correct position, never appended blindly.

## Infrastructure inventory

| Module | Use it for | Never |
|---|---|---|
| `Utilities.AppError` | The one application-wide error DU. `AppError.toMessage` is the only place error strings live. | Building an error string anywhere else. Adding a wildcard arm to `toMessage`. Using `TestingError` in `Src/`. |
| `Utilities.ResultHelper` | `result { }`, `convertListOfResultsToResultsList`, `convertOptionToDesiredTypeWithFallibleConverter` | Hand-rolling a fold or loop over `Result` values. Adding FsToolkit. |
| `Utilities.FieldUpdate` | `NoChange \| SetTo`. Converters: `map`, `mapNoChangeToOptionWithConversion`, `convertFieldUpdateToNewTypeFallible`, `convertFieldUpdateOptionToNewTypeOption`, `convertFieldUpdateOptionToNewTypeOptionFallible` | Writing FieldUpdate plumbing by hand. Using an option/flag to mean "don't update this field". |
| `Utilities.Clock` / `Utilities.Calendar` | `Clock.now()`, `Calendar.today()`, `dateFromInstant` | `DateTime.Now`, `DateTimeOffset.UtcNow`, `SystemClock`. Enforced by `Checks/check-clock.sh`. |
| `DataAccessLayer` | `QueryParameterValue`, `AcceptableExpectedRows`, `buildReadQuery`, `RowReader`, the execute functions, the transaction bracket `runFuncAndAutoRollback` | Touching Npgsql anywhere else. Enforced by `Checks/check-npgsql.sh`. Interpolating a value into SQL — structural fragments only. |
| `Model.LookupCache` | Account code ↔ ID and fiscal period key ↔ ID | Hand-writing a code-to-ID lookup query. It exists. |
| `Model.Money` | All money arithmetic — `add`, `subtractVal1FromVal2`, `sumList`, `splitByN` | Arithmetic on raw `decimal` money values. |
| `Logger.Audit` (`AuditEnvelope`) | One instant per user action, created at the route handler and threaded down | A fresh `Clock.now()` inside a mutating operation. |
| `Utilities.Json` | `Json.fromJson<'T>` / `Json.toJson<'T>` | Constructing your own `JsonSerializerOptions`. |

## Two conventions the code follows silently

The code obeys both of these everywhere, which means you cannot tell from reading it
whether they are deliberate. They are.

- **Parameter order: context first, subject last.** Transaction, then audit envelope, then
  the subject — so the subject rides the pipeline:
  `accountId |> Account.fetchById transaction`. Functions operating on several subjects may
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
