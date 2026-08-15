module ModelOrchestrator.JournalEntryHeaderOrchestration

open Model.Ledger.FiscalPeriods
open Model.Ledger.Journaling
open Model.Ledger.Journaling.JournalEntryComponent
open Utilities.AppError
open Utilities.ResultHelper


let confirmEntryDateIsInOpenFiscalPeriod (context: Context.Context) (entryDate: EntryDate) : Result<unit, AppError> =
    result {
        let! fiscalPeriod = entryDate |> EntryDate.fiscalPeriodId |> FiscalPeriod.fetchById context
        match fiscalPeriod |> FiscalPeriod.isOpen with
        | true -> return! Ok()
        | false -> return! Error(JournalEntryHeaderEntryDateInvalid(entryDate |> EntryDate.entryDate))
    }

let constructNewAndSaveToDb
    (context: Context.Context)
    (description: JournalEntryDescription)
    (source: JournalEntrySource option)
    (entryDate: EntryDate)
    : Result<JournalEntryHeader, AppError> =
    let journalEntryId = JournalEntryHeaderId.create()
    let now = context |> Context.getInitiationInstant
    let createdAt = now
    let modifiedAt = now
    let voidedAt = None
    result {
        do! entryDate |> confirmEntryDateIsInOpenFiscalPeriod context
        let journalEntryHeader =
            JournalEntryHeader.create journalEntryId description source entryDate voidedAt createdAt modifiedAt
        let! () = journalEntryHeader |> JournalEntryHeader.insertNewToDb context
        return journalEntryHeader
    }
