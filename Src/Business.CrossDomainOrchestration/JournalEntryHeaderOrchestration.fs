module Business.CrossDomainOrchestration.JournalEntryHeaderOrchestration

open App.Utility.IAppError
open App.Utility.Result
open App.Session
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.LedgerError
open Business.FinancialServices.Ledger.JournalEntryComponent

let private confirmEntryDateIsInOpenFiscalPeriod (context: Context.Context) (entryDate: EntryDate) : Result<unit, IAppError> =
    result {
        let! fiscalPeriod = entryDate |> EntryDate.fiscalPeriodId |> FiscalPeriod.fetchById context
        match fiscalPeriod |> FiscalPeriod.isOpen with
        | true -> return! Ok()
        | false -> return! Error(JournalEntryHeaderEntryDateInvalid(entryDate |> EntryDate.entryDate))
    }

let constructNewAndPersist
    (context: Context.Context)
    (description: JournalEntryDescription)
    (source: JournalEntrySource option)
    (entryDate: EntryDate)
    : Result<JournalEntryHeader.JournalEntryHeader, IAppError> =
    let journalEntryId = JournalEntryHeaderId.create()
    let now = context |> Context.getInitiationInstant
    let createdAt = now
    let modifiedAt = now
    let voidedAt = None
    result {
        do! entryDate |> confirmEntryDateIsInOpenFiscalPeriod context
        let journalEntryHeader =
            JournalEntryHeader.create journalEntryId description source entryDate voidedAt createdAt modifiedAt
        let! () = journalEntryHeader |> JournalEntryHeader.persist context
        return journalEntryHeader
    }
