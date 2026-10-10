module Business.FinancialServices.Positions.PreLedgerBalance

open NodaTime
open App.Utility.IAppError
open App.Utility.Result
open App.DataAccessLayer.QueryParameter
open App.DataAccessLayer.ExecuteReader
open App.DataAccessLayer.ExecuteNonQuery
open App.Session
open Business.FinancialServices
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Positions.PositionsError
open Business.FinancialServices.Positions.PositionsComponent

/// What a ledger Account held on a date before the ledger began, in the account's normal-balance direction.
type PreLedgerBalance = private {
    preLedgerBalanceId: PreLedgerBalanceId
    ledgerAccountId: AccountId
    balanceDate: LocalDate
    balance: Money.Money
    createdAt: Instant
    modifiedAt: Instant
}

let preLedgerBalanceId b = b.preLedgerBalanceId
let ledgerAccountId b = b.ledgerAccountId
let balanceDate b = b.balanceDate
let balance b = b.balance
let createdAt b = b.createdAt
let modifiedAt b = b.modifiedAt

let create
    (preLedgerBalanceId: PreLedgerBalanceId)
    (ledgerAccountId: AccountId)
    (balanceDate: LocalDate)
    (balance: Money.Money)
    (createdAt: Instant)
    (modifiedAt: Instant)
    : PreLedgerBalance =
    { preLedgerBalanceId = preLedgerBalanceId
      ledgerAccountId = ledgerAccountId
      balanceDate = balanceDate
      balance = balance
      createdAt = createdAt
      modifiedAt = modifiedAt }

/// Confirms the balance date is before the ledger began: earlier than the start of the earliest fiscal period, of which
/// there must be one. The code only names the account in the error.
let confirmBeforeLedger
    (accountCode: string)
    (balanceDate: LocalDate)
    (earliestFiscalPeriodStart: LocalDate option)
    : Result<unit, IAppError> =
    match earliestFiscalPeriodStart with
    | None -> error (PositionsPreLedgerNoFiscalPeriod(accountCode, balanceDate))
    | Some start when balanceDate >= start -> error (PositionsPreLedgerDateNotBeforeLedger(accountCode, balanceDate))
    | Some _ -> Ok()

/// Each account's latest balance dated on or before the date. An account with none is absent, not zero.
let latestOnOrBefore (date: LocalDate) (balances: PreLedgerBalance list) : Map<AccountId, PreLedgerBalance> =
    balances
    |> List.filter (fun b -> b.balanceDate <= date)
    |> List.groupBy (fun b -> b.ledgerAccountId)
    |> List.map (fun (accountId, own) -> accountId, own |> List.maxBy (fun b -> b.balanceDate))
    |> Map.ofList

let persist (context: Context.Context) (preLedgerBalance: PreLedgerBalance) : Result<unit, IAppError> =
    let queryStatement =
        """
        insert into positions.pre_ledger_balance(
            unique_id, ledger_account_id, balance_date, balance, created_at, modified_at)
        values (@unique_id, @ledger_account_id, @balance_date, @balance, @created_at, @modified_at);"""
    let parameters =
        [ { name = "@unique_id"; value = UniqueId(preLedgerBalance.preLedgerBalanceId |> PreLedgerBalanceId.value) }
          { name = "@ledger_account_id"; value = UniqueId(preLedgerBalance.ledgerAccountId |> AccountId.value) }
          { name = "@balance_date"; value = DbLocalDate preLedgerBalance.balanceDate }
          { name = "@balance"; value = Numeric(preLedgerBalance.balance |> Money.amount) }
          { name = "@created_at"; value = DbInstant preLedgerBalance.createdAt }
          { name = "@modified_at"; value = DbInstant preLedgerBalance.modifiedAt } ]
    executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters ExactlyOne

let delete (context: Context.Context) (preLedgerBalanceId: PreLedgerBalanceId) : Result<unit, IAppError> =
    let queryStatement =
        """
        delete from positions.pre_ledger_balance
        where unique_id = @unique_id;"""
    let parameters = [ { name = "@unique_id"; value = UniqueId(preLedgerBalanceId |> PreLedgerBalanceId.value) } ]
    executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters ExactlyOne

let private reconstitute raw =
    result {
        let uuid, accountId, balanceDate, balanceRaw, createdAt, modifiedAt = raw
        let! balance = balanceRaw |> Money.fromDecimal
        return
            create
                (uuid |> PreLedgerBalanceId.fromGuid)
                (accountId |> AccountId.fromGuid)
                balanceDate
                balance
                createdAt
                modifiedAt
    }

let private mapRawForDbRead (row: RowReader) =
    (row |> RowReader.getUuid "unique_id"),
    (row |> RowReader.getUuid "ledger_account_id"),
    (row |> RowReader.getDate "balance_date"),
    (row |> RowReader.getNumeric "balance"),
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
    : Result<PreLedgerBalance list, IAppError> =
    let from = "positions.pre_ledger_balance plb"
    let queryStatement = buildReadQuery cteList select from joinList predicate limit groupBy orderBy
    executeReaderQuery
        (context |> Context.getDatabaseTransaction) queryStatement parameters mapRawForDbRead reconstitute expectedRows

let private fetchAny
    (context: Context.Context)
    (predicate: string option)
    (parameters: QueryParameter list)
    : Result<PreLedgerBalance list, IAppError> =
    let select = "plb.unique_id, plb.ledger_account_id, plb.balance_date, plb.balance, plb.created_at, plb.modified_at"
    query context None select None predicate None None None parameters AnyQuantityIsAcceptable
    |> Result.map (List.sortBy (fun b -> b.balanceDate))

let private accountParameter (ledgerAccountId: AccountId) =
    { name = "@ledger_account_id"; value = UniqueId(ledgerAccountId |> AccountId.value) }

let fetchByAccountAndDate
    (context: Context.Context)
    (ledgerAccountId: AccountId)
    (balanceDate: LocalDate)
    : Result<PreLedgerBalance option, IAppError> =
    fetchAny
        context
        (Some "plb.ledger_account_id = @ledger_account_id and plb.balance_date = @balance_date")
        [ accountParameter ledgerAccountId; { name = "@balance_date"; value = DbLocalDate balanceDate } ]
    |> Result.map List.tryHead

/// Every balance dated on or before the date, in date order.
let fetchOnOrBefore (context: Context.Context) (date: LocalDate) : Result<PreLedgerBalance list, IAppError> =
    fetchAny context (Some "plb.balance_date <= @date") [ { name = "@date"; value = DbLocalDate date } ]

/// The balances dated in the range, both ends included, of one account or of every account, in date order.
let fetchBetween
    (context: Context.Context)
    (ledgerAccountId: AccountId option)
    (beginDate: LocalDate)
    (endDate: LocalDate)
    : Result<PreLedgerBalance list, IAppError> =
    let rangePredicate = "plb.balance_date between @begin_date and @end_date"
    let rangeParameters =
        [ { name = "@begin_date"; value = DbLocalDate beginDate }; { name = "@end_date"; value = DbLocalDate endDate } ]
    match ledgerAccountId with
    | Some accountId ->
        fetchAny
            context
            (Some $"plb.ledger_account_id = @ledger_account_id and {rangePredicate}")
            (accountParameter accountId :: rangeParameters)
    | None -> fetchAny context (Some rangePredicate) rangeParameters

/// Replaces the stored record's balance: re-recording an account and date replaces it.
let replaceBalance
    (context: Context.Context)
    (existing: PreLedgerBalance)
    (balance: Money.Money)
    : Result<PreLedgerBalance, IAppError> =
    let modifiedAt = context |> Context.getInitiationInstant
    let queryStatement =
        """
        update positions.pre_ledger_balance
        set balance = @balance, modified_at = @modified
        where unique_id = @unique_id;"""
    let parameters =
        [ { name = "@unique_id"; value = UniqueId(existing.preLedgerBalanceId |> PreLedgerBalanceId.value) }
          { name = "@balance"; value = Numeric(balance |> Money.amount) }
          { name = "@modified"; value = DbInstant modifiedAt } ]
    executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters ExactlyOne
    |> Result.map (fun () -> { existing with balance = balance; modifiedAt = modifiedAt })
