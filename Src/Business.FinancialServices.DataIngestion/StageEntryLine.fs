module Business.FinancialServices.DataIngestion.StageEntryLine

open App.Utility.IAppError
open App.DataAccessLayer.DalError
open App.DataAccessLayer
open App.DataAccessLayer.ExecuteNonQuery
open App.DataAccessLayer.ExecuteReader
open App.DataAccessLayer.QueryParameter
open App.Utility.FieldUpdate
open App.Utility.Result
open App.Session
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Ledger.JournalEntryComponent
open Business.FinancialServices.DataIngestion.DataIngestionError
open Business.FinancialServices.DataIngestion.StageEntryComponent

type StageEntryLine = private {
    stageEntryLineId: StageEntryLineId
    stageEntryHeaderId: StageEntryHeaderId
    amount: Money.Money
    lineType: JournalEntryLineType
    accountId: AccountId option
    memo: JournalEntryLineMemo option
    journalEntryLineId: JournalEntryLineId option }

type StageEntryLineFieldUpdates = {
    lineIdToUpdate: StageEntryLineId
    amountUpdate: FieldUpdate<Money.Money>
    entryTypeUpdate: FieldUpdate<JournalEntryLineType>
    accountIdUpdate: FieldUpdate<AccountId option>
    memoUpdate: FieldUpdate<JournalEntryLineMemo option>
    journalEntryLineIdUpdate: FieldUpdate<JournalEntryLineId option> }

let stageEntryLineId l = l.stageEntryLineId
let stageEntryHeaderId l = l.stageEntryHeaderId
let amount l = l.amount
let lineType l = l.lineType
let accountId l = l.accountId
let memo l = l.memo
let journalEntryLineId l = l.journalEntryLineId

let create
    (stageEntryLineId: StageEntryLineId)
    (stageEntryHeaderId: StageEntryHeaderId)
    (amount : Money.Money)
    (entryType : JournalEntryLineType)
    (accountId: AccountId option)
    (memo: JournalEntryLineMemo option)
    (journalEntryLineId: JournalEntryLineId option)
    : StageEntryLine = {
                stageEntryHeaderId = stageEntryHeaderId
                stageEntryLineId = stageEntryLineId
                amount = amount
                lineType = entryType
                accountId = accountId
                memo = memo
                journalEntryLineId = journalEntryLineId }

let confirmAccountId
    (context: Context.Context)
    (accountIdOption: AccountId option)
    : Result<unit, IAppError> =
    match accountIdOption with
    | None -> Ok ()
    | Some accountCode ->
        let uuid = accountCode |> AccountId.value
        uuid |> LookupCache.accountIdToCode.fetch (context |> Context.getDatabaseTransaction)
        |> whenNoRows (LedgerError.AccountIdDoesntMatch uuid)
        |> Result.map ignore

let persist (context: Context.Context) (stageEntryLine: StageEntryLine) : Result<unit, IAppError> =
    let queryStatement =
        """
        insert into ingestion.staged_entry_line (
	        unique_id, entry_id, amount, line_type, account_id, memo, journal_entry_line_id)
        values (
	        @unique_id,
            @entry_id,
            @amount,
            @line_type,
            @account_id,
            @memo,
            @journal_entry_line_id);"""
    result {
        let uuid = stageEntryLine.stageEntryLineId |> StageEntryLineId.value
        let headerUuid = stageEntryLine.stageEntryHeaderId |> StageEntryHeaderId.value
        let amount = stageEntryLine.amount |> Money.amount
        let lineType = stageEntryLine.lineType |> JournalEntryLineType.toString
        do! stageEntryLine.accountId |> confirmAccountId context
        let accountUuid = stageEntryLine.accountId |> Option.map AccountId.value
        let memo = stageEntryLine.memo |> Option.map JournalEntryLineMemo.value
        let journalEntryLineUuid = stageEntryLine.journalEntryLineId |> Option.map JournalEntryLineId.value
        let parameters =
            [
              { name = "@unique_id"; value = UniqueId(uuid) }
              { name = "@entry_id"; value = UniqueId(headerUuid) }
              { name = "@amount"; value = Numeric(amount) }
              { name = "@line_type"; value = CharString(lineType) }
              { name = "@account_id"; value = NullableUniqueId(accountUuid) }
              { name = "@memo"; value = NullableCharString(memo) }
              { name = "@journal_entry_line_id"; value = NullableUniqueId(journalEntryLineUuid) }
            ]
        return! executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters ExactlyOne
    }
        
let private reconstitute raw =
    result {
        let (uuid,
             headerUuid,
             amountDec,
             lineTypeStr,
             accountUuidOption,
             memoStrOption,
             journalEntryLineUuidOption) =
            raw
        let stageEntryLineId = uuid |> StageEntryLineId.fromGuid
        let stageEntryHeaderId = headerUuid |> StageEntryHeaderId.fromGuid
        let! amount = amountDec |> Money.fromDecimal
        let! lineType = lineTypeStr |> JournalEntryLineType.fromString
        let accountId = accountUuidOption |> Option.map AccountId.fromGuid
        let! memo = memoStrOption |> convertOptionToDesiredTypeWithFallibleConverter JournalEntryLineMemo.create
        let journalEntryLineId = journalEntryLineUuidOption |> Option.map JournalEntryLineId.fromGuid
        return
            create
                stageEntryLineId
                stageEntryHeaderId
                amount
                lineType
                accountId
                memo
                journalEntryLineId
    }

let private mapRawForDbRead (row: RowReader) =
    (row |> RowReader.getUuid "unique_id"),
    (row |> RowReader.getUuid "entry_id"),
    (row |> RowReader.getNumeric "amount"),
    (row |> RowReader.getString "line_type"),
    (row |> RowReader.getUuidOption "account_id"),
    (row |> RowReader.getStringOption "memo"),
    (row |> RowReader.getUuidOption "journal_entry_line_id")
    
let private query
    (context: Context.Context)
    (predicate: string option)
    (limit: int option)
    (parameters: QueryParameter list)
    (expectedRows: AcceptableExpectedRows)
    : Result<StageEntryLine list, IAppError> =
    let select =
        """
        sel.unique_id, sel.entry_id, sel.amount, sel.line_type, sel.account_id, sel.memo,
        sel.journal_entry_line_id
        """
    let from = "ingestion.staged_entry_line sel"
    let queryStatement = buildReadQuery None select from None predicate limit None None
    executeReaderQuery
        (context |> Context.getDatabaseTransaction)
        queryStatement
        parameters
        mapRawForDbRead
        reconstitute
        expectedRows

let fetchById (context: Context.Context) (lineId: StageEntryLineId) : Result<StageEntryLine, IAppError> =
    let predicate = "sel.unique_id = @unique_id"
    let uuid = lineId |> StageEntryLineId.value
    let parameters = [ { name = "@unique_id"; value = UniqueId uuid } ]
    query context (Some predicate) None parameters ExactlyOne |> Result.map List.head

let fetchByIdList
    (context: Context.Context)
    (lineIds: StageEntryLineId list)
    : Result<StageEntryLine list, IAppError> =
    if lineIds |> List.isEmpty then Error IngestionStageEntryLineIdListCannotBeEmpty else
    let ordinals = [ 1 .. lineIds.Length ]
    let zipped = List.zip ordinals lineIds
    let namesAndParameters =
        zipped
        |> List.map(fun (ordinal, id) ->
            let uuid = id |> StageEntryLineId.value
            let name = $"@stageEntryLineId{ordinal}"
            let parameter = { name = name; value = UniqueId uuid }
            name, parameter)
    let names = namesAndParameters |> List.map fst |> String.concat ", "
    let parameters = namesAndParameters |> List.map snd
    let predicate = $"sel.unique_id in ({names})"
    query context (Some predicate) None parameters AnyQuantityIsAcceptable

let fetchByHeaderId (context: Context.Context) (lineId: StageEntryHeaderId) : Result<StageEntryLine list, IAppError> =
    let predicate = "sel.entry_id = @unique_id"
    let accountIdGuid = lineId |> StageEntryHeaderId.value
    let parameters = [ { name = "@unique_id"; value = UniqueId accountIdGuid } ]
    query context (Some predicate) None parameters AnyQuantityIsAcceptable

let fetchByHeaderIdList
    (context: Context.Context)
    (stageEntryHeaderIds: StageEntryHeaderId list)
    : Result<StageEntryLine list, IAppError> =
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
    query context (Some predicate) None parameters AnyQuantityIsAcceptable

let update
    (context: Context.Context)
    (fieldUpdates: StageEntryLineFieldUpdates)
    : Result<StageEntryLine, IAppError> =
    result {
        let stageEntryLineId = fieldUpdates.lineIdToUpdate
        let amountUpdate = fieldUpdates.amountUpdate
        let entryTypeUpdate = fieldUpdates.entryTypeUpdate
        let accountIdUpdate = fieldUpdates.accountIdUpdate
        do! match accountIdUpdate with
                | NoChange -> Ok ()
                | SetTo x -> x |> confirmAccountId context
        let memoUpdate = fieldUpdates.memoUpdate
        let journalEntryLineIdUpdate = fieldUpdates.journalEntryLineIdUpdate
        let uuid = stageEntryLineId |> StageEntryLineId.value
        let baseParams =
            [ { name = "@unique_id"; value = UniqueId uuid } ]
        let updates =
            [
                  amountUpdate
                  |> mapNoChangeToOptionWithConversion(fun n ->
                      ("amount = @amount",
                       { name = "@amount"; value = Numeric(n |> Money.amount) }))
                  
                  entryTypeUpdate
                  |> mapNoChangeToOptionWithConversion(fun n ->
                      ("line_type = @line_type",
                       { name = "@line_type"; value = CharString(n |> JournalEntryLineType.toString) }))
                  
                  accountIdUpdate
                  |> mapNoChangeToOptionWithConversion(fun n ->
                      ("account_id = @account_id",
                       { name = "@account_id"; value = NullableUniqueId(n |> Option.map AccountId.value) }))
                  
                  memoUpdate
                  |> mapNoChangeToOptionWithConversion(fun n ->
                      ("memo = @memo",
                       { name = "@memo"; value = NullableCharString(n |> Option.map JournalEntryLineMemo.value) }))

                  journalEntryLineIdUpdate
                  |> mapNoChangeToOptionWithConversion(fun n ->
                      ("journal_entry_line_id = @journal_entry_line_id",
                       { name = "@journal_entry_line_id"
                         value = NullableUniqueId(n |> Option.map JournalEntryLineId.value) }))
            ]
            |> List.choose id
        let setClauses = updates |> List.map fst |> String.concat ", "
        let parameters = baseParams @ (updates |> List.map snd)
        let queryStatement =
            $"""
            UPDATE ingestion.staged_entry_line
            set
                {setClauses}
            WHERE unique_id = @unique_id;
        """
        do! if updates.IsEmpty then Error(IngestionStageEntryLineNoOp) else Ok()
        
        let! () = executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters ExactlyOne
        return! stageEntryLineId |> fetchById context
    }

let updateAccountId
    (context: Context.Context)
    (accountIdUpdate: FieldUpdate<AccountId option>)
    (stageEntryLineId : StageEntryLineId)
    : Result<StageEntryLine, IAppError> =
    let fieldUpdates = {
        lineIdToUpdate = stageEntryLineId
        amountUpdate = NoChange
        entryTypeUpdate = NoChange
        accountIdUpdate = accountIdUpdate
        memoUpdate = NoChange
        journalEntryLineIdUpdate = NoChange }
    update context fieldUpdates

let updateJournalEntryLineId
    (context: Context.Context)
    (journalEntryLineIdUpdate: FieldUpdate<JournalEntryLineId option>)
    (stageEntryLineId : StageEntryLineId)
    : Result<StageEntryLine, IAppError> =
    let fieldUpdates = {
        lineIdToUpdate = stageEntryLineId
        amountUpdate = NoChange
        entryTypeUpdate = NoChange
        accountIdUpdate = NoChange
        memoUpdate = NoChange
        journalEntryLineIdUpdate = journalEntryLineIdUpdate }
    update context fieldUpdates
