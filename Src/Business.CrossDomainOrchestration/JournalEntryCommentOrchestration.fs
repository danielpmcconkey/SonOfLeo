module Business.CrossDomainOrchestration.JournalEntryCommentOrchestration

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
open Business.FinancialServices.Ledger.JournalEntryComponent
open Business.FinancialServices.Ledger.JournalEntryComment


let private confirmJournalEntryHeader (context: Context.Context) (journalEntryId: JournalEntryHeaderId) : Result<unit, IAppError> =
    match journalEntryId |> JournalEntryHeader.fetchById context with
    | Ok _ -> Ok ()
    | Error e ->
        if e.DomainName = nameof DalError && e.CaseName = nameof DalError.DalResultantRowsDidntMatchExpectation
        then Error (JournalEntryHeaderIdDoesntExist (journalEntryId |> JournalEntryHeaderId.value))
        else Error e

let private confirmPrimaryAndSecondaryRelationship
    (primaryJournalEntryId: JournalEntryHeaderId)
    (secondaryJournalEntryId: JournalEntryHeaderId option)
    : Result<unit, IAppError> =
    match secondaryJournalEntryId with
    | None -> Ok()
    | Some x ->
        if x = primaryJournalEntryId then
            let primaryUuid = primaryJournalEntryId |> JournalEntryHeaderId.value
            let secondaryUuid = x |> JournalEntryHeaderId.value
            Error(JournalEntryCommentPrimaryAndSecondaryIdsAreSame(primaryUuid, secondaryUuid))
        else
            Ok()

let constructNewAndPersist
    (context: Context.Context)
    (primaryJournalEntryId: JournalEntryHeaderId)
    (secondaryJournalEntryId: JournalEntryHeaderId option)
    (commentText: CommentText)
    : Result<JournalEntryComment, IAppError> =
    let journalEntryCommentId = JournalEntryCommentId.create()
    let now = context |> Context.getInitiationInstant
    let createdAt = now
    let modifiedAt = now
    result {
        do! match primaryJournalEntryId |> confirmJournalEntryHeader context with
            | Ok _ -> Ok()
            | Error e ->
                if e.DomainName = nameof LedgerError && e.CaseName = nameof JournalEntryHeaderIdDoesntExist
                then Error (JournalEntryCommentPrimaryJeHeaderIdNotFound (primaryJournalEntryId |> JournalEntryHeaderId.value))
                else Error e
        do! match secondaryJournalEntryId |> convertOptionToDesiredTypeWithFallibleConverter (confirmJournalEntryHeader context) with
            | Ok _ -> Ok()
            | Error e ->
                if e.DomainName = nameof LedgerError && e.CaseName = nameof JournalEntryHeaderIdDoesntExist
                then
                    let uuid = secondaryJournalEntryId |> Option.get |> JournalEntryHeaderId.value
                    Error (JournalEntryCommentPrimaryJeHeaderIdNotFound uuid)
                else Error e
        do! confirmPrimaryAndSecondaryRelationship primaryJournalEntryId secondaryJournalEntryId
        let journalEntryComment =
            create
                journalEntryCommentId
                primaryJournalEntryId
                secondaryJournalEntryId
                commentText
                createdAt
                modifiedAt
        do! journalEntryComment |> persist context
        return journalEntryComment
    }

let updateComment
    (context: Context.Context)
    (journalEntryCommentId: JournalEntryCommentId)
    (commentUpdate: FieldUpdate.FieldUpdate<CommentText>)
    (secondaryIdUpdate: FieldUpdate.FieldUpdate<JournalEntryHeaderId option>)
    : Result<JournalEntryComment, IAppError> =
    let commentUuid = journalEntryCommentId |> JournalEntryCommentId.value
    let baseParams =
        [ { name = "@modified"; value = DbInstant(context |> Context.getInitiationInstant) }
          { name = "@unique_id"; value = UniqueId commentUuid } ]
    result {
        let! validSecondaryId =
            match secondaryIdUpdate with
            | FieldUpdate.NoChange -> Ok FieldUpdate.NoChange
            | FieldUpdate.SetTo x ->
                result {
                    let! existing = journalEntryCommentId |> (fetchById context)
                    let primaryJournalEntryId = existing |> primaryJournalEntryId
                    do! confirmPrimaryAndSecondaryRelationship primaryJournalEntryId x
                    return (FieldUpdate.SetTo x)
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
        do!
            if updates.IsEmpty then
                Error(JournalEntryCommentUpdateNoOp)
            else
                Ok()
        let setClauses = updates |> List.map fst |> String.concat ""
        let parameters = baseParams @ (updates |> List.map snd)
        let queryStatement =
            $"""    UPDATE ledger.journal_entry_comment
                            set
                                modified_at = @modified
                                {setClauses}
                            WHERE unique_id = @unique_id; """
        let! _ = executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters ExactlyOne
        return! journalEntryCommentId |> fetchById context
    }
