namespace ModelOrchestrator.JournalEntries

open System
open Model.Audit
open Model.Ledger.Accounts.AccountComponent
open Model.Ledger.JournalEntryPrimitives
open Model.Ledger.Journaling
open Model.Ledger.Journaling.JournalEntryComponent
open Model.Ledger.Accounts
open NodaTime
open Utilities.DAL
open Utilities.ListHelper
open Utilities.ResultCE

type JournalEntry =
  private  {    header: JournalEntryHeader
                lines: JournalEntryLine list
                externalReferences: JournalEntryExternalReference list // REQ-JE-1.46
                comments: JournalEntryComment list } // REQ-JE-1.55

module JournalEntryCreationAndConstruction =
    let header je = je.header
    let lines je = je.lines
    let externalReferences je = je.externalReferences
    let comments je = je.comments
        
    let private validateAmountEquality (lines: JournalEntryLine list) : Result<unit, string> =
        result {
            let! totalDebits = lines |> JournalEntryLine.sumLinesByType Debit
            let! totalCredits = lines |> JournalEntryLine.sumLinesByType Credit
            return!
                if totalCredits = totalDebits then Ok ()
                else Error "The sum of all debit line amounts must exactly equal the sum of all credit line amounts"
            }
    
    let private validateLineCount (lines: JournalEntryLine list) : Result<unit, string> =
        if lines |> List.length < 2
        then Error "Insufficient number of lines for a journal entry" // REQ-JE-1.12
        else Ok ()
        
    let validateLineList (lines: JournalEntryLine list) : Result<unit, string> =
        result {
            let! _ = validateLineCount lines // REQ-JE-1.12
            let! _ = validateAmountEquality lines // REQ-JE-1.13
            return ()
        }
    
    let private createValidHeader
            (headerPrimitives : JournalEntryHeaderPrimitives)
            (auditEnvelope: AuditEnvelope)
            (transaction: DbTransaction option)
            : Result<JournalEntryHeader, string> =
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
            : Result<unit, string> =
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
            : Result<JournalEntryLine list, string> =
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
            : Result<JournalEntryExternalReference list, string> =
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
            : Result<JournalEntryComment list, string> =
        commentsPrimitives
        |> List.map(fun comment -> 
                        JournalEntryComment.constructNewAndSaveToDb
                            jeId
                            comment.secondaryJournalEntryId
                            comment.commentText
                            auditEnvelope
                            transaction) 
        |> listOfResultsToResultsList
    
    let private validateNoNewVoidedEntries (newHeader: JournalEntryHeader) : Result<unit, string> =
        match newHeader |> JournalEntryHeader.voidedAt with
        | Some _ -> Error "Creating a new, already voided Journal Entry is not permitted"
        | None -> Ok ()
    
    /// orchestrateCreation validates all input and saves the new posted entry into the database
    let orchestrateCreation // REQ-JE-2.13
            (auditEnvelope: AuditEnvelope)
            (journalEntryPrimitives: JournalEntryPrimitives)
            : Result<JournalEntry, string> =
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
            : Result<JournalEntry, string> =
        result {    do! validateLineList lines
                    return {    header = header
                                lines = lines
                                externalReferences = externalReferences
                                comments = comments } }


