module InterfaceBridge.BoundaryConverters.JournalEntryFieldConverters

open InterfaceBridge.BoundaryConverters.AccountFieldConverters
open InterfaceBridge.InterfaceContracts.JournalContracts
open Model
open Model.Ledger.Accounts.AccountComponent
open Model.Ledger.Journaling
open Model.Ledger.Journaling.JournalEntryComponent
open ModelOrchestrator.JournalEntries
open Utilities.AppError
open Utilities.ResultHelper
open DataAccessLayer.DbTransaction

let ``convert JeDescriptionString Option to JeDescription Option``
    (stringOption: string option)
    : Result<JournalEntryDescription option, AppError> =
    let fallibleConverter = (fun string -> string |> JournalEntryDescription.create)
    stringOption |> convertOptionToDesiredTypeWithFallibleConverter fallibleConverter

let ``convert JeSourceString Option to JeSource Option``
    (stringOption: string option)
    : Result<JournalEntrySource option, AppError> =
    let fallibleConverter = (fun string -> string |> JournalEntrySource.create)
    stringOption |> convertOptionToDesiredTypeWithFallibleConverter fallibleConverter

let ``convert JournalEntryLineInput to JournalEntryLinePrimitives``
    (tran: DbTransaction)
    (input: JournalEntryLineInput)
    : Result<AccountId * Money * JournalEntryLineType * JournalEntryLineMemo option, AppError> =
    result {
        let! accountId = input.accountCode |> ``convert AccountCodeString to Id`` tran
        let! amount = input.amount |> Money.fromDecimal
        let! lineType = input.lineType |> JournalEntryLineType.fromString
        let! memo = input.memo |> convertOptionToDesiredTypeWithFallibleConverter JournalEntryLineMemo.create
        return (accountId, amount, lineType, memo)
    }

let ``convert [JournalEntryLineInput list] to [JournalEntryLinePrimitives list]``
    (tran: DbTransaction)
    (input: JournalEntryLineInput list)
    : Result<(AccountId * Money * JournalEntryLineType * JournalEntryLineMemo option) list, AppError> =
    input
    |> List.map(fun x -> x |> ``convert JournalEntryLineInput to JournalEntryLinePrimitives`` tran)
    |> convertListOfResultsToResultsList

let ``convert JournalEntryLine to JournalEntryLineReturn``
    (tran: DbTransaction)
    (model: JournalEntryLine)
    : Result<JournalEntryLineReturn, AppError> =
    result {
        let! accountCode = model |> JournalEntryLine.accountId |> ``convert AccountId to AccountCodeString`` tran
        return
            { id = model |> JournalEntryLine.journalEntryLineId |> JournalEntryLineId.value
              accountCode = accountCode
              amount = model |> JournalEntryLine.amount |> Money.amount
              lineType = model |> JournalEntryLine.lineType |> JournalEntryLineType.toString
              memo = model |> JournalEntryLine.memo |> Option.map(fun x -> x |> JournalEntryLineMemo.value)
              createdAt = model |> JournalEntryLine.createdAt
              modifiedAt = model |> JournalEntryLine.modifiedAt }
    }

let ``convert JournalEntryLine list to JournalEntryLineReturn list``
    (tran: DbTransaction)
    (input: JournalEntryLine list)
    : Result<JournalEntryLineReturn list, AppError> =
    input
    |> List.map(fun x -> x |> ``convert JournalEntryLine to JournalEntryLineReturn`` tran)
    |> convertListOfResultsToResultsList

let ``convert [JournalEntryExternalReferenceInput] to [JournalEntryExternalReferencePrimitives]``
    (input: JournalEntryExternalReferenceInput)
    : Result<JournalRefFinancialInstitution * JournalExternalReferenceText, AppError> =
    result {
        let! fi = input.financialInstitution |> JournalRefFinancialInstitution.create
        let! reference = input.referenceText |> JournalExternalReferenceText.create
        return (fi, reference)
    }

let ``convert [JournalEntryExternalReferenceInput list] to [JournalEntryExternalReferencePrimitives list]``
    (input: JournalEntryExternalReferenceInput list)
    : Result<(JournalRefFinancialInstitution * JournalExternalReferenceText) list, AppError> =
    input
    |> List.map(fun x ->
        x |> ``convert [JournalEntryExternalReferenceInput] to [JournalEntryExternalReferencePrimitives]``)
    |> convertListOfResultsToResultsList

let ``convert [JournalEntryCommentInput] to [JournalEntryCommentPrimitives]``
    (input: JournalEntryCommentInput)
    : Result<JournalEntryHeaderId option * CommentText, AppError> =
    result {
        let secondaryJournalEntryId = input.secondaryJournalEntryId |> Option.map JournalEntryHeaderId.fromGuid
        let! commentText = input.commentText |> CommentText.create
        return secondaryJournalEntryId, commentText
    }

let ``convert [JournalEntryCommentInput list] to [JournalEntryCommentPrimitives list]``
    (input: JournalEntryCommentInput list)
    : Result<(JournalEntryHeaderId option * CommentText) list, AppError> =
    input
    |> List.map(fun x -> x |> ``convert [JournalEntryCommentInput] to [JournalEntryCommentPrimitives]``)
    |> convertListOfResultsToResultsList

let ``convert JournalEntryHeader to JournalEntryHeaderReturn`` (model: JournalEntryHeader) : JournalEntryHeaderReturn =
    { id = model |> JournalEntryHeader.journalEntryHeaderId |> JournalEntryHeaderId.value
      description = model |> JournalEntryHeader.description |> JournalEntryDescription.value
      source = model |> JournalEntryHeader.source |> Option.map(fun x -> x |> JournalEntrySource.value)
      entryDate = model |> JournalEntryHeader.entryDate |> EntryDate.entryDate
      voidedAt = model |> JournalEntryHeader.voidedAt
      createdAt = model |> JournalEntryHeader.createdAt
      modifiedAt = model |> JournalEntryHeader.modifiedAt }

let ``convert JournalEntryExternalReference to JournalEntryExternalReferenceReturn``
    (model: JournalEntryExternalReference)
    : JournalEntryExternalReferenceReturn =
    { id =
        model
        |> JournalEntryExternalReference.journalEntryExternalReferenceId
        |> JournalEntryExternalReferenceId.value
      financialInstitution =
        model |> JournalEntryExternalReference.financialInstitution |> JournalRefFinancialInstitution.value
      referenceText = model |> JournalEntryExternalReference.referenceText |> JournalExternalReferenceText.value
      createdAt = model |> JournalEntryExternalReference.createdAt
      modifiedAt = model |> JournalEntryExternalReference.modifiedAt }

let ``convert JournalEntryExternalReference list to JournalEntryExternalReferenceReturn list``
    (model: JournalEntryExternalReference list)
    : JournalEntryExternalReferenceReturn list =
    model
    |> List.map(fun x -> x |> ``convert JournalEntryExternalReference to JournalEntryExternalReferenceReturn``)

let ``convert JournalEntryComment to JournalEntryCommentReturn``
    (model: JournalEntryComment)
    : JournalEntryCommentReturn =
    { id = model |> JournalEntryComment.journalEntryCommentId |> JournalEntryCommentId.value
      primaryJournalEntryId = model |> JournalEntryComment.primaryJournalEntryId |> JournalEntryHeaderId.value
      secondaryJournalEntryId =
        model |> JournalEntryComment.secondaryJournalEntryId |> Option.map JournalEntryHeaderId.value
      commentText = model |> JournalEntryComment.commentText |> CommentText.value
      createdAt = model |> JournalEntryComment.createdAt
      modifiedAt = model |> JournalEntryComment.modifiedAt }

let ``convert JournalEntryComment list to JournalEntryCommentReturn list``
    (model: JournalEntryComment list)
    : JournalEntryCommentReturn list =
    model |> List.map(fun x -> x |> ``convert JournalEntryComment to JournalEntryCommentReturn``)

let ``convert JournalEntry to JournalEntryReturn``
    (tran: DbTransaction)
    (journalEntry: JournalEntry)
    : Result<JournalEntryReturn, AppError> =
    result {
        let! lines =
            journalEntry
            |> JournalEntry.lines
            |> ``convert JournalEntryLine list to JournalEntryLineReturn list`` tran
        return
            { header = journalEntry |> JournalEntry.header |> ``convert JournalEntryHeader to JournalEntryHeaderReturn``
              lines = lines
              externalReferences =
                journalEntry
                |> JournalEntry.externalReferences
                |> ``convert JournalEntryExternalReference list to JournalEntryExternalReferenceReturn list``
              comments =
                journalEntry
                |> JournalEntry.comments
                |> ``convert JournalEntryComment list to JournalEntryCommentReturn list`` }
    }

let ``convert JournalEntry list to JournalEntryReturn list``
    (tran: DbTransaction)
    (journalEntries: JournalEntry list)
    : Result<JournalEntryReturn list, AppError> =
    journalEntries
    |> List.map(fun x -> x |> ``convert JournalEntry to JournalEntryReturn`` tran)
    |> convertListOfResultsToResultsList
