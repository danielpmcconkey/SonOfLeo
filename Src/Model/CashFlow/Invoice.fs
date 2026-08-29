module Model.CashFlow.Invoice

open Model
open Model.CashFlow.CashFlowComponent
open NodaTime
open Utilities.AppError
open Utilities.FieldUpdate
open Utilities.ResultHelper
open DataAccessLayer.ExecuteNonQuery
open DataAccessLayer.ExecuteReader
open DataAccessLayer.QueryParameters

let invoiceSelectFields = """
    inv.unique_id, inv.instance_id, inv.payment_agreement_id, inv.external_invoice_id, inv.invoice_date,
    inv.due_date, inv.amount, inv.invoice_state, inv.payment_state, inv.posted_state, inv.blocker_state,
    inv.blocker_note, inv.memo, inv.created_at, inv.modified_at
    """

type Invoice = private {
    invoiceId: InvoiceId
    instanceId: InstanceId
    paymentAgreementId: PaymentAgreementId
    externalInvoiceId: ExternalInvoiceId option
    invoiceDate: LocalDate
    dueDate: LocalDate
    amount: Money
    invoiceLifeCycleState: InvoiceLifeCycleState
    memo: InvoiceMemo option
    createdAt: Instant
    modifiedAt: Instant
}

type InvoiceFieldUpdates = {
    invoiceIdToUpdate: InvoiceId
    externalInvoiceIdUpdate: FieldUpdate<ExternalInvoiceId option>
    invoiceDateUpdate: FieldUpdate<LocalDate>
    dueDateUpdate: FieldUpdate<LocalDate>
    amountUpdate: FieldUpdate<Money>
    invoiceStateUpdate: FieldUpdate<InvoiceState>
    paymentStateUpdate: FieldUpdate<PaymentState>
    postedStateUpdate: FieldUpdate<PostedState>
    blockerUpdate: FieldUpdate<Blocker option>
    memoUpdate: FieldUpdate<InvoiceMemo option>
}

let invoiceId i = i.invoiceId
let instanceId i = i.instanceId
let paymentAgreementId i = i.paymentAgreementId
let externalInvoiceId i = i.externalInvoiceId
let invoiceDate i = i.invoiceDate
let dueDate i = i.dueDate
let amount i = i.amount
let invoiceLifeCycleState i = i.invoiceLifeCycleState
let memo i = i.memo
let createdAt i = i.createdAt
let modifiedAt i = i.modifiedAt

let create
    (invoiceId: InvoiceId)
    (instanceId: InstanceId)
    (paymentAgreementId: PaymentAgreementId)
    (externalInvoiceId: ExternalInvoiceId option)
    (invoiceDate: LocalDate)
    (dueDate: LocalDate)
    (amount: Money)
    (invoiceLifeCycleState: InvoiceLifeCycleState)
    (memo: InvoiceMemo option)
    (createdAt: Instant)
    (modifiedAt: Instant)
    : Invoice =
    { invoiceId = invoiceId
      instanceId = instanceId
      paymentAgreementId = paymentAgreementId
      externalInvoiceId = externalInvoiceId
      invoiceDate = invoiceDate
      dueDate = dueDate
      amount = amount
      invoiceLifeCycleState = invoiceLifeCycleState
      memo = memo
      createdAt = createdAt
      modifiedAt = modifiedAt }

let private blockerToColumns (blocker: Blocker option) : string option * string option =
    match blocker with
    | None -> None, None
    | Some NoFunds -> Some "NoFunds", None
    | Some Irresponsible -> Some "Irresponsible", None
    | Some(NeedsDecision note) -> Some "NeedsDecision", Some(note |> BlockerNote.value)
    | Some(Other note) -> Some "Other", Some(note |> BlockerNote.value)

let private blockerFromColumns
    (blockerState: string option)
    (blockerNote: string option)
    : Result<Blocker option, AppError> =
    match blockerState with
    | None ->
        match blockerNote with
        | None -> Ok None
        | Some _ -> Error(CashflowInvalidBlockerRow "blocker_note was set without a blocker_state.")
    | Some "NoFunds" ->
        match blockerNote with
        | None -> Ok(Some NoFunds)
        | Some _ -> Error(CashflowInvalidBlockerRow "NoFunds does not take a blocker_note.")
    | Some "Irresponsible" ->
        match blockerNote with
        | None -> Ok(Some Irresponsible)
        | Some _ -> Error(CashflowInvalidBlockerRow "Irresponsible does not take a blocker_note.")
    | Some "NeedsDecision" ->
        match blockerNote with
        | Some note -> note |> BlockerNote.create |> Result.map(NeedsDecision >> Some)
        | None -> Error(CashflowInvalidBlockerRow "NeedsDecision requires a blocker_note.")
    | Some "Other" ->
        match blockerNote with
        | Some note -> note |> BlockerNote.create |> Result.map(Other >> Some)
        | None -> Error(CashflowInvalidBlockerRow "Other requires a blocker_note.")
    | Some other -> Error(CashflowInvalidBlocker other)

let insertNewToDb
    (context: Context.Context)
    (invoice: Invoice)
    : Result<unit, AppError> =
    result {
        let query =
            """
            insert into cashflow.invoice(
	            unique_id, instance_id, payment_agreement_id, external_invoice_id, invoice_date, due_date, amount,
                invoice_state, payment_state, posted_state, blocker_state, blocker_note, memo, created_at, modified_at)
            values (
	            @unique_id, @instance_id, @payment_agreement_id, @external_invoice_id, @invoice_date, @due_date, @amount,
                @invoice_state, @payment_state, @posted_state, @blocker_state, @blocker_note, @memo, @created_at,
                @modified_at);"""
        let uuid = invoice.invoiceId |> InvoiceId.value
        let instanceUuid = invoice.instanceId |> InstanceId.value
        let paymentAgreementUuid = invoice.paymentAgreementId |> PaymentAgreementId.value
        let externalInvoiceId = invoice.externalInvoiceId |> Option.map ExternalInvoiceId.value
        let amount = invoice.amount |> Money.amount
        let invoiceState = invoice.invoiceLifeCycleState.invoiceState |> InvoiceState.toString
        let paymentState = invoice.invoiceLifeCycleState.paymentState |> PaymentState.toString
        let postedState = invoice.invoiceLifeCycleState.postedState |> PostedState.toString
        let blockerState, blockerNote = invoice.invoiceLifeCycleState.blocker |> blockerToColumns
        let memo = invoice.memo |> Option.map InvoiceMemo.value
        let parameters =
            [
              { name = "@unique_id"; value = UniqueId(uuid) }
              { name = "@instance_id"; value = UniqueId(instanceUuid) }
              { name = "@payment_agreement_id"; value = UniqueId(paymentAgreementUuid) }
              { name = "@external_invoice_id"; value = NullableCharString(externalInvoiceId) }
              { name = "@invoice_date"; value = DbLocalDate(invoice.invoiceDate) }
              { name = "@due_date"; value = DbLocalDate(invoice.dueDate) }
              { name = "@amount"; value = Numeric(amount) }
              { name = "@invoice_state"; value = CharString(invoiceState) }
              { name = "@payment_state"; value = CharString(paymentState) }
              { name = "@posted_state"; value = CharString(postedState) }
              { name = "@blocker_state"; value = NullableCharString(blockerState) }
              { name = "@blocker_note"; value = NullableCharString(blockerNote) }
              { name = "@memo"; value = NullableCharString(memo) }
              { name = "@created_at"; value = DbInstant(invoice.createdAt) }
              { name = "@modified_at"; value = DbInstant(invoice.modifiedAt) }
            ]
        return! executeNonQuery (context |> Context.getDatabaseTransaction) query parameters ExactlyOne
    }

let private reconstitute raw =
    result {
        let (uuid,
             instanceUuid,
             paymentAgreementUuid,
             externalInvoiceIdStr,
             invoiceDate,
             dueDate,
             amountDec,
             invoiceStateStr,
             paymentStateStr,
             postedStateStr,
             blockerState,
             blockerNote,
             memoStr,
             createdAt,
             modifiedAt) =
            raw
        let invoiceId = uuid |> InvoiceId.fromGuid
        let instanceId = instanceUuid |> InstanceId.fromGuid
        let paymentAgreementId = paymentAgreementUuid |> PaymentAgreementId.fromGuid
        let! externalInvoiceId =
            externalInvoiceIdStr |> convertOptionToDesiredTypeWithFallibleConverter ExternalInvoiceId.create
        let! amount = amountDec |> Money.fromDecimal
        let! invoiceState = invoiceStateStr |> InvoiceState.fromString
        let! paymentState = paymentStateStr |> PaymentState.fromString
        let! postedState = postedStateStr |> PostedState.fromString
        let! blocker = blockerFromColumns blockerState blockerNote
        let invoiceLifeCycleState =
            { invoiceState = invoiceState
              paymentState = paymentState
              postedState = postedState
              blocker = blocker }
        let! memo = memoStr |> convertOptionToDesiredTypeWithFallibleConverter InvoiceMemo.create
        return
            create
                invoiceId
                instanceId
                paymentAgreementId
                externalInvoiceId
                invoiceDate
                dueDate
                amount
                invoiceLifeCycleState
                memo
                createdAt
                modifiedAt
    }

let private mapRawForDbRead (row: RowReader) =
    (row |> RowReader.getUuid "unique_id"),
    (row |> RowReader.getUuid "instance_id"),
    (row |> RowReader.getUuid "payment_agreement_id"),
    (row |> RowReader.getStringOption "external_invoice_id"),
    (row |> RowReader.getDate "invoice_date"),
    (row |> RowReader.getDate "due_date"),
    (row |> RowReader.getNumeric "amount"),
    (row |> RowReader.getString "invoice_state"),
    (row |> RowReader.getString "payment_state"),
    (row |> RowReader.getString "posted_state"),
    (row |> RowReader.getStringOption "blocker_state"),
    (row |> RowReader.getStringOption "blocker_note"),
    (row |> RowReader.getStringOption "memo"),
    (row |> RowReader.getInstant "created_at"),
    (row |> RowReader.getInstant "modified_at")

let readRowsFromDb
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
    : Result<Invoice list, AppError> =
    let from = "cashflow.invoice inv"
    let query = buildReadQuery cteList select from joinList predicate limit groupBy orderBy
    executeReaderQuery
        (context |> Context.getDatabaseTransaction)
        query
        parameters
        mapRawForDbRead
        reconstitute
        expectedRows

let private fetchGenericRead
    (context: Context.Context)
    (predicate: string option)
    (limit: int option)
    (parameters: QueryParameter list)
    (expectedRows: AcceptableExpectedRows)
    : Result<Invoice list, AppError> =
    readRowsFromDb context None invoiceSelectFields None predicate limit None None parameters expectedRows

let fetchById (context: Context.Context) (invoiceId: InvoiceId) : Result<Invoice, AppError> =
    let predicate = "inv.unique_id = @unique_id"
    let uuid = invoiceId |> InvoiceId.value
    let parameters = [ { name = "@unique_id"; value = UniqueId uuid } ]
    fetchGenericRead context (Some predicate) None parameters ExactlyOne |> Result.map List.head

/// updateDb is incredibly powerful and should only be used very deliberately. It will let you update your database in a
/// type-unsafe manner. Only use it with controlled database transactions and with certainty that you are validating
/// your resultant data state appropriately.
let updateDb
    (context: Context.Context)
    (fieldUpdates: InvoiceFieldUpdates)
    : Result<Invoice, AppError> =
    let invoiceId = fieldUpdates.invoiceIdToUpdate
    let uuid = invoiceId |> InvoiceId.value
    let baseParams =
        [ { name = "@unique_id"; value = UniqueId uuid } ]
    let updates =
        [
              fieldUpdates.externalInvoiceIdUpdate
              |> FieldUpdate.mapNoChangeToOptionWithConversion(fun n ->
                  [ ("external_invoice_id = @external_invoice_id",
                     { name = "@external_invoice_id"; value = NullableCharString(n |> Option.map ExternalInvoiceId.value) }) ])

              fieldUpdates.invoiceDateUpdate
              |> FieldUpdate.mapNoChangeToOptionWithConversion(fun n ->
                  [ ("invoice_date = @invoice_date", { name = "@invoice_date"; value = DbLocalDate(n) }) ])

              fieldUpdates.dueDateUpdate
              |> FieldUpdate.mapNoChangeToOptionWithConversion(fun n ->
                  [ ("due_date = @due_date", { name = "@due_date"; value = DbLocalDate(n) }) ])

              fieldUpdates.amountUpdate
              |> FieldUpdate.mapNoChangeToOptionWithConversion(fun n ->
                  [ ("amount = @amount", { name = "@amount"; value = Numeric(n |> Money.amount) }) ])

              fieldUpdates.invoiceStateUpdate
              |> FieldUpdate.mapNoChangeToOptionWithConversion(fun n ->
                  [ ("invoice_state = @invoice_state",
                     { name = "@invoice_state"; value = CharString(n |> InvoiceState.toString) }) ])

              fieldUpdates.paymentStateUpdate
              |> FieldUpdate.mapNoChangeToOptionWithConversion(fun n ->
                  [ ("payment_state = @payment_state",
                     { name = "@payment_state"; value = CharString(n |> PaymentState.toString) }) ])

              fieldUpdates.postedStateUpdate
              |> FieldUpdate.mapNoChangeToOptionWithConversion(fun n ->
                  [ ("posted_state = @posted_state",
                     { name = "@posted_state"; value = CharString(n |> PostedState.toString) }) ])

              fieldUpdates.blockerUpdate
              |> FieldUpdate.mapNoChangeToOptionWithConversion(fun n ->
                  let blockerState, blockerNote = n |> blockerToColumns
                  [ ("blocker_state = @blocker_state", { name = "@blocker_state"; value = NullableCharString(blockerState) })
                    ("blocker_note = @blocker_note", { name = "@blocker_note"; value = NullableCharString(blockerNote) }) ])

              fieldUpdates.memoUpdate
              |> FieldUpdate.mapNoChangeToOptionWithConversion(fun n ->
                  [ ("memo = @memo", { name = "@memo"; value = NullableCharString(n |> Option.map InvoiceMemo.value) }) ])
        ]
        |> List.choose id
        |> List.collect id
    let setClauses = updates |> List.map fst |> String.concat ", "
    let parameters = baseParams @ (updates |> List.map snd)
    let query =
        $"""
        UPDATE cashflow.invoice
        set
            {setClauses}
        WHERE unique_id = @unique_id;
    """
    result {
        do! if updates |> List.isEmpty then Error(CashflowInvoiceUpdateNoOp) else Ok()
        do! executeNonQuery (context |> Context.getDatabaseTransaction) query parameters ExactlyOne
        return! invoiceId |> fetchById context
    }
