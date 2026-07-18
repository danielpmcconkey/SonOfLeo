module ModelOrchestrator.JournalEntryCommentOrchestration

open Model.Audit
open Model.Ledger.Journaling
open Model.Ledger.Journaling.JournalEntryComponent
open Utilities.AppError
open Utilities.DAL
open Utilities.ResultHelper
    
let validateJournalEntryHeader
        (transaction: DbTransaction option)
        (journalEntryId: JournalEntryHeaderId) 
        : Result<unit, AppError> =
    journalEntryId |> JournalEntryHeader.fetchById transaction |> Result.map ignore

let validatePrimaryAndSecondaryRelationship // REQ-JE-1.53
        (primaryJournalEntryId: JournalEntryHeaderId)
        (secondaryJournalEntryId: JournalEntryHeaderId option)
        : Result<unit, AppError> =
    match secondaryJournalEntryId with
    | None -> Ok ()
    | Some x ->
        if x = primaryJournalEntryId
        then
            let primaryUuid = primaryJournalEntryId |> JournalEntryHeaderId.value
            let secondaryUuid = x |> JournalEntryHeaderId.value
            Error (JournalEntryCommentPrimaryAndSecondaryIdsAreSame (primaryUuid, secondaryUuid))
        else Ok ()

let constructNewAndSaveToDb // REQ-JE-5.1
        (primaryJournalEntryId: JournalEntryHeaderId)
        (secondaryJournalEntryId: JournalEntryHeaderId option)
        (commentText: CommentText)
        (auditEnvelope: AuditEnvelope)
        (transaction: DbTransaction option)
        : Result<JournalEntryComment, AppError> =
    let journalEntryCommentId = JournalEntryCommentId.create () // REQ-JE-5.2
    let now = AuditEnvelope.instant auditEnvelope
    let createdAt =  now // REQ-SYS-3.2
    let modifiedAt = now // REQ-SYS-3.2
    result {
        do! primaryJournalEntryId |> validateJournalEntryHeader transaction
        do! match secondaryJournalEntryId with
            | None -> Ok ()
            | Some id -> id |> validateJournalEntryHeader transaction
        do! validatePrimaryAndSecondaryRelationship primaryJournalEntryId secondaryJournalEntryId
        let journalEntryComment =
            JournalEntryComment.create journalEntryCommentId primaryJournalEntryId secondaryJournalEntryId
                commentText createdAt modifiedAt
        do! JournalEntryComment.insertNewToDb journalEntryComment transaction
        return journalEntryComment }
        
let updateComment // REQ-JE-5.3
        (auditEnvelope: AuditEnvelope)
        (journalEntryCommentId: JournalEntryCommentId)
        (commentUpdate: FieldUpdate<CommentText>)
        (secondaryIdUpdate: FieldUpdate<JournalEntryHeaderId option>)
        (transaction: DbTransaction option)
        : Result<JournalEntryComment, AppError> =        
    let commentUuid = journalEntryCommentId |> JournalEntryCommentId.value
    let baseParams = [
        { name = "@modified"; value = DbInstant (AuditEnvelope.instant auditEnvelope) } // REQ-SYS-3.3 
        { name = "@unique_id"; value = UniqueId commentUuid };
    ]
    result {
        let! validSecondaryId =
            match secondaryIdUpdate with
                | NoChange -> Ok NoChange
                | SetTo x -> result {
                    let! existing = journalEntryCommentId |> (JournalEntryComment.fetchById transaction)
                    let primaryJournalEntryId = existing |> JournalEntryComment.primaryJournalEntryId
                    do! validatePrimaryAndSecondaryRelationship primaryJournalEntryId x
                    return (SetTo x)
                    }
        let updates =
            [
                match commentUpdate with
                | NoChange -> None
                | SetTo x ->
                    Some (", comment_text = @comment_text",
                          { name = "@comment_text"; value = CharString (x |> CommentText.value) })
                
                match validSecondaryId with
                | NoChange -> None
                | SetTo x ->
                    let validUuidOption = x |> Option.map JournalEntryHeaderId.value
                    Some (", journal_secondary_entry_id = @journal_secondary_entry_id",
                          { name = "@journal_secondary_entry_id"; value = NullableUniqueId validUuidOption })
            ] |> List.choose id
        let setClauses = updates |> List.map fst |> String.concat ""
        let parameters = baseParams @ (updates |> List.map snd)
        let query = $"""    UPDATE ledger.journal_entry_comment
                            set
                                modified_at = @modified -- REQ-SYS-3.3
                                {setClauses}
                            WHERE unique_id = @unique_id; """
        let! _ = executeNonQuery query parameters ExactlyOne transaction
        return! journalEntryCommentId |> JournalEntryComment.fetchById transaction
    }