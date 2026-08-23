namespace Model.Ledger.Journaling

open System
open Model.Ledger.Accounts.AccountComponent
open Model.Ledger.Journaling.JournalEntryComponent
open Model
open NodaTime
open Utilities.AppError
open Utilities.ResultHelper
open DataAccessLayer.QueryParameters
open DataAccessLayer.ExecuteReader
open DataAccessLayer.ExecuteNonQuery

type JournalEntryLine =
    private
        { journalEntryLineId: JournalEntryLineId
          journalEntryHeaderId: JournalEntryHeaderId
          accountId: AccountId
          amount: Money
          lineType: JournalEntryLineType
          memo: JournalEntryLineMemo option
          createdAt: Instant
          modifiedAt: Instant }

module JournalEntryLine =
    let journalEntryLineId jel = jel.journalEntryLineId
    let journalEntryHeaderId jel = jel.journalEntryHeaderId
    let accountId jel = jel.accountId
    let amount jel = jel.amount
    let lineType jel = jel.lineType
    let memo jel = jel.memo
    let createdAt jel = jel.createdAt
    let modifiedAt jel = jel.modifiedAt

    let create
        (journalEntryLineId: JournalEntryLineId)
        (journalEntryHeaderId: JournalEntryHeaderId)
        (accountId: AccountId)
        (amount: Money)
        (lineType: JournalEntryLineType)
        (memo: JournalEntryLineMemo option)
        (createdAt: Instant)
        (modifiedAt: Instant)
        : JournalEntryLine =
        { journalEntryLineId = journalEntryLineId
          journalEntryHeaderId = journalEntryHeaderId
          accountId = accountId
          amount = amount
          lineType = lineType
          memo = memo
          createdAt = createdAt
          modifiedAt = modifiedAt }

    let insertNewToDb (context: Context.Context) (journalEntryLine: JournalEntryLine) : Result<unit, AppError> =
        let query =
            """
            INSERT INTO ledger.journal_entry_line(
                unique_id, journal_entry_id, account_id, amount, line_type, 
                    memo, created_at, modified_at )
            VALUES (
                @unique_id, @journal_entry_id, @account_id, @amount, @line_type, 
                    @memo, @created_at, @modified_at );"""
        let journalEntryLineUuid = journalEntryLine.journalEntryLineId |> JournalEntryLineId.value
        let journalEntryUuid = journalEntryLine.journalEntryHeaderId |> JournalEntryHeaderId.value
        let accountIdUuid = journalEntryLine.accountId |> AccountId.value
        let parameters =
            [
              { name = "@unique_id"; value = UniqueId journalEntryLineUuid }
              { name = "@journal_entry_id"; value = UniqueId journalEntryUuid }
              { name = "@account_id"; value = UniqueId accountIdUuid }
              { name = "@amount"; value = Numeric(journalEntryLine.amount |> Money.amount) }
              { name = "@line_type"; value = CharString(journalEntryLine.lineType |> JournalEntryLineType.toString) }
              { name = "@memo"
                value = NullableCharString(journalEntryLine.memo |> Option.map JournalEntryLineMemo.value) }
              { name = "@created_at"; value = DbInstant journalEntryLine.createdAt }
              { name = "@modified_at"; value = DbInstant journalEntryLine.modifiedAt } ]
        executeNonQuery (context |> Context.getDatabaseTransaction) query parameters ExactlyOne

    /// The mapRow function is used to pass into DAL read functions to let DAL know
    /// how to map our query columns. Thus, we don't need to know anything about the
    /// underlying database architecture in this module and the DAL module doesn't
    /// need to know anything about our module here
    let private mapRawForDbRead (row: RowReader) =
        (row |> RowReader.getUuid "unique_id"),
        (row |> RowReader.getUuid "journal_entry_id"),
        (row |> RowReader.getUuid "account_id"),
        (row |> RowReader.getNumeric "amount"),
        (row |> RowReader.getString "line_type"),
        (row |> RowReader.getStringOption "memo"),
        (row |> RowReader.getInstant "created_at"),
        (row |> RowReader.getInstant "modified_at")


    /// reconstitute constructs from primitives, performing zero validation at
    /// the collective level. All fields are assumed to have come from a
    /// trusted source (e.g. the database) where such validation occurred at
    /// the time of writing the entity. Important: no additional DB lookups can
    /// be triggered inside this function since it is called within a database
    /// reader.
    let private reconstitute raw : Result<JournalEntryLine, AppError> =
        let id, jeId, accountId, amountDec, lineTypeStr, memoStrOpt, createdAt, modifiedAt = raw
        let journalEntryLineId = id |> JournalEntryLineId.fromGuid
        let journalEntryId = jeId |> JournalEntryHeaderId.fromGuid
        let accountId = accountId |> AccountId.fromGuid
        result {
            let! amount = amountDec |> Money.fromDecimal
            let! lineType = lineTypeStr |> JournalEntryLineType.fromString
            let! memo = memoStrOpt |> convertOptionToDesiredTypeWithFallibleConverter JournalEntryLineMemo.create
            return create journalEntryLineId journalEntryId accountId amount lineType memo createdAt modifiedAt
        }

    let private readRowsFromDb
        (context: Context.Context)
        (join: string option)
        (predicate: string option)
        (limit: int option)
        (orderBy: string option)
        (parameters: QueryParameter list)
        (expectedRows: AcceptableExpectedRows)
        : Result<JournalEntryLine list, AppError> =
        let select =
            """
            jel.unique_id, jel.journal_entry_id, jel.account_id, jel.amount,
            jel.line_type, jel.memo, jel.created_at, jel.modified_at
        """
        let from = "ledger.journal_entry_line jel"
        let query = buildReadQuery None select from join predicate limit None orderBy
        executeReaderQuery
            (context |> Context.getDatabaseTransaction)
            query
            parameters
            mapRawForDbRead
            reconstitute
            expectedRows

    let fetchById (context: Context.Context) (journalEntryLineId: JournalEntryLineId) : Result<JournalEntryLine, AppError> =
        let uuid = journalEntryLineId |> JournalEntryLineId.value
        let predicate = "jel.unique_id = @unique_id"
        let parameters = [ { name = "@unique_id"; value = UniqueId uuid } ]
        readRowsFromDb context None (Some predicate) None None parameters ExactlyOne |> Result.map List.head

    let fetchByJournalEntryHeaderId
        (context: Context.Context)
        (journalEntryHeaderId: JournalEntryHeaderId)
        : Result<JournalEntryLine list, AppError> =
        let uuid = journalEntryHeaderId |> JournalEntryHeaderId.value
        let predicate = "jel.journal_entry_id = @journal_entry_id"
        let parameters = [ { name = "@journal_entry_id"; value = UniqueId uuid } ]
        let orderBy = "jel.created_at"
        readRowsFromDb context None (Some predicate) None (Some orderBy) parameters AnyQuantityIsAcceptable

    let fetchByJournalEntryHeaderIdList
        (context: Context.Context)
        (journalEntryHeaderIds: JournalEntryHeaderId list)
        : Result<JournalEntryLine list, AppError> =
        if journalEntryHeaderIds |> List.isEmpty then Error JournalEntryHeaderIdListCannotBeEmpty else
        let ordinals = [ 1 .. journalEntryHeaderIds.Length ]
        let zipped = List.zip ordinals journalEntryHeaderIds
        let namesAndParameters =
            zipped
            |> List.map(fun (ordinal, id) ->
                let uuid = id |> JournalEntryHeaderId.value
                let name = $"@journal_entry_id{ordinal}"
                let parameter = { name = name; value = UniqueId uuid }
                name, parameter)
        let names = namesAndParameters |> List.map fst |> String.concat ", "
        let parameters = namesAndParameters |> List.map snd
        let predicate = $"jel.journal_entry_id in ({names})"
        readRowsFromDb context None (Some predicate) None None parameters AnyQuantityIsAcceptable

    let fetchByAccountId
        (context: Context.Context)
        (nonVoidedOnly: bool)
        (accountId: AccountId)
        : Result<JournalEntryLine list, AppError> =
        let join = Some "left join ledger.journal_entry je on jel.journal_entry_id = je.unique_id"
        let voidCheck =
            match nonVoidedOnly with
            | true -> $"{Environment.NewLine}and je.voided_at is null"
            | false -> String.Empty
        let accountIdGuid = accountId |> AccountId.value
        let predicate = Some $"jel.account_id = @account_id {voidCheck}"
        let parameters = [ { name = "@account_id"; value = UniqueId accountIdGuid } ]
        let orderBy = Some "jel.created_at"
        readRowsFromDb context join predicate None orderBy parameters AnyQuantityIsAcceptable

    let sumLinesByType (debitOrCredit: JournalEntryLineType) (lines: JournalEntryLine list) : Result<Money, AppError> =
        lines |> List.filter(fun x -> lineType x = debitOrCredit) |> List.map(amount) |> Money.sumList
