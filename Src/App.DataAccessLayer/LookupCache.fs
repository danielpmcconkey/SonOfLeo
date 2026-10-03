module App.DataAccessLayer.LookupCache

open System
open App.Utility.IAppError
open App.Utility.Result
open App.DataAccessLayer.DbTransaction
open App.DataAccessLayer.QueryParameter
open App.DataAccessLayer.ExecuteReader

(*
Note: the LookupCache is designed to support an easy translation between UUIDs used in the model and string codes and
keys used by the callers of our public user interfaces. It is designed currently to support short-burst CLI invocations
where the cache lifetime only needs to be the life of any single route. Therefore, there is no invalidation by design.

Each cache loads in full the first time it is asked for a key. A load that fails is returned to that caller as its
error, and the next fetch tries the load again.

Any future usages for this application that will carry longer life cycles will need to re-design this cache if it plans
to also involve any CRUD operations of core module entities.

This module holds only the machinery. Each concrete cache lives in the Business module that owns its table, so this tier
knows no upper tier's tables or columns.
*)

type Cache<'K, 'V when 'K: comparison>
    (loadAll: unit -> Result<Map<'K, 'V>, IAppError>, loadOne: DbTransaction -> 'K -> Result<'V, IAppError>) =
    let mutable cache : Map<'K, 'V> option = None
    member _.fetch context (key: 'K) : Result<'V, IAppError> =
        result {
            let! loaded =
                match cache with
                | Some loaded -> Ok loaded
                | None -> loadAll()
            cache <- Some loaded
            match loaded |> Map.tryFind key with
            | Some v -> return v
            | None ->
                let! v = key |> loadOne context
                cache <- Some(loaded |> Map.add key v)
                return v
        }

type idAndString = { id: Guid; key: string }

let private reconstitute (raw: Guid * string) : Result<idAndString, IAppError> =
    let id, key = raw
    Ok { id = id; key = key }

let private mapRawForDbRead (fieldNameId: string) (fieldNameKey: string) (row: RowReader) =
    let id = row |> RowReader.getUuid fieldNameId
    let key = row |> RowReader.getString fieldNameKey
    id, key
    
let private fetchAll table keyColumn =
  result {
      let! tran = createDbTransaction()
      let rows =
          try
              executeReaderQuery tran $"select unique_id, {keyColumn} from {table}" []
                  (mapRawForDbRead "unique_id" keyColumn) reconstitute AnyQuantityIsAcceptable
          finally
              // the read changes nothing, so rolling back only ends the transaction and releases its connection
              tran |> rollback |> ignore
      return! rows
  }

let private fetchOne table keyColumn whereColumn paramValue dbTransaction =
  executeReaderQuery dbTransaction
      $"select unique_id, {keyColumn} from {table} where {whereColumn} = @key"
      [ { name = "@key"; value = paramValue } ]
      (mapRawForDbRead "unique_id" keyColumn) reconstitute ExactlyOne
  |> Result.map List.head

let stringToIdCache table keyColumn =
  Cache<string, Guid>(
      (fun _ -> fetchAll table keyColumn |> Result.map (List.map (fun x -> x.key, x.id) >> Map.ofList)),
      (fun context key -> fetchOne table keyColumn keyColumn (CharString key) context |> Result.map (fun r -> r.id)))

let idToStringCache table keyColumn =
  Cache<Guid, string>(
      (fun _ -> fetchAll table keyColumn |> Result.map (List.map (fun x -> x.id, x.key) >> Map.ofList)),
      (fun context id -> fetchOne table keyColumn "unique_id" (UniqueId id) context |> Result.map (fun r -> r.key)))
