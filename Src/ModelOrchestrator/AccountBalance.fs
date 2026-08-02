module ModelOrchestrator.AccountBalance

open System
open Model
open Model.Ledger.Accounts.AccountComponent
open Model.Ledger.Journaling.JournalEntryComponent
open NodaTime
open Utilities.AppError
open DataAccessLayer.QueryParameters
open DataAccessLayer.ExecuteReader
open Utilities.ResultHelper
open Context.Context

type AccountBalance = { accountId: AccountId; totalCredits: Money; totalDebits: Money; netBalance: Money }
type AccountBalanceComponent =
    private { accountId: AccountId; lineType: JournalEntryLineType; accountType: AccountType; sumAtType: Money }

let private mapRawForDbRead (row: RowReader) : Guid * string * string * decimal =
    (row |> RowReader.getUuid "account_id"),
    (row |> RowReader.getString "line_type"),
    (row |> RowReader.getString "account_type"),
    (row |> RowReader.getNumeric "sum_at_type")

let private reconstitute (raw: Guid * string * string * decimal) : Result<AccountBalanceComponent, AppError> =
    let accountIdGuid, lineType, accountType, sumAtType = raw
    result {
        let accountId = accountIdGuid |> AccountId.fromGuid
        let! jeLineType = lineType |> JournalEntryLineType.fromString
        let! accountType = accountType |> AccountType.fromString
        let! sumAtTypeM = sumAtType |> Money.fromDecimal
        return { accountId = accountId; lineType = jeLineType; accountType = accountType; sumAtType = sumAtTypeM }
    }

let fetchByAccountIdList
    (context: Context)
    (accountIds: AccountId list)
    (asOf: LocalDate option)
    : Result<AccountBalance list, AppError> =
    match accountIds with
    | [] -> Error(AccountBalanceFetchInvalidArguments)
    | _ ->
        let asOfParam, asOfJoin =
            match asOf with
            | None -> [], ""
            | Some x -> [ { name = "@as_of"; value = DbLocalDate x } ], "and je.entry_date <= @as_of"
        let accountFilters =
            [ 1 .. (accountIds |> List.length) ]
            |> List.zip accountIds
            |> List.map(fun (accountId, iterator) ->
                let accountIdGuid = accountId |> AccountId.value
                ($"@account_id_{iterator}", { name = $"@account_id_{iterator}"; value = UniqueId accountIdGuid }))
        let accountIdsInString = accountFilters |> List.map fst |> String.concat ", "
        let parameters = asOfParam @ (accountFilters |> List.map snd)
        let query =
            $"""
            with line_types as (
                select '{Credit |> JournalEntryLineType.toString}' as line_type
                union all
                select '{Debit |> JournalEntryLineType.toString}' as line_type
            ), account_and_types as (
                select
                    a.unique_id as account_id,
                    lt.line_type,
                    a.account_type
                from ledger.account a
                cross join line_types lt
                where a.unique_id in ({accountIdsInString}) )
            select
                ant.account_id,
                ant.line_type,
                ant.account_type,
                sum ( case 
                        when je.voided_at is not null then 0
                        when jel.amount is null then 0 
						when je.entry_date is null then 0 -- the asOf only filters out the JE, not the line
                        else jel.amount end) as sum_at_type
            from account_and_types ant
            left join ledger.journal_entry_line jel on ant.account_id = jel.account_id
                and ant.line_type = jel.line_type
            left join ledger.journal_entry je on jel.journal_entry_id = je.unique_id
                {asOfJoin}
            group by 
                ant.account_id,
                ant.line_type,
                ant.account_type
            """
        result {
            let! moneyZero = Money.fromDecimal 0M
            let! components =
                executeReaderQuery
                    (context |> getDatabaseTransaction)
                    query
                    parameters
                    mapRawForDbRead
                    reconstitute
                    AnyQuantityIsAcceptable
            let balances =
                components
                |> List.groupBy(fun c -> c.accountId, c.accountType)
                |> List.map(fun ((accountId, accountType), rows) ->
                    let credits =
                        rows
                        |> List.tryFind(fun r -> r.lineType = Credit)
                        |> Option.map(fun r -> r.sumAtType)
                        |> Option.defaultValue moneyZero
                    let debits =
                        rows
                        |> List.tryFind(fun r -> r.lineType = Debit)
                        |> Option.map(fun r -> r.sumAtType)
                        |> Option.defaultValue moneyZero
                    if
                        accountType |> AccountType.normalBalance = AccountTypeNormalBalance.Debit
                    then
                        Money.subtractVal1FromVal2 credits debits
                    else
                        Money.subtractVal1FromVal2 debits credits
                    |> Result.map(fun bal ->
                        { accountId = accountId; totalCredits = credits; totalDebits = debits; netBalance = bal }))
                |> convertListOfResultsToResultsList
            return! balances
        }
