module Business.FinancialServices.Positions.BalanceAccountBalance

open NodaTime
open App.Utility.IAppError
open App.Utility.Result
open App.Utility.FieldUpdate
open App.DataAccessLayer.DalError
open App.DataAccessLayer.QueryParameter
open App.DataAccessLayer.ExecuteReader
open App.DataAccessLayer.ExecuteNonQuery
open App.Session
open Business.FinancialServices
open Business.FinancialServices.Positions.PositionsError
open Business.FinancialServices.Positions.PositionsComponent

/// What a Balance Account held or owed on one date, in its kind's own direction.
type BalanceAccountBalance = private {
    balanceAccountBalanceId: BalanceAccountBalanceId
    balanceAccountId: BalanceAccountId
    balanceDate: LocalDate
    amount: Money.Money
    createdAt: Instant
    modifiedAt: Instant
}

type BalanceAccountBalanceFieldUpdates = {
    balanceAccountBalanceIdToUpdate: BalanceAccountBalanceId
    amountUpdate: FieldUpdate<Money.Money>
}

let balanceAccountBalanceId b = b.balanceAccountBalanceId
let balanceAccountId b = b.balanceAccountId
let balanceDate b = b.balanceDate
let amount b = b.amount
let createdAt b = b.createdAt
let modifiedAt b = b.modifiedAt

let create
    (balanceAccountBalanceId: BalanceAccountBalanceId)
    (balanceAccountId: BalanceAccountId)
    (balanceDate: LocalDate)
    (amount: Money.Money)
    (createdAt: Instant)
    (modifiedAt: Instant)
    : BalanceAccountBalance =
    { balanceAccountBalanceId = balanceAccountBalanceId
      balanceAccountId = balanceAccountId
      balanceDate = balanceDate
      amount = amount
      createdAt = createdAt
      modifiedAt = modifiedAt }

/// A Balance Account's balance on a date: its latest Balance dated on or before it. None when it has none, which is not
/// a zero balance.
let balanceOn (date: LocalDate) (balances: BalanceAccountBalance list) : BalanceAccountBalance option =
    balances
    |> List.filter (fun b -> b.balanceDate <= date)
    |> List.sortByDescending (fun b -> b.balanceDate)
    |> List.tryHead

let persist (context: Context.Context) (balance: BalanceAccountBalance) : Result<unit, IAppError> =
    let queryStatement =
        """
        insert into positions.balance_account_balance(
            unique_id, balance_account_id, balance_date, amount, created_at, modified_at)
        values (@unique_id, @balance_account_id, @balance_date, @amount, @created_at, @modified_at);"""
    let parameters =
        [ { name = "@unique_id"; value = UniqueId(balance.balanceAccountBalanceId |> BalanceAccountBalanceId.value) }
          { name = "@balance_account_id"; value = UniqueId(balance.balanceAccountId |> BalanceAccountId.value) }
          { name = "@balance_date"; value = DbLocalDate balance.balanceDate }
          { name = "@amount"; value = Numeric(balance.amount |> Money.amount) }
          { name = "@created_at"; value = DbInstant balance.createdAt }
          { name = "@modified_at"; value = DbInstant balance.modifiedAt } ]
    executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters ExactlyOne

let delete (context: Context.Context) (balanceAccountBalanceId: BalanceAccountBalanceId) : Result<unit, IAppError> =
    let queryStatement =
        """
        delete from positions.balance_account_balance
        where unique_id = @unique_id;"""
    let parameters = [ { name = "@unique_id"; value = UniqueId(balanceAccountBalanceId |> BalanceAccountBalanceId.value) } ]
    executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters ExactlyOne

let private reconstitute raw =
    result {
        let uuid, balanceAccountId, balanceDate, amountRaw, createdAt, modifiedAt = raw
        let! amount = amountRaw |> Money.fromDecimal
        return
            create
                (uuid |> BalanceAccountBalanceId.fromGuid)
                (balanceAccountId |> BalanceAccountId.fromGuid)
                balanceDate
                amount
                createdAt
                modifiedAt
    }

let private mapRawForDbRead (row: RowReader) =
    (row |> RowReader.getUuid "unique_id"),
    (row |> RowReader.getUuid "balance_account_id"),
    (row |> RowReader.getDate "balance_date"),
    (row |> RowReader.getNumeric "amount"),
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
    : Result<BalanceAccountBalance list, IAppError> =
    let from = "positions.balance_account_balance babal"
    let queryStatement = buildReadQuery cteList select from joinList predicate limit groupBy orderBy
    executeReaderQuery
        (context |> Context.getDatabaseTransaction) queryStatement parameters mapRawForDbRead reconstitute expectedRows

let private fetchAny
    (context: Context.Context)
    (predicate: string option)
    (parameters: QueryParameter list)
    (expectedRows: AcceptableExpectedRows)
    : Result<BalanceAccountBalance list, IAppError> =
    let select =
        "babal.unique_id, babal.balance_account_id, babal.balance_date, babal.amount, babal.created_at, babal.modified_at"
    query context None select None predicate None None None parameters expectedRows
    |> Result.map (List.sortBy (fun b -> b.balanceDate))

let private balanceAccountParameter (balanceAccountId: BalanceAccountId) =
    { name = "@balance_account_id"; value = UniqueId(balanceAccountId |> BalanceAccountId.value) }

let fetchById (context: Context.Context) (balanceAccountBalanceId: BalanceAccountBalanceId) : Result<BalanceAccountBalance, IAppError> =
    let uuid = balanceAccountBalanceId |> BalanceAccountBalanceId.value
    fetchAny context (Some "babal.unique_id = @unique_id") [ { name = "@unique_id"; value = UniqueId uuid } ] ExactlyOne
    |> whenNoRows (PositionsBalanceAccountBalanceIdDoesntExist uuid)
    |> Result.map List.head

let fetchByAccountAndDate
    (context: Context.Context)
    (balanceAccountId: BalanceAccountId)
    (balanceDate: LocalDate)
    : Result<BalanceAccountBalance option, IAppError> =
    fetchAny
        context
        (Some "babal.balance_account_id = @balance_account_id and babal.balance_date = @balance_date")
        [ balanceAccountParameter balanceAccountId; { name = "@balance_date"; value = DbLocalDate balanceDate } ]
        AnyQuantityIsAcceptable
    |> Result.map List.tryHead

/// The account's Balances dated in the range, both ends included, in date order.
let fetchBetween
    (context: Context.Context)
    (balanceAccountId: BalanceAccountId)
    (beginDate: LocalDate)
    (endDate: LocalDate)
    : Result<BalanceAccountBalance list, IAppError> =
    fetchAny
        context
        (Some "babal.balance_account_id = @balance_account_id and babal.balance_date between @begin_date and @end_date")
        [ balanceAccountParameter balanceAccountId
          { name = "@begin_date"; value = DbLocalDate beginDate }
          { name = "@end_date"; value = DbLocalDate endDate } ]
        AnyQuantityIsAcceptable

/// Whether the account has any Balance at all.
let existsForAccount (context: Context.Context) (balanceAccountId: BalanceAccountId) : Result<bool, IAppError> =
    fetchAny context (Some "babal.balance_account_id = @balance_account_id") [ balanceAccountParameter balanceAccountId ] AnyQuantityIsAcceptable
    |> Result.map (List.isEmpty >> not)

/// Every Balance of every Balance Account dated on or before the date, in one read.
let fetchOnOrBefore (context: Context.Context) (date: LocalDate) : Result<BalanceAccountBalance list, IAppError> =
    fetchAny context (Some "babal.balance_date <= @date") [ { name = "@date"; value = DbLocalDate date } ] AnyQuantityIsAcceptable

let update (context: Context.Context) (fieldUpdates: BalanceAccountBalanceFieldUpdates) : Result<BalanceAccountBalance, IAppError> =
    let uuid = fieldUpdates.balanceAccountBalanceIdToUpdate |> BalanceAccountBalanceId.value
    let updates =
        [ fieldUpdates.amountUpdate
          |> mapNoChangeToOptionWithConversion (fun a -> "amount = @amount", { name = "@amount"; value = Numeric(a |> Money.amount) }) ]
        |> List.choose id
    let setClauses = updates |> List.map fst |> String.concat ", "
    let parameters =
        [ { name = "@unique_id"; value = UniqueId uuid }
          { name = "@modified"; value = DbInstant(context |> Context.getInitiationInstant) } ]
        @ (updates |> List.map snd)
    let queryStatement =
        $"""
        update positions.balance_account_balance
        set {setClauses}, modified_at = @modified
        where unique_id = @unique_id;"""
    result {
        do! if updates |> List.isEmpty then error PositionsBalanceAccountBalanceUpdateNoOp else Ok()
        do!
            executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters ExactlyOne
            |> whenNoRows (PositionsBalanceAccountBalanceIdDoesntExist uuid)
        return! fieldUpdates.balanceAccountBalanceIdToUpdate |> fetchById context
    }
