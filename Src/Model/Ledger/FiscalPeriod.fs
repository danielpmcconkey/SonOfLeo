namespace Model.Ledger.FiscalPeriods

open Model.Audit
open NodaTime
open Utilities.AppError
open Utilities.ResultHelper
open Utilities.DAL


type FiscalPeriod =
    private
        { fiscalPeriodId: FiscalPeriodId
          periodKey: FiscalPeriodKey
          startDate: LocalDate
          endDate: LocalDate
          isOpen: bool
          createdAt: Instant // REQ-SYS-3.1
          modifiedAt: Instant } // REQ-SYS-3.1
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
        (fiscalPeriodId: FiscalPeriodId) // REQ-FP-1.6
        (periodKey: FiscalPeriodKey) // REQ-FP-1.1
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
    let insertNewToDb (fp: FiscalPeriod) (transaction: DbTransaction option) : Result<unit, AppError> =
        let query =
            """
            insert into ledger.fiscal_period( -- REQ-SYS-5.1
                unique_id, period_key, start_date, end_date, is_open, created_at, modified_at)
            VALUES (@unique_id, @period_key, @start_date, @end_date, @is_open, @created_at, @modified_at);
            """
        let uuid = fp.fiscalPeriodId |> FiscalPeriodId.value
        let parameters =
            [ //  REQ-DAL-2.1, REQ-DAL-2.3
              { name = "@unique_id"; value = UniqueId uuid }
              { name = "@period_key"; value = CharString(FiscalPeriodKey.value fp.periodKey) }
              { name = "@start_date"; value = DbLocalDate fp.startDate }
              { name = "@end_date"; value = DbLocalDate fp.endDate }
              { name = "@is_open"; value = Boolean fp.isOpen }
              { name = "@created_at"; value = DbInstant fp.createdAt }
              { name = "@modified_at"; value = DbInstant fp.modifiedAt } ]
        executeNonQuery query parameters ExactlyOne transaction

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
        (predicate: string option)
        (limit: int option)
        (parameters: QueryParameter list)
        (expectedRows: AcceptableExpectedRows)
        (transaction: DbTransaction option)
        : Result<FiscalPeriod list, AppError> = // REQ-FP-3.1
        let select =
            "fp.unique_id, fp.period_key, fp.start_date, fp.end_date, fp.is_open, fp.created_at, fp.modified_at"
        let from = "ledger.fiscal_period fp"
        let query = buildReadQuery select from None predicate limit None None
        executeReaderQuery query parameters mapRawForDbRead reconstitute expectedRows transaction

    let fetchById (transaction: DbTransaction option) (id: FiscalPeriodId) : Result<FiscalPeriod, AppError> =
        let predicate = "fp.unique_id = @unique_id"
        let uuid = id |> FiscalPeriodId.value
        let parameters = [ { name = "@unique_id"; value = UniqueId uuid } ] // REQ-DAL-2.3
        readRowsFromDb (Some predicate) None parameters ExactlyOne transaction |> Result.map List.head

    /// fetchIdByKey should only be used sparingly, as it goes against
    /// the doctrine that the model deals in UUIDs while the boundary
    /// does the translation between keys and IDs
    let fetchIdByKey (transaction: DbTransaction option) (key: string) : Result<FiscalPeriodId, AppError> =
        let mapRaw (row: RowReader) =
            (row |> RowReader.getUuid "unique_id"), ()
        let constructFromRaw raw =
            let id, _ = raw
            Ok id
        let query = "select unique_id from ledger.fiscal_period where period_key = @period_key"
        let parameters = [ { name = "@period_key"; value = CharString key } ] // REQ-DAL-2.3

        match executeReaderQuery query parameters mapRaw constructFromRaw ExactlyOne transaction with
        | Ok x -> Ok(x |> List.head |> FiscalPeriodId.fromGuid)
        | Error(DalResultantRowsDidntMatchExpectation _) -> Error(FiscalPeriodNoPeriodMatchingKey key)
        | Error e -> Error e

    let fetchAll (transaction: DbTransaction option) (openOnly: bool) : Result<FiscalPeriod list, AppError> = // REQ-FP-3.4
        let predicate =
            match openOnly with
            | true -> Some "fp.is_open = true" // REQ-FP-3.5
            | _ -> None
        let parameters = []
        readRowsFromDb predicate None parameters AnyQuantityIsAcceptable transaction

    let private toggleOpenFlagById
        (fpId: FiscalPeriodId)
        (newValue: bool)
        (auditEnvelope: AuditEnvelope)
        (transaction: DbTransaction option)
        : Result<FiscalPeriod, AppError> =
        let enforcedCurrentValue = not newValue
        let uuid = fpId |> FiscalPeriodId.value
        let parameters =
            [ { name = "@modified"; value = DbInstant(AuditEnvelope.instant auditEnvelope) } // REQ-SYS-3.3
              { name = "@unique_id"; value = UniqueId uuid }
              { name = "@newValue"; value = Boolean newValue }
              { name = "@enforcedCurrentValue"; value = Boolean enforcedCurrentValue } ]
        let query =
            $"""
            UPDATE ledger.fiscal_period
            set
                modified_at = @modified -- REQ-SYS-3.3
                , is_open = @newValue
            WHERE unique_id = @unique_id -- REQ-FP-4.1.1, REQ-FP-4.2.1
            and is_open = @enforcedCurrentValue
            ;
        """
        result {
            let! () = executeNonQuery query parameters ExactlyOne transaction
            return! fpId |> fetchById transaction
        }

    let closeFiscalPeriod // REQ-FP-4.1
        (fpId: FiscalPeriodId)
        (auditEnvelope: AuditEnvelope)
        (transaction: DbTransaction option)
        : Result<FiscalPeriod, AppError> =
        toggleOpenFlagById fpId false auditEnvelope transaction

    let reopenFiscalPeriod // REQ-FP-4.2
        (fpId: FiscalPeriodId)
        (auditEnvelope: AuditEnvelope)
        (transaction: DbTransaction option)
        : Result<FiscalPeriod, AppError> =
        toggleOpenFlagById fpId true auditEnvelope transaction
