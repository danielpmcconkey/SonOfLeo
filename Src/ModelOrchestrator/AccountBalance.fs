module ModelOrchestrator.AccountBalance

open System
open Model
open Model.Ledger.Accounts.AccountComponent
open Model.Ledger.Journaling.JournalEntryComponent
open NodaTime
open Utilities
open Utilities.DAL
open Utilities.ResultCE

type AccountBalance = {
    accountId: Guid
    totalCredits: Money
    totalDebits: Money
    netBalance: Money
}
type AccountBalanceComponent = private {
    accountId: Guid
    lineType: JournalEntryLineType
    accountType: AccountType
    sumAtType: Money
}

let private mapRawForDbRead (row: RowReader)=
    ( row |> RowReader.getUuid "account_id" ),
    ( row |> RowReader.getString "line_type" ),
    ( row |> RowReader.getString "account_type" ),
    ( row |> RowReader.getNumeric "sum_at_type" )
            
let private constructFromRawForDbRead _transaction raw =
    let accountId, lineType, accountType, sumAtType = raw
    result {
        let! jeLineType = lineType |> JournalEntryLineType.fromString
        let! accountType = accountType |> AccountType.fromString
        let! sumAtTypeM = sumAtType |> Money.fromDecimal
        return { accountId = accountId; lineType = jeLineType; accountType = accountType; sumAtType = sumAtTypeM }
    }

let fetchByAccountIdList // REQ-JE-3.6
            (transaction: DbTransaction option)
            (accountIds: Guid list)
            (asOf: LocalDate option) // REQ-JE-3.6.2
            : Result<AccountBalance list, string> =
    match accountIds with
    | [] -> Error "fetchByAccountIdList requires at least one account ID"
    | _ ->
        let asOfParam, asOfJoin =
            match asOf with
            | None -> [], ""
            | Some x -> [{ name = "@as_of"; value = DbLocalDate x }], "and je.entry_date <= @as_of"
        let accountFilters =
            [1..(accountIds |> List.length)]
            |> List.zip accountIds
            |> List.map (fun (account_id, iterator) ->
                ($"@account_id_{iterator}", { name = $"@account_id_{iterator}"; value = UniqueId account_id })
                )
        let accountIdsInString = accountFilters |> List.map fst |> String.concat ", "
        let parameters = asOfParam @ (accountFilters |> List.map snd) 
        let query = $"""
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
                        else jel.amount end) as sum_at_type
            from account_and_types ant
            left join ledger.journal_entry_line jel on ant.account_id = jel.account_id
                and ant.line_type = jel.line_type
            left join ledger.journal_entry je on jel.journal_entry_id = je.unique_id
                {asOfJoin} -- REQ-JE-3.6.2
            group by 
                ant.account_id,
                ant.line_type,
                ant.account_type
            """
        result {
            let! moneyZero = Money.fromDecimal 0M
            let! components = executeReaderQuery query parameters
                                  mapRawForDbRead constructFromRawForDbRead
                                  AnyQuantityIsAcceptable transaction
            let balances =
                components
                |> List.groupBy (fun c -> c.accountId, c.accountType)
                |> List.map (fun ((accountId, accountType), rows) ->
                    let credits =
                        rows
                        |> List.tryFind(fun r -> r.lineType = Credit)
                        |> Option.map (fun r -> r.sumAtType)
                        |> Option.defaultValue moneyZero
                    let debits =
                        rows
                        |> List.tryFind(fun r -> r.lineType = Debit)
                        |> Option.map (fun r -> r.sumAtType)
                        |> Option.defaultValue moneyZero
                    if accountType |> AccountType.normalBalance = AccountTypeNormalBalance.Debit // REQ-JE-3.6.1
                        then Money.subtractVal1FromVal2 credits debits
                        else Money.subtractVal1FromVal2 debits credits
                    |> Result.map (fun bal -> 
                        { accountId = accountId; totalCredits = credits; totalDebits = debits; netBalance = bal }))
                |> ListHelper.listOfResultsToResultsList
            return! balances
        }