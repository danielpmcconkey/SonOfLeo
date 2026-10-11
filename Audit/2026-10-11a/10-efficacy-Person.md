# test-efficacy-person

## EFF-PER-1 — test-gap
- **Location:** REQ-PER-1.1; Tests/Tests.Isolated/Model/General/Person.fs:10-17; Tests/Tests.Integrated/InterfaceBridge/PersonRoutes.fs:94,170; Src/Business.General/PersonComponent.fs (PersonName.create)
- **Summary:** REQ-PER-1.1 says a Person name cannot be null or whitespace-only, but no test sends a null name. Only the empty and whitespace-only cases are tested.
- **Resolution:** fix-test

REQ-PER-1.1: "Person name cannot be null or whitespace only (post-trim, per REQ-SYS-1.1)." These are all the tests that cite it:
- Isolated Person.fs:10-17 is a Theory with InlineData("") and InlineData("   ") only.
- Person.fs:20 covers trimming.
- PersonRoutes.fs:94 (Create) and :170 (Update) send " \t ".
No test sends null, at the smart constructor or at the route (for example {"birthdate":"1990-01-01"} with personName missing, or "personName": null).

The null half is a separate behavior from the whitespace half. PersonName.create runs `let trimmed = raw.Trim()` before its IsNullOrWhiteSpace check. A null raw would throw a NullReferenceException instead of returning the typed PersonNameIsEmpty error. So the only thing rejecting a null name today is the JSON layer (FSharp.SystemTextJson refusing null in a non-option record field), and no test pins that.

REQ-PER-1.4, the sister requirement for birthdate, does have exactly this kind of boundary test: PersonRoutes.fs:74 asserts JsonDeserializationFailed with the exact message "Missing field for record type ...: birthdate". Other domains also cover null explicitly, e.g. InlineData(null) in InvoiceDataStates.fs:417 and PaymentAgreementDataStates.fs:359.

Smell test: if the JSON options were changed to allow null fields (or a non-JSON caller passed null), PersonName.create would throw rather than return a typed error. Every current REQ-PER-1.1 test would still pass.

**Action:** Add a route test that sends a Person Create payload with personName missing, and one with it null. Assert the typed rejection (JsonDeserializationFailed with the exact missing-field message, matching the REQ-PER-1.4 pattern at PersonRoutes.fs:74) and that no Person is stored. Or add InlineData(null) to the isolated Theory if the smart constructor is meant to own null rejection.

**Why:** A requirement with two conditions (null and whitespace-only) is only half covered when one condition has no test. Right now the null rejection depends on a serializer default that no test exercises, and the domain constructor's own handling of null is an exception, not a typed error.

---

## EFF-PER-2 — test-gap
- **Location:** REQ-PER-1.3 / REQ-PER-2.2; Tests/Tests.Integrated/CrossDomainOrchestration/PersonMaintenance.fs:145; Tests/Tests.Integrated/InterfaceBridge/PersonRoutes.fs:152; Src/Business.CrossDomainOrchestration/PersonOrchestration.fs (confirmNameFree, updatePerson)
- **Summary:** No test covers a Person update that re-sends the Person's own current name. Without one, the self-exclusion in the uniqueness check could break unnoticed and every such update would be wrongly rejected as a duplicate.
- **Resolution:** fix-test
- **Prior ruling:** UNIQUE-DB (2026-10-03, recorded 2026-10-05) ruled that update-path uniqueness needs no separate test, because a regressed guard still fails loudly with nothing written. That ruling covers a guard that stops rejecting duplicates. This finding is the opposite failure: the guard rejecting a valid, non-duplicate update. That is not a loud refusal of bad data; it wrongly refuses good data, and the database constraint does not backstop it. Matching would take squinting, so this is re-raised.

REQ-PER-1.3 forbids two Persons sharing a name, and REQ-PER-2.2 makes update subject to §1. On update, PersonOrchestration.updatePerson calls `confirmNameFree context newName (Some self)`, and confirmNameFree rejects only `| Some person when Some(person |> personId) <> except`. So a Person keeps its own name without conflicting with itself.

These are the update-path name tests:
- PersonMaintenance.fs:145 renames to another Person's name and expects PersonNameAlreadyExists.
- PersonMaintenance.fs:106/122 and PersonRoutes.fs:115 rename to a brand-new name.
- PersonRoutes.fs:152 re-sends the stored birthdate (the REQ-SYS-6.1 same-value case) but changes the name.
No test sends personNameUpdate = SetTo <the Person's current name>, for example together with a birthdate change.

Mutation check: replace the guard with `| Some _ -> Error(PersonNameAlreadyExists ...)`, ignoring `except`. Every existing test still passes, because 145 still rejects, and the new-name renames find no existing row. Meanwhile a legitimate update that re-sends the current name (which the REQ-SYS-6.1 "Why an unchanged value is not a no-op" rationale explicitly allows) would be refused as a duplicate of itself.

The DB UNIQUE(person_name) constraint (202610041000-CreateSchemaGeneralAndPerson.sql:20) would not catch this, because the false rejection happens in the application check before any write.

**Action:** Add a test (orchestration or route) that updates a fixture Person with personNameUpdate = SetTo <its current name> and birthdateUpdate = SetTo <a new past date>. Assert success, the unchanged name, and the new birthdate read back.

**Why:** Tests that only check rejections cannot catch a guard that rejects too much. When a uniqueness rule excludes the record itself, that exclusion is behavior in its own right, and only an acceptance case that hits it proves it exists.

---


