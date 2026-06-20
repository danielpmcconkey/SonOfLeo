namespace Model.Ledger

open System
open Model.Audit
open NodaTime
open Utilities.ResultCE
open Utilities.DAL

type FiscalPeriod =
    private  {  id: Guid
                periodKey: PeriodKey
                startDate: LocalDate
                endDate: LocalDate
                isOpen: bool
                createdAt: Instant                                 // REQ-SYS-3.1
                modifiedAt: Instant                                // REQ-SYS-3.1
    }
module FiscalPeriod =
    // accessors
    let id fp = fp.id 
    let periodKey fp = fp.periodKey
    let startDate fp = fp.startDate
    let endDate fp = fp.endDate
    let isOpen fp = fp.isOpen
    let createdAt fp = fp.createdAt
    let modifiedAt fp = fp.modifiedAt
    
    let reconstitute 
                (id: Guid)
                (periodKey: string)
                (startDate: LocalDate)
                (endDate: LocalDate)
                (isOpen: bool)
                (createdAt: Instant)
                (modifiedAt: Instant)
                : Result<FiscalPeriod, string> =
        result {
            let! validKeyResult = PeriodKey.fromString periodKey
            return {    id = id
                        periodKey = validKeyResult
                        startDate = startDate
                        endDate = endDate
                        isOpen = isOpen
                        createdAt = createdAt
                        modifiedAt = modifiedAt
            }
        }
    
    let constructNew
                (periodKey: string)
                (auditEnvelope: AuditEnvelope)
                : Result<FiscalPeriod, string> =
        let now = AuditEnvelope.instant auditEnvelope
        let createdAt =  now // REQ-SYS-3.2
        let modifiedAt = now // REQ-SYS-3.2
        result {
            let! validKeyResult = PeriodKey.fromString periodKey
            let year = periodKey[0..3]
            let yearNum = Int32.Parse(year) // we already validated via regex that this won't throw
            let month = periodKey[5..6]
            let monthNum = Int32.Parse(month) // we already validated via regex that this won't throw
            let startDate = LocalDate(yearNum, monthNum, 1)
            let endDate = startDate.PlusMonths(1).PlusDays(-1)
            return {    id = Guid.NewGuid()
                        periodKey = validKeyResult
                        startDate = startDate
                        endDate = endDate
                        isOpen = true
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
            { name = "@id"; value = UniqueId fp.id };
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
            let! () = insertNewToDb validFiscalPeriod 
            return validFiscalPeriod
        }