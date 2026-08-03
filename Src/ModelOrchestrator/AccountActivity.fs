module ModelOrchestrator.AccountActivity

open System
open Model
open Model.Ledger.Accounts.AccountComponent
open Model.Ledger.Journaling.JournalEntryComponent
open ModelOrchestrator.FetchFilters
open NodaTime
open Utilities.AppError
open Utilities.ResultHelper
open Model.Ledger.FiscalPeriods
open DataAccessLayer.QueryParameters
open DataAccessLayer.ExecuteReader
open Context.Context

type AccountActivityDetail =
    { lineId: JournalEntryLineId
      amount: Money
      lineType: JournalEntryLineType
      lineMemo: JournalEntryLineMemo option
      lineCreatedAt: Instant
      lineModifiedAt: Instant
      journalEntryHeaderId: JournalEntryHeaderId
      entryDate: LocalDate
      journalEntryDescription: JournalEntryDescription
      journalEntrySource: JournalEntrySource option
      journalEntryVoidedAt: Instant option }

type AccountActivity =
    { accountId: AccountId
      accountCode: AccountCode
      accountName: AccountName
      accountType: AccountType
      accountSubtype: AccountSubtype option
      accountParentId: AccountId option
      accountExternalRef: AccountExternalReference option
      activityDetail: AccountActivityDetail option }

let private mapRawForDbRead (row: RowReader) =
    (row |> RowReader.getUuid "account_id"),
    (row |> RowReader.getString "account_code"),
    (row |> RowReader.getString "account_name"),
    (row |> RowReader.getString "account_type"),
    (row |> RowReader.getStringOption "account_subtype"),
    (row |> RowReader.getUuidOption "account_parent_id"),
    (row |> RowReader.getStringOption "account_external_ref"),
    (row |> RowReader.getUuidOption "line_id"),
    (row |> RowReader.getNumericOption "amount"),
    (row |> RowReader.getStringOption "line_type"),
    (row |> RowReader.getStringOption "line_memo"),
    (row |> RowReader.getInstantOption "line_created_at"),
    (row |> RowReader.getInstantOption "line_modified_at"),
    (row |> RowReader.getUuidOption "journal_entry_id"),
    (row |> RowReader.getDateOption "entry_date"),
    (row |> RowReader.getStringOption "je_description"),
    (row |> RowReader.getStringOption "je_source"),
    (row |> RowReader.getInstantOption "je_voided_at")

let private reconstitute raw =
    // note: we use Option.get here because the entire details section is joined
    // in SQL together. If line ID is present, we can assume the entire JE line
    // is a valid JE line type, where certain fields are non-optional. This is
    // here as an exception. Option.get shouldn't be considered part of your everyday carry

    let (accountUuid,
         accountCodeString,
         accountNameString,
         accountTypeString,
         accountSubtypeString,
         accountParentUuid,
         accountExternalRefString,
         lineUuid,
         amountDecimal,
         lineTypeString,
         lineMemoString,
         lineCreatedAt,
         lineModifiedAt,
         journalEntryUuid,
         entryDate,
         journalEntryDescriptionString,
         journalEntrySourceString,
         journalEntryVoidedAt) =
        raw
    let accountId = accountUuid |> AccountId.fromGuid
    let accountParentId = accountParentUuid |> Option.map(AccountId.fromGuid)
    let lineId = lineUuid |> Option.map JournalEntryLineId.fromGuid
    let journalEntryHeaderId = journalEntryUuid |> Option.map JournalEntryHeaderId.fromGuid
    result {
        let! accountCode = accountCodeString |> AccountCode.create
        let! accountName = accountNameString |> AccountName.create
        let! accountType = accountTypeString |> AccountType.fromString
        let! accountSubtype =
            accountSubtypeString |> convertOptionToDesiredTypeWithFallibleConverter AccountSubtype.fromString
        let! accountExternalRef =
            accountExternalRefString
            |> convertOptionToDesiredTypeWithFallibleConverter AccountExternalReference.create
        let! amount = amountDecimal |> convertOptionToDesiredTypeWithFallibleConverter Money.fromDecimal
        let! lineType =
            lineTypeString |> convertOptionToDesiredTypeWithFallibleConverter JournalEntryLineType.fromString
        let! lineMemo = lineMemoString |> convertOptionToDesiredTypeWithFallibleConverter JournalEntryLineMemo.create
        let! journalEntryDescription =
            journalEntryDescriptionString
            |> convertOptionToDesiredTypeWithFallibleConverter JournalEntryDescription.create
        let! journalEntrySource =
            journalEntrySourceString
            |> convertOptionToDesiredTypeWithFallibleConverter JournalEntrySource.create
        return
            { accountId = accountId
              accountCode = accountCode
              accountName = accountName
              accountType = accountType
              accountSubtype = accountSubtype
              accountParentId = accountParentId
              accountExternalRef = accountExternalRef
              activityDetail =
                match lineId with
                | None -> None
                | Some lineId ->
                    Some
                        { lineId = lineId
                          amount = amount |> Option.get
                          lineType = lineType |> Option.get
                          lineMemo = lineMemo
                          lineCreatedAt = lineCreatedAt |> Option.get
                          lineModifiedAt = lineModifiedAt |> Option.get
                          journalEntryHeaderId = journalEntryHeaderId |> Option.get
                          entryDate = entryDate |> Option.get
                          journalEntryDescription = journalEntryDescription |> Option.get
                          journalEntrySource = journalEntrySource
                          journalEntryVoidedAt = journalEntryVoidedAt } }
    }

let fetchFiltered
    (context: Context)
    (filter: AccountActivityFilter)
    (sort: FetchSort option)
    : Result<AccountActivity list, AppError> =
    result {
        let! dateRange =
            match filter.temporalFilter with
            | None -> Ok None
            | Some(DateRange dr) -> Ok(Some(dr.beginDate, dr.endInclusive))
            | Some(FiscalPeriodIdentifier fpId) ->
                fpId
                |> FiscalPeriod.fetchById context
                |> Result.map(fun fp -> Some(fp |> FiscalPeriod.startDate, fp |> FiscalPeriod.endDate))

        let voidClause =
            if filter.unVoidedOnly then
                "and je.voided_at is null"
            else
                ""
        let sortClause =
            match sort with
            | None -> ""
            | Some AccountCodeAsc -> "order by a.code asc"
            | Some AccountCodeDesc -> "order by a.code desc"
            | Some EntryDateAsc -> "order by je.entry_date asc"
            | Some EntryDateDesc -> "order by je.entry_date desc"
            | Some AmountAsc -> "order by jel.amount asc"
            | Some AmountDesc -> "order by jel.amount desc"
        let whereClausesAndParams =
            [ filter.accountId
              |> Option.map(fun x ->
                  ("and a.unique_id = @account_id", { name = "@account_id"; value = UniqueId(x |> AccountId.value) }))

              filter.accountType
              |> Option.map(fun x ->
                  ("and a.account_type = @account_type",
                   { name = "@account_type"; value = CharString(x |> AccountType.toString) }))

              filter.accountSubtype
              |> Option.map(fun x ->
                  ("and a.account_subtype = @account_subtype",
                   { name = "@account_subtype"; value = CharString(x |> AccountSubtype.toString) }))

              filter.accountParentId
              |> Option.map(fun x ->
                  ("and a.parent_id = @parent_id", { name = "@parent_id"; value = UniqueId(x |> AccountId.value) }))

              dateRange
              |> Option.map(fun (x, _) ->
                  ("and je.entry_date >= @begin_date", { name = "@begin_date"; value = DbLocalDate x }))

              dateRange
              |> Option.map(fun (_, x) ->
                  ("and je.entry_date <= @end_date", { name = "@end_date"; value = DbLocalDate x }))

              filter.source
              |> Option.map(fun x ->
                  ("and je.je_source = @je_source",
                   { name = "@je_source"; value = CharString(x |> JournalEntrySource.value) }))

              filter.journalEntryId
              |> Option.map(fun x ->
                  let uuid = x |> JournalEntryHeaderId.value
                  ("and je.unique_id = @je_id", { name = "@je_id"; value = UniqueId uuid }))

              filter.amount
              |> Option.map(fun x ->
                  ("and jel.amount = @amount", { name = "@amount"; value = Numeric(x |> Money.amount) }))

              filter.description
              |> Option.map(fun x ->
                  let descVal = x |> JournalEntryDescription.value
                  ("and je.description like @description", { name = "@description"; value = CharString $"%%{descVal}%%" })) ]
            |> List.choose id
        let whereClauses = whereClausesAndParams |> List.map fst |> String.concat Environment.NewLine
        let parameters = whereClausesAndParams |> List.map snd
        let query =
            $"""
            select
                a.unique_id as account_id,
                a.code as account_code,
                a.account_name,
                a.account_type,
                a.account_subtype,
                a.parent_id as account_parent_id,
                a.external_ref as account_external_ref,
                jel.unique_id as line_id,
                jel.amount,
                jel.line_type,
                jel.memo as line_memo,
                jel.created_at as line_created_at,
                jel.modified_at as line_modified_at,
                je.unique_id as journal_entry_id,
                je.entry_date,
                je.description as je_description,
                je.je_source,
                je.voided_at as je_voided_at
            from ledger.account a
            left join ledger.journal_entry_line jel on jel.account_id = a.unique_id
            left join ledger.journal_entry je on jel.journal_entry_id = je.unique_id
            where 1 = 1
            {whereClauses}
            {voidClause}
            {sortClause}
            """
        return!
            executeReaderQuery
                (context |> getDatabaseTransaction)
                query
                parameters
                mapRawForDbRead
                reconstitute
                AnyQuantityIsAcceptable
    }
