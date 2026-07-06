module ModelOrchestrator.AccountBalance

open System
open Model
open Model.Ledger.Journaling.JournalEntryComponent
open Utilities
open Utilities.DAL
open Utilities.ResultCE

type AccountBalance = {
    accountId: Guid
    totalCredits: MoneyRecord
    totalDebits: MoneyRecord
    netBalance: MoneyRecord
}
type AccountBalanceComponent = private {
    accountId: Guid
    lineType: JournalEntryLineType
    sumAtType: MoneyRecord
}

let private mapRawForDbRead (row: RowReader)=
    ( row |> RowReader.getUuid "account_id" ),
    ( row |> RowReader.getString "line_type" ),
    ( row |> RowReader.getNumeric "sum_at_type" )
            
let private constructFromRawForDbRead _transaction raw =
    let accountId, lineType, sumAtType = raw
    result {
        let! jeLineType = lineType |> JournalEntryLineType.fromString
        let! sumAtTypeM = sumAtType |> MoneyModule.fromDecimal
        return { accountId = accountId; lineType = jeLineType; sumAtType = sumAtTypeM }
    }

let fetchByAccountIdList // REQ-JE-3.6
            (transaction: DbTransaction option)
            (accountIds: Guid list)
            : Result<AccountBalance list, string> =
    match accountIds with
    | [] -> Error "fetchByAccountIdList requires at least one account ID"
    | _ ->
        let accountFilters =
            [1..(accountIds |> List.length)]
            |> List.zip accountIds
            |> List.map (fun (account_id, iterator) ->
                ($"@account_id_{iterator}", { name = $"@account_id_{iterator}"; value = UniqueId account_id })
                )
        let accountIdsInString = accountFilters |> List.map fst |> String.concat ", "
        let parameters = accountFilters |> List.map snd
        let query = $"""
            with line_types as (
                select distinct line_type from ledger.journal_entry_line
            ), account_and_types as (
                select
                    a.unique_id as account_id,
                    lt.line_type
                from ledger.account a
                cross join line_types lt
                where a.unique_id in ({accountIdsInString})
            )
            select
                ant.account_id,
                ant.line_type,
                sum(case when jel.amount is null then 0 else jel.amount end) as sum_at_type
            from account_and_types ant
            left join ledger.journal_entry_line jel on ant.account_id = jel.account_id
                and ant.line_type = jel.line_type
            left join ledger.journal_entry je on jel.journal_entry_id = je.unique_id
            where je.voided_at is null
            group by 
                ant.account_id,
                ant.line_type
            """
        result {
            let! moneyZero = MoneyModule.fromDecimal 0M
            let! components = executeReaderQuery query parameters
                                  mapRawForDbRead constructFromRawForDbRead
                                  AnyQuantityIsAcceptable transaction
            let balances =
                components
                |> List.groupBy (fun c -> c.accountId)
                |> List.map (fun (accountId, rows) ->
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
                    MoneyModule.subtract debits credits
                    |> Result.map (fun bal -> 
                        { accountId = accountId; totalCredits = credits; totalDebits = debits; netBalance = bal }))
                |> ListHelper.listOfResultsToResultsList
            return! balances
        }