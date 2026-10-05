module Business.FinancialServices.Positions.DimensionValue

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

type DimensionValue = private {
    dimensionValueId: DimensionValueId
    dimension: Dimension
    dimensionValueName: DimensionValueName
    createdAt: Instant
    modifiedAt: Instant
}

type DimensionValueFieldUpdates = {
    dimensionValueIdToUpdate: DimensionValueId
    dimensionValueNameUpdate: FieldUpdate<DimensionValueName>
}

let dimensionValueId d = d.dimensionValueId
let dimension d = d.dimension
let dimensionValueName d = d.dimensionValueName
let createdAt d = d.createdAt
let modifiedAt d = d.modifiedAt

let create
    (dimensionValueId: DimensionValueId)
    (dimension: Dimension)
    (dimensionValueName: DimensionValueName)
    (createdAt: Instant)
    (modifiedAt: Instant)
    : DimensionValue =
    { dimensionValueId = dimensionValueId
      dimension = dimension
      dimensionValueName = dimensionValueName
      createdAt = createdAt
      modifiedAt = modifiedAt }

let persist (context: Context.Context) (dimensionValue: DimensionValue) : Result<unit, IAppError> =
    let queryStatement =
        """
        insert into positions.dimension_value(unique_id, dimension, value_name, created_at, modified_at)
        values (@unique_id, @dimension, @value_name, @created_at, @modified_at);"""
    let parameters =
        [ { name = "@unique_id"; value = UniqueId(dimensionValue.dimensionValueId |> DimensionValueId.value) }
          { name = "@dimension"; value = CharString(dimensionValue.dimension |> Dimension.toString) }
          { name = "@value_name"; value = CharString(dimensionValue.dimensionValueName |> DimensionValueName.value) }
          { name = "@created_at"; value = DbInstant dimensionValue.createdAt }
          { name = "@modified_at"; value = DbInstant dimensionValue.modifiedAt } ]
    executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters ExactlyOne

let private reconstitute raw =
    result {
        let uuid, dimensionStr, nameStr, createdAt, modifiedAt = raw
        let! dimension = dimensionStr |> Dimension.fromString
        let! name = nameStr |> DimensionValueName.create
        return create (uuid |> DimensionValueId.fromGuid) dimension name createdAt modifiedAt
    }

let private mapRawForDbRead (row: RowReader) =
    (row |> RowReader.getUuid "unique_id"),
    (row |> RowReader.getString "dimension"),
    (row |> RowReader.getString "value_name"),
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
    : Result<DimensionValue list, IAppError> =
    let from = "positions.dimension_value dv"
    let queryStatement = buildReadQuery cteList select from joinList predicate limit groupBy orderBy
    executeReaderQuery
        (context |> Context.getDatabaseTransaction) queryStatement parameters mapRawForDbRead reconstitute expectedRows

let private fetchAny
    (context: Context.Context)
    (predicate: string option)
    (parameters: QueryParameter list)
    (expectedRows: AcceptableExpectedRows)
    : Result<DimensionValue list, IAppError> =
    let select = "dv.unique_id, dv.dimension, dv.value_name, dv.created_at, dv.modified_at"
    query context None select None predicate None None None parameters expectedRows

let fetchById (context: Context.Context) (dimensionValueId: DimensionValueId) : Result<DimensionValue, IAppError> =
    let uuid = dimensionValueId |> DimensionValueId.value
    fetchAny context (Some "dv.unique_id = @unique_id") [ { name = "@unique_id"; value = UniqueId uuid } ] ExactlyOne
    |> whenNoRows (PositionsDimensionValueIdDoesntExist uuid)
    |> Result.map List.head

let fetchAll (context: Context.Context) : Result<DimensionValue list, IAppError> =
    fetchAny context None [] AnyQuantityIsAcceptable

let fetchByDimension (context: Context.Context) (dimension: Dimension) : Result<DimensionValue list, IAppError> =
    let parameters = [ { name = "@dimension"; value = CharString(dimension |> Dimension.toString) } ]
    fetchAny context (Some "dv.dimension = @dimension") parameters AnyQuantityIsAcceptable

let fetchByDimensionAndName
    (context: Context.Context)
    (dimension: Dimension)
    (dimensionValueName: DimensionValueName)
    : Result<DimensionValue option, IAppError> =
    let parameters =
        [ { name = "@dimension"; value = CharString(dimension |> Dimension.toString) }
          { name = "@value_name"; value = CharString(dimensionValueName |> DimensionValueName.value) } ]
    fetchAny context (Some "dv.dimension = @dimension and dv.value_name = @value_name") parameters AnyQuantityIsAcceptable
    |> Result.map List.tryHead

let update (context: Context.Context) (fieldUpdates: DimensionValueFieldUpdates) : Result<DimensionValue, IAppError> =
    let uuid = fieldUpdates.dimensionValueIdToUpdate |> DimensionValueId.value
    let updates =
        [
           fieldUpdates.dimensionValueNameUpdate
           |> mapNoChangeToOptionWithConversion (fun v ->
               ("value_name = @value_name", { name = "@value_name"; value = CharString(DimensionValueName.value v) })) ]
        |> List.choose id
    let setClauses = updates |> List.map fst |> String.concat ", "
    let parameters =
        [ { name = "@unique_id"; value = UniqueId uuid }
          { name = "@modified"; value = DbInstant(context |> Context.getInitiationInstant) } ]
        @ (updates |> List.map snd)
    let queryStatement =
        $"""
        update positions.dimension_value
        set {setClauses}, modified_at = @modified
        where unique_id = @unique_id;"""
    result {
        do! if updates |> List.isEmpty then error PositionsDimensionValueUpdateNoOp else Ok()
        do!
            executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters ExactlyOne
            |> whenNoRows (PositionsDimensionValueIdDoesntExist uuid)
        return! fieldUpdates.dimensionValueIdToUpdate |> fetchById context
    }
