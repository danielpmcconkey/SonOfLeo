module Model.CashFlow.PaymentAgreementLink

open Model.CashFlow.CashFlowComponent
open Model.DataIngestion.StageEntryComponent
open NodaTime
open Utilities.AppError
open Utilities.FieldUpdate
open Utilities.ResultHelper
open DataAccessLayer.ExecuteNonQuery
open DataAccessLayer.ExecuteReader
open DataAccessLayer.QueryParameters

type PaymentAgreementLink = private {
    paymentAgreementLinkId: PaymentAgreementLinkId
    paymentAgreementId: PaymentAgreementId
    stageEntryLineId: StageEntryLineId
    createdAt: Instant
    modifiedAt: Instant
}

type PaymentAgreementLinkFieldUpdates = {
    linkIdToUpdate: PaymentAgreementLinkId
    paymentAgreementIdUpdate: FieldUpdate<PaymentAgreementId>
}

let paymentAgreementLinkId l = l.paymentAgreementLinkId
let paymentAgreementId l = l.paymentAgreementId
let stageEntryLineId l = l.stageEntryLineId
let createdAt l = l.createdAt
let modifiedAt l = l.modifiedAt

let create
    (paymentAgreementLinkId: PaymentAgreementLinkId)
    (paymentAgreementId: PaymentAgreementId)
    (stageEntryLineId: StageEntryLineId)
    (createdAt: Instant)
    (modifiedAt: Instant)
    : PaymentAgreementLink =
    { paymentAgreementLinkId = paymentAgreementLinkId
      paymentAgreementId = paymentAgreementId
      stageEntryLineId = stageEntryLineId
      createdAt = createdAt
      modifiedAt = modifiedAt }

let persist (context: Context.Context) (link: PaymentAgreementLink) : Result<unit, AppError> =
    let queryStatement =
        """
        insert into cashflow.payment_agreement_link(
            unique_id, payment_agreement_id, stage_entry_line_id, created_at, modified_at)
        values (
            @unique_id,
            @payment_agreement_id,
            @stage_entry_line_id,
            @created_at,
            @modified_at);"""
    let uuid = link.paymentAgreementLinkId |> PaymentAgreementLinkId.value
    let agreementUuid = link.paymentAgreementId |> PaymentAgreementId.value
    let lineUuid = link.stageEntryLineId |> StageEntryLineId.value
    let parameters =
        [ { name = "@unique_id"; value = UniqueId uuid }
          { name = "@payment_agreement_id"; value = UniqueId agreementUuid }
          { name = "@stage_entry_line_id"; value = UniqueId lineUuid }
          { name = "@created_at"; value = DbInstant link.createdAt }
          { name = "@modified_at"; value = DbInstant link.modifiedAt } ]
    executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters ExactlyOne

let private reconstitute raw =
    result {
        let (uuid, agreementUuid, lineUuid, createdAt, modifiedAt) = raw
        let paymentAgreementLinkId = uuid |> PaymentAgreementLinkId.fromGuid
        let paymentAgreementId = agreementUuid |> PaymentAgreementId.fromGuid
        let stageEntryLineId = lineUuid |> StageEntryLineId.fromGuid
        return create paymentAgreementLinkId paymentAgreementId stageEntryLineId createdAt modifiedAt
    }

let private mapRawForDbRead (row: RowReader) =
    (row |> RowReader.getUuid "unique_id"),
    (row |> RowReader.getUuid "payment_agreement_id"),
    (row |> RowReader.getUuid "stage_entry_line_id"),
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
    : Result<PaymentAgreementLink list, AppError> =
    let from = "cashflow.payment_agreement_link pal"
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
    : Result<PaymentAgreementLink list, AppError> =
    let select = """
        pal.unique_id, pal.payment_agreement_id, pal.stage_entry_line_id, pal.created_at, pal.modified_at
        """
    query context None select None predicate limit None None parameters expectedRows

let fetchById
    (context: Context.Context)
    (linkId: PaymentAgreementLinkId)
    : Result<PaymentAgreementLink, AppError> =
    let predicate = "pal.unique_id = @unique_id"
    let uuid = linkId |> PaymentAgreementLinkId.value
    let parameters = [ { name = "@unique_id"; value = UniqueId uuid } ]
    fetchAny context (Some predicate) None parameters ExactlyOne |> Result.map List.head

let fetchByPaymentAgreementId
    (context: Context.Context)
    (agreementId: PaymentAgreementId)
    : Result<PaymentAgreementLink list, AppError> =
    let predicate = "pal.payment_agreement_id = @payment_agreement_id"
    let uuid = agreementId |> PaymentAgreementId.value
    let parameters = [ { name = "@payment_agreement_id"; value = UniqueId uuid } ]
    fetchAny context (Some predicate) None parameters AnyQuantityIsAcceptable

let fetchByPaymentAgreementIdList
    (context: Context.Context)
    (agreementIds: PaymentAgreementId list)
    : Result<PaymentAgreementLink list, AppError> =
    if agreementIds |> List.isEmpty then Error CashflowPaymentAgreementIdListCannotBeEmpty else
    let namesAndParameters =
        List.zip [ 1 .. agreementIds.Length ] agreementIds
        |> List.map (fun (ordinal, id) ->
            let name = $"@paymentAgreementId{ordinal}"
            name, { name = name; value = UniqueId(id |> PaymentAgreementId.value) })
    let names = namesAndParameters |> List.map fst |> String.concat ", "
    let parameters = namesAndParameters |> List.map snd
    let predicate = $"pal.payment_agreement_id in ({names})"
    fetchAny context (Some predicate) None parameters AnyQuantityIsAcceptable

/// fetchByStageEntryLineId returns a list of at most one -- the table is unique on stage_entry_line_id. An empty list
/// means the line is unclaimed, which is the normal case and the question automatic resolution asks before linking.
let fetchByStageEntryLineId
    (context: Context.Context)
    (lineId: StageEntryLineId)
    : Result<PaymentAgreementLink list, AppError> =
    let predicate = "pal.stage_entry_line_id = @stage_entry_line_id"
    let uuid = lineId |> StageEntryLineId.value
    let parameters = [ { name = "@stage_entry_line_id"; value = UniqueId uuid } ]
    fetchAny context (Some predicate) None parameters AnyQuantityIsAcceptable

let fetchByStageEntryLineIdList
    (context: Context.Context)
    (lineIds: StageEntryLineId list)
    : Result<PaymentAgreementLink list, AppError> =
    if lineIds |> List.isEmpty then Error IngestionStageEntryLineIdListCannotBeEmpty else
    let namesAndParameters =
        List.zip [ 1 .. lineIds.Length ] lineIds
        |> List.map (fun (ordinal, id) ->
            let name = $"@stageEntryLineId{ordinal}"
            name, { name = name; value = UniqueId(id |> StageEntryLineId.value) })
    let names = namesAndParameters |> List.map fst |> String.concat ", "
    let parameters = namesAndParameters |> List.map snd
    let predicate = $"pal.stage_entry_line_id in ({names})"
    fetchAny context (Some predicate) None parameters AnyQuantityIsAcceptable

let update
    (context: Context.Context)
    (fieldUpdates: PaymentAgreementLinkFieldUpdates)
    : Result<PaymentAgreementLink, AppError> =
    let linkId = fieldUpdates.linkIdToUpdate
    let uuid = linkId |> PaymentAgreementLinkId.value
    let baseParams =
        [ { name = "@unique_id"; value = UniqueId uuid } ]
    let updates =
        [
            fieldUpdates.paymentAgreementIdUpdate
            |> FieldUpdate.mapNoChangeToOptionWithConversion(fun n ->
                let agreementUuid = n |> PaymentAgreementId.value
                [ ("payment_agreement_id = @payment_agreement_id",
                   { name = "@payment_agreement_id"; value = UniqueId agreementUuid }) ])
        ]
        |> List.choose id
        |> List.collect id
    let setClauses = updates |> List.map fst |> String.concat ", "
    let parameters = baseParams @ (updates |> List.map snd)
    let queryStatement =
        $"""
        UPDATE cashflow.payment_agreement_link
        set
            {setClauses}
        WHERE unique_id = @unique_id;
    """
    result {
        do! if updates |> List.isEmpty then Error(CashflowPaymentAgreementLinkUpdateNoOp) else Ok()
        do! executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters ExactlyOne
        return! linkId |> fetchById context
    }

/// delete removes the row outright. This is the only hard delete in Src/ -- the ledger's indelibility rules do not
/// reach here, because a link is a belief about which obligation a bank row belongs to, and a wrong belief is removed
/// rather than voided. The classification diagnostic that produced it survives and is where the trail lives.
let delete (context: Context.Context) (linkId: PaymentAgreementLinkId) : Result<unit, AppError> =
    let queryStatement = "delete from cashflow.payment_agreement_link where unique_id = @unique_id;"
    let uuid = linkId |> PaymentAgreementLinkId.value
    let parameters = [ { name = "@unique_id"; value = UniqueId uuid } ]
    executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters ExactlyOne
