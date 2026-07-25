module DataAccessLayer.QueryParameters

open System
open NodaTime
open Npgsql // REQ-DAL-3.1
open NpgsqlTypes // REQ-DAL-3.1

type QueryParameterValue = // REQ-DAL-3.2
    | Integer of int
    | Numeric of decimal
    | CharString of string
    | DbInstant of Instant
    | DbLocalDate of LocalDate
    | UniqueId of Guid
    | Boolean of bool
    | NullableInteger of int option
    | NullableNumeric of decimal option
    | NullableCharString of string option
    | NullableDbInstant of Instant option
    | NullableDbLocalDate of LocalDate option
    | NullableUniqueId of Guid option
    | NullableBoolean of bool option

type QueryParameter =
    { // REQ-DAL-3.2
      name: string
      value: QueryParameterValue }


let private convertParamToDbParam (parameter: QueryParameter) : NpgsqlParameter = // REQ-DAL-3.2
    let dbType, value =
        match parameter.value with
        | Integer x -> NpgsqlDbType.Integer, box x
        | Numeric x -> NpgsqlDbType.Numeric, box x
        | CharString x -> NpgsqlDbType.Varchar, box x
        | DbInstant x -> NpgsqlDbType.TimestampTz, box x
        | DbLocalDate x -> NpgsqlDbType.Date, box x
        | UniqueId x -> NpgsqlDbType.Uuid, box x
        | Boolean x -> NpgsqlDbType.Boolean, box x
        | NullableInteger x ->
            NpgsqlDbType.Integer,
            match x with
            | Some b -> box b
            | None -> box DBNull.Value
        | NullableNumeric x ->
            NpgsqlDbType.Numeric,
            match x with
            | Some b -> box b
            | None -> box DBNull.Value
        | NullableCharString x ->
            NpgsqlDbType.Varchar,
            match x with
            | Some b -> box b
            | None -> box DBNull.Value
        | NullableDbInstant x ->
            NpgsqlDbType.TimestampTz,
            match x with
            | Some b -> box b
            | None -> box DBNull.Value
        | NullableDbLocalDate x ->
            NpgsqlDbType.Date,
            match x with
            | Some b -> box b
            | None -> box DBNull.Value
        | NullableUniqueId x ->
            NpgsqlDbType.Uuid,
            match x with
            | Some b -> box b
            | None -> box DBNull.Value
        | NullableBoolean x ->
            NpgsqlDbType.Boolean,
            match x with
            | Some b -> box b
            | None -> box DBNull.Value
    let p = NpgsqlParameter(parameter.name, dbType)
    p.Value <- value // necessary because NpgsqlParameter doesn't take a value in its constructor
    p

let internal buildParamsList (parameters: QueryParameter list) : NpgsqlParameter list = // REQ-DAL-3.2
    parameters |> List.map convertParamToDbParam
