namespace Model.Ledger.FiscalPeriods

open System
open Model.Audit
open NodaTime
open Utilities.ResultCE
open Utilities.DAL

type FiscalPeriod =
    private  {  uniqueId: Guid
                periodKey: PeriodKey
                startDate: LocalDate
                endDate: LocalDate
                isOpen: bool
                createdAt: Instant                                 // REQ-SYS-3.1
                modifiedAt: Instant                                // REQ-SYS-3.1
    }
module FiscalPeriod =
    // accessors
    let uniqueId fp = fp.uniqueId 
    let periodKey fp = fp.periodKey
    let startDate fp = fp.startDate
    let endDate fp = fp.endDate
    let isOpen fp = fp.isOpen
    let createdAt fp = fp.createdAt
    let modifiedAt fp = fp.modifiedAt
    
    let validateThenConstruct 
                (uniqueId: Guid) // REQ-FP-1.6
                (periodKey: string) // REQ-FP-1.1
                (isOpen: bool) // REQ-FP-1.8
                (createdAt: Instant)
                (modifiedAt: Instant)
                : Result<FiscalPeriod, string> =
        result {
            let! validKeyResult = PeriodKey.fromString periodKey
            let validKeyString = PeriodKey.value validKeyResult
            let year = validKeyString[0..3]
            let yearNum = Int32.Parse(year) // we already validated via regex that this won't throw
            let month = validKeyString[5..6]
            let monthNum = Int32.Parse(month) // we already validated via regex that this won't throw
            let startDate = LocalDate(yearNum, monthNum, 1) // REQ-FP-1.4, REQ-FP-2.3
            let endDate = startDate.PlusMonths(1).PlusDays(-1) // REQ-FP-1.5, REQ-FP-2.3
            return {    uniqueId = uniqueId
                        periodKey = validKeyResult
                        startDate = startDate
                        endDate = endDate
                        isOpen = isOpen
                        createdAt = createdAt
                        modifiedAt = modifiedAt
            }
        }

    let constructNewFromKeyString // REQ-FP-2.3.1
                (periodKey: string)
                (auditEnvelope: AuditEnvelope)
                : Result<FiscalPeriod, string> =
        let now = AuditEnvelope.instant auditEnvelope
        let createdAt =  now // REQ-SYS-3.2
        let modifiedAt = now // REQ-SYS-3.2
        let uuid = Guid.NewGuid()
        result {
            return! validateThenConstruct uuid periodKey true createdAt modifiedAt
            
        }

    /// insertNewToDb is a private function used as an interface to the DAL. It
    /// assumes that the calling function handled all necessary validations to
    /// ensure only legal data states persist 
    let private insertNewToDb (fp:FiscalPeriod) (transaction: DbTransaction option): Result<unit, string> =            
        let query = """
            insert into ledger.fiscal_period( -- REQ-SYS-5.1
                unique_id, period_key, start_date, end_date, is_open, created_at, modified_at)
            VALUES (@unique_id, @period_key, @start_date, @end_date, @is_open, @created_at, @modified_at);
            """
        let parameters = [ //  REQ-DAL-2.1, REQ-DAL-2.3 
            { name = "@unique_id"; value = UniqueId fp.uniqueId };
            { name = "@period_key"; value = CharString (PeriodKey.value fp.periodKey) };
            { name = "@start_date"; value = DbLocalDate fp.startDate };
            { name = "@end_date"; value = DbLocalDate fp.endDate };
            { name = "@is_open"; value = Boolean fp.isOpen };
            { name = "@created_at"; value = DbInstant fp.createdAt };
            { name = "@modified_at"; value = DbInstant fp.modifiedAt };
        ]
        executeNonQuery query parameters ExactlyOne transaction


    /// constructNewAndSaveToDb is used where you want to construct a net new
    /// Fiscal Period and insert it into the DB in one operation   
    let constructNewAndSaveToDb
                (periodKey: string)
                (auditEnvelope: AuditEnvelope)
                (transaction: DbTransaction option)
                : Result<FiscalPeriod, string> =

        result {
            let! validFiscalPeriod = constructNewFromKeyString periodKey auditEnvelope
            let! () = insertNewToDb validFiscalPeriod transaction// REQ-FP-2.4
            return validFiscalPeriod // REQ-FP-2.4
        }

    /// The mapRow function is used to pass into DAL read functions to let DAL know
    /// how to map our query columns. Thus, we don't need to know anything about the
    /// underlying database architecture in this module and the DAL module doesn't
    /// need to know anything about our module here 
    let mapRowForDbRead (row: RowReader) : Result<FiscalPeriod, string> =
        validateThenConstruct
            ( row |> RowReader.getUuid "unique_id" )
            ( row |> RowReader.getString "period_key" )
            ( row |> RowReader.getBool "is_open" )
            ( row |> RowReader.getInstant "created_at" )
            ( row |> RowReader.getInstant "modified_at" )

    /// readRowsFromDb is designed to produce a flexible read query that can
    /// satisfy diverse use cases 
    let private readRowsFromDb
            (predicate: string option)
            (limit: int option)
            (parameters: QueryParameter list)
            (expectedRows: AcceptableExpectedRows)
            (transaction: DbTransaction option)
            : Result<FiscalPeriod list, string> = // REQ-FP-3.1
        let select = "fp.unique_id, fp.period_key, fp.start_date, fp.end_date, fp.is_open, fp.created_at, fp.modified_at"
        let from = "ledger.fiscal_period fp"
        let query = buildReadQuery select from None predicate limit None None
        executeReaderQuery query parameters mapRowForDbRead expectedRows transaction

    let fetchByKey (transaction: DbTransaction option) (pk: string) : Result<FiscalPeriod, string> = // REQ-FP-3.2
        let predicate = "fp.period_key = @period_key"
        let parameters = [{ name = "@period_key"; value = CharString pk };] // REQ-DAL-2.3
        readRowsFromDb (Some predicate) None parameters ExactlyOne transaction
        |> Result.map List.head

    let fetchAll (transaction: DbTransaction option) (openOnly: bool) : Result<FiscalPeriod list, string> = // REQ-FP-3.4
        let predicate =
            match openOnly with
            | true -> Some "fp.is_open = true" // REQ-FP-3.5
            | _ -> None
        let parameters = []
        readRowsFromDb predicate None parameters AnyQuantityIsAcceptable transaction
    
    let private toggleOpenFlagByKey
            (pk: PeriodKey)
            (newValue: bool)
            (auditEnvelope: AuditEnvelope)
            (transaction: DbTransaction option)
            : Result<FiscalPeriod, string> =
        let pkString = pk |> PeriodKey.value
        let enforcedCurrentValue = not newValue
        let parameters = [
                { name = "@modified"; value = DbInstant (AuditEnvelope.instant auditEnvelope) } // REQ-SYS-3.3 
                { name = "@period_key"; value = CharString pkString };
                { name = "@newValue"; value = Boolean newValue };
                { name = "@enforcedCurrentValue"; value = Boolean enforcedCurrentValue };
            ]
        let query = $"""
            UPDATE ledger.fiscal_period
            set
                modified_at = @modified -- REQ-SYS-3.3
                , is_open = @newValue
            WHERE period_key = @period_key -- REQ-FP-4.1.1, REQ-FP-4.2.1
            and is_open = @enforcedCurrentValue
            ;
        """
        result {
            let! () = executeNonQuery query parameters ExactlyOne transaction
            return! pkString |> fetchByKey transaction
        }
    
    let closeFiscalPeriod // REQ-FP-4.1
            (pkString: string)
            (auditEnvelope: AuditEnvelope)
            (transaction: DbTransaction option)
            : Result<FiscalPeriod, string> =
        result {
            let! pk = PeriodKey.fromString pkString
            return! toggleOpenFlagByKey pk false auditEnvelope transaction
        }
    
    let reopenFiscalPeriod // REQ-FP-4.2
            (pkString: string)
            (auditEnvelope: AuditEnvelope)
            (transaction: DbTransaction option)
            : Result<FiscalPeriod, string> =
        result {
            let! pk = PeriodKey.fromString pkString
            return! toggleOpenFlagByKey pk true auditEnvelope transaction
        }
        