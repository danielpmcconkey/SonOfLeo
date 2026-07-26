module DataAccessLayer.ExecuteScalar

open System
open DataAccessLayer.DbTransaction
open DataAccessLayer.QueryParameters
open DataAccessLayer.DbConnections
open NodaTime
open Npgsql
open Utilities.AppError
open Utilities.ResultHelper

let stringUnboxing (objRaw: obj) : Result<string, AppError> =
    try
        if objRaw = null || objRaw = DBNull.Value then
            Error DalStringUnboxingReturnedNull
        else
            Ok(objRaw :?> string)
    with ex ->
        Error(DalErrorDuringStringUnboxing ex)

let stringOptionUnboxing (objRaw: obj) : Result<string option, AppError> =
    try
        if objRaw = null || objRaw = DBNull.Value then
            Ok None
        else
            Ok(Some(objRaw :?> string))
    with ex ->
        Error(DalErrorDuringStringUnboxing ex)

let intUnboxing (objRaw: obj) : Result<int, AppError> =
    try
        if objRaw = null || objRaw = DBNull.Value then
            Error DalIntUnboxingReturnedNull
        else
            let unboxed: int = objRaw |> unbox
            Ok unboxed
    with ex ->
        Error(DalErrorDuringIntUnboxing ex)

let intOptionUnboxing (objRaw: obj) : Result<int option, AppError> =
    try
        if objRaw = null || objRaw = DBNull.Value then
            Ok None
        else

            let unboxed: int = objRaw |> unbox
            Ok(Some unboxed)
    with ex ->
        Error(DalErrorDuringIntOptionUnboxing ex)

let longUnboxing (objRaw: obj) : Result<int64, AppError> =
    try
        if objRaw = null || objRaw = DBNull.Value then
            Error(DalLongUnboxingReturnedNull)
        else
            let unboxed: int64 = objRaw |> unbox
            Ok unboxed
    with ex ->
        Error(DalErrorDuringLongUnboxing ex)

let longOptionUnboxing (objRaw: obj) : Result<int64 option, AppError> =
    try
        if objRaw = null || objRaw = DBNull.Value then
            Ok None
        else
            let unboxed: int64 = objRaw |> unbox
            Ok(Some unboxed)
    with ex ->
        Error(DalErrorDuringLongOptionUnboxing ex)

let decimalUnboxing (objRaw: obj) : Result<decimal, AppError> =
    try
        if objRaw = null || objRaw = DBNull.Value then
            Error DalDecimalUnboxingReturnedNull
        else
            let unboxed: decimal = objRaw |> unbox
            Ok unboxed
    with ex ->
        Error(DalErrorDuringDecimalUnboxing ex)

let decimalOptionUnboxing (objRaw: obj) : Result<decimal option, AppError> =
    try
        if objRaw = null || objRaw = DBNull.Value then
            Ok None
        else
            let unboxed: decimal = objRaw |> unbox
            Ok(Some unboxed)
    with ex ->
        Error(DalErrorDuringDecimalOptionUnboxing ex)

let localDateUnboxing (objRaw: obj) : Result<LocalDate, AppError> =
    try
        if objRaw = null || objRaw = DBNull.Value then
            Error DalLocalDateUnboxingReturnedNull
        else
            let unboxed: LocalDate = objRaw |> unbox
            Ok unboxed
    with ex ->
        Error(DalErrorDuringLocalDateUnboxing ex)

let localDateOptionUnboxing (objRaw: obj) : Result<LocalDate option, AppError> =
    try
        if objRaw = null || objRaw = DBNull.Value then
            Ok None
        else
            let unboxed: LocalDate = objRaw |> unbox
            Ok(Some unboxed)
    with ex ->
        Error(DalErrorDuringLocalDateOptionUnboxing ex)

let instantUnboxing (objRaw: obj) : Result<Instant, AppError> =
    try
        if objRaw = null || objRaw = DBNull.Value then
            Error DalInstantUnboxingReturnedNull
        else
            let unboxed: Instant = objRaw |> unbox
            Ok unboxed
    with ex ->
        Error(DalErrorDuringInstantUnboxing ex)

let instantOptionUnboxing (objRaw: obj) : Result<Instant option, AppError> =
    try
        if objRaw = null || objRaw = DBNull.Value then
            Ok None
        else
            let unboxed: Instant = objRaw |> unbox
            Ok(Some unboxed)
    with ex ->
        Error(DalErrorDuringInstantOptionUnboxing ex)

let uuidUnboxing (objRaw: obj) : Result<Guid, AppError> =
    try
        if objRaw = null || objRaw = DBNull.Value then
            Error DalUuidUnboxingReturnedNull
        else
            let unboxed: Guid = objRaw |> unbox
            Ok unboxed
    with ex ->
        Error(DalErrorDuringUuidUnboxing ex)

let uuidOptionUnboxing (objRaw: obj) : Result<Guid option, AppError> =
    try
        if objRaw = null || objRaw = DBNull.Value then
            Ok None
        else
            let unboxed: Guid = objRaw |> unbox
            Ok(Some unboxed)
    with ex ->
        Error(DalErrorDuringUuidOptionUnboxing ex)

let executeScalar
    (dbTransaction: DbTransaction)
    (query: string)
    (parameters: QueryParameter list)
    (unboxingFunc: obj -> Result<'T, AppError>)
    : Result<'T, AppError> =
    result {
        let! ds = dataSource.Value
        let parameters = buildParamsList parameters
        let! rows =
            (*
             * standard dotnet I/O libraries throw standard dotnet exceptions
             * we use a try/with block to convert their results into more
             * paradigmatic F# Result Ok/Error at the impure boundary
             *)
            let objResult =
                try

                    match dbTransaction |> isNone with
                    | true ->
                        use connection = ds.OpenConnection()
                        use command = new NpgsqlCommand(query, connection)
                        parameters |> List.iter(fun p -> command.Parameters.Add(p) |> ignore)
                        command.ExecuteScalar()
                    | false ->
                        let tran, conn =
                            dbTransaction
                            |> getTranAndConn
                            |> Result.defaultWith(fun e -> failwith(AppError.toMessage e)) // we do this because we're already inside the boundary of DB try / catch. Result railroad doesn't really work here.
                        use command = new NpgsqlCommand(query, conn)
                        command.Transaction <- tran
                        parameters |> List.iter(fun p -> command.Parameters.Add(p) |> ignore)
                        command.ExecuteScalar()
                with ex ->
                    Error(DalErrorDuringScalarExecution ex)
            objResult |> unboxingFunc
        return rows
    }
