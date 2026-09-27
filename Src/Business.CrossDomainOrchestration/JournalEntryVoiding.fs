module Business.CrossDomainOrchestration.JournalEntryVoiding

open App.DataAccessLayer.DalError
open App.Utility
open App.Utility.IAppError
open App.Utility.Result
open App.DataAccessLayer.QueryParameter
open App.DataAccessLayer.ExecuteReader
open App.DataAccessLayer.ExecuteNonQuery
open App.Session
open Business.CrossDomainOrchestration.JournalEntryOrchestration
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.FiscalPeriodComponent
open Business.FinancialServices.Ledger.JournalEntryComponent
open Business.FinancialServices.Ledger.LedgerError
open Business.FinancialServices.CashFlow
open Business.FinancialServices.DataIngestion
open Business.FinancialServices.DataIngestion.StageEntryComponent

let private confirmJournalEntryIdIsReal
    (context: Context.Context)
    (journalEntryHeaderId: JournalEntryHeaderId)
    : Result<unit, IAppError> =
    journalEntryHeaderId |> JournalEntryHeader.fetchById context
    |> whenNoRows (JournalEntryHeaderIdDoesntExist(journalEntryHeaderId |> JournalEntryHeaderId.value))
    |> Result.map ignore

let private confirmFiscalPeriodIsStillOpenBeforeVoiding
    (context: Context.Context)
    (journalEntryHeader: JournalEntryHeader.JournalEntryHeader)
    : Result<unit, IAppError> =
    let entryDate = journalEntryHeader |> JournalEntryHeader.entryDate
    let fiscalPeriodId = entryDate |> EntryDate.fiscalPeriodId
    result {
        let! fiscalPeriod =
            let entryDatePrim = entryDate |> EntryDate.entryDate
            let uuid = fiscalPeriodId |> FiscalPeriodId.value
            fiscalPeriodId |> FiscalPeriod.fetchById context
            |> whenNoRows (JournalEntryVoidingCannotFetchFiscalPeriod(entryDatePrim, uuid))
        return!
            match fiscalPeriod |> FiscalPeriod.isOpen with
            | true -> Ok()
            | false ->
                Error(
                    JournalEntryVoidingFiscalPeriodIsClosed(
                        entryDate |> EntryDate.entryDate,
                        fiscalPeriodId |> FiscalPeriodId.value
                    )
                )
    }

let private voidById
    (context: Context.Context)
    (journalEntryHeaderId: JournalEntryHeaderId)
    : Result<unit, IAppError> =
    let uuid = journalEntryHeaderId |> JournalEntryHeaderId.value
    let now = context |> Context.getInitiationInstant
    let parameters =
        [ { name = "@modified"; value = DbInstant(now) }
          { name = "@newValue"; value = DbInstant(now) }
          { name = "@unique_id"; value = UniqueId uuid } ]
    let queryStatement =
        $"""
        UPDATE ledger.journal_entry
        set
            modified_at = @modified
            , voided_at = @newValue
        WHERE unique_id = @unique_id
        and voided_at is null
        ;
    """
    result {
        let! je =
            journalEntryHeaderId |> JournalEntryHeader.fetchById context
            |> whenNoRows (JournalEntryHeaderIdDoesntExist uuid)
        do! je |> confirmFiscalPeriodIsStillOpenBeforeVoiding context
        do! executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters ExactlyOne
    }

let private insertReason
    (context: Context.Context)
    (primaryJournalEntryId: JournalEntryHeaderId)
    (secondaryJournalEntryId: JournalEntryHeaderId option)
    (commentText: CommentText)
    : Result<unit, IAppError> =
    JournalEntryCommentOrchestration.constructNewAndPersist
        context
        primaryJournalEntryId
        secondaryJournalEntryId
        commentText
    |> Result.map ignore

/// A Payment the void will point back at its staged line, and that line.
type private PaymentFallback = { payment: Payment.Payment; stageEntryLineId: StageEntryLineId }

/// Every Payment pointing at one of the journal entry's lines must have a staged line to fall back to (REQ-JE-4.12).
/// This runs before anything is written, so a refused void leaves no reason comment behind.
let private fetchPaymentFallbacks
    (context: Context.Context)
    (journalEntryHeaderId: JournalEntryHeaderId)
    : Result<PaymentFallback list, IAppError> =
    result {
        let! journalEntryLines = journalEntryHeaderId |> JournalEntryLine.fetchByJournalEntryHeaderId context
        let! payments =
            journalEntryLines
            |> List.map JournalEntryLine.journalEntryLineId
            |> Payment.fetchByJournalEntryLineIdList context
        return!
            payments
            |> List.map (fun payment ->
                let paymentId = payment |> Payment.paymentId
                paymentId
                |> Payment.fetchStageEntryLineIdById context
                |> Result.bind (function
                    | Some stageEntryLineId -> Ok { payment = payment; stageEntryLineId = stageEntryLineId }
                    | None ->
                        CashFlowError.error(
                            CashFlowError.CashflowPaymentWithoutStagedLineBlocksVoid(
                                paymentId |> CashFlowComponent.PaymentId.value,
                                journalEntryHeaderId |> JournalEntryHeaderId.value))))
            |> convertListOfResultsToResultsList
    }

/// The staged entry posting made this journal entry goes back to Reviewed, recorded as Operator, and it and its lines
/// no longer name the journal entry or its lines (REQ-JE-4.11, REQ-STG-4.7).
let private unwindStaging
    (context: Context.Context)
    (journalEntryHeaderId: JournalEntryHeaderId)
    : Result<unit, IAppError> =
    result {
        let! stageEntryHeaders = journalEntryHeaderId |> StageEntryHeader.fetchByJournalEntryHeaderId context
        let! _ =
            stageEntryHeaders
            |> List.map (fun stageEntryHeader ->
                let stageEntryHeaderId = stageEntryHeader |> StageEntryHeader.stageEntryHeaderId
                result {
                    let! _ =
                        StageEntryHeader.update context
                            { headerIdToUpdate = stageEntryHeaderId
                              sourceFileUpdate = FieldUpdate.NoChange
                              entryDateUpdate = FieldUpdate.NoChange
                              descriptionUpdate = FieldUpdate.NoChange
                              ingestionSourceUpdate = FieldUpdate.NoChange
                              fiReferenceUpdate = FieldUpdate.NoChange
                              journalEntryHeaderIdUpdate = FieldUpdate.SetTo None
                              statusUpdate = FieldUpdate.SetTo Reviewed }
                    let! stageEntryLines = stageEntryHeaderId |> StageEntryLine.fetchByHeaderId context
                    let! _ =
                        stageEntryLines
                        |> List.filter (fun line -> line |> StageEntryLine.journalEntryLineId |> Option.isSome)
                        |> List.map (fun line ->
                            line
                            |> StageEntryLine.stageEntryLineId
                            |> StageEntryLine.updateJournalEntryLineId context (FieldUpdate.SetTo None))
                        |> convertListOfResultsToResultsList
                    return ()
                })
            |> convertListOfResultsToResultsList
        return ()
    }

/// Each Payment points at its staged line again, and its Invoice's and Instance's states are re-derived (REQ-JE-4.12).
let private unwindPayments
    (context: Context.Context)
    (paymentFallbacks: PaymentFallback list)
    : Result<unit, IAppError> =
    if paymentFallbacks |> List.isEmpty then Ok () else
    result {
        let! invoices =
            paymentFallbacks
            |> List.map (fun fallback -> fallback.payment |> Payment.invoiceId)
            |> List.distinct
            |> Invoice.fetchByIdList context
        let instanceIdOf (fallback: PaymentFallback) =
            let invoiceId = fallback.payment |> Payment.invoiceId
            invoices
            |> List.find (fun invoice -> invoice |> Invoice.invoiceId = invoiceId)
            |> Invoice.instanceId
        let! _ =
            paymentFallbacks
            |> List.groupBy instanceIdOf
            |> List.map (fun (instanceId, instanceFallbacks) ->
                let invoiceCompositeUpdates =
                    instanceFallbacks
                    |> List.groupBy (fun fallback -> fallback.payment |> Payment.invoiceId)
                    |> List.map (fun (invoiceId, invoiceFallbacks) ->
                        let invoiceCompositeUpdate: InstanceOrchestration.InvoiceCompositeUpdate =
                            { invoiceUpdates = invoiceId |> CashFlowOps.noChangeInvoiceUpdates
                              paymentUpdates =
                                invoiceFallbacks
                                |> List.map (fun fallback ->
                                    let paymentUpdate: Payment.PaymentFieldUpdates =
                                        { paymentIdToUpdate = fallback.payment |> Payment.paymentId
                                          journalEntryLineIdUpdate = FieldUpdate.SetTo None
                                          // a posted Payment's pointer no longer names its staged line, so name it again
                                          stageEntryLineIdUpdate = FieldUpdate.SetTo(Some fallback.stageEntryLineId)
                                          postedToFiDateUpdate = FieldUpdate.NoChange
                                          memoUpdate = FieldUpdate.NoChange }
                                    paymentUpdate)
                              paymentIdsToDelete = []
                              newPayments = [] }
                        invoiceCompositeUpdate)
                let compositeUpdate: InstanceOrchestration.InstanceCompositeUpdate =
                    { instanceUpdates =
                        { instanceIdToUpdate = instanceId
                          instanceDateUpdate = FieldUpdate.NoChange
                          isFulfilledUpdate = FieldUpdate.NoChange }
                      invoiceCompositeUpdates = invoiceCompositeUpdates
                      newInvoices = [] }
                compositeUpdate |> InstanceOrchestration.updateInstanceComposite context)
            |> convertListOfResultsToResultsList
        return ()
    }

let voidJournalEntry
    (context: Context.Context)
    (secondaryJournalEntryIdForComment: JournalEntryHeaderId option)
    (commentText: CommentText)
    (journalEntryHeaderId: JournalEntryHeaderId)
    : Result<JournalEntry, IAppError> =
    result {
        do! journalEntryHeaderId |> confirmJournalEntryIdIsReal context // validate here so the error message is helpful
        let! paymentFallbacks = journalEntryHeaderId |> fetchPaymentFallbacks context
        do! insertReason context journalEntryHeaderId secondaryJournalEntryIdForComment commentText
        do!
            journalEntryHeaderId
            |> voidById context
            |> whenNoRows (JournalEntryVoidingNoOp(journalEntryHeaderId |> JournalEntryHeaderId.value))
        do! journalEntryHeaderId |> unwindStaging context
        do! paymentFallbacks |> unwindPayments context
        return! journalEntryHeaderId |> JournalEntryOrchestration.fetchById context
    }
