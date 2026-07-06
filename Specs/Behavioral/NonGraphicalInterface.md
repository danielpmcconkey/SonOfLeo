# Non-Graphical interface requirements

Applies to command line, importing, reporting, API, etc. Any use case where an external actor triggers an action. Note, this section does not apply to any graphical user interface interactions, though it is assumed any such graphical UI will interface with an API via the requirements laid out in this document.

## 1. General non-graphical interaction

- **REQ-NGUI-1.1** All interface use cases must be triggered by the actor providing a domain, a verb, and a payload. Ex: Account Create {...}
- **REQ-NGUI-1.2** Some use cases may require additional inputs beyond domain, verb, and payload and the system must support extending the input.
- **REQ-NGUI-1.3** The system will respond to all interface triggers with a code denoting failure / success and a payload. Ex: 0 {...}
- **REQ-NGUI-1.3.1** In the event of an error, the payload will comprise the error message and, in cases of system exceptions, the full stack trace.
- **REQ-NGUI-1.4** The user interface must never force the actor to interact with Account UUIDs. All interface capabilities must present an option for the actor to reference accounts by code and all return payloads must include account codes when identifying an account.
- **REQ-NGUI-1.5** When a UI-facing operation references an Account entity by code and that code does not correspond to an existing Account entity, the operation must fail with an error.

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


## Waived from testing

Active requirements that are deliberately not verified by tests. Two-state rule: every
active requirement is either tested or in this table.

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


## Withdrawn

| ID          | Original Requirement | Reason |
|-------------|----------------------|--------|
| REQ-NGUI-2.3 | UI domain types will provide a 1:1 map to the primary domain types. Example, if the primary domain type for Account has an accountType field, the UI domain type will have an accountType field. | Moved to an interface contract paradigm |
| REQ-NGUI-2.3.1 | For compound types (e.g.: the Account type's activityPeriod), feature designers have the latitude to represent them as multiple peer fields in the UI domain type or as a compound "nested" type within the domain type. | Moved to an interface contract paradigm |
