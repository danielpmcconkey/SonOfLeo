module Tests.Integrated.ModelOrchestrator.AccountCreation

open System
open Context.Context
open Logger.Audit
open ModelOrchestrator
open Tests.Integrated.InterfaceBridge._routeResolver
open Tests.Integrated.Railroad
open Xunit
open Model.Ledger.Accounts
open Model.Ledger.Accounts.AccountComponent
open Utilities.AppError
open Tests.Integrated.GenericTestProperties

[<Fact>]
let ``REQ-AC-2.13 REQ-SYS-3.2 constructNew generates UUID`` () =
    runFuncAndAutoRollback AccountCreate (fun context ->
        let code = "abc1" |> AccountCode.create |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
        AccountCreation.constructNewAndSaveToDb
            context
            code
            genericAccountName
            genericAccountType
            genericAccountActivityPeriod
            genericAccountSubtype
            genericAccountParentId
            genericAccountReference
        |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
        |> Account.accountId
        |> AccountId.value
        |> fun id -> Assert.NotEqual(Guid.Empty, id)
        Ok ()) |> railroadWrapper

[<Fact>]
let ``REQ-AC-2.13 REQ-SYS-3.2 constructNew sets timestamps from AuditEnvelope`` () =
    runFuncAndAutoRollback AccountCreate (fun context ->
        let code = "abc2" |> AccountCode.create |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
        let expected = context |> getInitiationInstant
        let account =
            AccountCreation.constructNewAndSaveToDb
                context
                code
                genericAccountName
                genericAccountType
                genericAccountActivityPeriod
                genericAccountSubtype
                genericAccountParentId
                genericAccountReference
            |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
        Assert.Equal(expected, Account.createdAt account)
        Assert.Equal(expected, Account.modifiedAt account)
        Ok ()) |> railroadWrapper
