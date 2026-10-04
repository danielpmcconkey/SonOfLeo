# test-efficacy-person

## EFF-PER-1 — test-gap
- **Location:** REQ-PER-2.2; Tests/Tests.Integrated/CrossDomainOrchestration/PersonMaintenance.fs:95; Tests/Tests.Integrated/InterfaceBridge/PersonRoutes.fs:90, :127
- **Summary:** No test ever performs a successful Person update that leaves one field as NoChange, so the partial update in REQ-PER-2.2 is never shown to keep the field it was not asked to change.
- **Resolution:** fix-test

REQ-PER-2.2: 'a means to update a Person's name and birthdate'. Person.update (Src/Business.General/Person.fs:139-165) builds the SET clause only from fields that are SetTo, and PersonOrchestration.updatePerson (lines 56-63) skips validation for a NoChange field. Here is every successful update in scope. PersonMaintenance.fs:101 passes SetTo name and SetTo birthdate. PersonRoutes.fs:98 does the same. PersonRoutes.fs:134 passes SetTo newName and SetTo birth, re-sending the stored birthdate on purpose for REQ-SYS-6.1. Every test that passes NoChange expects a rejection: PersonMaintenance.fs:115, :129 and :150, and PersonRoutes.fs:117 and :151. So 'rename only' (birthdate NoChange) and 'change birthdate only' (name NoChange) never succeed in any test. Smell test: suppose the update wrote a default LocalDate whenever the birthdate was NoChange, or set person_name to an empty or default value whenever the name was NoChange. Every Person test would still pass, because no test checks the untouched field after a successful partial update.

**Action:** Add one orchestrator test where updatePerson gets SetTo newName and birthdate NoChange, and one where it gets name NoChange and SetTo newBirthdate. After each, read the Person back and assert that the unchanged field still equals the fixture value (for example PositionsFixture.jordanBirthdate or PositionsFixture.jordan).

**Why:** An update that takes optional fields has two behaviors: change what was given and keep what was not. The suite only tests the first. The second is the one a careless SQL builder or FieldUpdate mapping breaks, and nothing here would catch it.

---

## EFF-PER-2 — test-gap
- **Location:** REQ-PER-2.4; Src/Business.CrossDomainOrchestration/InvestmentOrchestration.fs:385; Src/Business.CrossDomainOrchestration/RealEstateOrchestration.fs:185; Tests/Tests.Integrated/CrossDomainOrchestration/InvestmentAccountMaintenance.fs:123; Tests/Tests.Integrated/CrossDomainOrchestration/PropertyMaintenance.fs:173
- **Summary:** REQ-PER-2.4 covers 'any operation' given a Person name, but only Person Update, Investment Account create and Property create are tested with an unknown name. The owner-replacing updates for Investment Accounts and Properties also resolve Person names and are never tested with one that matches no Person.
- **Resolution:** fix-test

REQ-PER-2.4: 'A Person name given to any operation that does not match an existing Person fails with a typed error naming it.' Five operations resolve a Person name through PersonOrchestration.fetchPersonByName: (1) Person Update (PersonOrchestration.fs:55), tested at PersonMaintenance.fs:148; (2) Investment Account create (InvestmentOrchestration.fs:302 via resolveOwners), tested at InvestmentAccountMaintenance.fs:123; (3) Investment Account update with SetTo owners (InvestmentOrchestration.fs:385), not tested; (4) Property create (RealEstateOrchestration.fs:129), tested at PropertyMaintenance.fs:173; (5) Property update with SetTo owners (RealEstateOrchestration.fs:185), not tested. A grep of Tests/ for PersonNameDoesntMatchId finds only the create tests and the Person Update test. The owner-update tests (InvestmentAccountMaintenance.fs:245, :257, :315; PropertyMaintenance.fs:323) send only names that exist, an empty set, or a joint-ownership violation. Today both updates share the private resolveOwners helper with their create paths, so the update case works by construction. Per Tests/README and failure-vector-is-a-user-interaction.md, 'replace an account's owners with an unknown name' is a separate user interaction from 'create an account with an unknown owner', and the REQ's 'any operation' wording covers it.

**Action:** Add one test each for Investment Account update and Property update with SetTo owners that include a name matching no Person. Assert PersonNameDoesntMatchId with that name, and assert that the stored owners are unchanged.

**Why:** A REQ that says 'any operation' is only partly covered when only some of the operations are tested. If one update path later stops going through the shared resolver (for example by resolving with fetchByName and dropping the None case), nothing would catch it.

---

## EFF-PER-3 — test-gap
- **Location:** REQ-PER-1.4; Tests/Tests.Integrated/InterfaceBridge/PersonRoutes.fs:79-81
- **Summary:** The REQ-PER-1.4 route test checks the missing-birthdate error with Assert.Contains("birthdate", message), which is the string-matching form of Specimen 4. The message is deterministic and could be asserted exactly.
- **Resolution:** fix-test

The assertion is `expectError (function AsError (JsonDeserializationFailed (_, message, _)) -> Some message | _ -> None) (fun message -> Assert.Contains("birthdate", message))`. The outer DU case is typed, but which field it names, the part this REQ is about, is checked by substring. Json.missingFieldMessage (Src/App.Utility/Json.fs:21-38) produces exactly `$"Missing field for record type {typeof<PersonCreateInput>.FullName}: birthdate"`, so an exact expected value is available. The test also ignores the first slot, the type name, so a deserialization failure against the wrong contract type would pass as long as its text contains 'birthdate'. Specimen 4 says: 'String Contains is the same bug plus brittleness... produce the wrong error with similar wording and it passes.' Example: a library message about a malformed `birthdate` value, or a name mismatch such as 'birthdateUpdate', would satisfy Contains while naming the wrong failure.

**Action:** Match `JsonDeserializationFailed (typeName, message, _)`, then assert typeName = typeof<Contracts.PersonCreateInput>.ToString() and Assert.Equal($"Missing field for record type {typeof<Contracts.PersonCreateInput>.FullName}: birthdate", message).

**Why:** The REQ-1.4 vector at the route is 'the caller is told the birthdate is missing'. Which field the error names is the thing under test, so it should be asserted exactly, the same way the typed DU case is. A substring match accepts any error that happens to mention the word.

---

## EFF-PER-4 — test-gap
- **Location:** REQ-PER-1.1, REQ-PER-1.2; Src/Ui.InterfaceBridge/Routes/PersonRoutes.fs:21, :30; Tests/Tests.Integrated/InterfaceBridge/PersonRoutes.fs:145
- **Summary:** Of the three name fields on the Person routes, only Update's personNameUpdate is ever sent an invalid name, and only an empty one. No route is sent a too-long name (REQ-PER-1.2), and the Create personName field and the Update current-name field are never sent an invalid name.
- **Resolution:** fix-test

PersonRoutes.fs has three separate (input field, PersonName.create) conversions: Create `input.personName |> PersonName.create` (line 21), Update `input.personName |> PersonName.create` for the addressing name (line 30), and Update `input.personNameUpdate |> convertFieldUpdateToNewTypeFallible PersonName.create` (line 31). The only route-level invalid-name test is PersonRoutes.fs:145 (REQ-PER-2.2 REQ-PER-1.1), which sends ' \t ' through personNameUpdate. REQ-PER-1.2 is tested only in isolation (Tests.Isolated/Model/General/Person.fs:25). REQ-PER-1.1 is tested at the route only for that one field. Tests/README.md and failure-vector-is-a-user-interaction.md call for 'One InterfaceBridge case per (input field, converter) pair, asserting the exact AppError case', because 'a correct constructor says nothing about whether anything calls it.' If the Create route bound the contract string straight into the domain (the ingestRawEntries defect that article describes), a blank or 101-character Person name could reach the database through Create and no test would fail. For the over-long case the varchar(100) column would raise an untyped DAL error instead of PersonNameTooLong.

**Action:** Add a route Theory over the Create personName field with a whitespace-only name, expecting PersonNameIsEmpty, and a 101-character name, expecting PersonNameTooLong with limit 100. Assert that no Person is stored. Add one case where the Update addressing name is invalid, and one where personNameUpdate is 101 characters.

**Why:** An isolated constructor test shows the rule is correct, not that each boundary field calls it. Each route field is its own user interaction, and only one of three is tested.

---

