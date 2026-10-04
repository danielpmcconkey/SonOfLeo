module Business.FinancialServices.Positions.AccountSnapshotLine

open System
open NodaTime
open App.Utility.IAppError
open App.Utility.Result
open App.DataAccessLayer.QueryParameter
open App.DataAccessLayer.ExecuteReader
open App.DataAccessLayer.ExecuteNonQuery
open App.Session
open Business.FinancialServices
open Business.FinancialServices.Positions.PositionsError
open Business.FinancialServices.Positions.PositionsComponent

type AccountSnapshotLine = private {
    accountSnapshotLineId: AccountSnapshotLineId
    accountSnapshotId: AccountSnapshotId
    holdingId: HoldingId
    quantity: Quantity.Quantity
    price: Price.Price
    marketValue: Money.Money
    reportedCostBasis: Money.Money option
}

let accountSnapshotLineId l = l.accountSnapshotLineId
let accountSnapshotId l = l.accountSnapshotId
let holdingId l = l.holdingId
let quantity l = l.quantity
let price l = l.price
let marketValue l = l.marketValue
let reportedCostBasis l = l.reportedCostBasis

let create
    (accountSnapshotLineId: AccountSnapshotLineId)
    (accountSnapshotId: AccountSnapshotId)
    (holdingId: HoldingId)
    (quantity: Quantity.Quantity)
    (price: Price.Price)
    (marketValue: Money.Money)
    (reportedCostBasis: Money.Money option)
    : AccountSnapshotLine =
    { accountSnapshotLineId = accountSnapshotLineId
      accountSnapshotId = accountSnapshotId
      holdingId = holdingId
      quantity = quantity
      price = price
      marketValue = marketValue
      reportedCostBasis = reportedCostBasis }

let tolerance = 0.05M

/// Checks one line's reported figures against each other. The figures are recorded verbatim, so nothing is derived
/// or corrected here; a line that doesn't hold together is refused. The labels only name the line in the error.
let checkFigures
    (accountName: string)
    (snapshotDate: LocalDate)
    (securityName: string)
    (quantity: Quantity.Quantity)
    (price: Price.Price)
    (marketValue: Money.Money)
    (reportedCostBasis: Money.Money option)
    : Result<unit, IAppError> =
    // The product is compared as a decimal, not as Money: it carries up to twelve places and can exceed Money's range,
    // and rounding it into Money first could hide a difference just over the tolerance.
    let product = Price.multiplyQuantity quantity price
    let marketValueAmount = marketValue |> Money.amount
    if not (quantity |> Quantity.isPositive) then
        error (PositionsSnapshotLineQuantityNotPositive(accountName, snapshotDate, securityName))
    elif marketValue |> Money.isNegative then
        error (PositionsSnapshotLineMarketValueNegative(accountName, snapshotDate, securityName, marketValueAmount))
    else
        match reportedCostBasis with
        | Some costBasis when costBasis |> Money.isNegative ->
            error (
                PositionsSnapshotLineCostBasisNegative(
                    accountName, snapshotDate, securityName, costBasis |> Money.amount
                )
            )
        | _ when Math.Abs(product - marketValueAmount) > tolerance ->
            error (
                PositionsSnapshotLineOutsideTolerance(accountName, snapshotDate, securityName, product, marketValueAmount)
            )
        | _ -> Ok()

let persist (context: Context.Context) (line: AccountSnapshotLine) : Result<unit, IAppError> =
    let queryStatement =
        """
        insert into positions.account_snapshot_line(
            unique_id, account_snapshot_id, holding_id, quantity, price, market_value, reported_cost_basis)
        values (
            @unique_id, @account_snapshot_id, @holding_id, @quantity, @price, @market_value, @reported_cost_basis);"""
    let parameters =
        [ { name = "@unique_id"; value = UniqueId(line.accountSnapshotLineId |> AccountSnapshotLineId.value) }
          { name = "@account_snapshot_id"; value = UniqueId(line.accountSnapshotId |> AccountSnapshotId.value) }
          { name = "@holding_id"; value = UniqueId(line.holdingId |> HoldingId.value) }
          { name = "@quantity"; value = Numeric(line.quantity |> Quantity.amount) }
          { name = "@price"; value = Numeric(line.price |> Price.amount) }
          { name = "@market_value"; value = Numeric(line.marketValue |> Money.amount) }
          { name = "@reported_cost_basis"
            value = NullableNumeric(line.reportedCostBasis |> Option.map Money.amount) } ]
    executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters ExactlyOne

let deleteByAccountSnapshot (context: Context.Context) (accountSnapshotId: AccountSnapshotId) : Result<unit, IAppError> =
    let queryStatement =
        """
        delete from positions.account_snapshot_line
        where account_snapshot_id = @account_snapshot_id;"""
    let parameters =
        [ { name = "@account_snapshot_id"; value = UniqueId(accountSnapshotId |> AccountSnapshotId.value) } ]
    executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters AnyQuantityIsAcceptable

let private reconstitute raw =
    result {
        let uuid, snapshotId, holdingId, quantityRaw, priceRaw, marketValueRaw, costBasisRaw = raw
        let! quantity = quantityRaw |> Quantity.fromDecimal
        let! price = priceRaw |> Price.fromDecimal
        let! marketValue = marketValueRaw |> Money.fromDecimal
        let! costBasis = costBasisRaw |> convertOptionToDesiredTypeWithFallibleConverter Money.fromDecimal
        return
            create
                (uuid |> AccountSnapshotLineId.fromGuid)
                (snapshotId |> AccountSnapshotId.fromGuid)
                (holdingId |> HoldingId.fromGuid)
                quantity
                price
                marketValue
                costBasis
    }

let private mapRawForDbRead (row: RowReader) =
    (row |> RowReader.getUuid "unique_id"),
    (row |> RowReader.getUuid "account_snapshot_id"),
    (row |> RowReader.getUuid "holding_id"),
    (row |> RowReader.getNumeric "quantity"),
    (row |> RowReader.getNumeric "price"),
    (row |> RowReader.getNumeric "market_value"),
    (row |> RowReader.getNumericOption "reported_cost_basis")

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
    : Result<AccountSnapshotLine list, IAppError> =
    let from = "positions.account_snapshot_line snapl"
    let queryStatement = buildReadQuery cteList select from joinList predicate limit groupBy orderBy
    executeReaderQuery
        (context |> Context.getDatabaseTransaction) queryStatement parameters mapRawForDbRead reconstitute expectedRows

let fetchByAccountSnapshot
    (context: Context.Context)
    (accountSnapshotId: AccountSnapshotId)
    : Result<AccountSnapshotLine list, IAppError> =
    let select =
        """
        snapl.unique_id, snapl.account_snapshot_id, snapl.holding_id, snapl.quantity, snapl.price, snapl.market_value,
        snapl.reported_cost_basis"""
    let parameters =
        [ { name = "@account_snapshot_id"; value = UniqueId(accountSnapshotId |> AccountSnapshotId.value) } ]
    query
        context
        None
        select
        None
        (Some "snapl.account_snapshot_id = @account_snapshot_id")
        None
        None
        None
        parameters
        AnyQuantityIsAcceptable
