namespace ModelOrchestrator.JournalEntries

open System
open Model.Audit
open Model.Ledger.JournalEntryPrimitives
open Model.Ledger.Journaling
open Model.Ledger.Journaling.JournalEntryComponent
open Model.Money
open Utilities.DAL
open Utilities.ListHelper
open Utilities.ResultCE

type JournalEntry =
  private  {    header: JournalEntryHeader
                lines: JournalEntryLine list
                externalReferences: JournalEntryExternalReference list
                comments: JournalEntryComment list }

module JournalEntry =
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
        
    let private validateLineList (lines: JournalEntryLine list) : Result<unit, string> =
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
    
    let private createValidLines
            (jeId : Guid)
            (linePrimitives : JournalEntryLinePrimitives list)
            (auditEnvelope: AuditEnvelope)
            (transaction: DbTransaction option)
            : Result<JournalEntryLine list, string> =
        linePrimitives
        |> List.map(fun line -> 
                        JournalEntryLine.constructNewAndSaveToDb
                            jeId
                            line.accountId
                            line.amount
                            line.lineType
                            line.memo
                            auditEnvelope
                            transaction) 
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
    
    let orchestrateCreation
            (auditEnvelope: AuditEnvelope)
            (journalEntryPrimitives: JournalEntryPrimitives)
            : Result<JournalEntry, string> =
        let transaction = createDbTransaction() |> Result.defaultWith failwith // if this fails, nothing can proceed
        let railRoad = result {
            let! validHeader = createValidHeader journalEntryPrimitives.header auditEnvelope (Some transaction)
            let jeId = validHeader |> JournalEntryHeader.uniqueId
            let! validLines = createValidLines jeId journalEntryPrimitives.lines auditEnvelope (Some transaction)
            let! validReferences = createValidExternalReferences jeId journalEntryPrimitives.externalReferences auditEnvelope (Some transaction)
            let! validComments = createValidComments jeId journalEntryPrimitives.comments auditEnvelope (Some transaction)
            do! validateLineList validLines
            return {    header = validHeader
                        lines = validLines
                        externalReferences = validReferences
                        comments = validComments }
        }
        match railRoad with
        | Error e ->
            transaction |> rollbackDbTransactionAndDisposeConnection |> Result.defaultWith failwith
            Error e
        | Ok je ->
            transaction |> commitDbTransactionAndDisposeConnection |> Result.defaultWith failwith
            Ok je
    
    let fetchById
            (uniqueId: Guid)
            : Result<JournalEntry, string> =
        result {
            let! validHeader = uniqueId |> JournalEntryHeader.fetchById None
            let! validLines = uniqueId |> JournalEntryLine.fetchByJournalEntryId None
            let! validReferences = uniqueId |> JournalEntryExternalReference.fetchByIdJournalEntryId None
            let! validComments = uniqueId |> JournalEntryComment.fetchByIdJournalEntryId None
            do! validateLineList validLines
            return {    header = validHeader
                        lines = validLines
                        externalReferences = validReferences
                        comments = validComments }
        }
    
    let fetchByPeriodKey
            (key: string)
            : Result<JournalEntry list, string> =
        result {
            let! headers = key |> JournalEntryHeader.fetchByPeriodKey None
            let headerResultsList = headers |> List.map(fun h ->
                let id = JournalEntryHeader.uniqueId h
                let entryResult = fetchById id 
                entryResult)
            return! headerResultsList |> listOfResultsToResultsList
        }


