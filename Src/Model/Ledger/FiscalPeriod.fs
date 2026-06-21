namespace Model.Ledger

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
    
    let reconstitute 
                (uniqueId: Guid) // REQ-FP-1.6
                (periodKey: string) // REQ-FP-1.1
                (startDate: LocalDate)
                (endDate: LocalDate)
                (isOpen: bool) // REQ-FP-1.8
                (createdAt: Instant)
                (modifiedAt: Instant)
                : Result<FiscalPeriod, string> =
        result {
            let! validKeyResult = PeriodKey.fromString periodKey
            return {    uniqueId = uniqueId
                        periodKey = validKeyResult
                        startDate = startDate
                        endDate = endDate
                        isOpen = isOpen
                        createdAt = createdAt
                        modifiedAt = modifiedAt
            }
        }

    let constructNew // REQ-FP-2.3.1
                (periodKey: string)
                (auditEnvelope: AuditEnvelope)
                : Result<FiscalPeriod, string> =
        let now = AuditEnvelope.instant auditEnvelope
        let createdAt =  now // REQ-SYS-3.2
        let modifiedAt = now // REQ-SYS-3.2
        result {
            let! validKeyResult = PeriodKey.fromString periodKey
            let validKeyString = PeriodKey.value validKeyResult
            let year = validKeyString[0..3]
            let yearNum = Int32.Parse(year) // we already validated via regex that this won't throw
            let month = validKeyString[5..6]
            let monthNum = Int32.Parse(month) // we already validated via regex that this won't throw
            let startDate = LocalDate(yearNum, monthNum, 1) // REQ-FP-1.4, REQ-FP-2.3
            let endDate = startDate.PlusMonths(1).PlusDays(-1) // REQ-FP-1.5, REQ-FP-2.3
            return {    uniqueId = Guid.NewGuid() // REQ-FP-2.1
                        periodKey = validKeyResult
                        startDate = startDate
                        endDate = endDate
                        isOpen = true // REQ-FP-2.6, REQ-FP-2.6.1
                        createdAt = createdAt
                        modifiedAt = modifiedAt
            }
        }

    /// insertNewToDb is a private function used as an interface to the DAL. It
    /// assumes that the calling function handled all necessary validations to
    /// ensure only legal data states persist 
    let private insertNewToDb (fp:FiscalPeriod): Result<unit, string> =            
        let query = """
            insert into ledger.fiscal_period( -- REQ-SYS-5.1
                id, period_key, start_date, end_date, is_open, created_at, modified_at)
            VALUES (@id, @period_key, @start_date, @end_date, @is_open, @created_at, @modified_at);
            """
        let parameters = [ //  REQ-DAL-2.1, REQ-DAL-2.3 
            { name = "@id"; value = UniqueId fp.uniqueId };
            { name = "@period_key"; value = CharString (PeriodKey.value fp.periodKey) };
            { name = "@start_date"; value = DbLocalDate fp.startDate };
            { name = "@end_date"; value = DbLocalDate fp.endDate };
            { name = "@is_open"; value = Boolean fp.isOpen };
            { name = "@created_at"; value = DbInstant fp.createdAt };
            { name = "@modified_at"; value = DbInstant fp.modifiedAt };
        ]
        executeNonQuery query parameters ExactlyOne


    /// constructNewAndSaveToDb is used where you want to construct a net new
    /// Fiscal Period and insert it into the DB in one operation   
    let constructNewAndSaveToDb
                (periodKey: string)
                (auditEnvelope: AuditEnvelope)
                : Result<FiscalPeriod, string> =

        result {
            let! validFiscalPeriod = constructNew periodKey auditEnvelope
            let! () = insertNewToDb validFiscalPeriod // REQ-FP-2.4
            return validFiscalPeriod // REQ-FP-2.4
        }

    /// The mapRow function is used to pass into DAL read functions to let DAL know
    /// how to map our query columns. Thus, we don't need to know anything about the
    /// underlying database architecture in this module and the DAL module doesn't
    /// need to know anything about our module here 
    let mapRowForDbRead (row: RowReader) : Result<FiscalPeriod, string> =
        reconstitute
            ( row |> RowReader.getUuid "id" )
            ( row |> RowReader.getString "period_key" )
            ( row |> RowReader.getDate "start_date" )
            ( row |> RowReader.getDate "end_date" )
            ( row |> RowReader.getBool "is_open" )
            ( row |> RowReader.getInstant "created_at" )
            ( row |> RowReader.getInstant "modified_at" )

    /// readRowsFromDb is designed to produce a flexible read query that can
    /// satisfy diverse use cases 
    let private readRowsFromDb
            (predicate: string option)
            (limit: int option)
            (parameters: QueryParameter list)
            (expectedRows: AcceptableExpectedRows): Result<FiscalPeriod list, string> = // REQ-FP-3.1
        let predicateString =
            match predicate with
            | Some x -> x
            | None -> String.Empty
        let limitString =
            match limit with
            | Some x -> $"limit {x}"
            | None -> String.Empty
        // todo: extract query assembly into the DAL after journaling slice is complete
        let query = $"""
            select  
                id, period_key, start_date, end_date, is_open, created_at, modified_at
            from ledger.fiscal_period
            {predicateString}
            {limitString}
            ;
            """
        executeReaderQuery query parameters mapRowForDbRead expectedRows

    let fetchByKey (pk: string) : Result<FiscalPeriod, string> = // REQ-FP-3.2
        let predicate = "where period_key = @period_key"
        let parameters = [{ name = "@period_key"; value = CharString pk };] // REQ-DAL-2.3
        readRowsFromDb (Some predicate) None parameters ExactlyOne
        |> Result.map List.head

    let fetchAll (openOnly: bool) : Result<FiscalPeriod list, string> = // REQ-FP-3.4
        let predicate =
            match openOnly with
            | true -> Some "where is_open = true" // REQ-FP-3.5
            | _ -> None
        let parameters = []
        readRowsFromDb predicate None parameters AnyQuantityIsAcceptable
    
    let private toggleOpenFlagByKey
            (pk: PeriodKey)
            (newValue: bool)
            (auditEnvelope: AuditEnvelope)
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
            let! () = executeNonQuery query parameters ExactlyOne
            return! fetchByKey pkString
        }
    
    let closeFiscalPeriod // REQ-FP-4.1
            (pk: PeriodKey)
            (auditEnvelope: AuditEnvelope)
            : Result<FiscalPeriod, string> =
        toggleOpenFlagByKey pk false auditEnvelope
    
    let reopenFiscalPeriod // REQ-FP-4.2
            (pk: PeriodKey)
            (auditEnvelope: AuditEnvelope)
            : Result<FiscalPeriod, string> =
        toggleOpenFlagByKey pk true auditEnvelope