module ModelOrchestrator.JournalEntryHeaderOrchestration

open Model.Ledger.FiscalPeriods
open Model.Ledger.Journaling
open Model.Ledger.Journaling.JournalEntryComponent
open Utilities.AppError
open Utilities.ResultHelper
open Context.Context

let confirmEntryDateIsInOpenFiscalPeriod (context: Context) (entryDate: EntryDate) : Result<unit, AppError> =
    result {
        let! fiscalPeriod = entryDate |> EntryDate.fiscalPeriodId |> FiscalPeriod.fetchById context
        match fiscalPeriod |> FiscalPeriod.isOpen with
        | true -> return! Ok()
        | false -> return! Error(JournalEntryHeaderEntryDateInvalid(entryDate |> EntryDate.entryDate))
    }

let constructNewAndSaveToDb
    (context: Context)
    (description: JournalEntryDescription)
    (source: JournalEntrySource option)
    (entryDate: EntryDate)
    : Result<JournalEntryHeader, AppError> =
    let journalEntryId = JournalEntryHeaderId.create() // REQ-JE-2.1
    let now = context |> getInitiationInstant
    let createdAt = now // REQ-SYS-3.2
    let modifiedAt = now // REQ-SYS-3.2
    let voidedAt = None // REQ-JE-2.14
    result {
        do! entryDate |> confirmEntryDateIsInOpenFiscalPeriod context // REQ-JE-2.7
        let journalEntryHeader =
            JournalEntryHeader.create journalEntryId description source entryDate voidedAt createdAt modifiedAt
        let! () = journalEntryHeader |> JournalEntryHeader.insertNewToDb context
        return journalEntryHeader
    }
