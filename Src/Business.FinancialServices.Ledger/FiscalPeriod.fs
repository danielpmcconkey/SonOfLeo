module Business.FinancialServices.Ledger.FiscalPeriod

open NodaTime
open App.Utility.IAppError
open App.Utility.Result
open App.DataAccessLayer.DalError
open App.DataAccessLayer.ExecuteNonQuery
open App.DataAccessLayer.ExecuteReader
open App.DataAccessLayer.QueryParameter
open App.Session
open Business.FinancialServices.Ledger.LedgerError
open Business.FinancialServices.Ledger.FiscalPeriodComponent
type FiscalPeriod =
    private
        { fiscalPeriodId: FiscalPeriodId
          periodKey: FiscalPeriodKey
          startDate: LocalDate
          endDate: LocalDate
          isOpen: bool
          createdAt: Instant
          modifiedAt: Instant }

let fiscalPeriodId fp = fp.fiscalPeriodId
let periodKey fp = fp.periodKey
let startDate fp = fp.startDate
let endDate fp = fp.endDate
let isOpen fp = fp.isOpen
let createdAt fp = fp.createdAt
let modifiedAt fp = fp.modifiedAt

let create
    (fiscalPeriodId: FiscalPeriodId)
    (periodKey: FiscalPeriodKey)
    (startDate: LocalDate)
    (endDate: LocalDate)
    (isOpen: bool)
    (createdAt: Instant)
    (modifiedAt: Instant)
    : FiscalPeriod =
    { fiscalPeriodId = fiscalPeriodId
      periodKey = periodKey
      startDate = startDate
      endDate = endDate
      isOpen = isOpen
      createdAt = createdAt
      modifiedAt = modifiedAt }

let persist (context: Context.Context) (fp: FiscalPeriod) : Result<unit, IAppError> =
    let queryStatement =
        """
        insert into ledger.fiscal_period(
            unique_id, period_key, start_date, end_date, is_open, created_at, modified_at)
        VALUES (@unique_id, @period_key, @start_date, @end_date, @is_open, @created_at, @modified_at);
        """
    let uuid = fp.fiscalPeriodId |> FiscalPeriodId.value
    let parameters =
        [
          { name = "@unique_id"; value = UniqueId uuid }
          { name = "@period_key"; value = CharString(FiscalPeriodKey.value fp.periodKey) }
          { name = "@start_date"; value = DbLocalDate fp.startDate }
          { name = "@end_date"; value = DbLocalDate fp.endDate }
          { name = "@is_open"; value = Boolean fp.isOpen }
          { name = "@created_at"; value = DbInstant fp.createdAt }
          { name = "@modified_at"; value = DbInstant fp.modifiedAt } ]
    executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters ExactlyOne

let private mapRawForDbRead (row: RowReader) =
    (row |> RowReader.getUuid "unique_id"),
    (row |> RowReader.getString "period_key"),
    (row |> RowReader.getDate "start_date"),
    (row |> RowReader.getDate "end_date"),
    (row |> RowReader.getBool "is_open"),
    (row |> RowReader.getInstant "created_at"),
    (row |> RowReader.getInstant "modified_at")

let private reconstitute raw =
    let id, key, startDate, endDate, isOpen, createdAt, modifiedAt = raw
    Ok
        { fiscalPeriodId = id |> FiscalPeriodId.fromGuid
          periodKey = key |> FiscalPeriodKey.reconstitute
          startDate = startDate
          endDate = endDate
          isOpen = isOpen
          createdAt = createdAt
          modifiedAt = modifiedAt }

let private query
    (context: Context.Context)
    (predicate: string option)
    (limit: int option)
    (parameters: QueryParameter list)
    (expectedRows: AcceptableExpectedRows)
    : Result<FiscalPeriod list, IAppError> =
    let select =
        "fp.unique_id, fp.period_key, fp.start_date, fp.end_date, fp.is_open, fp.created_at, fp.modified_at"
    let from = "ledger.fiscal_period fp"
    let queryStatement = buildReadQuery None select from None predicate limit None None
    executeReaderQuery
        (context |> Context.getDatabaseTransaction)
        queryStatement
        parameters
        mapRawForDbRead
        reconstitute
        expectedRows

let fetchById (context: Context.Context) (id: FiscalPeriodId) : Result<FiscalPeriod, IAppError> =
    let predicate = "fp.unique_id = @unique_id"
    let uuid = id |> FiscalPeriodId.value
    let parameters = [ { name = "@unique_id"; value = UniqueId uuid } ]
    query context (Some predicate) None parameters ExactlyOne
    |> whenNoRows (FiscalPeriodNoPeriodMatchingId uuid)
    |> Result.map List.head

let fetchIdByKey (context: Context.Context) (key: string) : Result<FiscalPeriodId, IAppError> =
    let mapRawForDbRead (row: RowReader) =
        (row |> RowReader.getUuid "unique_id"), ()
    let reconstitute raw =
        let id, _ = raw
        Ok id
    let queryStatement = "select unique_id from ledger.fiscal_period where period_key = @period_key"
    let parameters = [ { name = "@period_key"; value = CharString key } ]

    executeReaderQuery
        (context |> Context.getDatabaseTransaction) queryStatement parameters
        mapRawForDbRead reconstitute ExactlyOne
    |> whenNoRows (FiscalPeriodNoPeriodMatchingKey key)
    |> Result.map (List.head >> FiscalPeriodId.fromGuid)

let fetchAll (context: Context.Context) (openOnly: bool) : Result<FiscalPeriod list, IAppError> =
    let predicate =
        match openOnly with
        | true -> Some "fp.is_open = true"
        | _ -> None
    let parameters = []
    query context predicate None parameters AnyQuantityIsAcceptable

let private toggleOpenFlagById
    (context: Context.Context)
    (fpId: FiscalPeriodId)
    (newValue: bool)
    : Result<FiscalPeriod, IAppError> =
    let enforcedCurrentValue = not newValue
    let uuid = fpId |> FiscalPeriodId.value
    let parameters =
        [ { name = "@modified"; value = DbInstant(context |> Context.getInitiationInstant) }
          { name = "@unique_id"; value = UniqueId uuid }
          { name = "@newValue"; value = Boolean newValue }
          { name = "@enforcedCurrentValue"; value = Boolean enforcedCurrentValue } ]
    let queryStatement =
        $"""
        UPDATE ledger.fiscal_period
        set
            modified_at = @modified
            , is_open = @newValue
        WHERE unique_id = @unique_id
        and is_open = @enforcedCurrentValue
        ;
    """
    result {
        do! executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters ExactlyOne
            |> whenNoRows FiscalPeriodToggleOpenNoOp
        return! fpId |> fetchById context
    }

let closeFiscalPeriod
    (context: Context.Context)
    (fpId: FiscalPeriodId)
    : Result<FiscalPeriod, IAppError> =
    toggleOpenFlagById context fpId false

let reopenFiscalPeriod
    (context: Context.Context)
    (fpId: FiscalPeriodId)
    : Result<FiscalPeriod, IAppError> =
    toggleOpenFlagById context fpId true
