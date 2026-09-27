module Tests.Integrated.ConnectionPool

open System
open System.Collections.Generic
open System.Diagnostics.Metrics

(*
Reads the connection pool through the driver's own meter, so callers can tell whether connections came back without
knowing which driver is underneath. db.client.connection.count is tagged used or idle; db.client.connection.max is the
pool's ceiling. Both are observable instruments, so each read polls them once.
*)

let private recordMeasurement
    (measurements: List<int64 * Map<string, string>>)
    (value: int64)
    (tags: ReadOnlySpan<KeyValuePair<string, obj>>) =
    let mutable tagMap = Map.empty
    for tag in tags do
        tagMap <- tagMap |> Map.add tag.Key (string tag.Value)
    measurements.Add((value, tagMap))

let private driverMeasurements (instrumentName: string) : (int64 * Map<string, string>) list =
    let measurements = List<int64 * Map<string, string>>()
    use listener = new MeterListener()
    listener.InstrumentPublished <-
        Action<Instrument, MeterListener>(fun instrument l ->
            if instrument.Meter.Name = "Npgsql" && instrument.Name = instrumentName then
                l.EnableMeasurementEvents(instrument))
    listener.SetMeasurementEventCallback<int>(
        MeasurementCallback<int>(fun _ value tags _ -> recordMeasurement measurements (int64 value) tags))
    listener.SetMeasurementEventCallback<int64>(
        MeasurementCallback<int64>(fun _ value tags _ -> recordMeasurement measurements value tags))
    listener.Start()
    listener.RecordObservableInstruments()
    measurements |> List.ofSeq

/// connectionsInUse is how many pooled connections are rented out right now, across every pool this process holds.
let connectionsInUse () : int64 =
    driverMeasurements "db.client.connection.count"
    |> List.filter (fun (_, tags) -> tags |> Map.tryFind "db.client.connection.state" = Some "used")
    |> List.sumBy fst

/// maximumConnections is the largest pool ceiling this process holds, or None before any pool exists.
let maximumConnections () : int64 option =
    match driverMeasurements "db.client.connection.max" with
    | [] -> None
    | measurements -> measurements |> List.map fst |> List.max |> Some
