module ModelOrchestrator.JournalEntryExternalReferenceOrchestration

open Model.Audit
open Model.Ledger.Journaling
open Model.Ledger.Journaling.JournalEntryComponent
open Utilities.AppError
open DataAccessLayer.QueryParameters
open DataAccessLayer.DbTransaction
open DataAccessLayer.ExecuteReader
open DataAccessLayer.ExecuteNonQuery
open Utilities.FieldUpdate
open Utilities.ResultHelper

let validateJournalEntryHeader
    (transaction: DbTransaction)
    (journalEntryId: JournalEntryHeaderId)
    : Result<unit, AppError> =
    journalEntryId |> JournalEntryHeader.fetchById transaction |> Result.map ignore

let constructNewAndSaveToDb
    (journalEntryHeaderId: JournalEntryHeaderId)
    (financialInstitution: JournalRefFinancialInstitution)
    (referenceText: JournalExternalReferenceText)
    (auditEnvelope: AuditEnvelope)
    (transaction: DbTransaction)
    : Result<JournalEntryExternalReference, AppError> =
    let journalEntryExternalReferenceId = JournalEntryExternalReferenceId.create()
    let now = AuditEnvelope.instant auditEnvelope
    let createdAt = now // REQ-SYS-3.2
    let modifiedAt = now // REQ-SYS-3.2
    result {
        do! journalEntryHeaderId |> validateJournalEntryHeader transaction |> Result.map ignore
        let journalExternalReference =
            JournalEntryExternalReference.create
                journalEntryExternalReferenceId
                journalEntryHeaderId
                financialInstitution
                referenceText
                createdAt
                modifiedAt
        do! JournalEntryExternalReference.insertNewToDb journalExternalReference transaction
        return journalExternalReference
    }

let updateFiAndReferenceText // REQ-JE-4.9
    (transaction: DbTransaction)
    (auditEnvelope: AuditEnvelope)
    (fiUpdate: FieldUpdate<JournalRefFinancialInstitution>)
    (referenceUpdate: FieldUpdate<JournalExternalReferenceText>)
    (journalEntryExternalReferenceId: JournalEntryExternalReferenceId)
    : Result<JournalEntryExternalReference, AppError> =
    let uuid = journalEntryExternalReferenceId |> JournalEntryExternalReferenceId.value
    let baseParams =
        [ { name = "@modified"; value = DbInstant(AuditEnvelope.instant auditEnvelope) } // REQ-SYS-3.3
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
            modified_at = @modified -- REQ-SYS-3.3
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
        let! _ = executeNonQuery query parameters ExactlyOne transaction
        return! journalEntryExternalReferenceId |> JournalEntryExternalReference.fetchById transaction
    }
