module Business.CrossDomainOrchestration.PersonOrchestration

open NodaTime
open App.Utility
open App.Utility.IAppError
open App.Utility.Result
open App.Utility.FieldUpdate
open App.Session
open Business.General.BizGeneralError
open Business.General.PersonComponent
open Business.General.Person

let private confirmBirthdateNotInFuture (context: Context.Context) (birthdate: LocalDate) : Result<unit, IAppError> =
    let currentDate = context |> Context.getInitiationInstant |> Calendar.dateFromInstant
    if birthdate > currentDate then Error(PersonBirthdateLaterThanCurrentDate(birthdate, currentDate)) else Ok()

let private confirmNameFree
    (context: Context.Context)
    (personName: PersonName)
    (except: PersonId option)
    : Result<unit, IAppError> =
    result {
        let! existing = personName |> fetchByName context
        match existing with
        | Some person when Some(person |> personId) <> except ->
            return! Error(PersonNameAlreadyExists(personName |> PersonName.value))
        | _ -> return ()
    }

let constructNewAndPersist (context: Context.Context) (personName: PersonName) (birthdate: LocalDate) : Result<Person, IAppError> =
    let instant = context |> Context.getInitiationInstant
    let person = create (PersonId.create ()) personName birthdate instant instant
    result {
        do! confirmBirthdateNotInFuture context birthdate
        do! confirmNameFree context personName None
        do! person |> persist context
        return person
    }

let updatePerson (context: Context.Context) (fieldUpdates: PersonFieldUpdates) : Result<Person, IAppError> =
    result {
        let self = fieldUpdates.personIdToUpdate
        let! _ = self |> fetchById context
        do!
            match fieldUpdates.personNameUpdate with
            | SetTo newName -> confirmNameFree context newName (Some self)
            | NoChange -> Ok()
        do!
            match fieldUpdates.birthdateUpdate with
            | SetTo newBirthdate -> confirmBirthdateNotInFuture context newBirthdate
            | NoChange -> Ok()
        return! update context fieldUpdates
    }

let listPersons (context: Context.Context) : Result<Person list, IAppError> =
    fetchAll context |> Result.map (List.sortBy (personName >> PersonName.value))

/// The names of these Persons, ordered by name.
let personNamesOf (context: Context.Context) (personIds: Set<PersonId>) : Result<string list, IAppError> =
    personIds
    |> Set.toList
    |> List.map (fetchById context >> Result.map (personName >> PersonName.value))
    |> convertListOfResultsToResultsList
    |> Result.map List.sort
