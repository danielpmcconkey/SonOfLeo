module ModelOrchestrator.JournalEntryHeaderOrchestration

open Model.Audit
open Model.Ledger.FiscalPeriods
open Model.Ledger.Journaling
open Model.Ledger.Journaling.JournalEntryComponent
open Utilities.AppError
open Utilities.DAL
open Utilities.ResultHelper

let confirmEntryDateIsInOpenFiscalPeriod
    (transaction: DbTransaction option)
    (entryDate: EntryDate)
    : Result<unit, AppError> =
    result {
        let! fiscalPeriod = entryDate |> EntryDate.fiscalPeriodId |> FiscalPeriod.fetchById transaction
        match fiscalPeriod |> FiscalPeriod.isOpen with
        | true -> return! Ok()
        | false -> return! Error(JournalEntryHeaderEntryDateInvalid(entryDate |> EntryDate.entryDate))
    }

let constructNewAndSaveToDb
    (description: JournalEntryDescription)
    (source: JournalEntrySource option)
    (entryDate: EntryDate)
    (auditEnvelope: AuditEnvelope)
    (transaction: DbTransaction option)
    : Result<JournalEntryHeader, AppError> =
    let journalEntryId = JournalEntryHeaderId.create() // REQ-JE-2.1
    let now = AuditEnvelope.instant auditEnvelope
    let createdAt = now // REQ-SYS-3.2
    let modifiedAt = now // REQ-SYS-3.2
    let voidedAt = None // REQ-JE-2.14
    result {
        do! entryDate |> confirmEntryDateIsInOpenFiscalPeriod transaction // REQ-JE-2.7
        let journalEntryHeader =
            JournalEntryHeader.create journalEntryId description source entryDate voidedAt createdAt modifiedAt
        let! () = JournalEntryHeader.insertNewToDb journalEntryHeader transaction
        return journalEntryHeader
    }
