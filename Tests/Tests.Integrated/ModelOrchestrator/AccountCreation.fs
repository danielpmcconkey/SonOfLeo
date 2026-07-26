module Tests.Integrated.ModelOrchestrator.AccountCreation

open System
open Model.Audit
open ModelOrchestrator
open Tests.Integrated.Rollback
open Xunit
open Model.Ledger.Accounts
open Model.Ledger.Accounts.AccountComponent
open Utilities.AppError
open Tests.Integrated.GenericTestProperties

[<Fact>]
let ``REQ-AC-2.13 REQ-SYS-3.2 constructNew generates UUID`` () =
    withRollback(fun tran ->
        let code = "abc1" |> AccountCode.create |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
        AccountCreation.constructNewAndSaveToDb
            code
            genericAccountName
            genericAccountType
            genericAccountActivityPeriod
            genericAccountSubtype
            genericAccountParentId
            genericAccountReference
            genericAuditEnvelope
            tran
        |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
        |> Account.accountId
        |> AccountId.value
        |> fun id -> Assert.NotEqual(Guid.Empty, id))

[<Fact>]
let ``REQ-AC-2.13 REQ-SYS-3.2 constructNew sets timestamps from AuditEnvelope`` () =
    withRollback(fun tran ->
        let code = "abc2" |> AccountCode.create |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
        let expected = AuditEnvelope.instant genericAuditEnvelope
        let account =
            AccountCreation.constructNewAndSaveToDb
                code
                genericAccountName
                genericAccountType
                genericAccountActivityPeriod
                genericAccountSubtype
                genericAccountParentId
                genericAccountReference
                genericAuditEnvelope
                tran
            |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
        Assert.Equal(expected, Account.createdAt account)
        Assert.Equal(expected, Account.modifiedAt account))
