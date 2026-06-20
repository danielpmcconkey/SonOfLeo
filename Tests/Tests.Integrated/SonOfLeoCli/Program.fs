module Tests.Integrated.SonOfLeoCli.Program

open Model.Ledger.AccountComponent
open Model.UI.InterfaceContractTypes
open Model.UI.Json
open Tests.Integrated.GenericTestProperties
open Tests.Integrated.SonOfLeoCli.CliExecutor
open Xunit
open Model.Ledger.Account
open Utilities.ResultCE
open Tests.Integrated._Cleanup


[<Fact>]
let ``REQ-NGUI-1.3 System responds with a failure code when failing`` () = 
    let args = ["Account"; "Create"]
    let badPayload = "{}"
    let exitCode, _, _ = runCli args badPayload
    (exitCode = 1) |> Assert.True

[<Fact>]
let ``REQ-NGUI-1.3, REQ-NGUI-3.6 System responds with a success code when succeeding`` () = 
    let args = ["Account"; "FetchAll"]
    let payload = { activeOnly = true } |> toJson<AccountFetchAllInput> |> Result.defaultWith failwith
    let exitCode, _, _ = runCli args payload
    (exitCode = 0) |> Assert.True
    
[<Fact>]
let ``REQ-NGUI-1.3.1, REQ-NGUI-3.7 The stderr will comprise the error message`` () = 
    let expectedError = "Resultant rows didn't match expectation"
    let args = ["Account"; "FetchByCode"]
    let payload = { code = "burp" } |> toJson<AccountFetchByCodeInput> |> Result.defaultWith failwith
    let _, _, e = runCli args payload
    Assert.Equal(expectedError, e.Trim())

[<Fact>]
let ``REQ-NGUI-3.6 System responds with the payload via stdout upon success`` () =  
    let explicitName = "System responds with the payload via stdout upon success"
    let mutable idToCleanUp = None
    try
        let railroad = result {
            let! pushResult = 
                Account.constructNewAndSaveToDbUsingParentId genericAccountCodeString explicitName genericAccountTypeString
                    genericAccountActiveBegin genericAccountActiveEnd genericAccountSubtype genericAccountParentId
                    genericAccountReference genericAuditEnvelope
            let pushCode = pushResult |> Account.code |> AccountCode.value
            let pushId = Account.id pushResult
            idToCleanUp <- Some pushId
            
            let args = ["Account"; "FetchByCode"]
            let payload = { code = pushCode } |> toJson<AccountFetchByCodeInput> |> Result.defaultWith failwith
            let _, p, _ = runCli args payload
            let! fetched = fromJson<AccountReturn> p
            let fetchedName = fetched.name
            Assert.Equal(explicitName, fetchedName)
            
            return ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e
    finally
        match cleanUpAccountId idToCleanUp with
        | Ok () -> ()
        | Error e -> failwith e

[<Fact>]
let ``REQ-NGUI-3.8 The domain argument is case sensitive`` () = 
    let args = ["account"; "FetchAll"]
    let payload = { activeOnly = true } |> toJson<AccountFetchAllInput> |> Result.defaultWith failwith
    let exitCode, _, _ = runCli args payload
    (exitCode = 1) |> Assert.True

[<Fact>]
let ``REQ-NGUI-3.8 The verb argument is case sensitive`` () = 
    let args = ["Account"; "fetchAll"]
    let payload = { activeOnly = true } |> toJson<AccountFetchAllInput> |> Result.defaultWith failwith
    let exitCode, _, _ = runCli args payload
    (exitCode = 1) |> Assert.True

[<Fact>]
let ``REQ-NGUI-3.9 Incorrect routes must exit with an appropriate error`` () = 
    let expected = "Unknown command: Ropa Interior"
    let args = ["Ropa"; "Interior"]
    let payload = { activeOnly = true } |> toJson<AccountFetchAllInput> |> Result.defaultWith failwith
    let exitCode, _, e = runCli args payload
    (exitCode = 1) |> Assert.True
    Assert.Equal(expected, e.Trim())
 
    