module Model.CashFlow.PaymentAgreement

open Model
open Model.CashFlow.CashFlowComponent
open Model.Ledger.AccountComponent
open NodaTime
open Utilities.AppError
open Utilities.FieldUpdate
open Utilities.ResultHelper
open DataAccessLayer.ExecuteNonQuery
open DataAccessLayer.ExecuteReader
open DataAccessLayer.QueryParameters

type PaymentAgreement = private {
    paymentAgreementId: PaymentAgreementId
    masterAgreementID: MasterAgreementId
    debitAccount: DebitAccount
    creditAccount: CreditAccount
    expectedAmount: Money option
    memo: PaymentAgreementMemo option
    createdAt: Instant
    modifiedAt: Instant
}

type PaymentAgreementFieldUpdates = {
    paymentAgreementIdToUpdate: PaymentAgreementId
    debitAccountUpdate: FieldUpdate<DebitAccount>
    creditAccountUpdate: FieldUpdate<CreditAccount>
    expectedAmountUpdate: FieldUpdate<Money option>
    memoUpdate: FieldUpdate<PaymentAgreementMemo option>
}

let paymentAgreementId p = p.paymentAgreementId
let masterAgreementID p = p.masterAgreementID
let debitAccount p = p.debitAccount
let creditAccount p = p.creditAccount
let expectedAmount p = p.expectedAmount
let memo p = p.memo
let createdAt p = p.createdAt
let modifiedAt p = p.modifiedAt

let create
    (paymentAgreementId: PaymentAgreementId)
    (masterAgreementID: MasterAgreementId)
    (debitAccount: DebitAccount)
    (creditAccount: CreditAccount)
    (expectedAmount: Money option)
    (memo: PaymentAgreementMemo option)
    (createdAt: Instant)
    (modifiedAt: Instant)
    : PaymentAgreement =
    { paymentAgreementId = paymentAgreementId
      masterAgreementID = masterAgreementID
      debitAccount = debitAccount
      creditAccount = creditAccount
      expectedAmount = expectedAmount
      memo = memo
      createdAt = createdAt
      modifiedAt = modifiedAt }

let insertNewToDb
    (context: Context.Context)
    (paymentAgreement: PaymentAgreement)
    : Result<unit, AppError> =
    result {
        let query =
            """
            insert into cashflow.payment_agreement(
	            unique_id, master_agreement_id, debit_account, credit_account, expected_amount, memo, created_at,
                modified_at)
            values (
	            @unique_id, @master_agreement_id, @debit_account, @credit_account, @expected_amount, @memo, @created_at,
                @modified_at);"""
        let uuid = paymentAgreement.paymentAgreementId |> PaymentAgreementId.value
        let masterAgreementUuid = paymentAgreement.masterAgreementID |> MasterAgreementId.value
        let (DebitAccount debitAccountId) = paymentAgreement.debitAccount
        let (CreditAccount creditAccountId) = paymentAgreement.creditAccount
        let debitAccountUuid = debitAccountId |> AccountId.value
        let creditAccountUuid = creditAccountId |> AccountId.value
        let expectedAmount = paymentAgreement.expectedAmount |> Option.map Money.amount
        let memo = paymentAgreement.memo |> Option.map PaymentAgreementMemo.value
        let parameters =
            [
              { name = "@unique_id"; value = UniqueId(uuid) }
              { name = "@master_agreement_id"; value = UniqueId(masterAgreementUuid) }
              { name = "@debit_account"; value = UniqueId(debitAccountUuid) }
              { name = "@credit_account"; value = UniqueId(creditAccountUuid) }
              { name = "@expected_amount"; value = NullableNumeric(expectedAmount) }
              { name = "@memo"; value = NullableCharString(memo) }
              { name = "@created_at"; value = DbInstant(paymentAgreement.createdAt) }
              { name = "@modified_at"; value = DbInstant(paymentAgreement.modifiedAt) }
            ]
        return! executeNonQuery (context |> Context.getDatabaseTransaction) query parameters ExactlyOne
    }

let private reconstitute raw =
    result {
        let (uuid,
             masterAgreementUuid,
             debitAccountUuid,
             creditAccountUuid,
             expectedAmountDec,
             memoStr,
             createdAt,
             modifiedAt) =
            raw
        let paymentAgreementId = uuid |> PaymentAgreementId.fromGuid
        let masterAgreementID = masterAgreementUuid |> MasterAgreementId.fromGuid
        let debitAccount = debitAccountUuid |> AccountId.fromGuid |> DebitAccount
        let creditAccount = creditAccountUuid |> AccountId.fromGuid |> CreditAccount
        let! expectedAmount = expectedAmountDec |> convertOptionToDesiredTypeWithFallibleConverter Money.fromDecimal
        let! memo = memoStr |> convertOptionToDesiredTypeWithFallibleConverter PaymentAgreementMemo.create
        return
            create
                paymentAgreementId
                masterAgreementID
                debitAccount
                creditAccount
                expectedAmount
                memo
                createdAt
                modifiedAt
    }

let private mapRawForDbRead (row: RowReader) =
    (row |> RowReader.getUuid "unique_id"),
    (row |> RowReader.getUuid "master_agreement_id"),
    (row |> RowReader.getUuid "debit_account"),
    (row |> RowReader.getUuid "credit_account"),
    (row |> RowReader.getNumericOption "expected_amount"),
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
    : Result<PaymentAgreement list, AppError> =
    let from = "cashflow.payment_agreement pa"
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
    : Result<PaymentAgreement list, AppError> =
    let select = """
        pa.unique_id, pa.master_agreement_id, pa.debit_account, pa.credit_account, pa.expected_amount,
        pa.memo, pa.created_at, pa.modified_at
        """
    readRowsFromDb context None select None predicate limit None None parameters expectedRows

let fetchById (context: Context.Context) (paymentAgreementID: PaymentAgreementId) : Result<PaymentAgreement, AppError> =
    let predicate = "pa.unique_id = @unique_id"
    let uuid = paymentAgreementID |> PaymentAgreementId.value
    let parameters = [ { name = "@unique_id"; value = UniqueId uuid } ]
    fetchGenericRead context (Some predicate) None parameters ExactlyOne |> Result.map List.head

let fetchByMasterAgreementIdList
    (context: Context.Context)
    (masterAgreementIds: MasterAgreementId list)
    : Result<PaymentAgreement list, AppError> =
    if masterAgreementIds |> List.isEmpty then Error CashflowMasterAgreementIdListCannotBeEmpty else
    let namesAndParameters =
        List.zip [ 1 .. masterAgreementIds.Length ] masterAgreementIds
        |> List.map (fun (ordinal, id) ->
            let name = $"@masterAgreementId{ordinal}"
            name, { name = name; value = UniqueId(id |> MasterAgreementId.value) })
    let names = namesAndParameters |> List.map fst |> String.concat ", "
    let parameters = namesAndParameters |> List.map snd
    let predicate = $"pa.master_agreement_id in ({names})"
    fetchGenericRead context (Some predicate) None parameters AnyQuantityIsAcceptable

/// updateDb is incredibly powerful and should only be used very deliberately. It will let you update your database in a
/// type-unsafe manner. Only use it with controlled database transactions and with certainty that you are validating
/// your resultant data state appropriately.
let updateDb
    (context: Context.Context)
    (fieldUpdates: PaymentAgreementFieldUpdates)
    : Result<PaymentAgreement, AppError> =
    let paymentAgreementID = fieldUpdates.paymentAgreementIdToUpdate
    let uuid = paymentAgreementID |> PaymentAgreementId.value
    let baseParams =
        [ { name = "@unique_id"; value = UniqueId uuid } ]
    let updates =
        [
              fieldUpdates.debitAccountUpdate
              |> FieldUpdate.mapNoChangeToOptionWithConversion(fun n ->
                  let (DebitAccount accountId) = n
                  [ ("debit_account = @debit_account",
                     { name = "@debit_account"; value = UniqueId(accountId |> AccountId.value) }) ])

              fieldUpdates.creditAccountUpdate
              |> FieldUpdate.mapNoChangeToOptionWithConversion(fun n ->
                  let (CreditAccount accountId) = n
                  [ ("credit_account = @credit_account",
                     { name = "@credit_account"; value = UniqueId(accountId |> AccountId.value) }) ])

              fieldUpdates.expectedAmountUpdate
              |> FieldUpdate.mapNoChangeToOptionWithConversion(fun n ->
                  [ ("expected_amount = @expected_amount",
                     { name = "@expected_amount"; value = NullableNumeric(n |> Option.map Money.amount) }) ])

              fieldUpdates.memoUpdate
              |> FieldUpdate.mapNoChangeToOptionWithConversion(fun n ->
                  [ ("memo = @memo",
                     { name = "@memo"; value = NullableCharString(n |> Option.map PaymentAgreementMemo.value) }) ])
        ]
        |> List.choose id
        |> List.collect id
    let setClauses = updates |> List.map fst |> String.concat ", "
    let parameters = baseParams @ (updates |> List.map snd)
    let query =
        $"""
        UPDATE cashflow.payment_agreement
        set
            {setClauses}
        WHERE unique_id = @unique_id;
    """
    result {
        do! if updates |> List.isEmpty then Error(CashflowPaymentAgreementUpdateNoOp) else Ok()
        do! executeNonQuery (context |> Context.getDatabaseTransaction) query parameters ExactlyOne
        return! paymentAgreementID |> fetchById context
    }

