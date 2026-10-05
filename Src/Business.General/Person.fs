module Business.General.Person

open System
open NodaTime
open App.Utility.IAppError
open App.Utility.Result
open App.Utility.FieldUpdate
open App.DataAccessLayer.DalError
open App.DataAccessLayer.QueryParameter
open App.DataAccessLayer.ExecuteReader
open App.DataAccessLayer.ExecuteNonQuery
open App.Session
open Business.General.BizGeneralError
open Business.General.PersonComponent

type Person = private {
    personId: PersonId
    personName: PersonName
    birthdate: LocalDate
    createdAt: Instant
    modifiedAt: Instant
}

type PersonFieldUpdates = {
    personIdToUpdate: PersonId
    personNameUpdate: FieldUpdate<PersonName>
    birthdateUpdate: FieldUpdate<LocalDate>
}

let personId p = p.personId
let personName p = p.personName
let birthdate p = p.birthdate
let createdAt p = p.createdAt
let modifiedAt p = p.modifiedAt

let create
    (personId: PersonId)
    (personName: PersonName)
    (birthdate: LocalDate)
    (createdAt: Instant)
    (modifiedAt: Instant)
    : Person =
    { personId = personId
      personName = personName
      birthdate = birthdate
      createdAt = createdAt
      modifiedAt = modifiedAt }

let persist (context: Context.Context) (person: Person) : Result<unit, IAppError> =
    let queryStatement =
        """
        insert into general.person(unique_id, person_name, birthdate, created_at, modified_at)
        values (@unique_id, @person_name, @birthdate, @created_at, @modified_at);"""
    let parameters =
        [ { name = "@unique_id"; value = UniqueId(person.personId |> PersonId.value) }
          { name = "@person_name"; value = CharString(person.personName |> PersonName.value) }
          { name = "@birthdate"; value = DbLocalDate(person.birthdate) }
          { name = "@created_at"; value = DbInstant(person.createdAt) }
          { name = "@modified_at"; value = DbInstant(person.modifiedAt) } ]
    executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters ExactlyOne

let private reconstitute raw =
    result {
        let uuid, personNameStr, birthdate, createdAt, modifiedAt = raw
        let! personName = personNameStr |> PersonName.create
        return create (uuid |> PersonId.fromGuid) personName birthdate createdAt modifiedAt
    }

let private mapRawForDbRead (row: RowReader) =
    (row |> RowReader.getUuid "unique_id"),
    (row |> RowReader.getString "person_name"),
    (row |> RowReader.getDate "birthdate"),
    (row |> RowReader.getInstant "created_at"),
    (row |> RowReader.getInstant "modified_at")

let query
    (context: Context.Context)
    (cteList: string list option)
    (select: string)
    (joinList: string list option)
    (predicate: string option)
    (limit: int option)
    (groupBy: string option)
    (orderBy: string option)
    (parameters: QueryParameter list)
    (expectedRows: AcceptableExpectedRows)
    : Result<Person list, IAppError> =
    let from = "general.person per"
    let queryStatement = buildReadQuery cteList select from joinList predicate limit groupBy orderBy
    executeReaderQuery
        (context |> Context.getDatabaseTransaction)
        queryStatement
        parameters
        mapRawForDbRead
        reconstitute
        expectedRows

let private fetchAny
    (context: Context.Context)
    (predicate: string option)
    (parameters: QueryParameter list)
    (expectedRows: AcceptableExpectedRows)
    : Result<Person list, IAppError> =
    let select = "per.unique_id, per.person_name, per.birthdate, per.created_at, per.modified_at"
    query context None select None predicate None None None parameters expectedRows

let fetchById (context: Context.Context) (personId: PersonId) : Result<Person, IAppError> =
    let uuid = personId |> PersonId.value
    fetchAny context (Some "per.unique_id = @unique_id") [ { name = "@unique_id"; value = UniqueId uuid } ] ExactlyOne
    |> whenNoRows (PersonIdDoesntExist uuid)
    |> Result.map List.head

let fetchByName (context: Context.Context) (personName: PersonName) : Result<Person option, IAppError> =
    let parameters = [ { name = "@person_name"; value = CharString(personName |> PersonName.value) } ]
    fetchAny context (Some "per.person_name = @person_name") parameters AnyQuantityIsAcceptable
    |> Result.map List.tryHead

let fetchAll (context: Context.Context) : Result<Person list, IAppError> =
    fetchAny context None [] AnyQuantityIsAcceptable

let update (context: Context.Context) (fieldUpdates: PersonFieldUpdates) : Result<Person, IAppError> =
    let uuid = fieldUpdates.personIdToUpdate |> PersonId.value
    let updates =
        [ fieldUpdates.personNameUpdate
          |> mapNoChangeToOptionWithConversion (fun n ->
              ("person_name = @person_name", { name = "@person_name"; value = CharString(PersonName.value n) }))
          fieldUpdates.birthdateUpdate
          |> mapNoChangeToOptionWithConversion (fun d ->
              ("birthdate = @birthdate", { name = "@birthdate"; value = DbLocalDate d })) ]
        |> List.choose id
    let setClauses = updates |> List.map fst |> String.concat ", "
    let parameters =
        [ { name = "@unique_id"; value = UniqueId uuid }
          { name = "@modified"; value = DbInstant(context |> Context.getInitiationInstant) } ]
        @ (updates |> List.map snd)
    let queryStatement =
        $"""
        update general.person
        set {setClauses}, modified_at = @modified
        where unique_id = @unique_id;"""
    result {
        do! if updates |> List.isEmpty then Error PersonUpdateNoOp else Ok()
        do!
            executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters ExactlyOne
            |> whenNoRows (PersonIdDoesntExist uuid)
        return! fieldUpdates.personIdToUpdate |> fetchById context
    }
