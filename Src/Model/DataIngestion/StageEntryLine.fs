module Model.DataIngestion.StageEntryLine

open Model
open Model.Ledger.AccountComponent
open Model.Ledger.JournalEntryComponent
open Utilities.AppError
open DataAccessLayer.ExecuteNonQuery
open DataAccessLayer.ExecuteReader
open DataAccessLayer.QueryParameters
open Utilities.FieldUpdate
open Utilities.ResultHelper
open Model.DataIngestion.Classification.ClassificationRuleComponent
open Model.DataIngestion.StageEntryComponent
                
type StageEntryLine = private {
    stageEntryLineId: StageEntryLineId
    stageEntryHeaderId: StageEntryHeaderId
    amount: Money
    lineType: JournalEntryLineType
    accountId: AccountId option
    memo: JournalEntryLineMemo option
    classificationRuleId: ClassificationRuleId option }

type StageEntryLineFieldUpdates = {
    lineIdToUpdate: StageEntryLineId
    amountUpdate: FieldUpdate<Money>
    entryTypeUpdate: FieldUpdate<JournalEntryLineType>
    accountIdUpdate: FieldUpdate<AccountId option>
    memoUpdate: FieldUpdate<JournalEntryLineMemo option>
    classificationRuleIdUpdate: FieldUpdate<ClassificationRuleId option> }

let stageEntryLineId l = l.stageEntryLineId
let stageEntryHeaderId l = l.stageEntryHeaderId
let amount l = l.amount 
let lineType l = l.lineType 
let accountId l = l.accountId 
let memo l = l.memo 
let classificationRuleId l = l.classificationRuleId

let create
    (stageEntryLineId: StageEntryLineId)
    (stageEntryHeaderId: StageEntryHeaderId)
    (amount : Money)
    (entryType : JournalEntryLineType)
    (accountId: AccountId option)
    (memo: JournalEntryLineMemo option)
    (classificationRuleId: ClassificationRuleId option)
    : StageEntryLine = {
                stageEntryHeaderId = stageEntryHeaderId
                stageEntryLineId = stageEntryLineId
                amount = amount
                lineType = entryType
                accountId = accountId
                memo = memo 
                classificationRuleId = classificationRuleId }

let confirmAccountCode
    (context: Context.Context)
    (accountIdOption: AccountId option)
    : Result<unit, AppError> =
    match accountIdOption with
    | None -> Ok ()
    | Some accountCode ->
        let uuid = accountCode |> AccountId.value
        match uuid |> LookupCache.accountIdToCode.fetch context with // we don't need the code. we just check that the id exists in the DB this way
        | Ok _ -> Ok ()
        | Error(DalResultantRowsDidntMatchExpectation (_, 0)) -> Error (AccountIdDoesntMatch uuid)
        | Error e -> Error e

let insertNewToDb (context: Context.Context) (stageEntryLine: StageEntryLine) : Result<unit, AppError> =
    let query =
        """
        insert into ingestion.staged_entry_line (
	        unique_id, entry_id, amount, line_type, account_id, memo, classification_rule_id)
        values (
	        @unique_id, 
            @entry_id, 
            @amount, 
            @line_type, 
            @account_id,
            @memo,
            @classification_rule_id);"""
    result {
        let uuid = stageEntryLine.stageEntryLineId |> StageEntryLineId.value
        let headerUuid = stageEntryLine.stageEntryHeaderId |> StageEntryHeaderId.value
        let amount = stageEntryLine.amount |> Money.amount
        let lineType = stageEntryLine.lineType |> JournalEntryLineType.toString
        do! stageEntryLine.accountId |> confirmAccountCode context
        let accountUuid = stageEntryLine.accountId |> Option.map AccountId.value
        let memo = stageEntryLine.memo |> Option.map JournalEntryLineMemo.value
        let ruleUuid = stageEntryLine.classificationRuleId |> Option.map ClassificationRuleId.value
        let parameters =
            [
              { name = "@unique_id"; value = UniqueId(uuid) }
              { name = "@entry_id"; value = UniqueId(headerUuid) }
              { name = "@amount"; value = Numeric(amount) }
              { name = "@line_type"; value = CharString(lineType) }
              { name = "@account_id"; value = NullableUniqueId(accountUuid) }
              { name = "@memo"; value = NullableCharString(memo) }
              { name = "@classification_rule_id"; value = NullableUniqueId(ruleUuid) }
            ]
        return! executeNonQuery (context |> Context.getDatabaseTransaction) query parameters ExactlyOne
    }
        
let private reconstitute raw =
    result {
        let (uuid,
             headerUuid,
             amountDec,
             lineTypeStr,
             accountUuidOption,
             memoStrOption,
             ruleUuid) =
            raw
        let stageEntryLineId = uuid |> StageEntryLineId.fromGuid
        let stageEntryHeaderId = headerUuid |> StageEntryHeaderId.fromGuid
        let! amount = amountDec |> Money.fromDecimal
        let! lineType = lineTypeStr |> JournalEntryLineType.fromString
        let accountId = accountUuidOption |> Option.map AccountId.fromGuid
        let! memo = memoStrOption |> convertOptionToDesiredTypeWithFallibleConverter JournalEntryLineMemo.create
        let ruleId = ruleUuid |> Option.map ClassificationRuleId.fromGuid
        return
            create
                stageEntryLineId
                stageEntryHeaderId
                amount
                lineType
                accountId
                memo
                ruleId
    }

let private mapRawForDbRead (row: RowReader) =
    (row |> RowReader.getUuid "unique_id"),
    (row |> RowReader.getUuid "entry_id"),
    (row |> RowReader.getNumeric "amount"),
    (row |> RowReader.getString "line_type"),
    (row |> RowReader.getUuidOption "account_id"),
    (row |> RowReader.getStringOption "memo"),
    (row |> RowReader.getUuidOption "classification_rule_id")
    
let private readRowsFromDb
    (context: Context.Context)
    (predicate: string option)
    (limit: int option)
    (parameters: QueryParameter list)
    (expectedRows: AcceptableExpectedRows)
    : Result<StageEntryLine list, AppError> =
    let select =
        """
        sel.unique_id, sel.entry_id, sel.amount, sel.line_type, sel.account_id, sel.memo, sel.classification_rule_id
        """
    let from = "ingestion.staged_entry_line sel"
    let query = buildReadQuery None select from None predicate limit None None
    executeReaderQuery
        (context |> Context.getDatabaseTransaction)
        query
        parameters
        mapRawForDbRead
        reconstitute
        expectedRows

let fetchById (context: Context.Context) (lineId: StageEntryLineId) : Result<StageEntryLine, AppError> =
    let predicate = "sel.unique_id = @unique_id"
    let uuid = lineId |> StageEntryLineId.value
    let parameters = [ { name = "@unique_id"; value = UniqueId uuid } ]
    readRowsFromDb context (Some predicate) None parameters ExactlyOne |> Result.map List.head

let fetchByHeaderId (context: Context.Context) (lineId: StageEntryHeaderId) : Result<StageEntryLine list, AppError> =
    let predicate = "sel.entry_id = @unique_id"
    let accountIdGuid = lineId |> StageEntryHeaderId.value
    let parameters = [ { name = "@unique_id"; value = UniqueId accountIdGuid } ]
    readRowsFromDb context (Some predicate) None parameters AnyQuantityIsAcceptable

let fetchByHeaderIdList
    (context: Context.Context)
    (stageEntryHeaderIds: StageEntryHeaderId list)
    : Result<StageEntryLine list, AppError> =
    if stageEntryHeaderIds |> List.isEmpty then Error IngestionStageHeaderIdListCannotBeEmpty else
    let ordinals = [ 1 .. stageEntryHeaderIds.Length ]
    let zipped = List.zip ordinals stageEntryHeaderIds
    let namesAndParameters =
        zipped
        |> List.map(fun (ordinal, id) ->
            let uuid = id |> StageEntryHeaderId.value
            let name = $"@stageEntryHeaderId{ordinal}"
            let parameter = { name = name; value = UniqueId uuid }
            name, parameter)
    let names = namesAndParameters |> List.map fst |> String.concat ", "
    let parameters = namesAndParameters |> List.map snd
    let predicate = $"sel.entry_id in ({names})"
    readRowsFromDb context (Some predicate) None parameters AnyQuantityIsAcceptable
    
/// updateDb is incredibly powerful and should only be used very deliberately. It will let you update your database in a
/// type-unsafe manner. Only use it with controlled database transactions and with certainty that you are validating
/// your resultant data state appropriately.
let updateDb
    (context: Context.Context)
    (fieldUpdates: StageEntryLineFieldUpdates)
    : Result<StageEntryLine, AppError> =
    result {
        let stageEntryLineId = fieldUpdates.lineIdToUpdate
        let amountUpdate = fieldUpdates.amountUpdate
        let entryTypeUpdate = fieldUpdates.entryTypeUpdate
        let accountCodeUpdate = fieldUpdates.accountIdUpdate
        do! match accountCodeUpdate with
                | NoChange -> Ok ()
                | SetTo x -> x |> confirmAccountCode context
        let memoUpdate = fieldUpdates.memoUpdate
        let classificationRuleIdUpdate = fieldUpdates.classificationRuleIdUpdate
        let uuid = stageEntryLineId |> StageEntryLineId.value
        let baseParams =
            [ { name = "@unique_id"; value = UniqueId uuid } ]
        let updates =
            [
                  amountUpdate
                  |> FieldUpdate.mapNoChangeToOptionWithConversion(fun n ->
                      ("amount = @amount",
                       { name = "@amount"; value = Numeric(n |> Money.amount) }))
                  
                  entryTypeUpdate
                  |> FieldUpdate.mapNoChangeToOptionWithConversion(fun n ->
                      ("line_type = @line_type",
                       { name = "@line_type"; value = CharString(n |> JournalEntryLineType.toString) }))
                  
                  accountCodeUpdate
                  |> FieldUpdate.mapNoChangeToOptionWithConversion(fun n ->
                      ("account_id = @account_id",
                       { name = "@account_id"; value = NullableUniqueId(n |> Option.map AccountId.value) }))              
                  
                  memoUpdate
                  |> FieldUpdate.mapNoChangeToOptionWithConversion(fun n ->
                      ("memo = @memo",
                       { name = "@memo"; value = NullableCharString(n |> Option.map JournalEntryLineMemo.value) }))
                  
                  classificationRuleIdUpdate
                  |> FieldUpdate.mapNoChangeToOptionWithConversion(fun n ->
                      ("classification_rule_id = @classification_rule_id",
                       { name = "@classification_rule_id"
                         value = NullableUniqueId(n |> Option.map ClassificationRuleId.value) }))
            ]
            |> List.choose id
        let setClauses = updates |> List.map fst |> String.concat ", "
        let parameters = baseParams @ (updates |> List.map snd)
        let query =
            $"""
            UPDATE ingestion.staged_entry_line
            set
                {setClauses}
            WHERE unique_id = @unique_id;
        """
        do! if updates.IsEmpty then Error(IngestionStageEntryLineNoOp) else Ok()
        
        let! () = executeNonQuery (context |> Context.getDatabaseTransaction) query parameters ExactlyOne
        return! stageEntryLineId |> fetchById context
    }

/// updateCodeAndRuleId assumes the orchestrator is validating the code maps to a real account and that the rule ID is real
let updateAccountAndRuleId
    (context: Context.Context)
    (accountIdUpdate: FieldUpdate<AccountId option>)
    (classificationRuleIdUpdate: FieldUpdate<ClassificationRuleId option>)
    (stageEntryLineId : StageEntryLineId)
    : Result<StageEntryLine, AppError> =
    let fieldUpdates = {
        lineIdToUpdate = stageEntryLineId
        amountUpdate = NoChange
        entryTypeUpdate = NoChange
        accountIdUpdate = accountIdUpdate
        memoUpdate = NoChange
        classificationRuleIdUpdate = classificationRuleIdUpdate }
    updateDb context fieldUpdates
