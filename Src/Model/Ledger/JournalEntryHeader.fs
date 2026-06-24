namespace Model.Ledger.Journaling

open System
open Model.Audit
open Model.Ledger.FiscalPeriods
open Model.Ledger.Journaling.JournalEntryComponent
open NodaTime
open Utilities.ResultCE
open Utilities.DAL

type JournalEntryHeader =
  private  {    uniqueId: Guid                                     // REQ-JE-1.1, REQ-JE-1.2
                description: Description                           // REQ-JE-1.3
                source: Source option                              // REQ-JE-1.6
                entryDate: EntryDate                               // REQ-JE-1.9
                voidedAt: Instant option                           // REQ-JE-1.14
                createdAt: Instant
                modifiedAt: Instant }

module JournalEntryHeader =
    let uniqueId je = je.uniqueId
    let description je = je.description
    let source je = je.source
    let entryDate je = je.entryDate
    let voidedAt je = je.voidedAt
    let createdAt je = je.createdAt
    let modifiedAt je = je.modifiedAt

    /// constructOmni is your centralized constructor for assembling
    /// and validating component types. all other constructors must
    /// pass into this one
    let private constructOmni
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
            
            let! validDescription = Description.create description
            let! validSource =
                match source with
                | Some x -> Source.create x |> Result.map Some
                | None -> Ok None
            let! validEntryDate = entryDate |> EntryDate.create transaction
            return { uniqueId = uniqueId; description = validDescription; source = validSource
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
        let uniqueId = Guid.NewGuid()
        let now = AuditEnvelope.instant auditEnvelope
        let createdAt =  now // REQ-SYS-3.2
        let modifiedAt = now // REQ-SYS-3.2
        constructOmni uniqueId description source entryDate voidedAt createdAt modifiedAt transaction
    
    let private insertNewToDb (journalEntry:JournalEntryHeader) (transaction: DbTransaction option): Result<unit, string> =
        let query = """
            INSERT INTO ledger.journal_entry(
                unique_id, description, je_source, entry_date, fiscal_period_id, voided_at, created_at, modified_at)
            VALUES (
                @unique_id, @description, @je_source, @entry_date, @fiscal_period_id, @voided_at, @created_at, @modified_at);"""
        let parameters = [ //  REQ-DAL-2.1, REQ-DAL-2.3 
            { name = "@unique_id"; value = UniqueId journalEntry.uniqueId }
            { name = "@description"; value = CharString (journalEntry.description |> Description.value) };
            { name = "@source"; value = NullableCharString (journalEntry.source |> Option.map  Source.value) };
            { name = "@entry_date"; value = DbLocalDate (journalEntry.entryDate |> EntryDate.entryDate) };
            { name = "@fiscal_period_id"; value = UniqueId (journalEntry.entryDate |> EntryDate.fiscalPeriod |> FiscalPeriod.uniqueId) };
            { name = "@voided_at"; value = NullableDbInstant journalEntry.voidedAt };
            { name = "@created_at"; value = DbInstant journalEntry.createdAt };
            { name = "@modified_at"; value = DbInstant journalEntry.modifiedAt };
        ]
        executeNonQuery query parameters ExactlyOne transaction

    let constructNewAndSaveToDb
            (description: string)
            (source: string option)
            (entryDate: LocalDate)
            (voidedAt: Instant option)
            (auditEnvelope: AuditEnvelope)
            (transaction: DbTransaction option)
            : Result<JournalEntryHeader, string> =
        result {
            let! validJournalEntry = constructNew description source entryDate voidedAt auditEnvelope transaction
            let! () = insertNewToDb validJournalEntry transaction // REQ-
            return validJournalEntry }