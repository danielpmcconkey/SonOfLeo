module Business.CrossDomainOrchestration.JournalEntryExternalReferenceOrchestration

open App.Utility
open App.Utility.IAppError
open App.Utility.Result
open App.DataAccessLayer.DalError
open App.DataAccessLayer.ExecuteReader
open App.Session
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.LedgerError
open Business.FinancialServices.Ledger.JournalEntryComponent
open Business.FinancialServices.Ledger.JournalEntryExternalReference

let private confirmJournalEntryHeader (context: Context.Context) (journalEntryHeaderId: JournalEntryHeaderId) : Result<unit, IAppError> =
    journalEntryHeaderId |> JournalEntryHeader.fetchById context
    |> whenNoRows (JournalEntryHeaderIdDoesntExist (journalEntryHeaderId |> JournalEntryHeaderId.value))
    |> Result.map ignore

let constructNewAndPersist
    (context: Context.Context)
    (journalEntryHeaderId: JournalEntryHeaderId)
    (financialInstitution: JournalRefFinancialInstitution)
    (referenceText: JournalExternalReferenceText)
    : Result<JournalEntryExternalReference, IAppError> =
    let journalEntryExternalReferenceId = JournalEntryExternalReferenceId.create()
    let now = context |> Context.getInitiationInstant
    let createdAt = now
    let modifiedAt = now
    result {
        do! journalEntryHeaderId |> confirmJournalEntryHeader context
        let journalExternalReference =
            create
                journalEntryExternalReferenceId
                journalEntryHeaderId
                financialInstitution
                referenceText
                createdAt
                modifiedAt
        do! journalExternalReference |> persist context
        return journalExternalReference
    }

let updateFiAndReferenceText
    (context: Context.Context)
    (fiUpdate: FieldUpdate.FieldUpdate<JournalRefFinancialInstitution>)
    (referenceUpdate: FieldUpdate.FieldUpdate<JournalExternalReferenceText>)
    (journalEntryExternalReferenceId: JournalEntryExternalReferenceId)
    : Result<JournalEntryExternalReference, IAppError> =
    let uuid = journalEntryExternalReferenceId |> JournalEntryExternalReferenceId.value
    result {
        do!
            JournalEntryExternalReference.update context journalEntryExternalReferenceId fiUpdate referenceUpdate
            |> whenNoRows (JournalEntryExternalReferenceIdDoesntExist uuid)
        return! journalEntryExternalReferenceId |> fetchById context
    }
