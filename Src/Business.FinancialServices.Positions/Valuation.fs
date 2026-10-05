module Business.FinancialServices.Positions.Valuation

open NodaTime
open App.Utility.IAppError
open App.Utility.Result
open App.DataAccessLayer.QueryParameter
open App.DataAccessLayer.ExecuteReader
open App.DataAccessLayer.ExecuteNonQuery
open App.Session
open Business.FinancialServices
open Business.FinancialServices.Positions.PositionsComponent

type Valuation = private {
    valuationId: ValuationId
    propertyId: PropertyId
    valuationDate: LocalDate
    valuationValue: ValuationValue
    valuationBasis: ValuationBasis
    createdAt: Instant
    modifiedAt: Instant
}

let valuationId v = v.valuationId
let propertyId v = v.propertyId
let valuationDate v = v.valuationDate
let valuationValue v = v.valuationValue
let valuationBasis v = v.valuationBasis
let createdAt v = v.createdAt
let modifiedAt v = v.modifiedAt

let create
    (valuationId: ValuationId)
    (propertyId: PropertyId)
    (valuationDate: LocalDate)
    (valuationValue: ValuationValue)
    (valuationBasis: ValuationBasis)
    (createdAt: Instant)
    (modifiedAt: Instant)
    : Valuation =
    { valuationId = valuationId
      propertyId = propertyId
      valuationDate = valuationDate
      valuationValue = valuationValue
      valuationBasis = valuationBasis
      createdAt = createdAt
      modifiedAt = modifiedAt }

/// A Property's value on a date and where it came from: its latest Valuation dated on or before the date, else its
/// purchase basis.
let valueOn
    (date: LocalDate)
    (property: Property.Property)
    (valuations: Valuation list)
    : Money.Money * PropertyValueSource =
    valuations
    |> List.filter (fun v -> v.propertyId = (property |> Property.propertyId) && v.valuationDate <= date)
    |> List.sortByDescending (fun v -> v.valuationDate)
    |> List.tryHead
    |> function
        | Some latest -> (latest.valuationValue |> ValuationValue.value), ValuationDated latest.valuationDate
        | None -> (property |> Property.purchaseBasis |> PurchaseBasis.value), PurchaseBasisValue

let persist (context: Context.Context) (valuation: Valuation) : Result<unit, IAppError> =
    let queryStatement =
        """
        insert into positions.valuation(
            unique_id, property_id, valuation_date, valuation_value, basis, created_at, modified_at)
        values (@unique_id, @property_id, @valuation_date, @valuation_value, @basis, @created_at, @modified_at);"""
    let parameters =
        [ { name = "@unique_id"; value = UniqueId(valuation.valuationId |> ValuationId.value) }
          { name = "@property_id"; value = UniqueId(valuation.propertyId |> PropertyId.value) }
          { name = "@valuation_date"; value = DbLocalDate valuation.valuationDate }
          { name = "@valuation_value"; value = Numeric(valuation.valuationValue |> ValuationValue.value |> Money.amount) }
          { name = "@basis"; value = CharString(valuation.valuationBasis |> ValuationBasis.value) }
          { name = "@created_at"; value = DbInstant valuation.createdAt }
          { name = "@modified_at"; value = DbInstant valuation.modifiedAt } ]
    executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters ExactlyOne

/// Overwrites the value and basis of the Valuation already stored under this one's ID.
let replace (context: Context.Context) (valuation: Valuation) : Result<unit, IAppError> =
    let queryStatement =
        """
        update positions.valuation
        set valuation_value = @valuation_value, basis = @basis, modified_at = @modified_at
        where unique_id = @unique_id;"""
    let parameters =
        [ { name = "@unique_id"; value = UniqueId(valuation.valuationId |> ValuationId.value) }
          { name = "@valuation_value"; value = Numeric(valuation.valuationValue |> ValuationValue.value |> Money.amount) }
          { name = "@basis"; value = CharString(valuation.valuationBasis |> ValuationBasis.value) }
          { name = "@modified_at"; value = DbInstant valuation.modifiedAt } ]
    executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters ExactlyOne

let delete (context: Context.Context) (valuationId: ValuationId) : Result<unit, IAppError> =
    let queryStatement =
        """
        delete from positions.valuation
        where unique_id = @unique_id;"""
    let parameters = [ { name = "@unique_id"; value = UniqueId(valuationId |> ValuationId.value) } ]
    executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters ExactlyOne

let private reconstitute raw =
    result {
        let uuid, propertyId, valuationDate, valueRaw, basisStr, createdAt, modifiedAt = raw
        let! value = valueRaw |> Money.fromDecimal |> Result.bind ValuationValue.create
        let! basis = basisStr |> ValuationBasis.create
        return
            create
                (uuid |> ValuationId.fromGuid)
                (propertyId |> PropertyId.fromGuid)
                valuationDate
                value
                basis
                createdAt
                modifiedAt
    }

let private mapRawForDbRead (row: RowReader) =
    (row |> RowReader.getUuid "unique_id"),
    (row |> RowReader.getUuid "property_id"),
    (row |> RowReader.getDate "valuation_date"),
    (row |> RowReader.getNumeric "valuation_value"),
    (row |> RowReader.getString "basis"),
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
    : Result<Valuation list, IAppError> =
    let from = "positions.valuation val"
    let queryStatement = buildReadQuery cteList select from joinList predicate limit groupBy orderBy
    executeReaderQuery
        (context |> Context.getDatabaseTransaction) queryStatement parameters mapRawForDbRead reconstitute expectedRows

let private fetchAny
    (context: Context.Context)
    (predicate: string option)
    (parameters: QueryParameter list)
    (expectedRows: AcceptableExpectedRows)
    : Result<Valuation list, IAppError> =
    let select =
        "val.unique_id, val.property_id, val.valuation_date, val.valuation_value, val.basis, val.created_at, val.modified_at"
    query context None select None predicate None None None parameters expectedRows
    |> Result.map (List.sortBy (fun v -> v.valuationDate))

let fetchAll (context: Context.Context) : Result<Valuation list, IAppError> =
    fetchAny context None [] AnyQuantityIsAcceptable

let fetchByProperty (context: Context.Context) (propertyId: PropertyId) : Result<Valuation list, IAppError> =
    let parameters = [ { name = "@property_id"; value = UniqueId(propertyId |> PropertyId.value) } ]
    fetchAny context (Some "val.property_id = @property_id") parameters AnyQuantityIsAcceptable

let fetchByPropertyAndDate
    (context: Context.Context)
    (propertyId: PropertyId)
    (valuationDate: LocalDate)
    : Result<Valuation option, IAppError> =
    let parameters =
        [ { name = "@property_id"; value = UniqueId(propertyId |> PropertyId.value) }
          { name = "@valuation_date"; value = DbLocalDate valuationDate } ]
    fetchAny context (Some "val.property_id = @property_id and val.valuation_date = @valuation_date") parameters AnyQuantityIsAcceptable
    |> Result.map List.tryHead
