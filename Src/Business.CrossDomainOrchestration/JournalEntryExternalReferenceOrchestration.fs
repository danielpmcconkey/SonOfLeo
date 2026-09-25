module Business.CrossDomainOrchestration.JournalEntryExternalReferenceOrchestration

open App.Utility
open App.Utility.IAppError
open App.Utility.Result
open App.DataAccessLayer.DalError
open App.DataAccessLayer.QueryParameter
open App.DataAccessLayer.ExecuteReader
open App.DataAccessLayer.ExecuteNonQuery
open App.Session
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.LedgerError
open Business.FinancialServices.Ledger.JournalEntryExternalReference
open Business.FinancialServices.Ledger.JournalEntryComponent

let private confirmJournalEntryHeader (context: Context.Context) (journalEntryHeaderId: JournalEntryHeaderId) : Result<unit, IAppError> =
    match journalEntryHeaderId |> JournalEntryHeader.fetchById context with
    | Ok _ -> Ok ()
    | Error e ->
        if e.DomainName = nameof DalError && e.CaseName = nameof DalError.DalResultantRowsDidntMatchExpectation
        then Error (JournalEntryHeaderIdDoesntExist (journalEntryHeaderId |> JournalEntryHeaderId.value))
        else Error e

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
    let baseParams =
        [ { name = "@modified"; value = DbInstant(context |> Context.getInitiationInstant) }
          { name = "@unique_id"; value = UniqueId uuid } ]
    let updates =
        [ fiUpdate
          |> FieldUpdate.mapNoChangeToOptionWithConversion(fun fi ->
              ", financial_institution = @financial_institution",
              { name = "@financial_institution"; value = CharString(JournalRefFinancialInstitution.value fi) })

          referenceUpdate
          |> FieldUpdate.mapNoChangeToOptionWithConversion(fun referenceText ->
              ", reference = @reference",
              { name = "@reference"; value = CharString(JournalExternalReferenceText.value referenceText) }) ]
        |> List.choose id
    let setClauses = updates |> List.map fst |> String.concat ""
    let parameters = baseParams @ (updates |> List.map snd)
    let queryStatement =
        $"""
        UPDATE ledger.journal_entry_ext_reference
        set
            modified_at = @modified
                {setClauses}
            WHERE unique_id = @unique_id;
        ;
    """
    result {
        do!
            if updates.IsEmpty then
                Error(JournalEntryReferenceUpdateNoOp)
            else
                Ok()
        let! _ = executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters ExactlyOne
        return! journalEntryExternalReferenceId |> fetchById context
    }
