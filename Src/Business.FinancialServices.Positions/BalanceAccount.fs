module Business.FinancialServices.Positions.BalanceAccount

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
open Business.General.PersonComponent
open Business.FinancialServices.Positions.PositionsError
open Business.FinancialServices.Positions.PositionsComponent

type BalanceAccount = private {
    balanceAccountId: BalanceAccountId
    balanceAccountName: BalanceAccountName
    kind: BalanceAccountKind
    institution: Institution
    owners: Set<PersonId>
    createdAt: Instant
    modifiedAt: Instant
}

/// An ownersUpdate carries the complete new set. The kind is fixed at creation.
type BalanceAccountFieldUpdates = {
    balanceAccountIdToUpdate: BalanceAccountId
    balanceAccountNameUpdate: FieldUpdate<BalanceAccountName>
    institutionUpdate: FieldUpdate<Institution>
    ownersUpdate: FieldUpdate<Set<PersonId>>
}

let balanceAccountId b = b.balanceAccountId
let balanceAccountName b = b.balanceAccountName
let kind b = b.kind
let institution b = b.institution
let owners b = b.owners
let createdAt b = b.createdAt
let modifiedAt b = b.modifiedAt

let create
    (balanceAccountId: BalanceAccountId)
    (balanceAccountName: BalanceAccountName)
    (kind: BalanceAccountKind)
    (institution: Institution)
    (owners: Set<PersonId>)
    (createdAt: Instant)
    (modifiedAt: Instant)
    : BalanceAccount =
    { balanceAccountId = balanceAccountId
      balanceAccountName = balanceAccountName
      kind = kind
      institution = institution
      owners = owners
      createdAt = createdAt
      modifiedAt = modifiedAt }

let private balanceAccountParameter (balanceAccountId: BalanceAccountId) =
    { name = "@balance_account_id"; value = UniqueId(balanceAccountId |> BalanceAccountId.value) }

let private persistOwners
    (context: Context.Context)
    (balanceAccountId: BalanceAccountId)
    (owners: Set<PersonId>)
    : Result<unit, IAppError> =
    let queryStatement =
        """
        insert into positions.balance_account_owner(balance_account_id, person_id)
        values (@balance_account_id, @person_id);"""
    owners
    |> Set.toList
    |> List.map (fun personId ->
        let parameters =
            [ balanceAccountParameter balanceAccountId
              { name = "@person_id"; value = UniqueId(personId |> PersonId.value) } ]
        executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters ExactlyOne)
    |> convertListOfResultsToResultsList
    |> Result.map ignore

let private deleteOwners (context: Context.Context) (balanceAccountId: BalanceAccountId) : Result<unit, IAppError> =
    let queryStatement =
        """
        delete from positions.balance_account_owner
        where balance_account_id = @balance_account_id;"""
    executeNonQuery
        (context |> Context.getDatabaseTransaction)
        queryStatement
        [ balanceAccountParameter balanceAccountId ]
        AnyQuantityIsAcceptable

let persist (context: Context.Context) (balanceAccount: BalanceAccount) : Result<unit, IAppError> =
    let queryStatement =
        """
        insert into positions.balance_account(
            unique_id, balance_account_name, kind, institution, created_at, modified_at)
        values (@unique_id, @balance_account_name, @kind, @institution, @created_at, @modified_at);"""
    let parameters =
        [ { name = "@unique_id"; value = UniqueId(balanceAccount.balanceAccountId |> BalanceAccountId.value) }
          { name = "@balance_account_name"; value = CharString(balanceAccount.balanceAccountName |> BalanceAccountName.value) }
          { name = "@kind"; value = CharString(balanceAccount.kind |> BalanceAccountKind.toString) }
          { name = "@institution"; value = CharString(balanceAccount.institution |> Institution.value) }
          { name = "@created_at"; value = DbInstant balanceAccount.createdAt }
          { name = "@modified_at"; value = DbInstant balanceAccount.modifiedAt } ]
    result {
        do! executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters ExactlyOne
        do! persistOwners context balanceAccount.balanceAccountId balanceAccount.owners
    }

let private reconstitute raw =
    result {
        let uuid, nameStr, kindStr, institutionStr, ownerIds, createdAt, modifiedAt = raw
        let! name = nameStr |> BalanceAccountName.create
        let! kind = kindStr |> BalanceAccountKind.fromString
        let! institution = institutionStr |> Institution.create
        let! owners =
            match ownerIds with
            | Some (joined: string) -> Ok(joined.Split(',') |> Array.map (Guid.Parse >> PersonId.fromGuid) |> Set.ofArray)
            | None -> error (PositionsBalanceAccountHasNoOwners nameStr)
        return create (uuid |> BalanceAccountId.fromGuid) name kind institution owners createdAt modifiedAt
    }

let private mapRawForDbRead (row: RowReader) =
    (row |> RowReader.getUuid "unique_id"),
    (row |> RowReader.getString "balance_account_name"),
    (row |> RowReader.getString "kind"),
    (row |> RowReader.getString "institution"),
    (row |> RowReader.getStringOption "owner_ids"),
    (row |> RowReader.getInstant "created_at"),
    (row |> RowReader.getInstant "modified_at")

let private query
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
    : Result<BalanceAccount list, IAppError> =
    let from = "positions.balance_account bacc"
    let queryStatement = buildReadQuery cteList select from joinList predicate limit groupBy orderBy
    executeReaderQuery
        (context |> Context.getDatabaseTransaction) queryStatement parameters mapRawForDbRead reconstitute expectedRows

let private fetchAny
    (context: Context.Context)
    (predicate: string option)
    (parameters: QueryParameter list)
    (expectedRows: AcceptableExpectedRows)
    : Result<BalanceAccount list, IAppError> =
    let select =
        """
        bacc.unique_id, bacc.balance_account_name, bacc.kind, bacc.institution,
        (select string_agg(baow.person_id::text, ',' order by baow.person_id)
         from positions.balance_account_owner baow
         where baow.balance_account_id = bacc.unique_id) as owner_ids,
        bacc.created_at, bacc.modified_at"""
    query context None select None predicate None None None parameters expectedRows

let fetchById (context: Context.Context) (balanceAccountId: BalanceAccountId) : Result<BalanceAccount, IAppError> =
    let uuid = balanceAccountId |> BalanceAccountId.value
    fetchAny context (Some "bacc.unique_id = @unique_id") [ { name = "@unique_id"; value = UniqueId uuid } ] ExactlyOne
    |> whenNoRows (PositionsBalanceAccountIdDoesntExist uuid)
    |> Result.map List.head

let fetchAll (context: Context.Context) : Result<BalanceAccount list, IAppError> =
    fetchAny context None [] AnyQuantityIsAcceptable

let fetchByName
    (context: Context.Context)
    (balanceAccountName: BalanceAccountName)
    : Result<BalanceAccount option, IAppError> =
    let parameters =
        [ { name = "@balance_account_name"; value = CharString(balanceAccountName |> BalanceAccountName.value) } ]
    fetchAny context (Some "bacc.balance_account_name = @balance_account_name") parameters AnyQuantityIsAcceptable
    |> Result.map List.tryHead

let update (context: Context.Context) (fieldUpdates: BalanceAccountFieldUpdates) : Result<BalanceAccount, IAppError> =
    let balanceAccountId = fieldUpdates.balanceAccountIdToUpdate
    let uuid = balanceAccountId |> BalanceAccountId.value
    let columnUpdates =
        [ fieldUpdates.balanceAccountNameUpdate
          |> mapNoChangeToOptionWithConversion (fun n ->
              "balance_account_name = @balance_account_name",
              { name = "@balance_account_name"; value = CharString(BalanceAccountName.value n) })
          fieldUpdates.institutionUpdate
          |> mapNoChangeToOptionWithConversion (fun i ->
              "institution = @institution", { name = "@institution"; value = CharString(Institution.value i) }) ]
        |> List.choose id
    let ownersUpdate = fieldUpdates.ownersUpdate |> mapNoChangeToOptionWithConversion id
    let setClauses = columnUpdates |> List.map (fun (clause, _) -> $"{clause}, ") |> String.concat ""
    let parameters =
        [ { name = "@unique_id"; value = UniqueId uuid }
          { name = "@modified"; value = DbInstant(context |> Context.getInitiationInstant) } ]
        @ (columnUpdates |> List.map snd)
    let queryStatement =
        $"""
        update positions.balance_account
        set {setClauses}modified_at = @modified
        where unique_id = @unique_id;"""
    result {
        do!
            if columnUpdates |> List.isEmpty && ownersUpdate |> Option.isNone then error PositionsBalanceAccountUpdateNoOp
            else Ok()
        do!
            executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters ExactlyOne
            |> whenNoRows (PositionsBalanceAccountIdDoesntExist uuid)
        do!
            match ownersUpdate with
            | Some newOwners ->
                deleteOwners context balanceAccountId
                |> Result.bind (fun () -> persistOwners context balanceAccountId newOwners)
            | None -> Ok()
        return! balanceAccountId |> fetchById context
    }

/// Removes the Balance Account with its owners.
let delete (context: Context.Context) (balanceAccountId: BalanceAccountId) : Result<unit, IAppError> =
    let queryStatement =
        """
        delete from positions.balance_account
        where unique_id = @balance_account_id;"""
    result {
        do! deleteOwners context balanceAccountId
        do!
            executeNonQuery
                (context |> Context.getDatabaseTransaction)
                queryStatement
                [ balanceAccountParameter balanceAccountId ]
                ExactlyOne
            |> whenNoRows (PositionsBalanceAccountIdDoesntExist(balanceAccountId |> BalanceAccountId.value))
    }

let private idToNameCache =
    App.DataAccessLayer.LookupCache.idToStringCache "positions.balance_account" "balance_account_name"

/// Route-lifetime lookup from an ID to a balance account's name; see App.DataAccessLayer.LookupCache.
let idToName (context: Context.Context) (id: BalanceAccountId) : Result<BalanceAccountName, IAppError> =
    id |> BalanceAccountId.value
    |> idToNameCache.fetch (context |> Context.getDatabaseTransaction)
    |> Result.bind BalanceAccountName.create
