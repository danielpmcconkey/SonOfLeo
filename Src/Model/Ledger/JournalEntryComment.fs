namespace Model.Ledger.Journaling

open DataAccessLayer.ExecuteNonQuery
open Model.Ledger.Journaling.JournalEntryComponent
open NodaTime
open Utilities.AppError
open DataAccessLayer.QueryParameters
open DataAccessLayer.ExecuteReader
open Context.Context

type JournalEntryComment =
    private
        { journalEntryCommentId: JournalEntryCommentId
          primaryJournalEntryId: JournalEntryHeaderId
          secondaryJournalEntryId: JournalEntryHeaderId option
          commentText: CommentText
          createdAt: Instant
          modifiedAt: Instant }

module JournalEntryComment =
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

    let insertNewToDb (context: Context) (comment: JournalEntryComment) : Result<unit, AppError> =
        let query =
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
        executeNonQuery (context |> getDatabaseTransaction) query parameters ExactlyOne

    /// The mapRow function is used to pass into DAL read functions to let DAL know
    /// how to map our query columns. Thus, we don't need to know anything about the
    /// underlying database architecture in this module and the DAL module doesn't
    /// need to know anything about our module here
    let private mapRawForDbRead (row: RowReader) =
        (row |> RowReader.getUuid "unique_id"),
        (row |> RowReader.getUuid "journal_primary_entry_id"),
        (row |> RowReader.getUuidOption "journal_secondary_entry_id"),
        (row |> RowReader.getString "comment_text"),
        (row |> RowReader.getInstant "created_at"),
        (row |> RowReader.getInstant "modified_at")

    /// reconstitute constructs from primitives, performing zero validation at
    /// the collective level. All fields are assumed to have come from a
    /// trusted source (e.g. the database) where such validation occurred at
    /// the time of writing the entity. Important: no additional DB lookups can
    /// be triggered inside this function since it is called within a database
    /// reader.
    let private reconstitute raw : Result<JournalEntryComment, AppError> =
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

    let private readRowsFromDb
        (context: Context)
        (predicate: string option)
        (limit: int option)
        (orderBy: string option)
        (parameters: QueryParameter list)
        (expectedRows: AcceptableExpectedRows)
        : Result<JournalEntryComment list, AppError> =
        let select =
            """
                jec.unique_id, jec.journal_primary_entry_id, jec.journal_secondary_entry_id,
                jec.comment_text, jec.created_at, jec.modified_at
            """
        let from = "ledger.journal_entry_comment jec"
        let query = buildReadQuery select from None predicate limit None orderBy
        executeReaderQuery
            (context |> getDatabaseTransaction)
            query
            parameters
            mapRawForDbRead
            reconstitute
            expectedRows

    let fetchById
        (context: Context)
        (journalEntryCommentId: JournalEntryCommentId)
        : Result<JournalEntryComment, AppError> =
        let uuid = journalEntryCommentId |> JournalEntryCommentId.value
        let predicate = "jec.unique_id = @unique_id"
        let parameters = [ { name = "@unique_id"; value = UniqueId uuid } ]
        readRowsFromDb context (Some predicate) None None parameters ExactlyOne |> Result.map List.head

    /// fetchByJournalEntryId returns all comments associated to a Journal
    /// Entry, whether as the primary or secondary, ordered by comment create
    /// instant
    let fetchByJournalEntryId
        (context: Context)
        (journalEntryId: JournalEntryHeaderId)
        : Result<JournalEntryComment list, AppError> =
        let uuid = journalEntryId |> JournalEntryHeaderId.value
        let predicate =
            "jec.journal_primary_entry_id = @unique_id or jec.journal_secondary_entry_id = @unique_id"
        let parameters = [ { name = "@unique_id"; value = UniqueId uuid } ]
        let orderBy = "created_at"
        readRowsFromDb context (Some predicate) None (Some orderBy) parameters AnyQuantityIsAcceptable

    /// fetchByJournalEntryHeaderIdList only pull comments whose primary header ID is in the ID list because its
    /// purpose in this code base is to facilitate rapid assembly of full journal entry composite entities. If a header
    /// ID is referenced in a comment as a secondary, but that comment's primary header ID isn't already in the list of
    /// header IDs to pull for, then that comment isn't needed in the final assembly.
    let fetchByJournalEntryHeaderIdList
        (context: Context)
        (journalEntryHeaderIds: JournalEntryHeaderId list)
        : Result<JournalEntryComment list, AppError> =
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
        readRowsFromDb context (Some predicate) None None parameters AnyQuantityIsAcceptable
