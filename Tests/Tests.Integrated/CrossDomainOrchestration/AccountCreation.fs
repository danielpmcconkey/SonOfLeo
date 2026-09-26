module Tests.Integrated.CrossDomainOrchestration.AccountCreation

open App.Session
open Business.General
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.CrossDomainOrchestration
open System
open Ui.InterfaceBridge.CommandRoute
open App.Operation.CoreAuditableAction
open Business.FinancialServices.Ledger.LedgerAuditableAction
open Business.FinancialServices.DataIngestion.DataIngestionAuditableAction
open Business.FinancialServices.Classification.ClassificationAuditableAction
open Business.FinancialServices.CashFlow.CashFlowAuditableAction
open Tests.Helpers.Railroad
open Xunit
open Business.FinancialServices.Ledger.Account
open Business.FinancialServices.Ledger.AccountComponent
open App.Utility.IAppError
open Tests.Helpers.TestError
open Tests.Helpers.SadPath
open Tests.Helpers.GenericTestProperties
open App.DataAccessLayer.DalError
open Business.FinancialServices.Ledger.LedgerError

[<Fact>]
let ``REQ-AC-2.13 constructNew generates UUID`` () =
    runCommandRouteAndAutoRollback AccountCreate (fun context ->
        let code = "abc1" |> AccountCode.create |> Result.defaultWith(fun (e: IAppError) -> failwith(e.ToMessage()))
        AccountCreation.constructNewAndPersist
            context
            code
            genericAccountName
            genericAccountType
            genericActivityPeriod
            genericAccountSubtype
            genericAccountParentId
            genericAccountReference
        |> Result.defaultWith(fun (e: IAppError) -> failwith(e.ToMessage()))
        |> Account.accountId
        |> AccountId.value
        |> fun id -> Assert.NotEqual(Guid.Empty, id)
        Ok())
    |> railroadWrapper

[<Fact>]
let ``REQ-AC-2.13 REQ-SYS-3.2 constructNew sets timestamps from AuditEnvelope`` () =
    runCommandRouteAndAutoRollback AccountCreate (fun context ->
        let code = "abc2" |> AccountCode.create |> Result.defaultWith(fun (e: IAppError) -> failwith(e.ToMessage()))
        let expected = context |> Context.getInitiationInstant
        let account =
            AccountCreation.constructNewAndPersist
                context
                code
                genericAccountName
                genericAccountType
                genericActivityPeriod
                genericAccountSubtype
                genericAccountParentId
                genericAccountReference
            |> Result.defaultWith(fun (e: IAppError) -> failwith(e.ToMessage()))
        Assert.Equal(expected, Account.createdAt account)
        Assert.Equal(expected, Account.modifiedAt account)
        Ok())
    |> railroadWrapper

[<Fact>]
let ``REQ-AC-1.40 constructNew rejects non-existent parent ID`` () =
    runCommandRouteAndAutoRollback AccountCreate (fun context ->
        let code = "ac140" |> AccountCode.create |> Result.defaultWith(fun (e: IAppError) -> failwith(e.ToMessage()))
        let bogusParentId = Some(Guid.NewGuid() |> AccountId.fromGuid)
        let result =
            AccountCreation.constructNewAndPersist
                context
                code
                genericAccountName
                genericAccountType
                genericActivityPeriod
                genericAccountSubtype
                bogusParentId
                genericAccountReference
        isCorrectError result AccountIdDoesntMatch None)
    |> railroadWrapper
