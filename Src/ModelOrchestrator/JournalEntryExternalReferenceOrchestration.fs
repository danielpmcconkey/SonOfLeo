module ModelOrchestrator.JournalEntryExternalReferenceOrchestration

open Model.Ledger.Journaling
open Model.Ledger.Journaling.JournalEntryComponent
open Utilities.AppError
open DataAccessLayer.QueryParameters
open DataAccessLayer.ExecuteReader
open DataAccessLayer.ExecuteNonQuery
open Utilities.FieldUpdate
open Utilities.ResultHelper
open Context.Context

let validateJournalEntryHeader (context: Context) (journalEntryId: JournalEntryHeaderId) : Result<unit, AppError> =
    journalEntryId |> JournalEntryHeader.fetchById context |> Result.map ignore

let constructNewAndSaveToDb
    (context: Context)
    (journalEntryHeaderId: JournalEntryHeaderId)
    (financialInstitution: JournalRefFinancialInstitution)
    (referenceText: JournalExternalReferenceText)
    : Result<JournalEntryExternalReference, AppError> =
    let journalEntryExternalReferenceId = JournalEntryExternalReferenceId.create()
    let now = context |> getInitiationInstant
    let createdAt = now
    let modifiedAt = now
    result {
        do! journalEntryHeaderId |> validateJournalEntryHeader context
        do! match journalEntryHeaderId |> JournalEntryHeader.fetchById context with
            | Ok _ -> Ok ()
            | Error (DalResultantRowsDidntMatchExpectation(expected, actual)) ->
                if actual = 0 then Error(JournalEntryHeaderIdDoesntExist (journalEntryHeaderId |> JournalEntryHeaderId.value))
                else Error (DalResultantRowsDidntMatchExpectation(expected, actual))
            | Error e -> Error e
        let journalExternalReference =
            JournalEntryExternalReference.create
                journalEntryExternalReferenceId
                journalEntryHeaderId
                financialInstitution
                referenceText
                createdAt
                modifiedAt
        do! journalExternalReference |> JournalEntryExternalReference.insertNewToDb context
        return journalExternalReference
    }

let updateFiAndReferenceText
    (context: Context)
    (fiUpdate: FieldUpdate<JournalRefFinancialInstitution>)
    (referenceUpdate: FieldUpdate<JournalExternalReferenceText>)
    (journalEntryExternalReferenceId: JournalEntryExternalReferenceId)
    : Result<JournalEntryExternalReference, AppError> =
    let uuid = journalEntryExternalReferenceId |> JournalEntryExternalReferenceId.value
    let baseParams =
        [ { name = "@modified"; value = DbInstant(context |> getInitiationInstant) }
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
    let query =
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
        let! _ = executeNonQuery (context |> getDatabaseTransaction) query parameters ExactlyOne
        return! journalEntryExternalReferenceId |> JournalEntryExternalReference.fetchById context
    }
