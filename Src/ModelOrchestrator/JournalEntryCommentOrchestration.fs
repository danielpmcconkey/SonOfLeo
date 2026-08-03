module ModelOrchestrator.JournalEntryCommentOrchestration

open Model.Ledger.Journaling
open Model.Ledger.Journaling.JournalEntryComponent
open Utilities.AppError
open DataAccessLayer.QueryParameters
open DataAccessLayer.ExecuteReader
open DataAccessLayer.ExecuteNonQuery
open Utilities.FieldUpdate
open Utilities.ResultHelper
open Context.Context

let confirmJournalEntryHeader (context: Context) (journalEntryId: JournalEntryHeaderId) : Result<unit, AppError> =
    match journalEntryId |> JournalEntryHeader.fetchById context with
    | Ok _ -> Ok ()
    | Error (DalResultantRowsDidntMatchExpectation(expected, actual)) ->
        if actual = 0
        then Error (JournalEntryHeaderIdDoesntExist (journalEntryId |> JournalEntryHeaderId.value))
        else Error (DalResultantRowsDidntMatchExpectation(expected, actual))
    | Error e -> Error e

let confirmPrimaryAndSecondaryRelationship
    (primaryJournalEntryId: JournalEntryHeaderId)
    (secondaryJournalEntryId: JournalEntryHeaderId option)
    : Result<unit, AppError> =
    match secondaryJournalEntryId with
    | None -> Ok()
    | Some x ->
        if x = primaryJournalEntryId then
            let primaryUuid = primaryJournalEntryId |> JournalEntryHeaderId.value
            let secondaryUuid = x |> JournalEntryHeaderId.value
            Error(JournalEntryCommentPrimaryAndSecondaryIdsAreSame(primaryUuid, secondaryUuid))
        else
            Ok()

let constructNewAndSaveToDb
    (context: Context)
    (primaryJournalEntryId: JournalEntryHeaderId)
    (secondaryJournalEntryId: JournalEntryHeaderId option)
    (commentText: CommentText)
    : Result<JournalEntryComment, AppError> =
    let journalEntryCommentId = JournalEntryCommentId.create()
    let now = context |> getInitiationInstant
    let createdAt = now
    let modifiedAt = now
    result {
        do! match primaryJournalEntryId |> confirmJournalEntryHeader context with
            | Ok _ -> Ok()
            | Error (JournalEntryHeaderIdDoesntExist uuid) -> Error (JournalEntryCommentPrimaryJeHeaderIdNotFound uuid)
            | Error e -> Error e
        do! match secondaryJournalEntryId |> convertOptionToDesiredTypeWithFallibleConverter (confirmJournalEntryHeader context) with
            | Ok _ -> Ok()
            | Error (JournalEntryHeaderIdDoesntExist uuid) -> Error (JournalEntryCommentSecondaryJeHeaderIdNotFound uuid)
            | Error e -> Error e
        do! confirmPrimaryAndSecondaryRelationship primaryJournalEntryId secondaryJournalEntryId
        let journalEntryComment =
            JournalEntryComment.create
                journalEntryCommentId
                primaryJournalEntryId
                secondaryJournalEntryId
                commentText
                createdAt
                modifiedAt
        do! journalEntryComment |> JournalEntryComment.insertNewToDb context
        return journalEntryComment
    }

let updateComment
    (context: Context)
    (journalEntryCommentId: JournalEntryCommentId)
    (commentUpdate: FieldUpdate<CommentText>)
    (secondaryIdUpdate: FieldUpdate<JournalEntryHeaderId option>)
    : Result<JournalEntryComment, AppError> =
    let commentUuid = journalEntryCommentId |> JournalEntryCommentId.value
    let baseParams =
        [ { name = "@modified"; value = DbInstant(context |> getInitiationInstant) }
          { name = "@unique_id"; value = UniqueId commentUuid } ]
    result {
        let! validSecondaryId =
            match secondaryIdUpdate with
            | NoChange -> Ok NoChange
            | SetTo x ->
                result {
                    let! existing = journalEntryCommentId |> (JournalEntryComment.fetchById context)
                    let primaryJournalEntryId = existing |> JournalEntryComment.primaryJournalEntryId
                    do! confirmPrimaryAndSecondaryRelationship primaryJournalEntryId x
                    return (SetTo x)
                }

        let updates =
            [ commentUpdate
              |> FieldUpdate.mapNoChangeToOptionWithConversion(fun x ->
                  (", comment_text = @comment_text",
                   { name = "@comment_text"; value = CharString(x |> CommentText.value) }))

              validSecondaryId
              |> FieldUpdate.mapNoChangeToOptionWithConversion(fun x ->
                  let validUuidOption = x |> Option.map JournalEntryHeaderId.value
                  (", journal_secondary_entry_id = @journal_secondary_entry_id",
                   { name = "@journal_secondary_entry_id"; value = NullableUniqueId validUuidOption })) ]
            |> List.choose id

        let setClauses = updates |> List.map fst |> String.concat ""
        let parameters = baseParams @ (updates |> List.map snd)
        let query =
            $"""    UPDATE ledger.journal_entry_comment
                            set
                                modified_at = @modified
                                {setClauses}
                            WHERE unique_id = @unique_id; """
        let! _ = executeNonQuery (context |> getDatabaseTransaction) query parameters ExactlyOne
        return! journalEntryCommentId |> JournalEntryComment.fetchById context
    }
