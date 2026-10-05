open App.Utility.IAppError
open App.DataAccessLayer.DbTransaction
open App.DataAccessLayer.ExecuteReader
open App.Operation.CoreAuditableAction
open App.Session
open App.Utility.Result
open Business.FinancialServices.Ledger.AccountComponent
open Business.General
open Business.FinancialServices.Ledger.LedgerError
open NodaTime
open Ui.InterfaceBridge.InterfaceContracts.AccountContracts
open Ui.InterfaceBridge.InterfaceContracts.IngestionContracts

// feature flags
let isCoaAlreadyLoaded = true
let isTransplantWritten = true
    
let private readAccounts context  =
    let reconstitute
        (raw: string * string * string * string * LocalDate * LocalDate option * string option * string option * string option)
        : Result<string * AccountCreateInput, IAppError> =
        let (ordinal, codeString, nameString, accountTypeString, activeBegin,
             activeEnd, subtypeString, parentCodeString, extRefString) = raw
        result {
            let! accountCode = codeString |> AccountCode.create
            let! accountName = nameString |> AccountName.create
            let! accountType = accountTypeString |> AccountType.fromString
            let! activityPeriod =
                match ActivityPeriod.create activeBegin activeEnd ActivityPeriod.NotConsideredAvailableBeforeBeginDate with
                | Error (AsError (BizGeneralError.ActiveEndBeforeBegin _)) ->
                    error(AccountActiveEndBeforeBegin (activeBegin, activeEnd))
                | other -> other
            let! subtype =
                subtypeString
                |> Option.map(fun x -> x |> AccountSubtype.fromString |> Result.map Some)
                |> Option.defaultValue(Ok None)
            let! parentCode = parentCodeString |> convertOptionToDesiredTypeWithFallibleConverter AccountCode.create
            let! externalReference =
                extRefString
                |> Option.map(fun x -> x |> AccountExternalReference.create |> Result.map Some)
                |> Option.defaultValue(Ok None)
            let input = { code = codeString
                          name = nameString
                          accountTypeSt = accountTypeString
                          activeBegin = activeBegin
                          activeEnd = activeEnd
                          subType = subtypeString
                          parentCode = parentCodeString
                          reference = extRefString }
            return (ordinal, input)
        }
    let  mapRawForDbRead (row: RowReader) =
        (row |> RowReader.getString "ordinal"),
        (row |> RowReader.getString "code"),
        (row |> RowReader.getString "account_name"),
        (row |> RowReader.getString "account_type"),
        (row |> RowReader.getDate "active_begin"),
        (row |> RowReader.getDateOption "active_end"),
        (row |> RowReader.getStringOption "account_subtype"),
        (row |> RowReader.getStringOption "parent_code"),
        (row |> RowReader.getStringOption "reference")
    let queryStatement = $"""
    with recursive depth as (
        select id, 0 as lvl from ledger.account where parent_id is null
        union all
        select c.id, d.lvl + 1
        from ledger.account c join depth d on c.parent_id = d.id
    )
    select 
        'account-' || lpad((row_number() over (order by d.lvl, a.code))::text, 3, '0') as ordinal,
        a.code,
        a.name as account_name,
        case 
            when at.name = 'asset' then 'Asset' 
            when at.name = 'liability' then 'Liability' 
            when at.name = 'equity' then 'Equity' 
            when at.name = 'revenue' then 'Revenue' 
            when at.name = 'expense' then 'Expense' 
            end as account_type,
        '2026-01-01'::date as active_begin,
        null::date as active_end,
        a.account_subtype,
        ap.code as parent_code,
        a.external_ref as reference
    from ledger.account a
    join depth d on d.id = a.id
    join ledger.account_type at on a.account_type_id = at.id
    left join ledger.account ap on a.parent_id = ap.id
    where a.is_active = true
    order by d.lvl, a.code
    """
    
    executeReaderQuery
        (context |> Context.getDatabaseTransaction)
        queryStatement
        []
        mapRawForDbRead
        reconstitute
        AcceptableExpectedRows.AnyQuantityIsAcceptable

let private writeAccountJsons context =
    result {
        let! dropDir = App.Utility.Config.getConfigValue<string> "DropDir"
        let! accountsAndNameList = readAccounts context
        let! writes = 
            accountsAndNameList
            |> List.sortBy fst
            |> List.map(fun (ordinal, input) ->
                result {
                    let! json = input |> App.Utility.Json.Json.toJson<AccountCreateInput>
                    let fileName = $"{ordinal}.json"
                    let! fullPath = App.Utility.File.createFullPath dropDir fileName
                    return! App.Utility.File.writeTextFile fullPath json 
                })
            |> convertListOfResultsToResultsList
            |> Result.map ignore
        return ()
    }

/// Every LeoBloom journal entry as it stood at the cutoff, one staging record per line, keyed by fiscal period.
/// The cutoff falls between the 2026-08-23 and 2026-09-05 Saturday runs (nothing was posted in between): entries
/// created before it transplant; the 09-05 batch onward replays through the new parsers. An entry voided before the
/// cutoff is left out; one voided after it was live at the cutoff, so it comes across and its void is replayed by hand.
///
/// The fi source and reference matter beyond provenance: dedup matches a staged entry's (source, fi_reference)
/// against posted external references, and the replayed feeds overlap the cutoff ([redacted] sends its whole feed,
/// [redacted] year to date). So an imported entry keeps LeoBloom's institution reference type as its source and the
/// reference value unchanged, and the new parsers must emit the same pair. An entry with no institution reference
/// (hand posts, opening balances, true-ups), or whose reference another transplanted entry shares, gets source
/// "leobloom" and reference "je:<LeoBloom id>" — a shared reference would let dedup flag a real entry as a repeat.
let private readTransplantLines context =
    let mapRawForDbRead (row: RowReader) =
        (row |> RowReader.getString "period_key"),
        { baseStageEntryGroupId = row |> RowReader.getString "group_id"
          entryDate = row |> RowReader.getDate "entry_date"
          description = row |> RowReader.getString "description"
          fiSource = row |> RowReader.getString "fi_source"
          fiReference = row |> RowReader.getString "fi_reference"
          amount = row |> RowReader.getNumeric "amount"
          entryType = row |> RowReader.getString "entry_type"
          accountCode = row |> RowReader.getStringOption "account_code"
          memo = row |> RowReader.getStringOption "memo" }
    let queryStatement = """
    with je as (
        select * from ledger.journal_entry
        where created_at < timestamptz '2026-08-24 00:00 America/New_York'
          and (voided_at is null or voided_at >= timestamptz '2026-08-24 00:00 America/New_York')
    ),
    fi_ref as (
        select distinct on (r.journal_entry_id)
            r.journal_entry_id,
            case r.reference_type when '[redacted]' then '[redacted]' else r.reference_type end as fi_source,
            r.reference_value as fi_reference
        from ledger.journal_entry_reference r
        where r.reference_type in ([REDACTED])
        order by r.journal_entry_id, r.id
    ),
    keyed as (
        select je.id, f.fi_source, f.fi_reference,
               count(f.fi_reference) over (partition by f.fi_source, f.fi_reference) as sharers
        from je left join fi_ref f on f.journal_entry_id = je.id
    )
    select
        to_char(je.entry_date, 'YYYY-MM') as period_key,
        'je-' || je.id as group_id,
        je.entry_date,
        je.description,
        case when k.fi_reference is null or k.sharers > 1 then 'leobloom' else k.fi_source end as fi_source,
        case when k.fi_reference is null or k.sharers > 1 then 'je:' || je.id else k.fi_reference end as fi_reference,
        l.amount,
        case l.entry_type when 'debit' then 'Debit' else 'Credit' end as entry_type,
        a.code as account_code,
        nullif(trim(l.memo), '') as memo
    from je
    join keyed k on k.id = je.id
    join ledger.journal_entry_line l on l.journal_entry_id = je.id
    join ledger.account a on a.id = l.account_id
    order by je.entry_date, je.id, l.id
    """
    executeReaderQuery
        (context |> Context.getDatabaseTransaction)
        queryStatement
        []
        mapRawForDbRead
        Ok
        AcceptableExpectedRows.AnyQuantityIsAcceptable

/// One JSONL file per fiscal period, so each month can be ingested, posted and trial-balanced before the next.
let private writeTransplantFiles context =
    result {
        let! dropDir = App.Utility.Config.getConfigValue<string> "DropDir"
        let! lines = readTransplantLines context
        return!
            lines
            |> List.groupBy fst
            |> List.map (fun (periodKey, rows) ->
                result {
                    let! jsons =
                        rows
                        |> List.map (snd >> App.Utility.Json.Json.toJson<BaseStageRawRowInput>)
                        |> convertListOfResultsToResultsList
                    let! fullPath = App.Utility.File.createFullPath dropDir $"leobloom-transplant-{periodKey}.jsonl"
                    return! App.Utility.File.writeTextFile fullPath (String.concat "\n" jsons)
                })
            |> convertListOfResultsToResultsList
            |> Result.map ignore
    }

[<EntryPoint>]
let main _ =
    printfn "LeoBloom Data Migrator"
    let context = Context.create NoTransaction FetchOnly
    let railroad =
        result {
            do! if isCoaAlreadyLoaded = false then writeAccountJsons context else Ok ()
            do! if isTransplantWritten = false then writeTransplantFiles context else Ok ()
            return ()
        }
    match railroad with
    | Ok _ ->
        printfn "shit's written"
        0
    | Error e ->
        eprintfn "shit broke"
        eprintfn "%s.%s: %s" e.DomainName e.CaseName (e.ToMessage())
        1
    
    
        
