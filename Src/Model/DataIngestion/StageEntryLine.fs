module Model.DataIngestion.StageEntryLine

open Model
open Model.DataIngestion.Classification
open Model.Ledger.Accounts.AccountComponent
open Model.Ledger.Journaling.JournalEntryComponent
open Utilities.AppError
open Context.Context
open DataAccessLayer.ExecuteNonQuery
open DataAccessLayer.ExecuteReader
open DataAccessLayer.QueryParameters
open Utilities.FieldUpdate
open Utilities.ResultHelper
                
type StageEntryLine = private {
        stageEntryLineId: StageEntryLineId
        stageEntryHeaderId: StageEntryHeaderId
        amount : Money
        lineType : JournalEntryLineType
        accountCode: AccountCode option
        memo: JournalEntryLineMemo option
        classificationRuleId: ClassificationRuleId option
    }

let stageEntryLineId l = l.stageEntryLineId
let stageEntryHeaderId l = l.stageEntryHeaderId
let amount l = l.amount 
let lineType l = l.lineType 
let accountCode l = l.accountCode 
let memo l = l.memo 
let classificationRuleId l = l.classificationRuleId

let confirmAmountIsPositive (m: Money) : Result<unit, AppError> =
    if m |> Money.amount <= 0M
    then Error(IngestionStageLineNonPositiveAmount(m |> Money.amount))
    else Ok()

let create
    (stageEntryLineId: StageEntryLineId)
    (stageEntryHeaderId: StageEntryHeaderId)
    (amount : Money)
    (entryType : JournalEntryLineType)
    (accountCode: AccountCode option)
    (memo: JournalEntryLineMemo option)
    (classificationRuleId: ClassificationRuleId option)
    : StageEntryLine = {
                stageEntryHeaderId = stageEntryHeaderId
                stageEntryLineId = stageEntryLineId
                amount = amount
                lineType = entryType
                accountCode = accountCode
                memo = memo 
                classificationRuleId = classificationRuleId }

let insertNewToDb (context: Context) (stageEntryLine: StageEntryLine) : Result<unit, AppError> =
    let query =
        """
        insert into ingestion.staged_entry_line (
	        unique_id, entry_id, amount, line_type, code, memo, classification_rule_id)
        values (
	        @unique_id, 
            @entry_id, 
            @amount, 
            @line_type, 
            @code,
            @memo,
            @classification_rule_id);"""
    let uuid = stageEntryLine.stageEntryLineId |> StageEntryLineId.value
    let headerUuid = stageEntryLine.stageEntryHeaderId |> StageEntryHeaderId.value
    let amount = stageEntryLine.amount |> Money.amount
    let lineType = stageEntryLine.lineType |> JournalEntryLineType.toString
    let code = stageEntryLine.accountCode |> Option.map AccountCode.value
    let memo = stageEntryLine.memo |> Option.map JournalEntryLineMemo.value
    let ruleUuid = stageEntryLine.classificationRuleId |> Option.map ClassificationRuleId.value
    let parameters =
        [
          { name = "@unique_id"; value = UniqueId(uuid) }
          { name = "@entry_id"; value = UniqueId(headerUuid) }
          { name = "@amount"; value = Numeric(amount) }
          { name = "@line_type"; value = CharString(lineType) }
          { name = "@code"; value = NullableCharString(code) }
          { name = "@memo"; value = NullableCharString(memo) }
          { name = "@classification_rule_id"; value = NullableUniqueId(ruleUuid) }
        ]
    executeNonQuery (context |> getDatabaseTransaction) query parameters ExactlyOne
        
let private reconstitute raw =
    result {
        let (uuid,
             headerUuid,
             amountDec,
             lineTypeStr,
             codeStrOption,
             memoStrOption,
             ruleUuid) =
            raw
        let stageEntryLineId = uuid |> StageEntryLineId.fromGuid
        let stageEntryHeaderId = headerUuid |> StageEntryHeaderId.fromGuid
        let! amount = amountDec |> Money.fromDecimal
        let! lineType = lineTypeStr |> JournalEntryLineType.fromString
        let! code = codeStrOption |> convertOptionToDesiredTypeWithFallibleConverter AccountCode.create
        let! memo = memoStrOption |> convertOptionToDesiredTypeWithFallibleConverter JournalEntryLineMemo.create
        let ruleId = ruleUuid |> Option.map ClassificationRuleId.fromGuid
        return
            create
                stageEntryLineId
                stageEntryHeaderId
                amount
                lineType
                code
                memo
                ruleId
    }

let private mapRawForDbRead (row: RowReader) =
    (row |> RowReader.getUuid "unique_id"),
    (row |> RowReader.getUuid "entry_id"),
    (row |> RowReader.getNumeric "amount"),
    (row |> RowReader.getString "line_type"),
    (row |> RowReader.getStringOption "code"),
    (row |> RowReader.getStringOption "memo"),
    (row |> RowReader.getUuidOption "classification_rule_id")
    
let private readRowsFromDb
    (context: Context)
    (predicate: string option)
    (limit: int option)
    (parameters: QueryParameter list)
    (expectedRows: AcceptableExpectedRows)
    : Result<StageEntryLine list, AppError> =
    let select =
        """
        l.unique_id, l.entry_id, l.amount, l.line_type, l.code, l.memo, l.classification_rule_id
        """
    let from = "ingestion.staged_entry_line l"
    let query = buildReadQuery select from None predicate limit None None
    executeReaderQuery
        (context |> getDatabaseTransaction)
        query
        parameters
        mapRawForDbRead
        reconstitute
        expectedRows

let fetchById (context: Context) (lineId: StageEntryLineId) : Result<StageEntryLine, AppError> =
    let predicate = "l.unique_id = @unique_id"
    let uuid = lineId |> StageEntryLineId.value
    let parameters = [ { name = "@unique_id"; value = UniqueId uuid } ]
    readRowsFromDb context (Some predicate) None parameters ExactlyOne |> Result.map List.head

let fetchByHeaderId (context: Context) (lineId: StageEntryHeaderId) : Result<StageEntryLine list, AppError> =
    let predicate = "l.entry_id = @unique_id"
    let accountIdGuid = lineId |> StageEntryHeaderId.value
    let parameters = [ { name = "@unique_id"; value = UniqueId accountIdGuid } ]
    readRowsFromDb context (Some predicate) None parameters AnyQuantityIsAcceptable

let fetchByHeaderIdList
    (context: Context)
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
    let predicate = $"l.entry_id in ({names})"
    readRowsFromDb context (Some predicate) None parameters AnyQuantityIsAcceptable
    
let private updateDb
    (context: Context)
    (stageEntryHeaderIdUpdate: FieldUpdate<StageEntryHeaderId>)
    (amountUpdate: FieldUpdate<Money>)
    (entryTypeUpdate: FieldUpdate<JournalEntryLineType>)
    (accountCodeUpdate: FieldUpdate<AccountCode option>)
    (memoUpdate: FieldUpdate<JournalEntryLineMemo option>)
    (classificationRuleIdUpdate: FieldUpdate<ClassificationRuleId option>)
    (stageEntryLineId : StageEntryLineId)
    : Result<StageEntryLine, AppError> =
    let uuid = stageEntryLineId |> StageEntryLineId.value
    let baseParams =
        [ { name = "@unique_id"; value = UniqueId uuid } ]
    let updates =
        [
              stageEntryHeaderIdUpdate
              |> FieldUpdate.mapNoChangeToOptionWithConversion(fun n ->
                  ("entry_id = @entry_id",
                   { name = "@entry_id"; value = UniqueId(n |> StageEntryHeaderId.value) }))
              
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
                  ("code = @code",
                   { name = "@code"; value = NullableCharString(n |> Option.map AccountCode.value) }))              
              
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
    result {
        do! if updates.IsEmpty then Error(IngestionStageEntryLineNoOp) else Ok()
        let! () = executeNonQuery (context |> getDatabaseTransaction) query parameters ExactlyOne
        return! stageEntryLineId |> fetchById context
    }

/// updateCode assumes the orchestrator is validating the code maps to a real account 
let private updateCode
    (context: Context)
    (accountCodeUpdate: FieldUpdate<AccountCode option>)
    (stageEntryLineId : StageEntryLineId)
    : Result<StageEntryLine, AppError> =
    stageEntryLineId
    |> updateDb context NoChange NoChange NoChange accountCodeUpdate NoChange NoChange

/// updateCodeAndRuleId assumes the orchestrator is validating the code maps to a real account and that the rule ID is real
let private updateCodeAndRuleId
    (context: Context)
    (accountCodeUpdate: FieldUpdate<AccountCode option>)
    (classificationRuleIdUpdate: FieldUpdate<ClassificationRuleId option>)
    (stageEntryLineId : StageEntryLineId)
    : Result<StageEntryLine, AppError> =
    stageEntryLineId
    |> updateDb context NoChange NoChange NoChange accountCodeUpdate NoChange classificationRuleIdUpdate
