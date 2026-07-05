namespace Model.Ledger.Journaling

open System
open Model.Audit
open Model.Ledger.Accounts
open Model.Ledger.Journaling.JournalEntryComponent
open Model
open NodaTime
open Utilities.ResultCE
open Utilities.DAL

type JournalEntryLine =
  private  {    uniqueId: Guid                                     // REQ-JE-1.20, REQ-JE-1.21
                journalEntryId: Guid
                accountId: Guid
                amount: MoneyRecord
                lineType: JournalEntryLineType
                memo: LineMemo option                              // REQ-JE-1.26
                createdAt: Instant
                modifiedAt: Instant }

module JournalEntryLine =
    let uniqueId jel = jel.uniqueId
    let journalEntryId jel = jel.journalEntryId // REQ-JE-1.29
    let accountId jel = jel.accountId
    let amount jel = jel.amount // REQ-JE-1.23
    let lineType jel = jel.lineType
    let memo jel = jel.memo
    let createdAt jel = jel.createdAt
    let modifiedAt jel = jel.modifiedAt
    
    let validateAmount (m:MoneyRecord) : Result<MoneyRecord, string> =
        if MoneyModule.amount m <= 0M // REQ-JE-1.24
        then Error $"JEL amount fields cannot be less than or equal to 0.00"
        else Ok m
    
    let validateAccount (transaction: DbTransaction option)  (accountId: Guid) : Result<unit, string> =
        match accountId |> Account.fetchById transaction with
        | Error e -> Error e
        | Ok _ -> Ok ()

    /// validateThenConstruct is your centralized constructor for assembling
    /// and validating component types. all other constructors must
    /// pass into this one
    let private validateThenConstruct 
            (uniqueId: Guid)
            (journalEntryId: Guid)
            (accountId: Guid)
            (amount: decimal)
            (lineType: string)
            (memo: string option)
            (createdAt: Instant)
            (modifiedAt: Instant)
            (transaction: DbTransaction option)
            : Result<JournalEntryLine, string> =
        result {
            do! accountId |> validateAccount transaction //REQ-JE-1.22
            let! moneyAmount = amount |> MoneyModule.fromDecimal
            let! validAmount = moneyAmount |> validateAmount
            let! validType = lineType |> JournalEntryLineType.fromString
            let! validMemo =
                match memo with
                | Some x -> LineMemo.create x |> Result.map Some
                | None -> Ok None
                
            return {    uniqueId = uniqueId
                        journalEntryId = journalEntryId
                        accountId = accountId
                        amount = validAmount
                        lineType = validType
                        memo = validMemo
                        createdAt = createdAt
                        modifiedAt = modifiedAt } }

    let constructNew
            (journalEntryId: Guid)
            (accountId: Guid)
            (amount: decimal)
            (lineType: string)
            (memo: string option)
            (auditEnvelope: AuditEnvelope)
            (transaction: DbTransaction option)
            : Result<JournalEntryLine, string> =        
        let uniqueId = Guid.NewGuid() // REQ-JE-2.2
        let now = AuditEnvelope.instant auditEnvelope
        let createdAt =  now // REQ-SYS-3.2
        let modifiedAt = now // REQ-SYS-3.2
        validateThenConstruct uniqueId journalEntryId accountId amount lineType memo createdAt modifiedAt transaction
    
    let private insertNewToDb
            (journalEntryLine:JournalEntryLine)
            (transaction: DbTransaction option)
            : Result<unit, string> =
        let query = """
            INSERT INTO ledger.journal_entry_line(
                unique_id, journal_entry_id, account_id, amount, line_type, 
                    memo, created_at, modified_at )
            VALUES (
                @unique_id, @journal_entry_id, @account_id, @amount, @line_type, 
                    @memo, @created_at, @modified_at );"""
        let parameters = [ //  REQ-DAL-2.1, REQ-DAL-2.3 
            { name = "@unique_id"; value = UniqueId journalEntryLine.uniqueId }
            { name = "@journal_entry_id"; value = UniqueId journalEntryLine.journalEntryId }
            { name = "@account_id"; value = UniqueId journalEntryLine.accountId }
            { name = "@amount"; value = Numeric (journalEntryLine.amount |> MoneyModule.amount) };
            { name = "@line_type"; value = CharString (journalEntryLine.lineType |> JournalEntryLineType.toString) };
            { name = "@memo"; value = NullableCharString (journalEntryLine.memo |> Option.map  LineMemo.value) };
            { name = "@created_at"; value = DbInstant journalEntryLine.createdAt };
            { name = "@modified_at"; value = DbInstant journalEntryLine.modifiedAt };
        ]
        executeNonQuery query parameters ExactlyOne transaction

    let constructNewAndSaveToDb
            (journalEntryId: Guid)
            (accountId: Guid)
            (amount: decimal)
            (lineType: string)
            (memo: string option)
            (auditEnvelope: AuditEnvelope)
            (transaction: DbTransaction option)
            : Result<JournalEntryLine, string> =
        result {
            let! validJournalEntryLine =
                constructNew journalEntryId accountId amount lineType memo auditEnvelope transaction
            let! () = insertNewToDb validJournalEntryLine transaction // REQ-
            return validJournalEntryLine }

    /// The mapRow function is used to pass into DAL read functions to let DAL know
    /// how to map our query columns. Thus, we don't need to know anything about the
    /// underlying database architecture in this module and the DAL module doesn't
    /// need to know anything about our module here 
    let private mapRawForDbRead (row: RowReader) =
            ( row |> RowReader.getUuid "unique_id" ),
            ( row |> RowReader.getUuid "journal_entry_id" ),
            ( row |> RowReader.getUuid "account_id" ),
            ( row |> RowReader.getNumeric "amount" ),
            ( row |> RowReader.getString "line_type" ),
            ( row |> RowReader.getStringOption "memo" ),
            ( row |> RowReader.getInstant "created_at" ),
            ( row |> RowReader.getInstant "modified_at" )

    let private constructFromRawForDbRead
            (transaction: DbTransaction option)
            raw
            : Result<JournalEntryLine, string> =
        let id, jeId, accountId, amount, lineType, memo, createdAt, modifiedAt = raw
        validateThenConstruct id jeId accountId amount lineType memo createdAt modifiedAt transaction

    let private readRowsFromDb
            (join: string option)
            (predicate: string option)
            (limit: int option)
            (orderBy: string option)
            (parameters: QueryParameter list)
            (expectedRows: AcceptableExpectedRows)
            (transaction: DbTransaction option)
            : Result<JournalEntryLine list, string> = 
        let select = "jel.unique_id, jel.journal_entry_id, jel.account_id, jel.amount, jel.line_type, jel.memo, jel.created_at, jel.modified_at"
        let from = "ledger.journal_entry_line jel"
        let query = buildReadQuery select from join predicate limit None orderBy
        executeReaderQuery query parameters mapRawForDbRead constructFromRawForDbRead expectedRows transaction

    let fetchById
            (transaction: DbTransaction option)
            (uniqueId: Guid)
            : Result<JournalEntryLine, string> = 
        let predicate = "jel.unique_id = @unique_id"
        let parameters = [{ name = "@unique_id"; value = UniqueId uniqueId };] // REQ-DAL-2.3
        readRowsFromDb None (Some predicate) None None parameters ExactlyOne transaction
        |> Result.map List.head

    let fetchByJournalEntryId
            (transaction: DbTransaction option)
            (jeId: Guid)
            : Result<JournalEntryLine list, string> = 
        let predicate = "jel.journal_entry_id = @journal_entry_id"
        let parameters = [{ name = "@journal_entry_id"; value = UniqueId jeId };] // REQ-DAL-2.3
        let orderBy = "jel.created_at"
        readRowsFromDb None (Some predicate) None (Some orderBy) parameters AnyQuantityIsAcceptable transaction

    let fetchByAccountId // REQ-JE-3.4
            (transaction: DbTransaction option)
            (nonVoidedOnly: bool)
            (accountId: Guid)
            : Result<JournalEntryLine list, string> = 
        let join = Some "left join ledger.journal_entry je on jel.journal_entry_id = je.unique_id"
        let voidCheck =
            match nonVoidedOnly with
            | true -> $"{Environment.NewLine}and je.voided_at is null"
            | false -> String.Empty
        let predicate = Some $"jel.account_id = @account_id {voidCheck}"
        let parameters = [{ name = "@account_id"; value = UniqueId accountId };] // REQ-DAL-2.3
        let orderBy = Some "jel.created_at"
        readRowsFromDb join predicate None orderBy parameters AnyQuantityIsAcceptable transaction

    let sumLinesByType
            (debitOrCredit: JournalEntryLineType)
            (lines: JournalEntryLine list)
            : Result<MoneyRecord,string> =
        lines
        |> List.filter(fun x -> lineType x = debitOrCredit)
        |> List.map(amount) 
        |> MoneyModule.sumList
        