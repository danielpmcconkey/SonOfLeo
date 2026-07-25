namespace Model.Ledger.Journaling

open Model.Ledger.FiscalPeriods
open Model.Ledger.Journaling.JournalEntryComponent
open NodaTime
open Utilities.AppError
open Utilities.ResultHelper
open Utilities.DAL

type JournalEntryHeader =
  private  {    journalEntryHeaderId: JournalEntryHeaderId                     // REQ-JE-1.1, REQ-JE-1.2
                description: JournalEntryDescription               // REQ-JE-1.3
                source: JournalEntrySource option                  // REQ-JE-1.6
                entryDate: EntryDate                               // REQ-JE-1.9
                voidedAt: Instant option                           // REQ-JE-1.14
                createdAt: Instant
                modifiedAt: Instant }

module JournalEntryHeader =
    let journalEntryHeaderId je = je.journalEntryHeaderId
    let description je = je.description
    let source je = je.source
    let entryDate je = je.entryDate
    let voidedAt je = je.voidedAt
    let createdAt je = je.createdAt
    let modifiedAt je = je.modifiedAt
    
    let create
        (journalEntryId: JournalEntryHeaderId)
        (description: JournalEntryDescription)
        (source: JournalEntrySource option)
        (entryDate: EntryDate)
        (voidedAt: Instant option)
        (createdAt: Instant)
        (modifiedAt: Instant)
        : JournalEntryHeader = {    journalEntryHeaderId = journalEntryId
                                    description = description
                                    source = source
                                    entryDate = entryDate
                                    voidedAt = voidedAt
                                    createdAt = createdAt
                                    modifiedAt = modifiedAt }
    
    let insertNewToDb (journalEntry:JournalEntryHeader) (transaction: DbTransaction option): Result<unit, AppError> =
        let query = """
            INSERT INTO ledger.journal_entry(
                unique_id, description, je_source, entry_date, fiscal_period_id, voided_at, created_at, modified_at)
            VALUES (
                @unique_id, @description, @je_source, @entry_date, @fiscal_period_id, @voided_at, @created_at, @modified_at);"""
        let uuid = journalEntry.journalEntryHeaderId |> JournalEntryHeaderId.value
        let fpUuid = journalEntry.entryDate |> EntryDate.fiscalPeriodId |> FiscalPeriodId.value
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
    let private mapRawForDbRead (row: RowReader) =
        (*
         * Note, we intentionally don't pull the fiscal period ID because
         * the FP is embedded into the EntryDate type
         *)
        ( row |> RowReader.getUuid "unique_id" ),
        ( row |> RowReader.getString "description" ),
        ( row |> RowReader.getStringOption "je_source" ),
        ( row |> RowReader.getDate "entry_date" ),
        (row |> RowReader.getUuid "fiscal_period_id" ),
        ( row |> RowReader.getInstantOption "voided_at" ),
        ( row |> RowReader.getInstant "created_at" ),
        ( row |> RowReader.getInstant "modified_at" )

    let private reconstitute
            raw
            : Result<JournalEntryHeader, AppError> =
        let id, descriptionStr, jeSourceStr, entryDateLd, fiscalPeriodUuid, voidedAt, createdAt, modifiedAt = raw
        let journalEntryId = id |> JournalEntryHeaderId.fromGuid
        let fiscalPeriodId = fiscalPeriodUuid |> FiscalPeriodId.fromGuid
        let entryDate = EntryDate.createWithFiscalPeriodId entryDateLd fiscalPeriodId
        result {
            let! description = descriptionStr |> JournalEntryDescription.create
            let! source = jeSourceStr |> convertOptionToDesiredTypeWithFallibleConverter JournalEntrySource.create
            return create journalEntryId description source entryDate voidedAt createdAt modifiedAt }

    let readRowsFromDb
            (join: string option)
            (predicate: string option)
            (limit: int option)
            (orderBy: string option)
            (parameters: QueryParameter list)
            (expectedRows: AcceptableExpectedRows)
            (transaction: DbTransaction option)
            : Result<JournalEntryHeader list, AppError> = 
        let selectColumns = "je.unique_id, je.description, je.je_source, je.entry_date, je.fiscal_period_id, je.voided_at, je.created_at, je.modified_at"
        let from = "ledger.journal_entry je"
        let query = buildReadQuery selectColumns from join predicate limit None orderBy
        executeReaderQuery query parameters mapRawForDbRead reconstitute expectedRows transaction

    let fetchById // REQ-JE-3.2
            (transaction: DbTransaction option)
            (journalEntryHeaderId: JournalEntryHeaderId)
            : Result<JournalEntryHeader, AppError> = 
        let uuid = journalEntryHeaderId |> JournalEntryHeaderId.value
        let predicate = Some "je.unique_id = @unique_id"
        let parameters = [{ name = "@unique_id"; value = UniqueId uuid };] // REQ-DAL-2.3
        readRowsFromDb None predicate None None parameters ExactlyOne transaction
        |> Result.map List.head

    let fetchByPeriod // REQ-JE-3.3
            (transaction: DbTransaction option)
            (periodId: FiscalPeriodId)
            : Result<JournalEntryHeader list, AppError> =
        let uuid = periodId |> FiscalPeriodId.value
        let predicate = Some "je.fiscal_period_id = @fiscal_period_id"
        let orderBy = Some "je.entry_date asc"
        result {
            let parameters = [{ name = "@fiscal_period_id"; value = UniqueId uuid };]
            return! readRowsFromDb None predicate None orderBy parameters AnyQuantityIsAcceptable transaction
        }
