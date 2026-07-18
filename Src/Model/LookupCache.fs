module Model.LookupCache

open System
open Utilities.AppError
open Utilities.DAL
open Utilities.ResultCE

type Cache<'K, 'V when 'K : comparison> (
    loadAll : unit -> Result<Map<'K, 'V>, AppError>,
    loadOne: 'K -> Result<'V, AppError>
    ) =
    let mutable cache =
        loadAll() |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
    member _.fetch (key: 'K) : Result<'V, AppError> =
        match cache |> Map.tryFind key with
        | Some v -> Ok v
        | None ->
            match loadOne key with
            | Ok v ->
                cache <- cache |> Map.add key v
                Ok v
            | Error e -> Error e // REQ-NGUI-1.5

type idAndString = { id: Guid; key: string }

let mapRawForDbRead
        (fieldNameId: string)
        (fieldNameKey: string)
        (row: RowReader) =
    let id = row |> RowReader.getUuid fieldNameId
    let key = row |> RowReader.getString fieldNameKey
    id, key

let constructFromRawForDbRead (raw: Guid * string) : Result<idAndString, AppError> =
    let id, key = raw
    Ok { id = id; key = key }


let accountCodeToId = Cache<string, Guid>(
    (fun () ->
        let query = "select unique_id, code from ledger.account"
        result {
            let! rows = executeReaderQuery query [] (mapRawForDbRead "unique_id" "code") constructFromRawForDbRead AnyQuantityIsAcceptable None
            return rows |> List.map (fun x -> x.key, x.id) |> Map.ofList } ),
    (fun code -> 
        let query = "select unique_id, code from ledger.account where code = @code"
        let parameters = [{ name = "@code"; value = CharString code  };] // REQ-DAL-2.3
        result {
            let! rows = executeReaderQuery query parameters (mapRawForDbRead "unique_id" "code") constructFromRawForDbRead ExactlyOne None
            return (rows |> List.head).id } ) )

let accountIdToCode = Cache<Guid, string>(
    (fun () ->
        let query = "select unique_id, code from ledger.account"
        result {
            let! rows = executeReaderQuery query [] (mapRawForDbRead "unique_id" "code") constructFromRawForDbRead AnyQuantityIsAcceptable None
            return rows |> List.map (fun x -> x.id, x.key) |> Map.ofList } ),
    (fun id -> 
        let query = "select unique_id, code from ledger.account where unique_id = @unique_id"
        let parameters = [{ name = "@unique_id"; value = UniqueId id  };] 
        result {
            let! rows = executeReaderQuery query parameters (mapRawForDbRead "unique_id" "code") constructFromRawForDbRead ExactlyOne None
            return (rows |> List.head).key } ) )

let fiscalPeriodKeyToId = Cache<string, Guid>(
    (fun () ->
        let query = "select unique_id, period_key from ledger.fiscal_period"
        result { //     executeReaderQuery query parameters mapRawForDbRead reconstitute expectedRows transaction
            let! rows = executeReaderQuery query [] (mapRawForDbRead "unique_id" "period_key") constructFromRawForDbRead AnyQuantityIsAcceptable None
            return rows |> List.map (fun x -> x.key, x.id) |> Map.ofList } ),
    (fun periodKey -> 
        let query = "select unique_id, period_key from ledger.fiscal_period where period_key = @period_key"
        let parameters = [{ name = "@period_key"; value = CharString periodKey  };] // REQ-DAL-2.3
        result {
            let! rows = executeReaderQuery query parameters (mapRawForDbRead "unique_id" "period_key") constructFromRawForDbRead ExactlyOne None
            return (rows |> List.head).id } ) )

let fiscalPeriodIdToKey = Cache<Guid, string>(
    (fun () ->
        let query = "select unique_id, period_key from ledger.fiscal_period"
        result {
            let! rows = executeReaderQuery query [] (mapRawForDbRead "unique_id" "period_key") constructFromRawForDbRead AnyQuantityIsAcceptable None
            return rows |> List.map (fun x -> x.id, x.key) |> Map.ofList } ),
    (fun id -> 
        let query = "select unique_id, period_key from ledger.fiscal_period where unique_id = @unique_id"
        let parameters = [{ name = "@unique_id"; value = UniqueId id  };] 
        result {
            let! rows = executeReaderQuery query parameters (mapRawForDbRead "unique_id" "period_key") constructFromRawForDbRead ExactlyOne None
            return (rows |> List.head).key } ) )
