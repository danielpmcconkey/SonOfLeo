module Model.LookupCache

open System
open DataAccessLayer.ExecuteReader
open Utilities.AppError
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

Additional note: we have 3 separate fetch all functions on the ledger.account table. This is intentional as we do not
want to couple these together.
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

let private constructFromRawForDbRead (raw: Guid * string) : Result<idAndString, AppError> =
    let id, key = raw
    Ok { id = id; key = key }

let private mapRawForDbRead (fieldNameId: string) (fieldNameKey: string) (row: RowReader) =
    let id = row |> RowReader.getUuid fieldNameId
    let key = row |> RowReader.getString fieldNameKey
    id, key
    
let private fetchAll table keyColumn =
  let tran = createDbTransaction() |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
  executeReaderQuery tran $"select unique_id, {keyColumn} from {table}" []
      (mapRawForDbRead "unique_id" keyColumn) constructFromRawForDbRead AnyQuantityIsAcceptable

let private fetchOne table keyColumn whereColumn paramValue context =
  executeReaderQuery (context |> Context.getDatabaseTransaction)
      $"select unique_id, {keyColumn} from {table} where {whereColumn} = @key"
      [ { name = "@key"; value = paramValue } ]
      (mapRawForDbRead "unique_id" keyColumn) constructFromRawForDbRead ExactlyOne
  |> Result.map List.head

let private stringToIdCache table keyColumn =
  Cache<string, Guid>(
      (fun _ -> fetchAll table keyColumn |> Result.map (List.map (fun x -> x.key, x.id) >> Map.ofList)),
      (fun context key -> fetchOne table keyColumn keyColumn (CharString key) context |> Result.map (fun r -> r.id)))

let private idToStringCache table keyColumn =
  Cache<Guid, string>(
      (fun _ -> fetchAll table keyColumn |> Result.map (List.map (fun x -> x.id, x.key) >> Map.ofList)),
      (fun context id -> fetchOne table keyColumn "unique_id" (UniqueId id) context |> Result.map (fun r -> r.key)))

let accountCodeToId = stringToIdCache "ledger.account" "code"
let accountIdToCode = idToStringCache "ledger.account" "code"
let accountIdToName = idToStringCache "ledger.account" "account_name"
let fiscalPeriodKeyToId = stringToIdCache "ledger.fiscal_period" "period_key"
let fiscalPeriodIdToKey = idToStringCache "ledger.fiscal_period" "period_key"
