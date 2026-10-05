module Tests.Integrated.InterfaceBridge.PersonRoutes

open System
open NodaTime
open App.DataAccessLayer.DbTransaction
open App.Session
open App.Operation.CoreAuditableAction
open App.Utility.FieldUpdate
open App.Utility.IAppError
open App.Utility.Json
open App.Utility.Result
open App.Utility.UtilityError
open Business.General.BizGeneralError
open Business.General.PersonComponent
open Business.General.Person
open Business.CrossDomainOrchestration
open Tests.Helpers
open Tests.Helpers.Cleanup
open Tests.Helpers.PositionsValues
open Tests.Helpers.Railroad
open Tests.Helpers.RouteResolver
open Xunit

module Contracts = Ui.InterfaceBridge.InterfaceContracts.PersonContracts

(* Every Person route that writes commits. So each test names its Person uniquely, reads back through a fresh
   context after the route returns, and deletes by name in a finally. *)

let private fresh () = Context.create NoTransaction FetchOnly

let private uniqueName () = $"Route Person {Guid.NewGuid():N}"

let private send verb (payload: string) = routeUiCommandForTesting "Person" verb [] payload

let private create (name: string) (birth: LocalDate) =
    ({ personName = name; birthdate = birth } : Contracts.PersonCreateInput)
    |> Json.toJson
    |> Result.bind (send "Create")
    |> Result.bind Json.fromJson<Contracts.PersonReturn>

let private update (input: Contracts.PersonUpdateInput) =
    input |> Json.toJson |> Result.bind (send "Update") |> Result.bind Json.fromJson<Contracts.PersonReturn>

/// Every stored Person with the given name, as (name, birthdate).
let private storedAs (name: string) =
    PersonOrchestration.listPersons (fresh ())
    |> Result.map (
        List.map (fun p -> p |> personName |> PersonName.value, p |> birthdate)
        >> List.filter (fun (n, _) -> n = name))

let private cleanUp (names: string list) =
    names |> List.map (fun n -> fun () -> cleanUpPersonByName n) |> cleanUpAll |> railroadWrapper

[<Collection("SharedTestData")>]
type PersonRoutesTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-PER-2.1 a Person Create payload creates the Person, and the return carries its name and birthdate`` () =
        let name = uniqueName ()
        let birth = LocalDate(1991, 6, 15)
        try
            result {
                let! returned = create name birth
                Assert.Equal(name, returned.personName)
                Assert.Equal(birth, returned.birthdate)
                let! stored = storedAs name
                Assert.Equal<(string * LocalDate) list>([ name, birth ], stored)
            }
            |> railroadWrapper
        finally
            cleanUp [ name ]

    [<Fact>]
    member _.``REQ-PER-1.4 a Person Create payload with no birthdate is rejected with a typed error naming the missing birthdate, and no Person is created`` () =
        let name = uniqueName ()
        try
            result {
                let payload = $"""{{"personName": "{name}"}}"""
                let inputType = typeof<Contracts.PersonCreateInput>
                send "Create" payload
                |> expectError
                    (function AsError (JsonDeserializationFailed (typeName, message, _)) -> Some(typeName, message) | _ -> None)
                    (fun (typeName, message) ->
                        Assert.Equal(inputType.ToString(), typeName)
                        Assert.Equal($"Missing field for record type {inputType.FullName}: birthdate", message))
                let! stored = storedAs name
                Assert.Empty(stored)
            }
            |> railroadWrapper
        finally
            cleanUp [ name ]

    [<Fact>]
    member _.``REQ-PER-2.1 REQ-PER-1.1 a Person Create payload with a whitespace-only name is rejected with a typed empty-name error, and no Person is created`` () =
        let created = ResizeArray<string>()
        try
            let names () = PersonOrchestration.listPersons (fresh ()) |> Result.map (List.map (fun p -> p |> personName |> PersonName.value))
            let before = names ()
            match create " \t " (LocalDate(1991, 6, 15)) with
            | Error (AsError (PersonNameIsEmpty raw)) -> Assert.Equal(" \t ", raw)
            | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
            | Ok returned ->
                created.Add returned.personName
                Assert.Fail "Expected failure; got success"
            result {
                let! before = before
                let! after = names ()
                Assert.Equal<string list>(before, after)
            }
            |> railroadWrapper
        finally
            cleanUp (created |> List.ofSeq)

    [<Fact>]
    member _.``REQ-PER-2.2 a Person Update payload naming a new name and birthdate changes both, and the return carries the new values`` () =
        let name = uniqueName ()
        let newName = uniqueName ()
        let newBirth = LocalDate(1988, 11, 3)
        try
            result {
                let! _ = create name (LocalDate(1991, 6, 15))
                let! returned =
                    update { personName = name; personNameUpdate = SetTo newName; birthdateUpdate = SetTo newBirth }
                Assert.Equal(newName, returned.personName)
                Assert.Equal(newBirth, returned.birthdate)
                let! storedNew = storedAs newName
                Assert.Equal<(string * LocalDate) list>([ newName, newBirth ], storedNew)
                let! storedOld = storedAs name
                Assert.Empty(storedOld)
            }
            |> railroadWrapper
        finally
            cleanUp [ name; newName ]

    [<Fact>]
    member _.``REQ-PER-2.2 REQ-SYS-6.1 a Person Update payload naming no field is rejected with a typed no-change error, and the Person is unchanged`` () =
        let name = uniqueName ()
        let birth = LocalDate(1991, 6, 15)
        try
            result {
                let! _ = create name birth
                update { personName = name; personNameUpdate = NoChange; birthdateUpdate = NoChange }
                |> expectError (function AsError PersonUpdateNoOp -> Some() | _ -> None) ignore
                let! stored = storedAs name
                Assert.Equal<(string * LocalDate) list>([ name, birth ], stored)
            }
            |> railroadWrapper
        finally
            cleanUp [ name ]

    [<Fact>]
    member _.``REQ-PER-2.2 REQ-SYS-6.1 a Person Update payload changing the name and re-sending the stored birthdate succeeds, changing the name and leaving the birthdate as stored`` () =
        let name = uniqueName ()
        let newName = uniqueName ()
        let birth = LocalDate(1991, 6, 15)
        try
            result {
                let! _ = create name birth
                let! returned = update { personName = name; personNameUpdate = SetTo newName; birthdateUpdate = SetTo birth }
                Assert.Equal(newName, returned.personName)
                Assert.Equal(birth, returned.birthdate)
                let! stored = storedAs newName
                Assert.Equal<(string * LocalDate) list>([ newName, birth ], stored)
            }
            |> railroadWrapper
        finally
            cleanUp [ name; newName ]

    [<Fact>]
    member _.``REQ-PER-2.2 REQ-PER-1.1 a Person Update payload with a whitespace-only name is rejected with a typed empty-name error, and the stored name is unchanged`` () =
        let name = uniqueName ()
        let birth = LocalDate(1991, 6, 15)
        try
            result {
                let! _ = create name birth
                update { personName = name; personNameUpdate = SetTo " \t "; birthdateUpdate = NoChange }
                |> expectError
                    (function AsError (PersonNameIsEmpty raw) -> Some raw | _ -> None)
                    (fun raw -> Assert.Equal(" \t ", raw))
                let! stored = storedAs name
                Assert.Equal<(string * LocalDate) list>([ name, birth ], stored)
            }
            |> railroadWrapper
        finally
            cleanUp [ name ]

    [<Fact>]
    member _.``REQ-PER-2.3 the Person List route returns every Person with its name and birthdate, ordered by name`` () =
        result {
            let! returnedJson = send "List" ""
            let! returned = Json.fromJson<Contracts.PersonReturn list> returnedJson
            Assert.Equal<(string * LocalDate) list>(
                [ PositionsFixture.alex, PositionsFixture.alexBirthdate
                  PositionsFixture.jordan, PositionsFixture.jordanBirthdate
                  PositionsFixture.sam, PositionsFixture.samBirthdate ],
                returned |> List.map (fun p -> p.personName, p.birthdate))
        }
        |> railroadWrapper
