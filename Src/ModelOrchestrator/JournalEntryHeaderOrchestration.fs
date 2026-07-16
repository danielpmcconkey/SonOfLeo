module ModelOrchestrator.JournalEntryHeaderOrchestration

open Model.Audit
open Model.Ledger.FiscalPeriods
open Model.Ledger.Journaling
open Model.Ledger.Journaling.JournalEntryComponent
open NodaTime
open Utilities.AppError
open Utilities.DAL
open Utilities.ResultCE
    
let confirmEntryDateIsInOpenFiscalPeriod
        (entryDate: EntryDate)
        : Result<unit, AppError> =
    match entryDate |> EntryDate.fiscalPeriod |> FiscalPeriod.isOpen with
    | true -> Ok ()
    | false -> Error (JournalEntryHeaderEntryDateInvalid (entryDate |> EntryDate.entryDate))

let constructNewAndSaveToDb
        (description: JournalEntryDescription)
        (source: JournalEntrySource option)
        (entryDate: EntryDate)
        (voidedAt: Instant option)
        (auditEnvelope: AuditEnvelope)
        (transaction: DbTransaction option)
        : Result<JournalEntryHeader, AppError> =
    let journalEntryId = JournalEntryId.create () // REQ-JE-2.1
    let now = AuditEnvelope.instant auditEnvelope
    let createdAt =  now // REQ-SYS-3.2
    let modifiedAt = now // REQ-SYS-3.2
    result {
        do! entryDate |> confirmEntryDateIsInOpenFiscalPeriod // REQ-JE-2.7
        let journalEntryHeader =
            JournalEntryHeader.create journalEntryId description source entryDate voidedAt createdAt modifiedAt 
        let! () = JournalEntryHeader.insertNewToDb journalEntryHeader transaction
        return journalEntryHeader }