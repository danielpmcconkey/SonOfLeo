# Non-Graphical interface requirements

Applies to command line, importing, reporting, API, etc. Any use case where an external actor triggers an action. Note, this section does not apply to any graphical user interface interactions, though it is assumed any such graphical UI will interface with an API via the requirements laid out in this document.

## 1. General non-graphical interaction

- **REQ-NGUI-1.1** All interface use cases must be triggered by the actor providing a domain, a verb, and a payload. Ex: Account Create {...}
- **REQ-NGUI-1.2** Some use cases may require additional inputs beyond domain, verb, and payload and the system must support extending the input.
- **REQ-NGUI-1.3** The system will respond to all interface triggers with a code denoting failure / success and a payload. Ex: 0 {...}
- **REQ-NGUI-1.3.1** In the event of an error, the payload will comprise the error message.
- **REQ-NGUI-1.3.2** In the event of a system exception, the error payload will additionally include the full stack trace.
- **REQ-NGUI-1.4** The user interface must never force the actor to interact with Account UUIDs. All interface capabilities must present an option for the actor to reference accounts by code and all return payloads must include account codes when identifying an account.
- **REQ-NGUI-1.5** When a UI-facing operation references an Account entity by code and that code does not correspond to an existing Account entity, the operation must fail with an error.
- **REQ-NGUI-1.6** All interface return payloads that identify an account must include the account name alongside the account code.

## 2. UI domain types

- **REQ-NGUI-2.1** The payloads described above will be represented by their own domain types in this system.
- **REQ-NGUI-2.1.1** Such payload types should be considered the interface contracts for each use case.
- **REQ-NGUI-2.2** The UI domain types do not provide any business validation beyond which fields are required vs optional.
- **REQ-NGUI-2.3** stricken
- **REQ-NGUI-2.3.1** stricken
- **REQ-NGUI-2.4** The interface layer (see Definitions) will be responsible for marshalling / unmarshalling between UI domain types and their serialized format. No other layer in this system will be allowed to perform such conversions.

## 3. Command line interface

- **REQ-NGUI-3.1** The actor will provide the domain component of the trigger via the first command line argument (see REQ-NGUI-1.1).
- **REQ-NGUI-3.2** The actor will provide the verb component of the trigger via the second command line argument (see REQ-NGUI-1.1).
- **REQ-NGUI-3.3** The actor will provide the payload component of the trigger via stdin.
- **REQ-NGUI-3.4** The actor will provide any additional components of the trigger via the third-through-nth command line arguments (see REQ-NGUI-1.2).
- **REQ-NGUI-3.5** The actor will provide the payload component of the trigger formatted as a JSON serialization of whichever UI domain type is required for the use case.
- **REQ-NGUI-3.6** Upon successful execution, the system will return the payload via stdout and exit with code 0.
- **REQ-NGUI-3.7** Upon unsuccessful execution, the system will return the error via stderr and exit with a non-0 code.
- **REQ-NGUI-3.8** The domain and verb command line arguments must be treated as case sensitive
- **REQ-NGUI-3.9** If the actor provides an incorrect or otherwise unsupported domain and verb combination, the CLI application must exit with an appropriate error 
- **REQ-NGUI-3.10** The actor may provide the payload component of the trigger via a `--file` argument followed by a file path, as an alternative to stdin (REQ-NGUI-3.3). The contents of the specified file replace the stdin payload. This mechanism applies to both the main CLI (this section) and the Reports CLI (section 4).

## 4. Reports CLI

- **REQ-NGUI-4.1** The system must provide a separate Reports CLI executable for report generation, distinct from the main CLI.
- **REQ-NGUI-4.2** The Reports CLI accepts a report name as its first command line argument. The report name is case sensitive.
- **REQ-NGUI-4.3** The Reports CLI accepts the payload via stdin or the `--file` flag (REQ-NGUI-3.10).
- **REQ-NGUI-4.4** Upon successful execution, the Reports CLI returns the payload via stdout and exits with code 0. Upon failure, it returns the error via stderr and exits with a non-zero code.
- **REQ-NGUI-4.5** If the actor provides an unsupported report name, the Reports CLI must exit with a typed error.


## Waived from testing

Active requirements that are enforced (by type system, code review, schema, or
construction pattern) but deliberately not verified by tests.

| ID             | Reason testing is waived  | Approved         |
|----------------|---|------------------|
| REQ-NGUI-1.1 | It's too broadly scoped | Dan, 2026-06-18  |
| REQ-NGUI-1.2 | There are no use cases that meet this yet | Dan, 2026-06-18 |
| REQ-NGUI-1.4 | You can't test a negative and it's also quite clear by the interface contracts that codes are present | Dan, 2026-07-06 | 
| REQ-NGUI-2.1   | It's too broadly scoped | Dan, 2026-06-18  |
| REQ-NGUI-2.1.1 | It's too broadly scoped | Dan, 2026-06-18  |
| REQ-NGUI-2.2   | It's too broadly scoped | Dan, 2026-06-18  |
| REQ-NGUI-2.4   | It's too broadly scoped | Dan, 2026-06-18  |
| REQ-NGUI-3.1   | It's too broadly scoped | Dan, 2026-06-18  |
| REQ-NGUI-3.2   | It's too broadly scoped | Dan, 2026-06-18  |
| REQ-NGUI-3.3   | It's too broadly scoped | Dan, 2026-06-18  |
| REQ-NGUI-3.4   | It's too broadly scoped | Dan, 2026-06-18  |
| REQ-NGUI-3.5   | It's too broadly scoped | Dan, 2026-06-18  |
| REQ-NGUI-1.3.2 | Cannot force a .NET system exception through command routes in a test; stack-trace inclusion is verified by code review | Dan, 2026-08-20 |
| REQ-NGUI-1.6   | Negative existence claim — cannot prove every payload includes account name; enforced by code review and periodic audit | Dan, 2026-08-07 |
| REQ-NGUI-3.10  | CLI binary invocation — verified by code review and manual testing | Dan, 2026-08-07 |
| REQ-NGUI-4.1   | Architectural constraint — verified by the existence of the Reports project | Dan, 2026-08-07 |
| REQ-NGUI-4.3   | Too broadly scoped | Dan, 2026-08-07 |

## Unenforceable

Active requirements that bind humans, not code. Nothing in the system enforces these.

| ID | Why it cannot be enforced | Approved |
|---|---|---|
|  |  |  |

## Withdrawn

| ID          | Original Requirement | Reason |
|-------------|----------------------|--------|
| REQ-NGUI-2.3 | UI domain types will provide a 1:1 map to the primary domain types. Example, if the primary domain type for Account has an accountType field, the UI domain type will have an accountType field. | Moved to an interface contract paradigm |
| REQ-NGUI-2.3.1 | For compound types (e.g.: the Account type's activityPeriod), feature designers have the latitude to represent them as multiple peer fields in the UI domain type or as a compound "nested" type within the domain type. | Moved to an interface contract paradigm |
