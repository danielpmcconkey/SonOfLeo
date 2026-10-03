module Business.FinancialServices.Ledger.JournalEntryComment

open NodaTime
open App.Utility.IAppError
open App.Utility.FieldUpdate
open App.DataAccessLayer.QueryParameter
open App.DataAccessLayer.ExecuteReader
open App.DataAccessLayer.ExecuteNonQuery
open App.Session
open Business.FinancialServices.Ledger.LedgerError
open Business.FinancialServices.Ledger.JournalEntryComponent

type JournalEntryComment =
    private
        { journalEntryCommentId: JournalEntryCommentId
          primaryJournalEntryId: JournalEntryHeaderId
          secondaryJournalEntryId: JournalEntryHeaderId option
          commentText: CommentText
          createdAt: Instant
          modifiedAt: Instant }

let journalEntryCommentId jec = jec.journalEntryCommentId
let primaryJournalEntryId jec = jec.primaryJournalEntryId
let secondaryJournalEntryId jec = jec.secondaryJournalEntryId
let commentText jec = jec.commentText
let createdAt jec = jec.createdAt
let modifiedAt jec = jec.modifiedAt

let create
    (journalEntryCommentId: JournalEntryCommentId)
    (primaryJournalEntryId: JournalEntryHeaderId)
    (secondaryJournalEntryId: JournalEntryHeaderId option)
    (commentText: CommentText)
    (createdAt: Instant)
    (modifiedAt: Instant)
    : JournalEntryComment =
    { journalEntryCommentId = journalEntryCommentId
      primaryJournalEntryId = primaryJournalEntryId
      secondaryJournalEntryId = secondaryJournalEntryId
      commentText = commentText
      createdAt = createdAt
      modifiedAt = modifiedAt }

let persist (context: Context.Context) (comment: JournalEntryComment) : Result<unit, IAppError> =
    let queryStatement =
        """
        INSERT INTO ledger.journal_entry_comment(
            unique_id, journal_primary_entry_id, journal_secondary_entry_id, comment_text, created_at, modified_at)
        VALUES (
            @unique_id, @journal_primary_entry_id, @journal_secondary_entry_id, @comment_text, @created_at, @modified_at);"""
    let commentUuid = comment.journalEntryCommentId |> JournalEntryCommentId.value
    let primaryUuid = comment.primaryJournalEntryId |> JournalEntryHeaderId.value
    let secondaryUuid = comment.secondaryJournalEntryId |> Option.map JournalEntryHeaderId.value
    let parameters =
        [
          { name = "@unique_id"; value = UniqueId commentUuid }
          { name = "@journal_primary_entry_id"; value = UniqueId primaryUuid }
          { name = "@journal_secondary_entry_id"; value = NullableUniqueId secondaryUuid }
          { name = "@comment_text"; value = CharString(comment.commentText |> CommentText.value) }
          { name = "@created_at"; value = DbInstant comment.createdAt }
          { name = "@modified_at"; value = DbInstant comment.modifiedAt } ]
    executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters ExactlyOne

let private mapRawForDbRead (row: RowReader) =
    (row |> RowReader.getUuid "unique_id"),
    (row |> RowReader.getUuid "journal_primary_entry_id"),
    (row |> RowReader.getUuidOption "journal_secondary_entry_id"),
    (row |> RowReader.getString "comment_text"),
    (row |> RowReader.getInstant "created_at"),
    (row |> RowReader.getInstant "modified_at")

let private reconstitute raw : Result<JournalEntryComment, IAppError> =
    let id, primaryJeId, secondaryJeId, commentTextStr, createdAt, modifiedAt = raw
    let journalEntryCommentId = id |> JournalEntryCommentId.fromGuid
    let primaryJournalEntryId = primaryJeId |> JournalEntryHeaderId.fromGuid
    let secondaryJournalEntryId = secondaryJeId |> Option.map JournalEntryHeaderId.fromGuid
    let commentTextResult = commentTextStr |> CommentText.create
    match commentTextResult with
    | Error e -> Error e
    | Ok commentText ->
        Ok
            { journalEntryCommentId = journalEntryCommentId
              primaryJournalEntryId = primaryJournalEntryId
              secondaryJournalEntryId = secondaryJournalEntryId
              commentText = commentText
              createdAt = createdAt
              modifiedAt = modifiedAt }

let private query
    (context: Context.Context)
    (predicate: string option)
    (limit: int option)
    (orderBy: string option)
    (parameters: QueryParameter list)
    (expectedRows: AcceptableExpectedRows)
    : Result<JournalEntryComment list, IAppError> =
    let select =
        """
            jec.unique_id, jec.journal_primary_entry_id, jec.journal_secondary_entry_id,
            jec.comment_text, jec.created_at, jec.modified_at
        """
    let from = "ledger.journal_entry_comment jec"
    let queryStatement = buildReadQuery None select from None predicate limit None orderBy
    executeReaderQuery
        (context |> Context.getDatabaseTransaction)
        queryStatement
        parameters
        mapRawForDbRead
        reconstitute
        expectedRows

let fetchById
    (context: Context.Context)
    (journalEntryCommentId: JournalEntryCommentId)
    : Result<JournalEntryComment, IAppError> =
    let uuid = journalEntryCommentId |> JournalEntryCommentId.value
    let predicate = "jec.unique_id = @unique_id"
    let parameters = [ { name = "@unique_id"; value = UniqueId uuid } ]
    query context (Some predicate) None None parameters ExactlyOne |> Result.map List.head

let fetchByJournalEntryId
    (context: Context.Context)
    (journalEntryId: JournalEntryHeaderId)
    : Result<JournalEntryComment list, IAppError> =
    let uuid = journalEntryId |> JournalEntryHeaderId.value
    let predicate =
        "jec.journal_primary_entry_id = @unique_id or jec.journal_secondary_entry_id = @unique_id"
    let parameters = [ { name = "@unique_id"; value = UniqueId uuid } ]
    let orderBy = "created_at"
    query context (Some predicate) None (Some orderBy) parameters AnyQuantityIsAcceptable

/// fetchByJournalEntryHeaderIdList only pull comments whose primary header ID is in the ID list because its
/// purpose in this code base is to facilitate rapid assembly of full journal entry composite entities. If a header
/// ID is referenced in a comment as a secondary, but that comment's primary header ID isn't already in the list of
/// header IDs to pull for, then that comment isn't needed in the final assembly.
let fetchByJournalEntryHeaderIdList
    (context: Context.Context)
    (journalEntryHeaderIds: JournalEntryHeaderId list)
    : Result<JournalEntryComment list, IAppError> =
    if journalEntryHeaderIds |> List.isEmpty then Error JournalEntryHeaderIdListCannotBeEmpty else
    let ordinals = [ 1 .. journalEntryHeaderIds.Length ]
    let zipped = List.zip ordinals journalEntryHeaderIds
    let namesAndParameters =
        zipped
        |> List.map(fun (ordinal, id) ->
            let uuid = id |> JournalEntryHeaderId.value
            let name = $"@journal_entry_id{ordinal}"
            let parameter = { name = name; value = UniqueId uuid }
            name, parameter)
    let names = namesAndParameters |> List.map fst |> String.concat ", "
    let parameters = namesAndParameters |> List.map snd
    let predicate = $"jec.journal_primary_entry_id in ({names})"
    query context (Some predicate) None None parameters AnyQuantityIsAcceptable

/// update writes the changed fields of one comment, and refuses a call that changes nothing.
let update
    (context: Context.Context)
    (journalEntryCommentId: JournalEntryCommentId)
    (commentUpdate: FieldUpdate<CommentText>)
    (secondaryIdUpdate: FieldUpdate<JournalEntryHeaderId option>)
    : Result<unit, IAppError> =
    let updates =
        [ commentUpdate
          |> mapNoChangeToOptionWithConversion(fun x ->
              (", comment_text = @comment_text",
               { name = "@comment_text"; value = CharString(x |> CommentText.value) }))

          secondaryIdUpdate
          |> mapNoChangeToOptionWithConversion(fun x ->
              let validUuidOption = x |> Option.map JournalEntryHeaderId.value
              (", journal_secondary_entry_id = @journal_secondary_entry_id",
               { name = "@journal_secondary_entry_id"; value = NullableUniqueId validUuidOption })) ]
        |> List.choose id
    if updates.IsEmpty then Error(JournalEntryCommentUpdateNoOp) else
    let setClauses = updates |> List.map fst |> String.concat ""
    let parameters =
        [ { name = "@modified"; value = DbInstant(context |> Context.getInitiationInstant) }
          { name = "@unique_id"; value = UniqueId(journalEntryCommentId |> JournalEntryCommentId.value) } ]
        @ (updates |> List.map snd)
    let queryStatement =
        $"""
        UPDATE ledger.journal_entry_comment
        set
            modified_at = @modified
            {setClauses}
        WHERE unique_id = @unique_id;
    """
    executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters ExactlyOne
