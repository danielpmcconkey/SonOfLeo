module Tests.Integrated.CrossDomainOrchestration.PersonMaintenance

open NodaTime
open App.DataAccessLayer.DbTransaction
open App.Operation.CoreAuditableAction
open App.Utility
open App.Utility.FieldUpdate
open App.Utility.IAppError
open App.Utility.Result
open App.Session
open Business.General.BizGeneralAuditableAction
open Business.General.BizGeneralError
open Business.General.PersonComponent
open Business.General.Person
open Business.CrossDomainOrchestration
open Ui.InterfaceBridge.CommandRoute
open Tests.Helpers
open Tests.Helpers.PositionsValues
open Tests.Helpers.Railroad
open Tests.Helpers.TestError
open Xunit

let private today (context: Context.Context) = context |> Context.getInitiationInstant |> Calendar.dateFromInstant

let private summary (person: Person) = person |> personName |> PersonName.value, person |> birthdate

let private alreadyExists = function AsError (PersonNameAlreadyExists name) -> Some name | _ -> None
let private notFound = function AsError (PersonNameDoesntMatchId name) -> Some name | _ -> None
let private birthdateInFuture = function AsError (PersonBirthdateLaterThanCurrentDate (given, current)) -> Some(given, current) | _ -> None

let private storedAs context name =
    PersonOrchestration.listPersons context
    |> Result.map (List.map summary >> List.filter (fun (n, _) -> n = name))

[<Collection("SharedTestData")>]
type PersonMaintenanceTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-PER-2.1 creating a Person from a name and a birthdate stores both, and the list returns that Person with that name and birthdate`` () =
        runCommandRouteAndAutoRollback PersonCreate (fun context ->
            result {
                let birth = LocalDate(1975, 2, 28)
                let! created = PersonOrchestration.createPerson context (toPersonName "Casey Example") birth
                Assert.Equal(("Casey Example", birth), created |> summary)
                let! listed = storedAs context "Casey Example"
                Assert.Equal<(string * LocalDate) list>([ "Casey Example", birth ], listed)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-PER-1.3 creating a Person whose name exactly matches an existing Person's is rejected with a typed error naming the name, and no second Person is stored`` () =
        runCommandRouteAndAutoRollback PersonCreate (fun context ->
            result {
                PersonOrchestration.createPerson context (toPersonName PositionsFixture.alex) (LocalDate(1990, 1, 1))
                |> expectError alreadyExists (fun name -> Assert.Equal(PositionsFixture.alex, name))
                let! listed = storedAs context PositionsFixture.alex
                Assert.Equal<(string * LocalDate) list>([ PositionsFixture.alex, PositionsFixture.alexBirthdate ], listed)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-PER-1.3 REQ-SYS-1.1 creating a Person whose name is an existing Person's name wrapped in whitespace is rejected as a duplicate naming the trimmed name`` () =
        runCommandRouteAndAutoRollback PersonCreate (fun context ->
            PersonOrchestration.createPerson context (toPersonName $"  {PositionsFixture.sam}\t") (LocalDate(1990, 1, 1))
            |> expectError alreadyExists (fun name -> Assert.Equal(PositionsFixture.sam, name))
            |> Ok)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-PER-1.3 a Person whose name differs from an existing Person's only by letter case is created alongside it`` () =
        runCommandRouteAndAutoRollback PersonCreate (fun context ->
            result {
                let! _ = PersonOrchestration.createPerson context (toPersonName "ALEX EXAMPLE") (LocalDate(1990, 1, 1))
                let! listed = PersonOrchestration.listPersons context
                let names = listed |> List.map (personName >> PersonName.value)
                Assert.Contains("ALEX EXAMPLE", names)
                Assert.Contains(PositionsFixture.alex, names)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-PER-1.5 REQ-SYS-3.4 a birthdate on the calendar date of the operation's initiation instant is accepted, and one the day after is rejected with a typed error naming the date`` () =
        runCommandRouteAndAutoRollback PersonCreate (fun context ->
            result {
                let currentDate = today context
                let! created = PersonOrchestration.createPerson context (toPersonName "Newborn Example") currentDate
                Assert.Equal(currentDate, created |> birthdate)
                PersonOrchestration.createPerson context (toPersonName "Unborn Example") (currentDate.PlusDays(1))
                |> expectError birthdateInFuture (fun (given, current) ->
                    Assert.Equal(currentDate.PlusDays(1), given)
                    Assert.Equal(currentDate, current))
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-PER-2.2 updating a Person addressed by its current name changes its name and birthdate, and its old name then matches no Person`` () =
        runCommandRouteAndAutoRollback PersonUpdate (fun context ->
            result {
                let newBirth = LocalDate(1981, 5, 6)
                let! updated =
                    PersonOrchestration.updatePerson
                        context (toPersonName PositionsFixture.jordan) (SetTo(toPersonName "Jordan Renamed")) (SetTo newBirth)
                Assert.Equal(("Jordan Renamed", newBirth), updated |> summary)
                let! fetched = PersonOrchestration.fetchPersonByName context (toPersonName "Jordan Renamed")
                Assert.Equal(("Jordan Renamed", newBirth), fetched |> summary)
                PersonOrchestration.fetchPersonByName context (toPersonName PositionsFixture.jordan)
                |> expectError notFound (fun name -> Assert.Equal(PositionsFixture.jordan, name))
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-PER-2.2 REQ-PER-1.3 renaming a Person to another Person's name is rejected with a typed error naming the name, and neither Person changes`` () =
        runCommandRouteAndAutoRollback PersonUpdate (fun context ->
            result {
                PersonOrchestration.updatePerson
                    context (toPersonName PositionsFixture.jordan) (SetTo(toPersonName PositionsFixture.sam)) NoChange
                |> expectError alreadyExists (fun name -> Assert.Equal(PositionsFixture.sam, name))
                let! jordan = PersonOrchestration.fetchPersonByName context (toPersonName PositionsFixture.jordan)
                let! sam = PersonOrchestration.fetchPersonByName context (toPersonName PositionsFixture.sam)
                Assert.Equal((PositionsFixture.jordan, PositionsFixture.jordanBirthdate), jordan |> summary)
                Assert.Equal((PositionsFixture.sam, PositionsFixture.samBirthdate), sam |> summary)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-PER-2.2 REQ-PER-1.5 updating a Person's birthdate to the day after the current date is rejected with a typed error naming the date, and the stored birthdate is unchanged`` () =
        runCommandRouteAndAutoRollback PersonUpdate (fun context ->
            result {
                let tomorrow = (today context).PlusDays(1)
                PersonOrchestration.updatePerson context (toPersonName PositionsFixture.sam) NoChange (SetTo tomorrow)
                |> expectError birthdateInFuture (fun (given, _) -> Assert.Equal(tomorrow, given))
                let! sam = PersonOrchestration.fetchPersonByName context (toPersonName PositionsFixture.sam)
                Assert.Equal(PositionsFixture.samBirthdate, sam |> birthdate)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-PER-2.3 listing Persons returns every fixture Person with its name and birthdate, ordered by name, and no other Person`` () =
        PersonOrchestration.listPersons (Context.create NoTransaction FetchOnly)
        |> Result.map (fun listed ->
            Assert.Equal<(string * LocalDate) list>(
                [ PositionsFixture.alex, PositionsFixture.alexBirthdate
                  PositionsFixture.jordan, PositionsFixture.jordanBirthdate
                  PositionsFixture.sam, PositionsFixture.samBirthdate ],
                listed |> List.map summary))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-PER-2.4 updating a Person by a name that matches no Person fails with a typed error naming that name`` () =
        runCommandRouteAndAutoRollback PersonUpdate (fun context ->
            PersonOrchestration.updatePerson context (toPersonName "Nobody Example") NoChange (SetTo(LocalDate(1990, 1, 1)))
            |> expectError notFound (fun name -> Assert.Equal("Nobody Example", name))
            |> Ok)
        |> railroadWrapper
