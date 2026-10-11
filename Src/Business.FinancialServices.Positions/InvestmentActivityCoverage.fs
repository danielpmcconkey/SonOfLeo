module Business.FinancialServices.Positions.InvestmentActivityCoverage

open NodaTime
open App.Utility.IAppError
open App.Utility.Result
open App.DataAccessLayer.QueryParameter
open App.DataAccessLayer.ExecuteReader
open App.DataAccessLayer.ExecuteNonQuery
open App.Session
open Business.FinancialServices.Positions.PositionsComponent

/// A range of dates for which an Investment Account's activity has been supplied. An account's rows never overlap or
/// adjoin: recording merges them.
type InvestmentActivityCoverage = private {
    investmentActivityCoverageId: InvestmentActivityCoverageId
    investmentAccountId: InvestmentAccountId
    beginDate: LocalDate
    endDate: LocalDate
    createdAt: Instant
    modifiedAt: Instant
}

let investmentActivityCoverageId c = c.investmentActivityCoverageId
let investmentAccountId c = c.investmentAccountId
let beginDate c = c.beginDate
let endDate c = c.endDate
let createdAt c = c.createdAt
let modifiedAt c = c.modifiedAt

let create
    (investmentActivityCoverageId: InvestmentActivityCoverageId)
    (investmentAccountId: InvestmentAccountId)
    (beginDate: LocalDate)
    (endDate: LocalDate)
    (createdAt: Instant)
    (modifiedAt: Instant)
    : InvestmentActivityCoverage =
    { investmentActivityCoverageId = investmentActivityCoverageId
      investmentAccountId = investmentAccountId
      beginDate = beginDate
      endDate = endDate
      createdAt = createdAt
      modifiedAt = modifiedAt }

let persist (context: Context.Context) (coverage: InvestmentActivityCoverage) : Result<unit, IAppError> =
    let queryStatement =
        """
        insert into positions.investment_activity_coverage(
            unique_id, investment_account_id, begin_date, end_date, created_at, modified_at)
        values (@unique_id, @investment_account_id, @begin_date, @end_date, @created_at, @modified_at);"""
    let parameters =
        [ { name = "@unique_id"; value = UniqueId(coverage.investmentActivityCoverageId |> InvestmentActivityCoverageId.value) }
          { name = "@investment_account_id"; value = UniqueId(coverage.investmentAccountId |> InvestmentAccountId.value) }
          { name = "@begin_date"; value = DbLocalDate coverage.beginDate }
          { name = "@end_date"; value = DbLocalDate coverage.endDate }
          { name = "@created_at"; value = DbInstant coverage.createdAt }
          { name = "@modified_at"; value = DbInstant coverage.modifiedAt } ]
    executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters ExactlyOne

let delete (context: Context.Context) (investmentActivityCoverageId: InvestmentActivityCoverageId) : Result<unit, IAppError> =
    let queryStatement = "delete from positions.investment_activity_coverage where unique_id = @unique_id;"
    let parameters = [ { name = "@unique_id"; value = UniqueId(investmentActivityCoverageId |> InvestmentActivityCoverageId.value) } ]
    executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters ExactlyOne

let private reconstitute raw : Result<InvestmentActivityCoverage, IAppError> =
    let uuid, accountId, beginDate, endDate, createdAt, modifiedAt = raw
    Ok(
        create
            (uuid |> InvestmentActivityCoverageId.fromGuid)
            (accountId |> InvestmentAccountId.fromGuid)
            beginDate
            endDate
            createdAt
            modifiedAt
    )

let private mapRawForDbRead (row: RowReader) =
    (row |> RowReader.getUuid "unique_id"),
    (row |> RowReader.getUuid "investment_account_id"),
    (row |> RowReader.getDate "begin_date"),
    (row |> RowReader.getDate "end_date"),
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
    : Result<InvestmentActivityCoverage list, IAppError> =
    let from = "positions.investment_activity_coverage invcov"
    let queryStatement = buildReadQuery cteList select from joinList predicate limit groupBy orderBy
    executeReaderQuery
        (context |> Context.getDatabaseTransaction) queryStatement parameters mapRawForDbRead reconstitute expectedRows

let private fetchAny
    (context: Context.Context)
    (predicate: string option)
    (parameters: QueryParameter list)
    : Result<InvestmentActivityCoverage list, IAppError> =
    let select =
        "invcov.unique_id, invcov.investment_account_id, invcov.begin_date, invcov.end_date, invcov.created_at, invcov.modified_at"
    query context None select None predicate None None None parameters AnyQuantityIsAcceptable
    |> Result.map (List.sortBy (fun c -> c.beginDate))

/// The account's coverage rows sharing at least one date with the range, both ends included, in date order.
let fetchByInvestmentAccountBetween
    (context: Context.Context)
    (investmentAccountId: InvestmentAccountId)
    (beginDate: LocalDate)
    (endDate: LocalDate)
    : Result<InvestmentActivityCoverage list, IAppError> =
    let parameters =
        [ { name = "@investment_account_id"; value = UniqueId(investmentAccountId |> InvestmentAccountId.value) }
          { name = "@begin_date"; value = DbLocalDate beginDate }
          { name = "@end_date"; value = DbLocalDate endDate } ]
    fetchAny
        context
        (Some "invcov.investment_account_id = @investment_account_id and invcov.begin_date <= @end_date and invcov.end_date >= @begin_date")
        parameters
