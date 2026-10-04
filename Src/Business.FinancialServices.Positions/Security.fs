module Business.FinancialServices.Positions.Security

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
open Business.FinancialServices.Positions.PositionsError
open Business.FinancialServices.Positions.PositionsComponent

type Security = private {
    securityId: SecurityId
    securityName: SecurityName
    ticker: Ticker option
    dimensionValues: Map<Dimension, DimensionValueId>
    createdAt: Instant
    modifiedAt: Instant
}

/// A dimension in dimensionValueUpdates is set to its value, or cleared with None; a dimension absent is unchanged.
type SecurityFieldUpdates = {
    securityIdToUpdate: SecurityId
    securityNameUpdate: FieldUpdate<SecurityName>
    tickerUpdate: FieldUpdate<Ticker option>
    dimensionValueUpdates: Map<Dimension, DimensionValueId option>
}

let securityId s = s.securityId
let securityName s = s.securityName
let ticker s = s.ticker
let dimensionValues s = s.dimensionValues
let dimensionValueIn (dimension: Dimension) s = s.dimensionValues |> Map.tryFind dimension
let createdAt s = s.createdAt
let modifiedAt s = s.modifiedAt

let create
    (securityId: SecurityId)
    (securityName: SecurityName)
    (ticker: Ticker option)
    (dimensionValues: Map<Dimension, DimensionValueId>)
    (createdAt: Instant)
    (modifiedAt: Instant)
    : Security =
    { securityId = securityId
      securityName = securityName
      ticker = ticker
      dimensionValues = dimensionValues
      createdAt = createdAt
      modifiedAt = modifiedAt }

let private dimensionParameter (dimension: Dimension) (valueId: DimensionValueId option) =
    let column = dimension |> Dimension.securityColumn
    column, { name = $"@{column}"; value = NullableUniqueId(valueId |> Option.map DimensionValueId.value) }

let persist (context: Context.Context) (security: Security) : Result<unit, IAppError> =
    let dimensionColumns =
        Dimension.all |> List.map (fun d -> dimensionParameter d (security.dimensionValues |> Map.tryFind d))
    let columnList = dimensionColumns |> List.map fst |> String.concat ", "
    let parameterList = dimensionColumns |> List.map (fun (_, p) -> p.name) |> String.concat ", "
    let queryStatement =
        $"""
        insert into positions.security(
            unique_id, security_name, ticker, {columnList}, created_at, modified_at)
        values (@unique_id, @security_name, @ticker, {parameterList}, @created_at, @modified_at);"""
    let parameters =
        [ { name = "@unique_id"; value = UniqueId(security.securityId |> SecurityId.value) }
          { name = "@security_name"; value = CharString(security.securityName |> SecurityName.value) }
          { name = "@ticker"; value = NullableCharString(security.ticker |> Option.map Ticker.value) }
          { name = "@created_at"; value = DbInstant security.createdAt }
          { name = "@modified_at"; value = DbInstant security.modifiedAt } ]
        @ (dimensionColumns |> List.map snd)
    executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters ExactlyOne

let private reconstitute raw =
    result {
        let uuid, nameStr, tickerStr, dimensionGuids, createdAt, modifiedAt = raw
        let! name = nameStr |> SecurityName.create
        let! ticker = tickerStr |> convertOptionToDesiredTypeWithFallibleConverter Ticker.create
        let dimensionValues =
            List.zip Dimension.all dimensionGuids
            |> List.choose (fun (dimension, guid) ->
                guid |> Option.map (fun g -> dimension, g |> DimensionValueId.fromGuid))
            |> Map.ofList
        return create (uuid |> SecurityId.fromGuid) name ticker dimensionValues createdAt modifiedAt
    }

let private mapRawForDbRead (row: RowReader) =
    (row |> RowReader.getUuid "unique_id"),
    (row |> RowReader.getString "security_name"),
    (row |> RowReader.getStringOption "ticker"),
    (Dimension.all |> List.map (fun d -> row |> RowReader.getUuidOption (d |> Dimension.securityColumn))),
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
    : Result<Security list, IAppError> =
    let from = "positions.security sec"
    let queryStatement = buildReadQuery cteList select from joinList predicate limit groupBy orderBy
    executeReaderQuery
        (context |> Context.getDatabaseTransaction) queryStatement parameters mapRawForDbRead reconstitute expectedRows

let private fetchAny
    (context: Context.Context)
    (predicate: string option)
    (parameters: QueryParameter list)
    (expectedRows: AcceptableExpectedRows)
    : Result<Security list, IAppError> =
    let dimensionColumns = Dimension.all |> List.map (fun d -> $"sec.{Dimension.securityColumn d}") |> String.concat ", "
    let select = $"sec.unique_id, sec.security_name, sec.ticker, {dimensionColumns}, sec.created_at, sec.modified_at"
    query context None select None predicate None None None parameters expectedRows

let fetchById (context: Context.Context) (securityId: SecurityId) : Result<Security, IAppError> =
    let uuid = securityId |> SecurityId.value
    fetchAny context (Some "sec.unique_id = @unique_id") [ { name = "@unique_id"; value = UniqueId uuid } ] ExactlyOne
    |> whenNoRows (PositionsSecurityIdDoesntExist uuid)
    |> Result.map List.head

let fetchAll (context: Context.Context) : Result<Security list, IAppError> =
    fetchAny context None [] AnyQuantityIsAcceptable

let fetchByName (context: Context.Context) (securityName: SecurityName) : Result<Security option, IAppError> =
    let parameters = [ { name = "@security_name"; value = CharString(securityName |> SecurityName.value) } ]
    fetchAny context (Some "sec.security_name = @security_name") parameters AnyQuantityIsAcceptable
    |> Result.map List.tryHead

let fetchByTicker (context: Context.Context) (ticker: Ticker) : Result<Security option, IAppError> =
    let parameters = [ { name = "@ticker"; value = CharString(ticker |> Ticker.value) } ]
    fetchAny context (Some "sec.ticker = @ticker") parameters AnyQuantityIsAcceptable
    |> Result.map List.tryHead

let update (context: Context.Context) (fieldUpdates: SecurityFieldUpdates) : Result<Security, IAppError> =
    let uuid = fieldUpdates.securityIdToUpdate |> SecurityId.value
    let dimensionUpdates =
        fieldUpdates.dimensionValueUpdates
        |> Map.toList
        |> List.map (fun (dimension, valueId) ->
            let column, parameter = dimensionParameter dimension valueId
            $"{column} = {parameter.name}", parameter)
    let updates =
        ([ fieldUpdates.securityNameUpdate
           |> mapNoChangeToOptionWithConversion (fun n ->
               ("security_name = @security_name", { name = "@security_name"; value = CharString(SecurityName.value n) }))
           fieldUpdates.tickerUpdate
           |> mapNoChangeToOptionWithConversion (fun t ->
               ("ticker = @ticker", { name = "@ticker"; value = NullableCharString(t |> Option.map Ticker.value) })) ]
         |> List.choose id)
        @ dimensionUpdates
    let setClauses = updates |> List.map fst |> String.concat ", "
    let parameters =
        [ { name = "@unique_id"; value = UniqueId uuid }
          { name = "@modified"; value = DbInstant(context |> Context.getInitiationInstant) } ]
        @ (updates |> List.map snd)
    let queryStatement =
        $"""
        update positions.security
        set {setClauses}, modified_at = @modified
        where unique_id = @unique_id;"""
    result {
        do! if updates |> List.isEmpty then error PositionsSecurityUpdateNoOp else Ok()
        do!
            executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters ExactlyOne
            |> whenNoRows (PositionsSecurityIdDoesntExist uuid)
        return! fieldUpdates.securityIdToUpdate |> fetchById context
    }

/// Route-lifetime lookups between a Security's ID and its name; see App.DataAccessLayer.LookupCache.
let nameToId = App.DataAccessLayer.LookupCache.stringToIdCache "positions.security" "security_name"
let idToName = App.DataAccessLayer.LookupCache.idToStringCache "positions.security" "security_name"
