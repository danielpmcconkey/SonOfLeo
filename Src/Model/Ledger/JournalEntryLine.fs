namespace Model.Ledger

open System
open Model.Audit
open Model.Ledger.JournalEntryComponent
open Model.Money
open NodaTime
open Utilities.ResultCE
open Utilities.DAL

type JournalEntryLine =
  private  {    uniqueId: Guid                                     // REQ-JE-1.20
                journalEntryId: Guid
                account: Account
                amount: Money
                lineType: JournalEntryLineType
                memo: LineMemo option                                // REQ-JE-1.26
                createdAt: Instant
                modifiedAt: Instant }

module JournalEntryLine =
    let uniqueId jel = jel.uniqueId
    let journalEntryId jel = jel.journalEntryId // REQ-JE-1.29
    let account jel = jel.account
    let amount jel = jel.amount // REQ-JE-1.23
    let lineType jel = jel.lineType
    let memo jel = jel.memo
    let createdAt jel = jel.createdAt
    let modifiedAt jel = jel.modifiedAt
    
    let validateAmount (m:Money) : Result<Money, string> =
        if Money.amount m <= 0M // REQ-JE-1.24
        then Error $"JEL amount fields cannot be less than or equal to 0.00"
        else Ok m

    /// constructOmni is your centralized constructor for assembling
    /// and validating component types. all other constructors must
    /// pass into this one
    let private constructOmni 
            (uniqueId: Guid)
            (journalEntryId: Guid)
            (accountId: Guid)
            (amount: decimal)
            (lineType: string)
            (memo: string option)
            (createdAt: Instant)
            (modifiedAt: Instant)
            : Result<JournalEntryLine, string> =
        result {
            let! validAccount = Account.fetchById accountId //REQ-JE-1.22
            let! moneyAmount = amount |> Money.fromDecimal
            let! validAmount = moneyAmount |> validateAmount
            let! validType = lineType |> JournalEntryLineType.fromString
            let! validMemo =
                match memo with
                | Some x -> LineMemo.create x |> Result.map Some
                | None -> Ok None
                
            return {    uniqueId = uniqueId
                        journalEntryId = journalEntryId
                        account = validAccount
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
            : Result<JournalEntryLine, string> =        
        let uniqueId = Guid.NewGuid()
        let now = AuditEnvelope.instant auditEnvelope
        let createdAt =  now // REQ-SYS-3.2
        let modifiedAt = now // REQ-SYS-3.2
        constructOmni uniqueId journalEntryId accountId amount lineType memo createdAt modifiedAt
    
    let private insertNewToDb (journalEntryLine:JournalEntryLine): Result<unit, string> =
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
            { name = "@journal_entry_id"; value = UniqueId journalEntryLine.account.uniqueId }
            { name = "@amount"; value = Numeric (journalEntryLine.amount |> Money.amount) };
            { name = "@line_type"; value = CharString (journalEntryLine.lineType |> JournalEntryLineType.toString) };
            { name = "@memo"; value = NullableCharString (journalEntryLine.memo |> Option.map  LineMemo.value) };
            { name = "@created_at"; value = DbInstant journalEntryLine.createdAt };
            { name = "@modified_at"; value = DbInstant journalEntryLine.modifiedAt };
        ]
        executeNonQuery query parameters ExactlyOne

    let constructNewAndSaveToDb
            (journalEntryId: Guid)
            (accountId: Guid)
            (amount: decimal)
            (lineType: string)
            (memo: string option)
            (auditEnvelope: AuditEnvelope)
            : Result<JournalEntryLine, string> =
        result {
            let! validJournalEntryLine = constructNew journalEntryId accountId amount lineType memo auditEnvelope
            let! () = insertNewToDb validJournalEntryLine // REQ-
            return validJournalEntryLine }