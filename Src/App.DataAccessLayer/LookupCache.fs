module App.DataAccessLayer.LookupCache

open System
open App.DataAccessLayer.DalError
open App.DataAccessLayer.ExecuteReader
open App.DataAccessLayer.DbTransaction
open App.DataAccessLayer.QueryParameter


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
    (loadAll: unit -> Result<Map<'K, 'V>, DalError>, loadOne: DbTransaction -> 'K -> Result<'V, DalError>) =
    let mutable cache = loadAll() |> Result.defaultWith(fun e -> failwith(DalError.toMessage e))
    member _.fetch context (key: 'K) : Result<'V, DalError> =
        match cache |> Map.tryFind key with
        | Some v -> Ok v
        | None ->
            match key |> loadOne context with
            | Ok v ->
                cache <- cache |> Map.add key v
                Ok v
            | Error e -> Error e

type idAndString = { id: Guid; key: string }

let private reconstitute (raw: Guid * string) : Result<idAndString, DalError> =
    let id, key = raw
    Ok { id = id; key = key }

let private mapRawForDbRead (fieldNameId: string) (fieldNameKey: string) (row: RowReader) =
    let id = row |> RowReader.getUuid fieldNameId
    let key = row |> RowReader.getString fieldNameKey
    id, key
    
let private fetchAll table keyColumn =
  let tran = createDbTransaction() |> Result.defaultWith(fun e -> failwith(DalError.toMessage e))
  executeReaderQuery tran $"select unique_id, {keyColumn} from {table}" []
      (mapRawForDbRead "unique_id" keyColumn) reconstitute AnyQuantityIsAcceptable

let private fetchOne table keyColumn whereColumn paramValue dbTransaction =
  executeReaderQuery dbTransaction
      $"select unique_id, {keyColumn} from {table} where {whereColumn} = @key"
      [ { name = "@key"; value = paramValue } ]
      (mapRawForDbRead "unique_id" keyColumn) reconstitute ExactlyOne
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
let masterAgreementNameToId = stringToIdCache "cashflow.master_agreement" "agreement_name"
let masterAgreementIdToName = idToStringCache "cashflow.master_agreement" "agreement_name"
let paymentAgreementNameToId = stringToIdCache "cashflow.payment_agreement" "payment_agreement_name"
let paymentAgreementIdToName = idToStringCache "cashflow.payment_agreement" "payment_agreement_name"
