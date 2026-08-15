module Model.LookupCache

open System
open DataAccessLayer.ExecuteReader
open Utilities.AppError
open Utilities.ResultHelper
open DataAccessLayer.DbTransaction
open DataAccessLayer.QueryParameters


(*
Note: the LookupCache is designed to support an easy translation between UUIDs used in the model and string codes and
keys used by the callers of our public user interfaces. It is designed currently to support short-burst CLI invocations
where the cache lifetime only needs to be the life of any single route. Therefore, there is no invalidation by design.

We also intentionally fail loudly using failwith on init load. That tells the caller that something is wrong and they
need to triage before proceeding. This is deliberate.

Any future usages for this application that will carry longer life cycles will need to re-design this cache if it plans
to also involve any CRUD operations of core module entities.
*)

type Cache<'K, 'V when 'K: comparison>
    (loadAll: unit -> Result<Map<'K, 'V>, AppError>, loadOne: Context.Context -> 'K -> Result<'V, AppError>) =
    let mutable cache = loadAll() |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
    member _.fetch context (key: 'K) : Result<'V, AppError> =
        match cache |> Map.tryFind key with
        | Some v -> Ok v
        | None ->
            match key |> loadOne context with
            | Ok v ->
                cache <- cache |> Map.add key v
                Ok v
            | Error e -> Error e

type idAndString = { id: Guid; key: string }

let mapRawForDbRead (fieldNameId: string) (fieldNameKey: string) (row: RowReader) =
    let id = row |> RowReader.getUuid fieldNameId
    let key = row |> RowReader.getString fieldNameKey
    id, key

let constructFromRawForDbRead (raw: Guid * string) : Result<idAndString, AppError> =
    let id, key = raw
    Ok { id = id; key = key }


let accountCodeToId =
    Cache<string, Guid>(
        (fun _ ->
            let tran = createDbTransaction() |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
            let query = "select unique_id, code from ledger.account"
            result {
                let! rows =
                    executeReaderQuery
                        tran
                        query
                        []
                        (mapRawForDbRead "unique_id" "code")
                        constructFromRawForDbRead
                        AnyQuantityIsAcceptable
                return rows |> List.map(fun x -> x.key, x.id) |> Map.ofList
            }),
        (fun context code ->
            let query = "select unique_id, code from ledger.account where code = @code"
            let parameters = [ { name = "@code"; value = CharString code } ]
            result {
                let! rows =
                    executeReaderQuery
                        (context |> Context.getDatabaseTransaction)
                        query
                        parameters
                        (mapRawForDbRead "unique_id" "code")
                        constructFromRawForDbRead
                        ExactlyOne
                return (rows |> List.head).id
            })
    )

let accountIdToCode =
    Cache<Guid, string>(
        (fun _ ->
            let tran = createDbTransaction() |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
            let query = "select unique_id, code from ledger.account"
            result {
                let! rows =
                    executeReaderQuery
                        tran
                        query
                        []
                        (mapRawForDbRead "unique_id" "code")
                        constructFromRawForDbRead
                        AnyQuantityIsAcceptable
                return rows |> List.map(fun x -> x.id, x.key) |> Map.ofList
            }),
        (fun context id ->
            let query = "select unique_id, code from ledger.account where unique_id = @unique_id"
            let parameters = [ { name = "@unique_id"; value = UniqueId id } ]
            result {
                let! rows =
                    executeReaderQuery
                        (context |> Context.getDatabaseTransaction)
                        query
                        parameters
                        (mapRawForDbRead "unique_id" "code")
                        constructFromRawForDbRead
                        ExactlyOne
                return (rows |> List.head).key
            })
    )

let fiscalPeriodKeyToId =
    Cache<string, Guid>(
        (fun _ ->
            let tran = createDbTransaction() |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
            let query = "select unique_id, period_key from ledger.fiscal_period"
            result { //     executeReaderQuery query parameters mapRawForDbRead reconstitute expectedRows transaction
                let! rows =
                    executeReaderQuery
                        tran
                        query
                        []
                        (mapRawForDbRead "unique_id" "period_key")
                        constructFromRawForDbRead
                        AnyQuantityIsAcceptable
                return rows |> List.map(fun x -> x.key, x.id) |> Map.ofList
            }),
        (fun context periodKey ->
            let query = "select unique_id, period_key from ledger.fiscal_period where period_key = @period_key"
            let parameters = [ { name = "@period_key"; value = CharString periodKey } ]
            result {
                let! rows =
                    executeReaderQuery
                        (context |> Context.getDatabaseTransaction)
                        query
                        parameters
                        (mapRawForDbRead "unique_id" "period_key")
                        constructFromRawForDbRead
                        ExactlyOne
                return (rows |> List.head).id
            })
    )

let fiscalPeriodIdToKey =
    Cache<Guid, string>(
        (fun _ ->
            let tran = createDbTransaction() |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
            let query = "select unique_id, period_key from ledger.fiscal_period"
            result {
                let! rows =
                    executeReaderQuery
                        tran
                        query
                        []
                        (mapRawForDbRead "unique_id" "period_key")
                        constructFromRawForDbRead
                        AnyQuantityIsAcceptable
                return rows |> List.map(fun x -> x.id, x.key) |> Map.ofList
            }),
        (fun context id ->
            let query = "select unique_id, period_key from ledger.fiscal_period where unique_id = @unique_id"
            let parameters = [ { name = "@unique_id"; value = UniqueId id } ]
            result {
                let! rows =
                    executeReaderQuery
                        (context |> Context.getDatabaseTransaction)
                        query
                        parameters
                        (mapRawForDbRead "unique_id" "period_key")
                        constructFromRawForDbRead
                        ExactlyOne
                return (rows |> List.head).key
            })
    )

let accountIdToName =
    Cache<Guid, string>(
        (fun _ ->
            let tran = createDbTransaction() |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
            let query = "select unique_id, account_name from ledger.account"
            result {
                let! rows =
                    executeReaderQuery
                        tran
                        query
                        []
                        (mapRawForDbRead "unique_id" "account_name")
                        constructFromRawForDbRead
                        AnyQuantityIsAcceptable
                return rows |> List.map(fun x -> x.id, x.key) |> Map.ofList
            }),
        (fun context id ->
            let query = "select unique_id, account_name from ledger.account where unique_id = @unique_id"
            let parameters = [ { name = "@unique_id"; value = UniqueId id } ]
            result {
                let! rows =
                    executeReaderQuery
                        (context |> Context.getDatabaseTransaction)
                        query
                        parameters
                        (mapRawForDbRead "unique_id" "account_name")
                        constructFromRawForDbRead
                        ExactlyOne
                return (rows |> List.head).key
            })
    )
