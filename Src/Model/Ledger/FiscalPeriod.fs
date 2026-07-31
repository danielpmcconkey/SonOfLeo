namespace Model.Ledger.FiscalPeriods

open DataAccessLayer.ExecuteNonQuery
open DataAccessLayer.ExecuteReader
open DataAccessLayer.QueryParameters
open NodaTime
open Utilities.AppError
open Utilities.ResultHelper
open Context.Context


type FiscalPeriod =
    private
        { fiscalPeriodId: FiscalPeriodId
          periodKey: FiscalPeriodKey
          startDate: LocalDate
          endDate: LocalDate
          isOpen: bool
          createdAt: Instant
          modifiedAt: Instant }
module FiscalPeriod =
    // accessors
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

    /// insertNewToDb is a private function used as an interface to the DAL. It
    /// assumes that the calling function handled all necessary validations to
    /// ensure only legal data states persist
    let insertNewToDb (context: Context) (fp: FiscalPeriod) : Result<unit, AppError> =
        let query =
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
        executeNonQuery (context |> getDatabaseTransaction) query parameters ExactlyOne

    /// The mapRow function is used to pass into DAL read functions to let DAL know
    /// how to map our query columns. Thus, we don't need to know anything about the
    /// underlying database architecture in this module and the DAL module doesn't
    /// need to know anything about our module here
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

    /// readRowsFromDb is designed to produce a flexible read query that can
    /// satisfy diverse use cases
    let private readRowsFromDb
        (context: Context)
        (predicate: string option)
        (limit: int option)
        (parameters: QueryParameter list)
        (expectedRows: AcceptableExpectedRows)
        : Result<FiscalPeriod list, AppError> =
        let select =
            "fp.unique_id, fp.period_key, fp.start_date, fp.end_date, fp.is_open, fp.created_at, fp.modified_at"
        let from = "ledger.fiscal_period fp"
        let query = buildReadQuery select from None predicate limit None None
        executeReaderQuery
            (context |> getDatabaseTransaction)
            query
            parameters
            mapRawForDbRead
            reconstitute
            expectedRows

    let fetchById (context: Context) (id: FiscalPeriodId) : Result<FiscalPeriod, AppError> =
        let predicate = "fp.unique_id = @unique_id"
        let uuid = id |> FiscalPeriodId.value
        let parameters = [ { name = "@unique_id"; value = UniqueId uuid } ]
        readRowsFromDb context (Some predicate) None parameters ExactlyOne |> Result.map List.head

    /// fetchIdByKey should only be used sparingly, as it goes against
    /// the doctrine that the model deals in UUIDs while the boundary
    /// does the translation between keys and IDs
    let fetchIdByKey (context: Context) (key: string) : Result<FiscalPeriodId, AppError> =
        let mapRaw (row: RowReader) =
            (row |> RowReader.getUuid "unique_id"), ()
        let constructFromRaw raw =
            let id, _ = raw
            Ok id
        let query = "select unique_id from ledger.fiscal_period where period_key = @period_key"
        let parameters = [ { name = "@period_key"; value = CharString key } ]

        match
            executeReaderQuery (context |> getDatabaseTransaction) query parameters mapRaw constructFromRaw ExactlyOne
        with
        | Ok x -> Ok(x |> List.head |> FiscalPeriodId.fromGuid)
        | Error(DalResultantRowsDidntMatchExpectation _) -> Error(FiscalPeriodNoPeriodMatchingKey key)
        | Error e -> Error e

    let fetchAll (context: Context) (openOnly: bool) : Result<FiscalPeriod list, AppError> =
        let predicate =
            match openOnly with
            | true -> Some "fp.is_open = true"
            | _ -> None
        let parameters = []
        readRowsFromDb context predicate None parameters AnyQuantityIsAcceptable

    let private toggleOpenFlagById
        (context: Context)
        (fpId: FiscalPeriodId)
        (newValue: bool)
        : Result<FiscalPeriod, AppError> =
        let enforcedCurrentValue = not newValue
        let uuid = fpId |> FiscalPeriodId.value
        let parameters =
            [ { name = "@modified"; value = DbInstant(context |> getInitiationInstant) }
              { name = "@unique_id"; value = UniqueId uuid }
              { name = "@newValue"; value = Boolean newValue }
              { name = "@enforcedCurrentValue"; value = Boolean enforcedCurrentValue } ]
        let query =
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
            let! () = executeNonQuery (context |> getDatabaseTransaction) query parameters ExactlyOne
            return! fpId |> fetchById context
        }

    let closeFiscalPeriod
        (context: Context)
        (fpId: FiscalPeriodId)
        : Result<FiscalPeriod, AppError> =
        toggleOpenFlagById context fpId false

    let reopenFiscalPeriod
        (context: Context)
        (fpId: FiscalPeriodId)
        : Result<FiscalPeriod, AppError> =
        toggleOpenFlagById context fpId true
