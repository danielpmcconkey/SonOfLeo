module Tests.Integrated.DataAccessLayer.DalTests

open System
open DataAccessLayer.DbTransaction
open DataAccessLayer.ExecuteNonQuery
open DataAccessLayer.ExecuteReader
open DataAccessLayer.ExecuteScalar
open Logger.Audit
open Tests.Helpers.Railroad
open Utilities.ResultHelper
open Xunit
open Utilities.AppError
open Tests.Helpers.SadPath


let unBoxingNull
    (unboxingFunc: obj -> Result<'T, AppError>)
    : Result<unit, AppError> =
    let context = Context.create NoTransaction FetchOnly
    match executeScalar (context |> Context.getDatabaseTransaction) "select 'burp' where 1 = 0" [] unboxingFunc with
    | Ok _ -> Ok ()
    | Error e -> Error e
    
let unBoxingNonNullReturnsString
    (unboxingFunc: obj -> Result<'T, AppError>)
    : Result<unit, AppError> =
    let context = Context.create NoTransaction FetchOnly
    match executeScalar (context |> Context.getDatabaseTransaction) "SELECT 'hello'" [] unboxingFunc with
    | Ok _ -> Ok ()
    | Error e -> Error e
    
let unBoxingNonNullReturnsInt
    (unboxingFunc: obj -> Result<'T, AppError>)
    : Result<unit, AppError> =
    let context = Context.create NoTransaction FetchOnly
    match executeScalar (context |> Context.getDatabaseTransaction) "SELECT 1" [] unboxingFunc with
    | Ok _ -> Ok ()
    | Error e -> Error e

let errorNonQuery ()
    : Result<unit, AppError> =
    let context = Context.create NoTransaction FetchOnly
    match executeNonQuery (context |> Context.getDatabaseTransaction) "SEL ECT from ledger.account;" [] Zero with
    | Ok _ -> Ok ()
    | Error e -> Error e

let errorReaderQuery ()
    : Result<unit, AppError> =
    let context = Context.create NoTransaction FetchOnly
    let mapRaw _ = ("", "")
    let contructFromRaw _ = Ok ""
    match executeReaderQuery (context |> Context.getDatabaseTransaction) "SEL ECT from ledger.account;" [] mapRaw contructFromRaw Zero with
    | Ok _ -> Ok ()
    | Error e -> Error e

let errorScalar ()
    : Result<unit, AppError> =
    let context = Context.create NoTransaction FetchOnly
    match executeScalar (context |> Context.getDatabaseTransaction) "SEL ECT from ledger.account;" [] stringUnboxing with
    | Ok _ -> Ok ()
    | Error e -> Error e

let errorRowCount ()
    : Result<unit, AppError> =
    let context = Context.create NoTransaction FetchOnly
    let mapRaw _ = ("", "")
    let contructFromRaw _ = Ok ""
    match executeReaderQuery (context |> Context.getDatabaseTransaction) "select code, account_name from ledger.account where 1 = 2;" [] mapRaw contructFromRaw ExactlyOne with
    | Ok _ -> Ok ()
    | Error e -> Error e
    
(* Note: several of the DAL errors are impossible to provoke from a buildable, functioning code base (e.g.
DalEnvVarNotSet). Therefore, we elect not to try testing them here*)
[<Theory>]
[<InlineData("DalCantCompleteTransactionOfNone")>]
[<InlineData("DalCantUseTransactionOfNoneInAutoCommit")>]
[<InlineData("DalDecimalUnboxingReturnedNull")>]
[<InlineData("DalErrorDuringAutoCompleteTransactionRun")>]
[<InlineData("DalErrorDuringDecimalOptionUnboxing")>]
[<InlineData("DalErrorDuringDecimalUnboxing")>]
[<InlineData("DalErrorDuringInstantOptionUnboxing")>]
[<InlineData("DalErrorDuringInstantUnboxing")>]
[<InlineData("DalErrorDuringIntOptionUnboxing")>]
[<InlineData("DalErrorDuringIntUnboxing")>]
[<InlineData("DalErrorDuringLocalDateOptionUnboxing")>]
[<InlineData("DalErrorDuringLocalDateUnboxing")>]
[<InlineData("DalErrorDuringLongOptionUnboxing")>]
[<InlineData("DalErrorDuringLongUnboxing")>]
[<InlineData("DalErrorDuringNonQueryExecution")>]
[<InlineData("DalErrorDuringReaderQueryExecution")>]
[<InlineData("DalErrorDuringScalarExecution")>]
[<InlineData("DalErrorDuringStringOptionUnboxing")>]
[<InlineData("DalErrorDuringStringUnboxing")>]
[<InlineData("DalErrorDuringUuidOptionUnboxing")>]
[<InlineData("DalErrorDuringUuidUnboxing")>]
[<InlineData("DalInstantUnboxingReturnedNull")>]
[<InlineData("DalIntUnboxingReturnedNull")>]
[<InlineData("DalLocalDateUnboxingReturnedNull")>]
[<InlineData("DalLongUnboxingReturnedNull")>]
[<InlineData("DalResultantRowsDidntMatchExpectation")>]
[<InlineData("DalStringUnboxingReturnedNull")>]
[<InlineData("DalUuidUnboxingReturnedNull")>]
let ``DAL errors surface when they should`` expectedError = 
    result {
        let resultOfAction =
            match expectedError with
            | "DalCantCompleteTransactionOfNone" -> createNoTransaction() |> commit
            | "DalCantUseTransactionOfNoneInAutoCommit" ->
                let func _ = Ok()
                let context = Context.create NoTransaction FetchOnly
                runWithAutoCompleteTransaction (context |> Context.getDatabaseTransaction) (fun () -> func context)
            | "DalDecimalUnboxingReturnedNull" -> unBoxingNull decimalUnboxing
            | "DalErrorDuringAutoCompleteTransactionRun" ->
                let func _ = (raise (ApplicationException("everybody stay calm. I'm a trained professional.")))
                let context = Context.create NewTransaction FetchOnly
                runWithAutoCompleteTransaction (context |> Context.getDatabaseTransaction) (fun () -> func context)
            | "DalErrorDuringDecimalOptionUnboxing" -> unBoxingNonNullReturnsString decimalOptionUnboxing
            | "DalErrorDuringDecimalUnboxing" -> unBoxingNonNullReturnsString decimalUnboxing
            | "DalErrorDuringInstantOptionUnboxing" -> unBoxingNonNullReturnsString instantOptionUnboxing
            | "DalErrorDuringInstantUnboxing" -> unBoxingNonNullReturnsString instantUnboxing
            | "DalErrorDuringIntOptionUnboxing" -> unBoxingNonNullReturnsString intOptionUnboxing
            | "DalErrorDuringIntUnboxing" -> unBoxingNonNullReturnsString intUnboxing
            | "DalErrorDuringLocalDateOptionUnboxing" -> unBoxingNonNullReturnsString localDateOptionUnboxing
            | "DalErrorDuringLocalDateUnboxing" -> unBoxingNonNullReturnsString localDateUnboxing
            | "DalErrorDuringLongOptionUnboxing" -> unBoxingNonNullReturnsString longOptionUnboxing
            | "DalErrorDuringLongUnboxing" -> unBoxingNonNullReturnsString longUnboxing
            | "DalErrorDuringNonQueryExecution" -> errorNonQuery()
            | "DalErrorDuringReaderQueryExecution" -> errorReaderQuery()
            | "DalErrorDuringScalarExecution" -> errorScalar()
            | "DalErrorDuringStringOptionUnboxing" -> unBoxingNonNullReturnsInt stringOptionUnboxing
            | "DalErrorDuringStringUnboxing" -> unBoxingNonNullReturnsInt stringUnboxing
            | "DalErrorDuringUuidOptionUnboxing" -> unBoxingNonNullReturnsString uuidOptionUnboxing
            | "DalErrorDuringUuidUnboxing" -> unBoxingNonNullReturnsString uuidUnboxing
            | "DalInstantUnboxingReturnedNull" -> unBoxingNull instantUnboxing
            | "DalIntUnboxingReturnedNull" -> unBoxingNull intUnboxing
            | "DalLocalDateUnboxingReturnedNull" -> unBoxingNull localDateUnboxing
            | "DalLongUnboxingReturnedNull" -> unBoxingNull longUnboxing
            | "DalResultantRowsDidntMatchExpectation" -> errorRowCount()
            | "DalStringUnboxingReturnedNull" -> unBoxingNull stringUnboxing
            | "DalUuidUnboxingReturnedNull" -> unBoxingNull uuidUnboxing
            | _ -> Error(TestingError "Some dipshit done goofed.")
        do!
            isCorrectErrorString
                resultOfAction
                expectedError
                (Some "This implies your entire project is AFU.")
        return ()
    }
    |> railroadWrapper
