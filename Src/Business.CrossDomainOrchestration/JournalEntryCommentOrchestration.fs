module Business.CrossDomainOrchestration.JournalEntryCommentOrchestration

open App.Utility
open App.Utility.IAppError
open App.Utility.Result
open App.DataAccessLayer.DalError
open App.DataAccessLayer.ExecuteReader
open App.Session
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.LedgerError
open Business.FinancialServices.Ledger.JournalEntryComponent
open Business.FinancialServices.Ledger.JournalEntryComment


let private confirmJournalEntryHeader (context: Context.Context) (journalEntryId: JournalEntryHeaderId) : Result<unit, IAppError> =
    journalEntryId |> JournalEntryHeader.fetchById context
    |> whenNoRows (JournalEntryHeaderIdDoesntExist (journalEntryId |> JournalEntryHeaderId.value))
    |> Result.map ignore

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
            | Error (AsError (JournalEntryHeaderIdDoesntExist uuid)) -> Error (JournalEntryCommentPrimaryJeHeaderIdNotFound uuid)
            | other -> other
        do! match secondaryJournalEntryId |> convertOptionToDesiredTypeWithFallibleConverter (confirmJournalEntryHeader context) with
            | Error (AsError (JournalEntryHeaderIdDoesntExist uuid)) -> Error (JournalEntryCommentSecondaryJeHeaderIdNotFound uuid)
            | other -> other |> Result.map ignore
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
    result {
        let! existing =
            journalEntryCommentId |> fetchById context |> whenNoRows (JournalEntryCommentIdDoesntExist commentUuid)
        let! validSecondaryId =
            match secondaryIdUpdate with
            | FieldUpdate.NoChange -> Ok FieldUpdate.NoChange
            | FieldUpdate.SetTo x ->
                result {
                    // the new secondary must exist, checked here rather than left to the foreign key (REQ-SYS-6.3)
                    do! match x |> convertOptionToDesiredTypeWithFallibleConverter (confirmJournalEntryHeader context) with
                        | Error (AsError (JournalEntryHeaderIdDoesntExist uuid)) ->
                            Error (JournalEntryCommentSecondaryJeHeaderIdNotFound uuid)
                        | other -> other |> Result.map ignore
                    do! confirmPrimaryAndSecondaryRelationship (existing |> primaryJournalEntryId) x
                    return (FieldUpdate.SetTo x)
                }

        do!
            JournalEntryComment.update context journalEntryCommentId commentUpdate validSecondaryId
            |> whenNoRows (JournalEntryCommentIdDoesntExist commentUuid)
        return! journalEntryCommentId |> fetchById context
    }
