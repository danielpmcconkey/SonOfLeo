module Business.FinancialServices.Positions.Property

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
open Business.General.Person
open Business.FinancialServices
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Positions.PositionsError
open Business.FinancialServices.Positions.PositionsComponent

type Property = private {
    propertyId: PropertyId
    propertyName: PropertyName
    propertyUse: PropertyUse
    ownedPeriod: OwnedPeriod
    purchaseBasis: PurchaseBasis
    assetAccountIds: Set<AccountId>
    owners: Set<PersonId>
    mortgageAccountIds: Set<AccountId>
    createdAt: Instant
    modifiedAt: Instant
}

/// An ownersUpdate, assetAccountIdsUpdate or mortgageAccountIdsUpdate carries the complete new set.
type PropertyFieldUpdates = {
    propertyIdToUpdate: PropertyId
    propertyNameUpdate: FieldUpdate<PropertyName>
    propertyUseUpdate: FieldUpdate<PropertyUse>
    ownedPeriodUpdate: FieldUpdate<OwnedPeriod>
    purchaseBasisUpdate: FieldUpdate<PurchaseBasis>
    assetAccountIdsUpdate: FieldUpdate<Set<AccountId>>
    ownersUpdate: FieldUpdate<Set<PersonId>>
    mortgageAccountIdsUpdate: FieldUpdate<Set<AccountId>>
}

let propertyId p = p.propertyId
let propertyName p = p.propertyName
let propertyUse p = p.propertyUse
let ownedPeriod p = p.ownedPeriod
let purchaseBasis p = p.purchaseBasis
let assetAccountIds p = p.assetAccountIds
let owners p = p.owners
let mortgageAccountIds p = p.mortgageAccountIds
let createdAt p = p.createdAt
let modifiedAt p = p.modifiedAt

let create
    (propertyId: PropertyId)
    (propertyName: PropertyName)
    (propertyUse: PropertyUse)
    (ownedPeriod: OwnedPeriod)
    (purchaseBasis: PurchaseBasis)
    (assetAccountIds: Set<AccountId>)
    (owners: Set<PersonId>)
    (mortgageAccountIds: Set<AccountId>)
    (createdAt: Instant)
    (modifiedAt: Instant)
    : Property =
    { propertyId = propertyId
      propertyName = propertyName
      propertyUse = propertyUse
      ownedPeriod = ownedPeriod
      purchaseBasis = purchaseBasis
      assetAccountIds = assetAccountIds
      owners = owners
      mortgageAccountIds = mortgageAccountIds
      createdAt = createdAt
      modifiedAt = modifiedAt }

let private propertyParameter (propertyId: PropertyId) =
    { name = "@property_id"; value = UniqueId(propertyId |> PropertyId.value) }

let private persistChildren
    (context: Context.Context)
    (table: string)
    (column: string)
    (propertyId: PropertyId)
    (childIds: Set<Guid>)
    : Result<unit, IAppError> =
    let queryStatement =
        $"""
        insert into positions.{table}(property_id, {column})
        values (@property_id, @child_id);"""
    childIds
    |> Set.toList
    |> List.map (fun childId ->
        let parameters = [ propertyParameter propertyId; { name = "@child_id"; value = UniqueId childId } ]
        executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters ExactlyOne)
    |> convertListOfResultsToResultsList
    |> Result.map ignore

let private deleteChildren (context: Context.Context) (table: string) (propertyId: PropertyId) : Result<unit, IAppError> =
    let queryStatement =
        $"""
        delete from positions.{table}
        where property_id = @property_id;"""
    executeNonQuery
        (context |> Context.getDatabaseTransaction) queryStatement [ propertyParameter propertyId ] AnyQuantityIsAcceptable

let private persistOwners context propertyId (owners: Set<PersonId>) =
    owners |> Set.map PersonId.value |> persistChildren context "property_owner" "person_id" propertyId

let private persistAssetAccounts context propertyId (accountIds: Set<AccountId>) =
    accountIds
    |> Set.map AccountId.value
    |> persistChildren context "property_asset_account" "ledger_account_id" propertyId

let private persistMortgageAccounts context propertyId (accountIds: Set<AccountId>) =
    accountIds
    |> Set.map AccountId.value
    |> persistChildren context "property_mortgage_account" "ledger_account_id" propertyId

let persist (context: Context.Context) (property: Property) : Result<unit, IAppError> =
    let queryStatement =
        """
        insert into positions.property(
            unique_id, property_name, property_use, acquisition_date, disposal_date, purchase_basis,
            created_at, modified_at)
        values (
            @unique_id, @property_name, @property_use, @acquisition_date, @disposal_date, @purchase_basis,
            @created_at, @modified_at);"""
    let parameters =
        [ { name = "@unique_id"; value = UniqueId(property.propertyId |> PropertyId.value) }
          { name = "@property_name"; value = CharString(property.propertyName |> PropertyName.value) }
          { name = "@property_use"; value = CharString(property.propertyUse |> PropertyUse.toString) }
          { name = "@acquisition_date"; value = DbLocalDate(property.ownedPeriod |> OwnedPeriod.acquisitionDate) }
          { name = "@disposal_date"; value = NullableDbLocalDate(property.ownedPeriod |> OwnedPeriod.disposalDate) }
          { name = "@purchase_basis"; value = Numeric(property.purchaseBasis |> PurchaseBasis.value |> Money.amount) }
          { name = "@created_at"; value = DbInstant property.createdAt }
          { name = "@modified_at"; value = DbInstant property.modifiedAt } ]
    result {
        do! executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters ExactlyOne
        do! persistOwners context property.propertyId property.owners
        do! persistAssetAccounts context property.propertyId property.assetAccountIds
        do! persistMortgageAccounts context property.propertyId property.mortgageAccountIds
    }

let private parseIdSet (raw: string option) : Set<Guid> =
    match raw with
    | None -> Set.empty
    | Some joined -> joined.Split(',') |> Array.map Guid.Parse |> Set.ofArray

let private reconstitute raw =
    result {
        let uuid, nameStr, useStr, acquisitionDate, disposalDate, purchaseBasisRaw, assetIds, ownerIds, mortgageIds,
            createdAt, modifiedAt = raw
        let! name = nameStr |> PropertyName.create
        let! propertyUse = useStr |> PropertyUse.fromString
        let! ownedPeriod = OwnedPeriod.create acquisitionDate disposalDate
        let! purchaseBasis = purchaseBasisRaw |> Money.fromDecimal |> Result.bind PurchaseBasis.create
        do! if ownerIds |> Option.isNone then error (PositionsPropertyHasNoOwners nameStr) else Ok()
        return
            create
                (uuid |> PropertyId.fromGuid)
                name
                propertyUse
                ownedPeriod
                purchaseBasis
                (assetIds |> parseIdSet |> Set.map AccountId.fromGuid)
                (ownerIds |> parseIdSet |> Set.map PersonId.fromGuid)
                (mortgageIds |> parseIdSet |> Set.map AccountId.fromGuid)
                createdAt
                modifiedAt
    }

let private mapRawForDbRead (row: RowReader) =
    (row |> RowReader.getUuid "unique_id"),
    (row |> RowReader.getString "property_name"),
    (row |> RowReader.getString "property_use"),
    (row |> RowReader.getDate "acquisition_date"),
    (row |> RowReader.getDateOption "disposal_date"),
    (row |> RowReader.getNumeric "purchase_basis"),
    (row |> RowReader.getStringOption "asset_account_ids"),
    (row |> RowReader.getStringOption "owner_ids"),
    (row |> RowReader.getStringOption "mortgage_account_ids"),
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
    : Result<Property list, IAppError> =
    let from = "positions.property prop"
    let queryStatement = buildReadQuery cteList select from joinList predicate limit groupBy orderBy
    executeReaderQuery
        (context |> Context.getDatabaseTransaction) queryStatement parameters mapRawForDbRead reconstitute expectedRows

let private fetchAny
    (context: Context.Context)
    (predicate: string option)
    (parameters: QueryParameter list)
    (expectedRows: AcceptableExpectedRows)
    : Result<Property list, IAppError> =
    let select =
        """
        prop.unique_id, prop.property_name, prop.property_use, prop.acquisition_date, prop.disposal_date,
        prop.purchase_basis,
        (select string_agg(paa.ledger_account_id::text, ',' order by paa.ledger_account_id)
         from positions.property_asset_account paa
         where paa.property_id = prop.unique_id) as asset_account_ids,
        (select string_agg(prow.person_id::text, ',' order by prow.person_id)
         from positions.property_owner prow
         where prow.property_id = prop.unique_id) as owner_ids,
        (select string_agg(pma.ledger_account_id::text, ',' order by pma.ledger_account_id)
         from positions.property_mortgage_account pma
         where pma.property_id = prop.unique_id) as mortgage_account_ids,
        prop.created_at, prop.modified_at"""
    query context None select None predicate None None None parameters expectedRows

let fetchById (context: Context.Context) (propertyId: PropertyId) : Result<Property, IAppError> =
    let uuid = propertyId |> PropertyId.value
    fetchAny context (Some "prop.unique_id = @unique_id") [ { name = "@unique_id"; value = UniqueId uuid } ] ExactlyOne
    |> whenNoRows (PositionsPropertyIdDoesntExist uuid)
    |> Result.map List.head

let fetchAll (context: Context.Context) : Result<Property list, IAppError> =
    fetchAny context None [] AnyQuantityIsAcceptable

let fetchByName (context: Context.Context) (propertyName: PropertyName) : Result<Property option, IAppError> =
    let parameters = [ { name = "@property_name"; value = CharString(propertyName |> PropertyName.value) } ]
    fetchAny context (Some "prop.property_name = @property_name") parameters AnyQuantityIsAcceptable
    |> Result.map List.tryHead

let fetchByAssetAccountId (context: Context.Context) (accountId: AccountId) : Result<Property option, IAppError> =
    let parameters = [ { name = "@ledger_account_id"; value = UniqueId(accountId |> AccountId.value) } ]
    let predicate =
        """
        exists (select 1 from positions.property_asset_account paaf
                where paaf.property_id = prop.unique_id and paaf.ledger_account_id = @ledger_account_id)"""
    fetchAny context (Some predicate) parameters AnyQuantityIsAcceptable
    |> Result.map List.tryHead

let fetchByMortgageAccountId (context: Context.Context) (accountId: AccountId) : Result<Property option, IAppError> =
    let parameters = [ { name = "@ledger_account_id"; value = UniqueId(accountId |> AccountId.value) } ]
    let predicate =
        """
        exists (select 1 from positions.property_mortgage_account pmaf
                where pmaf.property_id = prop.unique_id and pmaf.ledger_account_id = @ledger_account_id)"""
    fetchAny context (Some predicate) parameters AnyQuantityIsAcceptable
    |> Result.map List.tryHead

let update (context: Context.Context) (fieldUpdates: PropertyFieldUpdates) : Result<Property, IAppError> =
    let propertyId = fieldUpdates.propertyIdToUpdate
    let uuid = propertyId |> PropertyId.value
    let columnUpdates =
        [ fieldUpdates.propertyNameUpdate
          |> mapNoChangeToOptionWithConversion (fun n ->
              [ "property_name = @property_name", { name = "@property_name"; value = CharString(PropertyName.value n) } ])
          fieldUpdates.propertyUseUpdate
          |> mapNoChangeToOptionWithConversion (fun u ->
              [ "property_use = @property_use", { name = "@property_use"; value = CharString(PropertyUse.toString u) } ])
          fieldUpdates.ownedPeriodUpdate
          |> mapNoChangeToOptionWithConversion (fun p ->
              [ "acquisition_date = @acquisition_date",
                { name = "@acquisition_date"; value = DbLocalDate(OwnedPeriod.acquisitionDate p) }
                "disposal_date = @disposal_date",
                { name = "@disposal_date"; value = NullableDbLocalDate(OwnedPeriod.disposalDate p) } ])
          fieldUpdates.purchaseBasisUpdate
          |> mapNoChangeToOptionWithConversion (fun b ->
              [ "purchase_basis = @purchase_basis",
                { name = "@purchase_basis"; value = Numeric(b |> PurchaseBasis.value |> Money.amount) } ]) ]
        |> List.choose id
        |> List.concat
    let ownersUpdate = fieldUpdates.ownersUpdate |> mapNoChangeToOptionWithConversion id
    let assetUpdate = fieldUpdates.assetAccountIdsUpdate |> mapNoChangeToOptionWithConversion id
    let mortgageUpdate = fieldUpdates.mortgageAccountIdsUpdate |> mapNoChangeToOptionWithConversion id
    let setClauses = columnUpdates |> List.map (fun (clause, _) -> $"{clause}, ") |> String.concat ""
    let parameters =
        [ { name = "@unique_id"; value = UniqueId uuid }
          { name = "@modified"; value = DbInstant(context |> Context.getInitiationInstant) } ]
        @ (columnUpdates |> List.map snd)
    let queryStatement =
        $"""
        update positions.property
        set {setClauses}modified_at = @modified
        where unique_id = @unique_id;"""
    result {
        do!
            if columnUpdates |> List.isEmpty
               && ownersUpdate |> Option.isNone
               && assetUpdate |> Option.isNone
               && mortgageUpdate |> Option.isNone then
                error PositionsPropertyUpdateNoOp
            else
                Ok()
        do!
            executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters ExactlyOne
            |> whenNoRows (PositionsPropertyIdDoesntExist uuid)
        do!
            match ownersUpdate with
            | Some newOwners ->
                deleteChildren context "property_owner" propertyId
                |> Result.bind (fun () -> persistOwners context propertyId newOwners)
            | None -> Ok()
        do!
            match assetUpdate with
            | Some newAssetAccounts ->
                deleteChildren context "property_asset_account" propertyId
                |> Result.bind (fun () -> persistAssetAccounts context propertyId newAssetAccounts)
            | None -> Ok()
        do!
            match mortgageUpdate with
            | Some newMortgageAccounts ->
                deleteChildren context "property_mortgage_account" propertyId
                |> Result.bind (fun () -> persistMortgageAccounts context propertyId newMortgageAccounts)
            | None -> Ok()
        return! propertyId |> fetchById context
    }

/// Removes the Property with its owners and ledger links.
let delete (context: Context.Context) (propertyId: PropertyId) : Result<unit, IAppError> =
    let queryStatement =
        """
        delete from positions.property
        where unique_id = @property_id;"""
    result {
        do! deleteChildren context "property_owner" propertyId
        do! deleteChildren context "property_asset_account" propertyId
        do! deleteChildren context "property_mortgage_account" propertyId
        do!
            executeNonQuery
                (context |> Context.getDatabaseTransaction) queryStatement [ propertyParameter propertyId ] ExactlyOne
            |> whenNoRows (PositionsPropertyIdDoesntExist(propertyId |> PropertyId.value))
    }
