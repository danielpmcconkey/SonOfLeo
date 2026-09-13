module Model.CashFlow.Payment

open System
open Model
open Model.CashFlow.CashFlowComponent
open Model.Ledger.JournalEntryComponent
open NodaTime
open Utilities.AppError
open Utilities.FieldUpdate
open Utilities.ResultHelper
open DataAccessLayer.ExecuteNonQuery
open DataAccessLayer.ExecuteReader
open DataAccessLayer.QueryParameters
open Model.DataIngestion.StageEntryComponent

type Payment = private {
    paymentId: PaymentId
    invoiceId: InvoiceId
    transactionPointer: TransactionPointer
    amount: PaymentAmount // not separately tracked in the database; here for read convenience
    postedToFiDate: PostedToFiDate option // the date the payment hit the actual external account
    postedToLedgerDate: PostedToLedgerDate option // not separately tracked in the database; here for read convenience
    memo: PaymentMemo option
    createdAt: Instant
    modifiedAt: Instant
}

type PaymentFieldUpdates = {
    paymentIdToUpdate: PaymentId
    journalEntryLineIdUpdate: FieldUpdate<JournalEntryLineId option>
    stageEntryLineIdUpdate: FieldUpdate<StageEntryLineId option>
    postedToFiDateUpdate: FieldUpdate<LocalDate option>
    memoUpdate: FieldUpdate<PaymentMemo option>
}

let paymentId p = p.paymentId
let invoiceId p = p.invoiceId
let transactionPointer p = p.transactionPointer
let amount p = p.amount
let postedToFiDate p = p.postedToFiDate
let postedToLedgerDate p = p.postedToLedgerDate
let memo p = p.memo
let createdAt p = p.createdAt
let modifiedAt p = p.modifiedAt

let create
    (paymentId: PaymentId)
    (invoiceId: InvoiceId)
    (transactionPointer: TransactionPointer)
    (amount: PaymentAmount)
    (postedToFiDate: PostedToFiDate option)
    (postedToLedgerDate: PostedToLedgerDate option)
    (memo: PaymentMemo option)
    (createdAt: Instant)
    (modifiedAt: Instant)
    : Payment =
    { paymentId = paymentId
      invoiceId = invoiceId
      transactionPointer = transactionPointer
      amount = amount
      postedToFiDate = postedToFiDate
      postedToLedgerDate = postedToLedgerDate
      memo = memo
      createdAt = createdAt
      modifiedAt = modifiedAt }

/// applyFieldUpdates folds the two independent id updates back into one pointer on the same Posted-wins rule the row
/// decode uses, so setting a stage line on an already-posted payment leaves the in-hand pointer Posted even though the
/// write still sets the column.
let applyFieldUpdates (fieldUpdates: PaymentFieldUpdates) (payment: Payment) : Result<Payment, AppError> =
    let currentJournalEntryLineId, currentStageEntryLineId =
        match payment.transactionPointer with
        | CashFlowComponent.Posted journalEntryLineId -> Some journalEntryLineId, None
        | CashFlowComponent.Staged stageEntryLineId -> None, Some stageEntryLineId
    let journalEntryLineId =
        fieldUpdates.journalEntryLineIdUpdate |> FieldUpdate.valueOrCurrent currentJournalEntryLineId
    let stageEntryLineId =
        fieldUpdates.stageEntryLineIdUpdate |> FieldUpdate.valueOrCurrent currentStageEntryLineId
    let postedToFiDate =
        fieldUpdates.postedToFiDateUpdate
        |> FieldUpdate.convertFieldUpdateOptionToNewTypeOption (fun localDate ->
            { PostedToFiDate.localDate = localDate })
        |> FieldUpdate.valueOrCurrent payment.postedToFiDate
    result {
        let! transactionPointer =
            match journalEntryLineId, stageEntryLineId with
            | Some journalEntryLineId, _ -> Ok(CashFlowComponent.Posted journalEntryLineId)
            | None, Some stageEntryLineId -> Ok(CashFlowComponent.Staged stageEntryLineId)
            | None, None ->
                Error(
                    CashflowInvalidPaymentTransactionPointerRow
                        "neither journal_entry_line_id nor stage_entry_line_id was set; at least one must be set.")
        return
            { payment with
                transactionPointer = transactionPointer
                postedToFiDate = postedToFiDate
                memo = fieldUpdates.memoUpdate |> FieldUpdate.valueOrCurrent payment.memo }
    }

let private transactionPointerToColumns (transactionPointer: TransactionPointer) : Guid option * Guid option =
    match transactionPointer with
    | CashFlowComponent.Posted journalEntryLineId -> (journalEntryLineId |> JournalEntryLineId.value |> Some), None
    | CashFlowComponent.Staged stageEntryLineId -> None, (stageEntryLineId |> StageEntryLineId.value |> Some)

let persist
    (context: Context.Context)
    (payment: Payment)
    : Result<unit, AppError> =
    result {
        let queryStatement =
            """
            insert into cashflow.payment(
	            unique_id, invoice_id, journal_entry_line_id, stage_entry_line_id, posted_to_fi_date, memo,
                created_at, modified_at)
            values (
	            @unique_id, @invoice_id, @journal_entry_line_id, @stage_entry_line_id, @posted_to_fi_date, @memo,
                @created_at, @modified_at);"""
        let uuid = payment.paymentId |> PaymentId.value
        let invoiceUuid = payment.invoiceId |> InvoiceId.value
        let journalEntryLineUuid, stageEntryLineUuid = payment.transactionPointer |> transactionPointerToColumns
        let memo = payment.memo |> Option.map PaymentMemo.value
        let postedToFiDate = payment.postedToFiDate |> Option.map _.localDate
        let parameters =
            [
              { name = "@unique_id"; value = UniqueId(uuid) }
              { name = "@invoice_id"; value = UniqueId(invoiceUuid) }
              { name = "@journal_entry_line_id"; value = NullableUniqueId(journalEntryLineUuid) }
              { name = "@stage_entry_line_id"; value = NullableUniqueId(stageEntryLineUuid) }
              { name = "@posted_to_fi_date"; value = NullableDbLocalDate(postedToFiDate) }
              { name = "@memo"; value = NullableCharString(memo) }
              { name = "@created_at"; value = DbInstant(payment.createdAt) }
              { name = "@modified_at"; value = DbInstant(payment.modifiedAt) }
            ]
        return! executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters ExactlyOne
    }

let private transactionPointerFromColumns
    (journalEntryLineUuid: Guid option)
    (stageEntryLineUuid: Guid option)
    : Result<TransactionPointer, AppError> =
    // Note: it is not an illegal state for the database to have both a stage reference and a ledger reference. Both
    // being populated is the normal end state, not corruption. The standard lifecycle is for the data ingestion to load
    // the FI transaction into stage and run the classifier. Then the operator will review obligations to see if any of
    // the staged transactions represent a new payment. At which point, the operator will add a new payment record into
    // the database with the link to stage. The operator will use this knowledge to update the account code in stage
    // before posting to the ledger. Once posted, the ledger's JournalEntryLineId will be known and the operator will
    // close the loop by updating the payment record. Terminal state on happy path includes both values.
    match journalEntryLineUuid, stageEntryLineUuid with
    | Some journalEntryLineUuid, _ ->
        journalEntryLineUuid |> JournalEntryLineId.fromGuid |> CashFlowComponent.Posted |> Ok
    | None, Some stageEntryLineUuid ->
        stageEntryLineUuid |> StageEntryLineId.fromGuid |> CashFlowComponent.Staged |> Ok
    | None, None ->
        Error(
            CashflowInvalidPaymentTransactionPointerRow
                "neither journal_entry_line_id nor stage_entry_line_id was set; at least one must be set.")

let private reconstitute raw =
    result {
        let (uuid,
             invoiceUuid,
             journalEntryLineUuid,
             stageEntryLineUuid,
             amountDec,
             postedToFiLocalDateOpt,
             postedToLedgerLocalDateOpt,
             memoStr,
             createdAt,
             modifiedAt) =
            raw
        let paymentId = uuid |> PaymentId.fromGuid
        let invoiceId = invoiceUuid |> InvoiceId.fromGuid
        let! transactionPointer = transactionPointerFromColumns journalEntryLineUuid stageEntryLineUuid
        let! amount =
            match amountDec with
            | Some d -> d |> Money.fromDecimal
            | None ->
                Error(
                    CashflowInvalidPaymentAmountRow
                        "the computed amount column was null; no matching journal_entry_line/staged_entry_line was \
                         found for this payment's flow direction and account.")
        let! memo = memoStr |> convertOptionToDesiredTypeWithFallibleConverter PaymentMemo.create
        let postedToFiDate = postedToFiLocalDateOpt |> Option.map(fun x ->{ PostedToFiDate.localDate = x })
        let postedToLedgerDate = postedToLedgerLocalDateOpt |> Option.map(fun x -> { PostedToLedgerDate.localDate = x })
        return
            create
                paymentId
                invoiceId
                transactionPointer
                { money = amount }
                postedToFiDate
                postedToLedgerDate
                memo
                createdAt
                modifiedAt
    }

let private mapRawForDbRead (row: RowReader) =
    (row |> RowReader.getUuid "unique_id"),
    (row |> RowReader.getUuid "invoice_id"),
    (row |> RowReader.getUuidOption "journal_entry_line_id"),
    (row |> RowReader.getUuidOption "stage_entry_line_id"),
    (row |> RowReader.getNumericOption "amount"),
    (row |> RowReader.getDateOption "posted_to_fi_date"),
    (row |> RowReader.getDateOption "posted_to_ledger_date"),
    (row |> RowReader.getStringOption "memo"),
    (row |> RowReader.getInstant "created_at"),
    (row |> RowReader.getInstant "modified_at")

let query
    (context: Context.Context)
    (cteList: string list option)
    (select: string)
    (joinList: string list option)
    (predicate: string option)
    (limit: int option)
    (groupBy: string option)
    (orderBy: string option)
    (parameters: QueryParameter list)
    (expectedRows: AcceptableExpectedRows)
    : Result<Payment list, AppError> =
    let from = "cashflow.payment pmt"
    let queryStatement = buildReadQuery cteList select from joinList predicate limit groupBy orderBy
    executeReaderQuery
        (context |> Context.getDatabaseTransaction)
        queryStatement
        parameters
        mapRawForDbRead
        reconstitute
        expectedRows

let private fetchAny
    (context: Context.Context)
    (predicate: string option)
    (limit: int option)
    (parameters: QueryParameter list)
    (expectedRows: AcceptableExpectedRows)
    : Result<Payment list, AppError> =
    let select = """
        pmt.unique_id, pmt.invoice_id, pmt.journal_entry_line_id, pmt.stage_entry_line_id,
        case when jel.unique_id is not null then jel.amount else sel.amount end as amount,
        pmt.posted_to_fi_date, je.entry_date as posted_to_ledger_date, pmt.memo, pmt.created_at,
        pmt.modified_at
        """
    let join =
        [
            "left join ledger.journal_entry_line jel on pmt.journal_entry_line_id = jel.unique_id"
            "left join ledger.journal_entry je on jel.journal_entry_id = je.unique_id"
            "left join ingestion.staged_entry_line sel on pmt.stage_entry_line_id = sel.unique_id"
        ]
    query context None select (Some join) predicate limit None None parameters expectedRows

let fetchById (context: Context.Context) (paymentId: PaymentId) : Result<Payment, AppError> =
    let predicate = "pmt.unique_id = @unique_id"
    let uuid = paymentId |> PaymentId.value
    let parameters = [ { name = "@unique_id"; value = UniqueId uuid } ]
    fetchAny context (Some predicate) None parameters ExactlyOne |> Result.map List.head

let fetchByInvoiceIdList
    (context: Context.Context)
    (invoiceIds: InvoiceId list)
    : Result<Payment list, AppError> =
    if invoiceIds |> List.isEmpty then Error CashflowInvoiceIdListCannotBeEmpty else
    let namesAndParameters =
        List.zip [ 1 .. invoiceIds.Length ] invoiceIds
        |> List.map (fun (ordinal, id) ->
            let name = $"@invoiceId{ordinal}"
            name, { name = name; value = UniqueId(id |> InvoiceId.value) })
    let names = namesAndParameters |> List.map fst |> String.concat ", "
    let parameters = namesAndParameters |> List.map snd
    let predicate = $"pmt.invoice_id in ({names})"
    fetchAny context (Some predicate) None parameters AnyQuantityIsAcceptable

let fetchByStageEntryLineIdList
    (context: Context.Context)
    (lineIds: StageEntryLineId list)
    : Result<Payment list, AppError> =
    if lineIds |> List.isEmpty then Error IngestionStageEntryLineIdListCannotBeEmpty else
    let namesAndParameters =
        List.zip [ 1 .. lineIds.Length ] lineIds
        |> List.map (fun (ordinal, id) ->
            let name = $"@stageEntryLineId{ordinal}"
            name, { name = name; value = UniqueId(id |> StageEntryLineId.value) })
    let names = namesAndParameters |> List.map fst |> String.concat ", "
    let parameters = namesAndParameters |> List.map snd
    let predicate = $"pmt.stage_entry_line_id in ({names})"
    fetchAny context (Some predicate) None parameters AnyQuantityIsAcceptable

let update
    (context: Context.Context)
    (fieldUpdates: PaymentFieldUpdates)
    : Result<Payment, AppError> =
    let paymentId = fieldUpdates.paymentIdToUpdate
    let uuid = paymentId |> PaymentId.value
    let baseParams =
        [ { name = "@unique_id"; value = UniqueId uuid } ]
    let updates =
        [
              fieldUpdates.journalEntryLineIdUpdate
              |> FieldUpdate.mapNoChangeToOptionWithConversion(fun n ->
                  [ ("journal_entry_line_id = @journal_entry_line_id",
                     { name = "@journal_entry_line_id"
                       value = NullableUniqueId(n |> Option.map JournalEntryLineId.value) }) ])

              fieldUpdates.stageEntryLineIdUpdate
              |> FieldUpdate.mapNoChangeToOptionWithConversion(fun n ->
                  [ ("stage_entry_line_id = @stage_entry_line_id",
                     { name = "@stage_entry_line_id"
                       value = NullableUniqueId(n |> Option.map StageEntryLineId.value) }) ])

              fieldUpdates.postedToFiDateUpdate
              |> FieldUpdate.mapNoChangeToOptionWithConversion(fun n ->
                  [ ("posted_to_fi_date = @posted_to_fi_date",
                     { name = "@posted_to_fi_date"; value = NullableDbLocalDate(n) }) ])

              fieldUpdates.memoUpdate
              |> FieldUpdate.mapNoChangeToOptionWithConversion(fun n ->
                  [ ("memo = @memo",
                     { name = "@memo"; value = NullableCharString(n |> Option.map PaymentMemo.value) }) ])
        ]
        |> List.choose id
        |> List.collect id
    let setClauses = updates |> List.map fst |> String.concat ", "
    let parameters = baseParams @ (updates |> List.map snd)
    let queryStatement =
        $"""
        UPDATE cashflow.payment
        set
            {setClauses}
        WHERE unique_id = @unique_id;
    """
    result {
        do! if updates |> List.isEmpty then Error(CashflowPaymentUpdateNoOp) else Ok()
        do! executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters ExactlyOne
        return! paymentId |> fetchById context
    }
