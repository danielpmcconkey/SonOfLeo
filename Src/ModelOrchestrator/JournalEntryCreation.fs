namespace ModelOrchestrator.JournalEntries

open System
open Model.Audit
open Model.Ledger.Accounts.AccountComponent
open Model.Ledger.JournalEntryPrimitives
open Model.Ledger.Journaling
open Model.Ledger.Journaling.JournalEntryComponent
open Model.Ledger.Accounts
open NodaTime
open Utilities.AppError
open Utilities.DAL
open Utilities.ListHelper
open Utilities.ResultCE

type JournalEntry =
  private  {    header: JournalEntryHeader
                lines: JournalEntryLine list
                externalReferences: JournalEntryExternalReference list // REQ-JE-1.46
                comments: JournalEntryComment list } // REQ-JE-1.55

module JournalEntry =
    let header je = je.header
    let lines je = je.lines
    let externalReferences je = je.externalReferences
    let comments je = je.comments
        
    let private validateAmountEquality (lines: JournalEntryLine list) : Result<unit, AppError> =
        result {
            let! totalDebits = lines |> JournalEntryLine.sumLinesByType Debit
            let! totalCredits = lines |> JournalEntryLine.sumLinesByType Credit
            return!
                if totalCredits = totalDebits then Ok ()
                else Error "The sum of all debit line amounts must exactly equal the sum of all credit line amounts"
            }
    
    let private validateLineCount (lines: JournalEntryLine list) : Result<unit, AppError> =
        if lines |> List.length < 2
        then Error "Insufficient number of lines for a journal entry" // REQ-JE-1.12
        else Ok ()
        
    let validateLineList (lines: JournalEntryLine list) : Result<unit, AppError> =
        result {
            let! _ = validateLineCount lines // REQ-JE-1.12
            let! _ = validateAmountEquality lines // REQ-JE-1.13
            return ()
        }
    
    let private createValidHeader
            (headerPrimitives : JournalEntryHeaderPrimitives)
            (auditEnvelope: AuditEnvelope)
            (transaction: DbTransaction option)
            : Result<JournalEntryHeader, AppError> =
        JournalEntryHeader.constructNewAndSaveToDb
            headerPrimitives.description
            headerPrimitives.source
            headerPrimitives.entryDate
            headerPrimitives.voidedAt
            auditEnvelope
            transaction
    
    let private validateAccountByLine
            (transaction: DbTransaction option)
            (entryDate: LocalDate)
            (line: JournalEntryLinePrimitives)
            : Result<unit, AppError> =
        result {
            let! account = line.accountId |> AccountId.fromGuid |>  Account.fetchById transaction
            return!
                match account |> Account.isActive entryDate with
                | true -> Ok ()
                | false -> Error $"Account {line.accountId} is not active relative to the Journal Entry's entry date" // REQ-JE-2.8
        }
    
    let private createValidLines
            (journalEntryId : JournalEntryId)
            (entryDate: LocalDate)
            (linePrimitives : JournalEntryLinePrimitives list)
            (auditEnvelope: AuditEnvelope)
            (transaction: DbTransaction option)
            : Result<JournalEntryLine list, AppError> =
        linePrimitives
        |> List.map(fun line ->
                result {    do! line |> validateAccountByLine transaction entryDate // REQ-JE-2.8
                            return! JournalEntryLine.constructNewAndSaveToDb
                                journalEntryId
                                line.accountId
                                line.amount
                                line.lineType
                                line.memo
                                auditEnvelope
                                transaction })
        |> listOfResultsToResultsList
    
    let private createValidExternalReferences
            (jeId : Guid)
            (referencePrimitives : JournalEntryExternalReferencePrimitives list)
            (auditEnvelope: AuditEnvelope)
            (transaction: DbTransaction option)
            : Result<JournalEntryExternalReference list, AppError> =
        referencePrimitives
        |> List.map(fun extRef -> 
                        JournalEntryExternalReference.constructNewAndSaveToDb
                            jeId
                            extRef.financialInstitution
                            extRef.referenceText
                            auditEnvelope
                            transaction) 
        |> listOfResultsToResultsList
    
    let private createValidComments
            (jeId : Guid)
            (commentsPrimitives : JournalEntryCommentPrimitives list)
            (auditEnvelope: AuditEnvelope)
            (transaction: DbTransaction option)
            : Result<JournalEntryComment list, AppError> =
        commentsPrimitives
        |> List.map(fun comment -> 
                        JournalEntryComment.constructNewAndSaveToDb
                            jeId
                            comment.secondaryJournalEntryId
                            comment.commentText
                            auditEnvelope
                            transaction) 
        |> listOfResultsToResultsList
    
    let private validateNoNewVoidedEntries (newHeader: JournalEntryHeader) : Result<unit, AppError> =
        match newHeader |> JournalEntryHeader.voidedAt with
        | Some _ -> Error "Creating a new, already voided Journal Entry is not permitted"
        | None -> Ok ()

    /// constructNewAndSaveToDb validates that the components work together to
    /// form a valid whole before adding it to the persistence layer. All new
    /// account creation should route through here before being sent to the
    /// persistence layer. Internal model functions may construct through other
    /// means if they're operating on known good data. 
    let constructNewAndSaveToDb // REQ-JE-2.13
            (description: JournalEntryDescription)
            (source: JournalEntrySource option)
            (entryDate: LocalDate)
            (lines: (AccountId * Money * LineType * JournalEntryLineMemo option) list)
            (references: (FinancialInstitution * ReferenceText) list)
            (comments: (JournalEntryId option * CommentText) list)
            (auditEnvelope: AuditEnvelope)
            (transaction: DbTransaction option)
            : Result<JournalEntry, AppError> =
        let transaction = createDbTransaction() |> Result.defaultWith failwith // if this fails, nothing can proceed
        let railRoad = result {
            let! validHeader = createValidHeader journalEntryPrimitives.header auditEnvelope (Some transaction)
            do! validHeader |> validateNoNewVoidedEntries
            let jeId = validHeader |> JournalEntryHeader.journalEntryId
            let entryDate = JournalEntryHeader.entryDate validHeader |> EntryDate.entryDate  
            let! validLines = createValidLines jeId entryDate journalEntryPrimitives.lines auditEnvelope (Some transaction)
            let! validReferences = createValidExternalReferences jeId journalEntryPrimitives.externalReferences auditEnvelope (Some transaction)
            let! validComments = createValidComments jeId journalEntryPrimitives.comments auditEnvelope (Some transaction)
            do! validateLineList validLines
            return {    header = validHeader
                        lines = validLines
                        externalReferences = validReferences
                        comments = validComments }
        }
        match railRoad with // REQ-JE-2.11
        | Error e ->
            transaction |> rollbackDbTransactionAndDisposeConnection |> Result.defaultWith failwith // REQ-JE-2.12, REQ-JE-2.8, REQ-JE-1.12, REQ-JE-1.13
            Error e
        | Ok je ->
            transaction |> commitDbTransactionAndDisposeConnection |> Result.defaultWith failwith
            Ok je
    
    /// used by "sister" modules who need to construct a full journal entry
    /// from already validated components. this function ensures that the unit as a whole is still fully validated
    let constructFromPreValidatedComponents
            (header: JournalEntryHeader)
            (lines: JournalEntryLine list)
            (externalReferences: JournalEntryExternalReference list)
            (comments: JournalEntryComment list)
            : Result<JournalEntry, AppError> =
        result {    do! validateLineList lines
                    return {    header = header
                                lines = lines
                                externalReferences = externalReferences
                                comments = comments } }


