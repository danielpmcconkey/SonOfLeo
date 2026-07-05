module ModelOrchestrator.AccountActivity

open System
open NodaTime
open Utilities.DAL
open Utilities.ResultCE
open Model.Ledger.FiscalPeriods

type AccountActivitySort =
    | AccountCode
    | EntryDate

type AccountActivityFilterDateRange = {
    beginDate: LocalDate
    endInclusive: LocalDate
}

type AccountActivityTemporalFilter =
    | FiscalPeriodId of Guid
    | DateRange of AccountActivityFilterDateRange

type AccountActivityFilter = {
    accountId: Guid option
    temporalFilter: AccountActivityTemporalFilter option
    source: string option
    accountType: string option
    accountSubtype: string option
    accountParentId: Guid option
    journalEntryId: Guid option
    unVoidedOnly: bool
}

type AccountActivityDetail = {    lineId: Guid
                                  amount: decimal
                                  lineType: string
                                  lineMemo: string option
                                  lineCreatedAt: Instant
                                  lineModifiedAt: Instant 
                                  journalEntryId: Guid
                                  entryDate: LocalDate
                                  journalEntryDescription: string
                                  journalEntrySource: string option
                                  journalEntryVoidedAt: Instant option }

/// AccountActivityRow should only be used for reading from the persistence
/// layer. It performs no validation of its own. Think of it as a reporting type
type AccountActivity = {  accountId: Guid
                          accountCode: string
                          accountName: string
                          accountType: string
                          accountSubtype: string option
                          accountParentId: Guid option
                          accountExternalRef: string option
                          activityDetail: AccountActivityDetail option }

let private mapRawForDbRead (row: RowReader)  =
        ( row |> RowReader.getUuid "account_id" ),
        ( row |> RowReader.getString "account_code" ),
        ( row |> RowReader.getString "account_name" ),
        ( row |> RowReader.getString "account_type" ),
        ( row |> RowReader.getStringOption "account_subtype" ),
        ( row |> RowReader.getUuidOption "account_parent_id" ),
        ( row |> RowReader.getStringOption "account_external_ref" ),
        ( row |> RowReader.getUuidOption "line_id" ),
        ( row |> RowReader.getNumericOption "amount" ),
        ( row |> RowReader.getStringOption "line_type" ),
        ( row |> RowReader.getStringOption "line_memo" ),
        ( row |> RowReader.getInstantOption "line_created_at" ),
        ( row |> RowReader.getInstantOption "line_modified_at" ),
        ( row |> RowReader.getUuidOption "journal_entry_id" ),
        ( row |> RowReader.getDateOption "entry_date" ),
        ( row |> RowReader.getStringOption "je_description" ),
        ( row |> RowReader.getStringOption "je_source" ),
        ( row |> RowReader.getInstantOption "je_voided_at" )

let private constructFromRawForDbRead _transaction raw =

    let accountId, accountCode, accountName, accountType, accountSubtype, accountParentId,
        accountExternalRef, lineId, amount, lineType, lineMemo, lineCreatedAt, lineModifiedAt,
        journalEntryId, entryDate, journalEntryDescription, journalEntrySource, journalEntryVoidedAt = raw

    Ok {    accountId = accountId
            accountCode = accountCode
            accountName = accountName
            accountType = accountType
            accountSubtype = accountSubtype
            accountParentId = accountParentId
            accountExternalRef = accountExternalRef
            activityDetail =
                match lineId with
                | None -> None
                | Some lid -> Some {    lineId = lid 
                                        amount = amount |> Option.get 
                                        lineType = lineType |> Option.get 
                                        lineMemo = lineMemo
                                        lineCreatedAt = lineCreatedAt |> Option.get 
                                        lineModifiedAt = lineModifiedAt |> Option.get 
                                        journalEntryId = journalEntryId |> Option.get 
                                        entryDate = entryDate |> Option.get 
                                        journalEntryDescription = journalEntryDescription |> Option.get 
                                        journalEntrySource = journalEntrySource
                                        journalEntryVoidedAt = journalEntryVoidedAt }}

let fetchFiltered // REQ-JE-3.9
        (transaction: DbTransaction option)
        (filter: AccountActivityFilter)
        (sort: AccountActivitySort option)
        : Result<AccountActivity list, string> =
    result {
        let! dateRange = 
            match filter.temporalFilter with
            | None -> Ok None
            | Some (DateRange dr) -> Ok (Some (dr.beginDate, dr.endInclusive))
            | Some (FiscalPeriodId fpId) ->
                fpId
                |> FiscalPeriod.fetchById transaction
                |> Result.map (fun fp -> Some (fp |> FiscalPeriod.startDate, fp |> FiscalPeriod.endDate))

        let voidClause = if filter.unVoidedOnly then "and je.voided_at is null" else ""
        let sortClause =
            match sort with
            | None -> ""
            | Some AccountCode -> "order by a.code"
            | Some EntryDate -> "order by je.entry_date"
        let whereClausesAndParams =
            [
                filter.accountId |> Option.map (
                    fun x -> ("and a.unique_id = @account_id", { name = "@account_id"; value = UniqueId x }))
                filter.accountType |> Option.map (
                    fun x -> ("and a.account_type = @account_type", { name = "@account_type"; value = CharString x }))
                filter.accountSubtype |> Option.map (
                    fun x -> ("and a.account_subtype = @account_subtype", { name = "@account_subtype"; value = CharString x }))
                filter.accountParentId |> Option.map (
                    fun x -> ("and a.parent_id = @parent_id", { name = "@parent_id"; value = UniqueId x }))
                dateRange |> Option.map (
                    fun (x, _) -> ("and je.entry_date >= @begin_date", { name = "@begin_date"; value = DbLocalDate x }))
                dateRange |> Option.map (
                    fun (_, x) -> ("and je.entry_date <= @end_date", { name = "@end_date"; value = DbLocalDate x }))
                filter.source |> Option.map (
                    fun x -> ("and je.je_source = @je_source", { name = "@je_source"; value = CharString x }))
                filter.journalEntryId |> Option.map (
                    fun x -> ("and je.unique_id = @je_id", { name = "@je_id"; value = UniqueId x }))
            ] |> List.choose id
        let whereClauses = whereClausesAndParams |> List.map fst |> String.concat Environment.NewLine
        let parameters = whereClausesAndParams |> List.map snd
        let query = $"""
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
        return! executeReaderQuery query parameters mapRawForDbRead constructFromRawForDbRead AnyQuantityIsAcceptable transaction
    }
    