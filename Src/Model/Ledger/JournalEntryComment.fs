namespace Model.Ledger.Journaling

open System
open Model.Audit
open NodaTime
open Utilities.DAL
open Utilities.ResultCE

type CommentText = private CommentText of string
module CommentText =
    let value (CommentText d) = d 
    let create (raw: string) : Result<CommentText, string> =
        let trimmed = raw.Trim() // REQ-SYS-1.1
        if String.IsNullOrWhiteSpace trimmed then
            Error "CommentText cannot be empty"  // REQ-JE-1.54, REQ-SYS-1.2
        elif trimmed.Length > 2000 then
            Error "CommentText cannot exceed 2000 characters" // REQ-JE-1.54
        else
            Ok (CommentText trimmed)

type JournalEntryComment =
  private  {    uniqueId: Guid // REQ-JE-1.50
                primaryJournalEntryId: Guid // REQ-JE-1.51
                secondaryJournalEntryId: Guid option // REQ-JE-1.52
                commentText: CommentText
                createdAt: Instant
                modifiedAt: Instant }

module JournalEntryComment =
    let uniqueId jec = jec.uniqueId
    let primaryJournalEntryId jec = jec.primaryJournalEntryId
    let secondaryJournalEntryId jec = jec.secondaryJournalEntryId
    let commentText jec = jec.commentText
    let createdAt jec = jec.createdAt
    let modifiedAt jec= jec.modifiedAt
    
    let validateJournalEntryHeader
            (transaction: DbTransaction option)
            (uniqueId: Guid) 
            : Result<unit, string> =
        uniqueId |> JournalEntryHeader.fetchById transaction |> Result.map ignore
    
    let validatePrimaryAndSecondaryRelationship // REQ-JE-1.53
            (primaryJournalEntryId: Guid)
            (secondaryJournalEntryId: Guid option)
            : Result<unit, string> =
        match secondaryJournalEntryId with
        | None -> Ok ()
        | Some x ->
            if x = primaryJournalEntryId
            then Error "Primary and secondary journal entries cannot be the same."
            else Ok ()

    /// validateThenConstruct is your centralized constructor for assembling
    /// and validating component types. all other constructors must
    /// pass into this one
    let private validateThenConstruct
            (uniqueId: Guid) // REQ-JE-5.2
            (primaryJournalEntryId: Guid) // REQ-JE-5.1
            (secondaryJournalEntryId: Guid option) // REQ-JE-5.1
            (commentText: string)
            (createdAt: Instant) // REQ-JE-5.2
            (modifiedAt: Instant) // REQ-JE-5.2
            (transaction: DbTransaction option)
            : Result<JournalEntryComment, string> =
        result {
            let! validCommentText = commentText |> CommentText.create
            do! primaryJournalEntryId |> validateJournalEntryHeader transaction
            do! match secondaryJournalEntryId with
                | None -> Ok ()
                | Some id -> id |> validateJournalEntryHeader transaction
            do! validatePrimaryAndSecondaryRelationship primaryJournalEntryId secondaryJournalEntryId
            return { uniqueId = uniqueId; primaryJournalEntryId = primaryJournalEntryId
                     secondaryJournalEntryId = secondaryJournalEntryId; commentText = validCommentText
                     createdAt = createdAt; modifiedAt = modifiedAt } }

    let constructNew
            (primaryJournalEntryId: Guid) // REQ-JE-5.1
            (secondaryJournalEntryId: Guid option) // REQ-JE-5.1
            (commentText: string)
            (auditEnvelope: AuditEnvelope)
            (transaction: DbTransaction option)
            : Result<JournalEntryComment, string> =
        let uniqueId = Guid.NewGuid() // REQ-JE-5.2
        let now = AuditEnvelope.instant auditEnvelope
        let createdAt =  now // REQ-SYS-3.2
        let modifiedAt = now // REQ-SYS-3.2
        validateThenConstruct uniqueId primaryJournalEntryId secondaryJournalEntryId commentText createdAt modifiedAt transaction
    
    let private insertNewToDb (comment:JournalEntryComment) (transaction: DbTransaction option): Result<unit, string> =
        let query = """
            INSERT INTO ledger.journal_entry_comment(
                unique_id, journal_primary_entry_id, journal_secondary_entry_id, comment_text, created_at, modified_at)
            VALUES (
                @unique_id, @journal_primary_entry_id, @journal_secondary_entry_id, @comment_text, @created_at, @modified_at);"""
        let parameters = [ //  REQ-DAL-2.1, REQ-DAL-2.3 
            { name = "@unique_id"; value = UniqueId comment.uniqueId }
            { name = "@journal_primary_entry_id"; value = UniqueId comment.primaryJournalEntryId }
            { name = "@journal_secondary_entry_id"; value = NullableUniqueId comment.secondaryJournalEntryId }
            { name = "@comment_text"; value = CharString (comment.commentText |> CommentText.value) };
            { name = "@created_at"; value = DbInstant comment.createdAt };
            { name = "@modified_at"; value = DbInstant comment.modifiedAt };
        ]
        executeNonQuery query parameters ExactlyOne transaction

    let constructNewAndSaveToDb // REQ-JE-5.1
            (primaryJournalEntryId: Guid)
            (secondaryJournalEntryId: Guid option)
            (commentText: string)
            (auditEnvelope: AuditEnvelope)
            (transaction: DbTransaction option)
            : Result<JournalEntryComment, string> =
        result {
            let! validJournalEntryComment =
                constructNew primaryJournalEntryId secondaryJournalEntryId commentText auditEnvelope transaction
            let! () = insertNewToDb validJournalEntryComment transaction
            return validJournalEntryComment }

    /// The mapRow function is used to pass into DAL read functions to let DAL know
    /// how to map our query columns. Thus, we don't need to know anything about the
    /// underlying database architecture in this module and the DAL module doesn't
    /// need to know anything about our module here 
    let mapRowForDbRead
            (transaction: DbTransaction option)
            (row: RowReader)
            : Result<JournalEntryComment, string> =
        validateThenConstruct
            ( row |> RowReader.getUuid "unique_id" )
            ( row |> RowReader.getUuid "journal_primary_entry_id" )
            ( row |> RowReader.getUuidOption "journal_secondary_entry_id" )
            ( row |> RowReader.getString "comment_text" )
            ( row |> RowReader.getInstant "created_at" )
            ( row |> RowReader.getInstant "modified_at" )
            transaction

    let private readRowsFromDb
            (predicate: string option)
            (limit: int option)
            (orderBy: string option)
            (parameters: QueryParameter list)
            (expectedRows: AcceptableExpectedRows)
            (transaction: DbTransaction option)
            : Result<JournalEntryComment list, string> = 
        let select = """
                jec.unique_id, jec.journal_primary_entry_id, jec.journal_secondary_entry_id,
                jec.comment_text, jec.created_at, jec.modified_at
            """
        let from = "ledger.journal_entry_comment jec"
        let query = buildReadQuery select from None predicate limit None orderBy
        executeReaderQuery query parameters (mapRowForDbRead transaction) expectedRows transaction

    let fetchById
            (transaction: DbTransaction option)
            (uniqueId: Guid)
            : Result<JournalEntryComment, string> = 
        let predicate = "jec.unique_id = @unique_id"
        let parameters = [{ name = "@unique_id"; value = UniqueId uniqueId };] // REQ-DAL-2.3
        readRowsFromDb (Some predicate) None None parameters ExactlyOne transaction
        |> Result.map List.head

    /// fetchByJournalEntryId returns all comments associated to a Journal
    /// Entry, whether as the primary or secondary, ordered by comment create
    /// instant
    let fetchByJournalEntryId
            (transaction: DbTransaction option)
            (uniqueId: Guid)
            : Result<JournalEntryComment list, string> = 
        let predicate = "jec.journal_primary_entry_id = @unique_id or jec.journal_secondary_entry_id = @unique_id"
        let parameters = [{ name = "@unique_id"; value = UniqueId uniqueId };] // REQ-DAL-2.3
        let orderBy = "created_at"
        readRowsFromDb (Some predicate) None (Some orderBy) parameters AnyQuantityIsAcceptable transaction
        
    let updateCommentText // REQ-JE-5.3
            (auditEnvelope: AuditEnvelope)
            (uniqueId: Guid)
            (newText: string)
            (transaction: DbTransaction option)
            : Result<JournalEntryComment, string> = 
        let query = $"""
            UPDATE ledger.journal_entry_comment
            set
                modified_at = @modified -- REQ-SYS-3.3
                , comment_text = @new_text
            WHERE unique_id = @unique_id
            ;
        """
        result {
            let! validCommentText = CommentText.create newText
            let parameters = [
                    { name = "@unique_id"; value = UniqueId uniqueId };
                    { name = "@modified"; value = DbInstant (AuditEnvelope.instant auditEnvelope) } // REQ-SYS-3.3 
                    { name = "@new_text"; value = CharString (validCommentText |> CommentText.value)}
                ]
            let! _ = executeNonQuery query parameters ExactlyOne transaction
            return! uniqueId |> fetchById transaction
        }
