module Business.FinancialServices.Positions.AccountSnapshotLot

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

/// An open tax lot as the institution reported it on a snapshot line. Lots are recorded, never derived.
type AccountSnapshotLot = private {
    accountSnapshotLotId: AccountSnapshotLotId
    accountSnapshotLineId: AccountSnapshotLineId
    ordinal: int
    acquiredDate: LocalDate
    quantity: Quantity.Quantity
    reportedCostBasis: Money.Money option
}

let accountSnapshotLotId l = l.accountSnapshotLotId
let accountSnapshotLineId l = l.accountSnapshotLineId
let ordinal l = l.ordinal
let acquiredDate l = l.acquiredDate
let quantity l = l.quantity
let reportedCostBasis l = l.reportedCostBasis

let create
    (accountSnapshotLotId: AccountSnapshotLotId)
    (accountSnapshotLineId: AccountSnapshotLineId)
    (ordinal: int)
    (acquiredDate: LocalDate)
    (quantity: Quantity.Quantity)
    (reportedCostBasis: Money.Money option)
    : AccountSnapshotLot =
    { accountSnapshotLotId = accountSnapshotLotId
      accountSnapshotLineId = accountSnapshotLineId
      ordinal = ordinal
      acquiredDate = acquiredDate
      quantity = quantity
      reportedCostBasis = reportedCostBasis }

let private confirmLot
    (accountLabel: string)
    (snapshotDate: LocalDate)
    (securityLabel: string)
    (acquiredDate: LocalDate, quantity: Quantity.Quantity, reportedCostBasis: Money.Money option)
    : Result<unit, IAppError> =
    if not (quantity |> Quantity.isPositive) then
        error (PositionsLotQuantityNotPositive(accountLabel, snapshotDate, securityLabel))
    else
        match reportedCostBasis with
        | Some costBasis when costBasis |> Money.isNegative ->
            error (PositionsLotCostBasisNegative(accountLabel, snapshotDate, securityLabel, costBasis |> Money.amount))
        | _ when acquiredDate > snapshotDate ->
            error (PositionsLotAcquiredAfterSnapshot(accountLabel, snapshotDate, securityLabel, acquiredDate))
        | _ -> Ok()

/// Confirms one line's lots: each on its own, then their quantities summed exactly against the line's. The lots' costs
/// are not summed against the line's: each is rounded to the cent on its own. A line with no lots is always confirmed.
/// The labels only name the line in the error.
let confirmLots
    (accountLabel: string)
    (snapshotDate: LocalDate)
    (securityLabel: string)
    (lineQuantity: Quantity.Quantity)
    (lots: (LocalDate * Quantity.Quantity * Money.Money option) list)
    : Result<unit, IAppError> =
    result {
        do!
            lots
            |> List.map (confirmLot accountLabel snapshotDate securityLabel)
            |> convertListOfResultsToResultsList
            |> Result.map ignore
        let lotSum = lots |> List.map (fun (_, q, _) -> q) |> Quantity.sum
        let lineAmount = lineQuantity |> Quantity.amount
        if not (lots |> List.isEmpty) && lotSum <> lineAmount then
            return!
                error (PositionsLotsDontSumToLineQuantity(accountLabel, snapshotDate, securityLabel, lotSum, lineAmount))
    }

let persist (context: Context.Context) (lot: AccountSnapshotLot) : Result<unit, IAppError> =
    let queryStatement =
        """
        insert into positions.account_snapshot_lot(
            unique_id, account_snapshot_line_id, ordinal, acquired_date, quantity, reported_cost_basis)
        values (@unique_id, @account_snapshot_line_id, @ordinal, @acquired_date, @quantity, @reported_cost_basis);"""
    let parameters =
        [ { name = "@unique_id"; value = UniqueId(lot.accountSnapshotLotId |> AccountSnapshotLotId.value) }
          { name = "@account_snapshot_line_id"; value = UniqueId(lot.accountSnapshotLineId |> AccountSnapshotLineId.value) }
          { name = "@ordinal"; value = Integer lot.ordinal }
          { name = "@acquired_date"; value = DbLocalDate lot.acquiredDate }
          { name = "@quantity"; value = Numeric(lot.quantity |> Quantity.amount) }
          { name = "@reported_cost_basis"; value = NullableNumeric(lot.reportedCostBasis |> Option.map Money.amount) } ]
    executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters ExactlyOne

let private reconstitute raw =
    result {
        let uuid, lineId, ordinal, acquiredDate, quantityRaw, costBasisRaw, investmentAccountId, snapshotDate, holdingId =
            raw
        let! quantity = quantityRaw |> Quantity.fromDecimal
        let! costBasis = costBasisRaw |> convertOptionToDesiredTypeWithFallibleConverter Money.fromDecimal
        do! confirmLot $"{investmentAccountId}" snapshotDate $"holding {holdingId}" (acquiredDate, quantity, costBasis)
        return
            create
                (uuid |> AccountSnapshotLotId.fromGuid)
                (lineId |> AccountSnapshotLineId.fromGuid)
                ordinal
                acquiredDate
                quantity
                costBasis
    }

let private mapRawForDbRead (row: RowReader) =
    (row |> RowReader.getUuid "unique_id"),
    (row |> RowReader.getUuid "account_snapshot_line_id"),
    (row |> RowReader.getInt "ordinal"),
    (row |> RowReader.getDate "acquired_date"),
    (row |> RowReader.getNumeric "quantity"),
    (row |> RowReader.getNumericOption "reported_cost_basis"),
    (row |> RowReader.getUuid "investment_account_id"),
    (row |> RowReader.getDate "snapshot_date"),
    (row |> RowReader.getUuid "holding_id")

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
    : Result<AccountSnapshotLot list, IAppError> =
    let from = "positions.account_snapshot_lot snplot"
    let queryStatement = buildReadQuery cteList select from joinList predicate limit groupBy orderBy
    executeReaderQuery
        (context |> Context.getDatabaseTransaction) queryStatement parameters mapRawForDbRead reconstitute expectedRows

let private select =
    """
    snplot.unique_id, snplot.account_snapshot_line_id, snplot.ordinal, snplot.acquired_date, snplot.quantity,
    snplot.reported_cost_basis, snplothd.investment_account_id, snplothd.snapshot_date, snplotln.holding_id"""

let private joins =
    [ "join positions.account_snapshot_line snplotln on snplotln.unique_id = snplot.account_snapshot_line_id"
      "join positions.account_snapshot snplothd on snplothd.unique_id = snplotln.account_snapshot_id" ]

/// Lots in the order supplied, line by line.
let private inOrder (lots: AccountSnapshotLot list) =
    lots |> List.sortBy (fun l -> (l.accountSnapshotLineId |> AccountSnapshotLineId.value), l.ordinal)

let fetchByAccountSnapshot
    (context: Context.Context)
    (accountSnapshotId: AccountSnapshotId)
    : Result<AccountSnapshotLot list, IAppError> =
    let parameters =
        [ { name = "@account_snapshot_id"; value = UniqueId(accountSnapshotId |> AccountSnapshotId.value) } ]
    query
        context None select (Some joins) (Some "snplotln.account_snapshot_id = @account_snapshot_id") None None None
        parameters AnyQuantityIsAcceptable
    |> Result.map inOrder

/// The lots of every line of the snapshots a caller's common table expression selects. The CTE is named in
/// snapshotCte and must carry a unique_id column of account snapshot IDs.
let fetchBySnapshotsOf
    (context: Context.Context)
    (cteList: string list)
    (snapshotCte: string)
    (parameters: QueryParameter list)
    : Result<AccountSnapshotLot list, IAppError> =
    let predicate = $"snplotln.account_snapshot_id in (select unique_id from {snapshotCte})"
    query context (Some cteList) select (Some joins) (Some predicate) None None None parameters AnyQuantityIsAcceptable
    |> Result.map inOrder
