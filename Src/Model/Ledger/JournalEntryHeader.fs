namespace Model.Ledger.Journaling

open System
open Model.Audit
open Model.Ledger.FiscalPeriods
open Model.Ledger.Journaling.JournalEntryComponent
open NodaTime
open Utilities.ResultCE
open Utilities.DAL

type JournalEntryHeader =
  private  {    journalEntryId: JournalEntryId                     // REQ-JE-1.1, REQ-JE-1.2
                description: JournalEntryDescription               // REQ-JE-1.3
                source: JournalEntrySource option                  // REQ-JE-1.6
                entryDate: EntryDate                               // REQ-JE-1.9
                voidedAt: Instant option                           // REQ-JE-1.14
                createdAt: Instant
                modifiedAt: Instant }

module JournalEntryHeader =
    let journalEntryId je = je.journalEntryId
    let description je = je.description
    let source je = je.source
    let entryDate je = je.entryDate
    let voidedAt je = je.voidedAt
    let createdAt je = je.createdAt
    let modifiedAt je = je.modifiedAt

    /// validateThenConstruct is your centralized constructor for assembling
    /// and validating component types. all other constructors must
    /// pass into this one
    let private validateThenConstruct
            (uniqueId: Guid)
            (description: string)
            (source: string option)
            (entryDate: LocalDate)
            (voidedAt: Instant option)
            (createdAt: Instant)
            (modifiedAt: Instant)
            (transaction: DbTransaction option)
            : Result<JournalEntryHeader, string> =
        result {
            let journalEntryId = uniqueId |> JournalEntryId.fromGuid
            let! validDescription = JournalEntryDescription.create description
            let! validSource =
                match source with
                | Some x -> JournalEntrySource.create x |> Result.map Some
                | None -> Ok None
            let! validEntryDate = entryDate |> EntryDate.create transaction
            return { journalEntryId = journalEntryId; description = validDescription; source = validSource
                     entryDate = validEntryDate; voidedAt = voidedAt; createdAt = createdAt
                     modifiedAt = modifiedAt } }

    let constructNew
            (description: string)
            (source: string option)
            (entryDate: LocalDate)
            (voidedAt: Instant option)
            (auditEnvelope: AuditEnvelope)
            (transaction: DbTransaction option)
            : Result<JournalEntryHeader, string> =
        let uniqueId = Guid.NewGuid() // REQ-JE-2.1
        let now = AuditEnvelope.instant auditEnvelope
        let createdAt =  now // REQ-SYS-3.2
        let modifiedAt = now // REQ-SYS-3.2
        validateThenConstruct uniqueId description source entryDate voidedAt createdAt modifiedAt transaction
    
    let private insertNewToDb (journalEntry:JournalEntryHeader) (transaction: DbTransaction option): Result<unit, string> =
        let query = """
            INSERT INTO ledger.journal_entry(
                unique_id, description, je_source, entry_date, fiscal_period_id, voided_at, created_at, modified_at)
            VALUES (
                @unique_id, @description, @je_source, @entry_date, @fiscal_period_id, @voided_at, @created_at, @modified_at);"""
        let uuid = journalEntry.journalEntryId |> JournalEntryId.value
        let fpUuid = journalEntry.entryDate |> EntryDate.fiscalPeriod |> FiscalPeriod.fiscalPeriodId |> FiscalPeriodId.value
        let parameters = [ //  REQ-DAL-2.1, REQ-DAL-2.3 
            { name = "@unique_id"; value = UniqueId uuid }
            { name = "@description"; value = CharString (journalEntry.description |> JournalEntryDescription.value) };
            { name = "@je_source"; value = NullableCharString (journalEntry.source |> Option.map  JournalEntrySource.value) };
            { name = "@entry_date"; value = DbLocalDate (journalEntry.entryDate |> EntryDate.entryDate) };
            { name = "@fiscal_period_id"; value = UniqueId fpUuid };
            { name = "@voided_at"; value = NullableDbInstant journalEntry.voidedAt };
            { name = "@created_at"; value = DbInstant journalEntry.createdAt };
            { name = "@modified_at"; value = DbInstant journalEntry.modifiedAt };
        ]
        executeNonQuery query parameters ExactlyOne transaction

    /// The mapRow function is used to pass into DAL read functions to let DAL know
    /// how to map our query columns. Thus, we don't need to know anything about the
    /// underlying database architecture in this module and the DAL module doesn't
    /// need to know anything about our module here 
    let mapRawForDbRead (row: RowReader) =
        (*
         * Note, we intentionally don't pull the fiscal period ID because
         * the FP is embedded into the EntryDate type
         *)
        ( row |> RowReader.getUuid "unique_id" ),
        ( row |> RowReader.getString "description" ),
        ( row |> RowReader.getStringOption "je_source" ),
        ( row |> RowReader.getDate "entry_date" ),
        ( row |> RowReader.getInstantOption "voided_at" ),
        ( row |> RowReader.getInstant "created_at" ),
        ( row |> RowReader.getInstant "modified_at" )

    let private constructFromRawForDbRead
            (transaction: DbTransaction option)
            raw
            : Result<JournalEntryHeader, string> =
        let id, description, jeSource, entryDate, voidedAt, createdAt, modifiedAt = raw
        validateThenConstruct id description jeSource entryDate voidedAt createdAt modifiedAt transaction

    let private readRowsFromDb
            (join: string option)
            (predicate: string option)
            (limit: int option)
            (orderBy: string option)
            (parameters: QueryParameter list)
            (expectedRows: AcceptableExpectedRows)
            (transaction: DbTransaction option)
            : Result<JournalEntryHeader list, string> = 
        (*
         * Note, we intentionally don't pull the fiscal period ID because
         * the FP is embedded into the EntryDate type
         *)
        let selectColumns = "je.unique_id, je.description, je.je_source, je.entry_date, je.voided_at, je.created_at, je.modified_at"
        let from = "ledger.journal_entry je"
        let query = buildReadQuery selectColumns from join predicate limit None orderBy
        executeReaderQuery query parameters mapRawForDbRead constructFromRawForDbRead expectedRows transaction

    let fetchById // REQ-JE-3.2
            (transaction: DbTransaction option)
            (journalEntryId: JournalEntryId)
            : Result<JournalEntryHeader, string> = 
        let uuid = journalEntryId |> JournalEntryId.value
        let predicate = Some "je.unique_id = @unique_id"
        let parameters = [{ name = "@unique_id"; value = UniqueId uuid };] // REQ-DAL-2.3
        readRowsFromDb None predicate None None parameters ExactlyOne transaction
        |> Result.map List.head

    let fetchByPeriod // REQ-JE-3.3
            (transaction: DbTransaction option)
            (periodId: FiscalPeriodId)
            : Result<JournalEntryHeader list, string> =
        let uuid = periodId |> FiscalPeriodId.value
        let predicate = Some "je.fiscal_period_id = @fiscal_period_id"
        let orderBy = Some "je.entry_date asc"
        result {
            let parameters = [{ name = "@fiscal_period_id"; value = UniqueId uuid };]
            return! readRowsFromDb None predicate None orderBy parameters AnyQuantityIsAcceptable transaction
        }
    
    let validateEntryDateIsInOpenFiscalPeriod
            (transaction: DbTransaction option)
            (entryDate: LocalDate)
            : Result<unit, string> =
        result {
            let! validEntryDate = entryDate |> EntryDate.create transaction
            return!
                match validEntryDate |> EntryDate.fiscalPeriod |> FiscalPeriod.isOpen with
                | true -> Ok ()
                | false -> Error $"Entry date of {entryDate} is not associated to an open Fiscal Period." }

    let constructNewAndSaveToDb
            (description: string)
            (source: string option)
            (entryDate: LocalDate)
            (voidedAt: Instant option)
            (auditEnvelope: AuditEnvelope)
            (transaction: DbTransaction option)
            : Result<JournalEntryHeader, string> =
        result {
            do! entryDate |> validateEntryDateIsInOpenFiscalPeriod transaction // REQ-JE-2.7
            let! validJournalEntry = constructNew description source entryDate voidedAt auditEnvelope transaction
            let! () = insertNewToDb validJournalEntry transaction
            return validJournalEntry }
