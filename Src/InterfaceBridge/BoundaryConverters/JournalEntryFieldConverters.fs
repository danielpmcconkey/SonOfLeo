module InterfaceBridge.BoundaryConverters.JournalEntryFieldConverters

open InterfaceBridge.BoundaryConverters.AccountFieldConverters
open InterfaceBridge.InterfaceContracts.JournalContracts
open Model
open Model.Ledger.JournalEntryPrimitives
open Model.Ledger.Journaling
open Model.Ledger.Journaling.JournalEntryComponent
open InterfaceBridge.BoundaryConverters.GenericFieldHelpers
open ModelOrchestrator.JournalEntries
open Utilities
open Utilities.ResultCE

let ``convert JeDescriptionString Option to JeDescription Option``
        (stringOption: string option)
        : Result<JournalEntryDescription option, AppError> =
    let fallibleConverter = (fun string -> string |> JournalEntryDescription.create)
    stringOption
    |> ``convert Option to Desired Type with Fallible Converter`` fallibleConverter

let ``convert JeSourceString Option to JeSource Option``
        (stringOption: string option)
        : Result<JournalEntrySource option, AppError> =
    let fallibleConverter = (fun string -> string |> JournalEntrySource.create)
    stringOption
    |> ``convert Option to Desired Type with Fallible Converter`` fallibleConverter

let ``convert JournalEntryLineInput to JournalEntryLinePrimitives``
        (input: JournalEntryLineInput)
        : Result<JournalEntryLinePrimitives, AppError> =
    result {
        let! accountId = input.accountCode |> ``convert AccountCodeString to AccountUuid``
        return {
                accountId = accountId
                amount = input.amount
                lineType = input.lineType
                memo = input.memo } }

let ``convert JournalEntryLineInput list to JournalEntryLinePrimitives list``
        (input: JournalEntryLineInput list)
        : Result<JournalEntryLinePrimitives list, AppError> =
    input
    |> List.map(fun x -> x |> ``convert JournalEntryLineInput to JournalEntryLinePrimitives``)
    |> ListHelper.listOfResultsToResultsList

let ``convert JournalEntryLine to JournalEntryLineReturn``
        (model: JournalEntryLine)
        : Result<JournalEntryLineReturn, AppError> = result {
     let! accountCode = model |> JournalEntryLine.accountId |> ``convert AccountId to AccountCodeString``
     return {   id = model |> JournalEntryLine.uniqueId
                accountCode = accountCode
                amount = model |> JournalEntryLine.amount |> Money.amount
                lineType = model |> JournalEntryLine.lineType |> JournalEntryLineType.toString
                memo = model |> JournalEntryLine.memo |> Option.map(fun x -> x |> JournalEntryLineMemo.value)
                createdAt = model |> JournalEntryLine.createdAt
                modifiedAt = model |> JournalEntryLine.modifiedAt } }

let ``convert JournalEntryLine list to JournalEntryLineReturn list``
        (input: JournalEntryLine list)
        : Result<JournalEntryLineReturn list, AppError> =
    input
    |> List.map(fun x -> x |> ``convert JournalEntryLine to JournalEntryLineReturn``)
    |> ListHelper.listOfResultsToResultsList

let ``convert JournalEntryExternalReferenceInput to JournalEntryExternalReferencePrimitives``
        (input: JournalEntryExternalReferenceInput)
        : JournalEntryExternalReferencePrimitives = {
    financialInstitution = input.financialInstitution 
    referenceText = input.referenceText }

let ``convert JournalEntryExternalReferenceInput list to JournalEntryExternalReferencePrimitives list``
        (input: JournalEntryExternalReferenceInput list)
        : JournalEntryExternalReferencePrimitives list =
    input |> List.map(fun x -> x |> ``convert JournalEntryExternalReferenceInput to JournalEntryExternalReferencePrimitives``)

let ``convert JournalEntryCommentInput to JournalEntryCommentPrimitives``
        (input: JournalEntryCommentInput)
        : JournalEntryCommentPrimitives = {
    secondaryJournalEntryId = input.secondaryJournalEntryId
    commentText = input.commentText }

let ``convert JournalEntryCommentInput list to JournalEntryCommentPrimitives list``
        (input: JournalEntryCommentInput list)
        : JournalEntryCommentPrimitives list = 
    input |> List.map(fun x -> x |> ``convert JournalEntryCommentInput to JournalEntryCommentPrimitives``)

let ``convert JournalEntryHeaderInput to JournalEntryHeaderPrimitives`` 
        (input: JournalEntryHeaderInput)
        : JournalEntryHeaderPrimitives = {
    description = input.description
    source = input.source
    entryDate = input.entryDate
    voidedAt = None } // creating a new JE that's been pre-voided is against the rules

let ``convert JournalEntryInput to JournalEntryPrimitives``
        (input: JournalEntryInput)
        : Result<JournalEntryPrimitives, AppError> = result {
    let! lines = input.lines |> ``convert JournalEntryLineInput list to JournalEntryLinePrimitives list``
    return { header = input.header |> ``convert JournalEntryHeaderInput to JournalEntryHeaderPrimitives``
             lines = lines
             externalReferences =
                 input.externalReferences
                 |> ``convert JournalEntryExternalReferenceInput list to JournalEntryExternalReferencePrimitives list``
             comments =
                 input.comments |> ``convert JournalEntryCommentInput list to JournalEntryCommentPrimitives list`` } }

let ``convert JournalEntryHeader to JournalEntryHeaderReturn``
        (model: JournalEntryHeader)
        : JournalEntryHeaderReturn = {
    id = model |> JournalEntryHeader.journalEntryId
    description = model |> JournalEntryHeader.description |> JournalEntryDescription.value
    source = model |> JournalEntryHeader.source |> Option.map(fun x -> x |> JournalEntrySource.value)
    entryDate = model |> JournalEntryHeader.entryDate |> EntryDate.entryDate
    voidedAt = model |> JournalEntryHeader.voidedAt
    createdAt = model |> JournalEntryHeader.createdAt
    modifiedAt = model |> JournalEntryHeader.modifiedAt }

let ``convert JournalEntryExternalReference to JournalEntryExternalReferenceReturn``
        (model: JournalEntryExternalReference)
        : JournalEntryExternalReferenceReturn = {
    id = model |> JournalEntryExternalReference.journalEntryExternalReferenceId
    financialInstitution = model |> JournalEntryExternalReference.financialInstitution |> JournalRefFinancialInstitution.value
    referenceText = model |> JournalEntryExternalReference.referenceText |> JournalExternalReferenceText.value
    createdAt = model |> JournalEntryExternalReference.createdAt
    modifiedAt = model |> JournalEntryExternalReference.modifiedAt }

let ``convert JournalEntryExternalReference list to JournalEntryExternalReferenceReturn list``
        (model: JournalEntryExternalReference list)
        : JournalEntryExternalReferenceReturn list =
    model |> List.map(fun x -> x |> ``convert JournalEntryExternalReference to JournalEntryExternalReferenceReturn``)

let ``convert JournalEntryComment to JournalEntryCommentReturn``
        (model: JournalEntryComment)
        : JournalEntryCommentReturn = {
    id = model |> JournalEntryComment.uniqueId
    secondaryJournalEntryId = model |> JournalEntryComment.secondaryJournalEntryId
    commentText = model |> JournalEntryComment.commentText |> CommentText.value
    createdAt = model |> JournalEntryComment.createdAt
    modifiedAt = model |> JournalEntryComment.modifiedAt }

let ``convert JournalEntryComment list to JournalEntryCommentReturn list``
        (model: JournalEntryComment list)
        : JournalEntryCommentReturn list =
    model |> List.map(fun x -> x |> ``convert JournalEntryComment to JournalEntryCommentReturn``)
    
let ``convert JournalEntry to JournalEntryReturn``
    (journalEntry: JournalEntry)
    : Result<JournalEntryReturn, AppError> =
    result {
        let! lines = journalEntry |> JournalEntryCreationAndConstruction.lines |> ``convert JournalEntryLine list to JournalEntryLineReturn list``
        return {
        header = journalEntry |> JournalEntryCreationAndConstruction.header |> ``convert JournalEntryHeader to JournalEntryHeaderReturn``
        lines = lines
        externalReferences =
            journalEntry
            |> JournalEntryCreationAndConstruction.externalReferences
            |> ``convert JournalEntryExternalReference list to JournalEntryExternalReferenceReturn list``
        comments =
            journalEntry
            |> JournalEntryCreationAndConstruction.comments
            |> ``convert JournalEntryComment list to JournalEntryCommentReturn list`` } }
    
let ``convert JournalEntry list to JournalEntryReturn list``
    (journalEntries: JournalEntry list)
    : Result<JournalEntryReturn list, AppError> =
    journalEntries
    |> List.map(fun x -> x |> ``convert JournalEntry to JournalEntryReturn``)
    |> ListHelper.listOfResultsToResultsList


