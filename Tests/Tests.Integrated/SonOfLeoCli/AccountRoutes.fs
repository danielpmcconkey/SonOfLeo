module Tests.Integrated.SonOfLeoCli.AccountRoutes

open System
open Model.UI.InterfaceContractTypes
open Model.UI.Json
open Tests.Integrated.GenericTestProperties
open Tests.Integrated.SonOfLeoCli.CliExecutor
open Xunit
open Model.Ledger.Account
open Utilities.ResultCE
open Tests.Integrated._Cleanup



[<Fact>]
let ``REQ-AC-2.21 Account Create happy path`` () =  
    let mutable idToCleanUp = None
    try
        let railroad = result {
            let! pushResult = 
                Account.constructNewAndSaveToDb genericAccountCodeString genericAccountNameString genericAccountTypeString
                    genericAccountActiveBegin genericAccountActiveEnd genericAccountSubtype genericAccountParentId
                    genericAccountReference genericAuditEnvelope
            let pushId = Account.id pushResult
            idToCleanUp <- Some pushId
            
            let args = ["Account"; "Create"]
            let payload = { id = pushId } |> toJson<AccountFetchByIdInput> |> Result.defaultWith failwith
            let (code, _, _) = runCli args payload
            Assert.Equal(0, code)
            
            return ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e
    finally
        match cleanUpAccountId idToCleanUp with
        | Ok () -> ()
        | Error e -> failwith e
