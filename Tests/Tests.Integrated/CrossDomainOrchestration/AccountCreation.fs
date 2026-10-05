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
open App.Utility.Result
open Tests.Helpers

[<Fact>]
let ``REQ-AC-2.14 REQ-SYS-3.2 constructNew sets timestamps from AuditEnvelope`` () =
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

[<Collection("SharedTestData")>]
type AccountCreationTests(fixture: TestDataFixture) =

    let createWith context (code: string) accountType subtype =
        AccountCreation.constructNewAndPersist
            context
            (code |> AccountCode.create |> Result.defaultWith(fun (e: IAppError) -> failwith(e.ToMessage())))
            genericAccountName
            accountType
            genericActivityPeriod
            subtype
            genericAccountParentId
            genericAccountReference

    [<Fact>]
    member _.``REQ-AC-2.13 two accounts created in one transaction get IDs distinct from each other and from every fixture account's ID``() =
        runCommandRouteAndAutoRollback AccountCreate (fun context ->
            result {
                let! first = createWith context "AC-2.13-1" genericAccountType genericAccountSubtype
                let! second = createWith context "AC-2.13-2" genericAccountType genericAccountSubtype
                let firstId = first |> Account.accountId
                let secondId = second |> Account.accountId
                Assert.NotEqual(firstId, secondId)
                let fixtureIds = fixture.Data.accounts |> List.map Account.accountId
                Assert.DoesNotContain(firstId, fixtureIds)
                Assert.DoesNotContain(secondId, fixtureIds)
                return ()
            })
        |> railroadWrapper

    [<Theory>]
    [<InlineData("Equity")>]
    [<InlineData("Liability")>]
    member _.``REQ-AC-1.28 REQ-AC-1.31 REQ-AC-1.32 creating an Equity or a Liability account with the Cash subtype is refused with AccountInvalidTypeSubtypeCombo naming the pair, and no account is stored``(accountTypeString: string) =
        runCommandRouteAndAutoRollback AccountCreate (fun context ->
            result {
                let! accountType = accountTypeString |> AccountType.fromString
                let code = $"AC-1.28-{accountTypeString.Substring(0, 1)}"
                let () =
                    match createWith context code accountType (Some AccountSubtype.Cash) with
                    | Error (AsError (AccountInvalidTypeSubtypeCombo (typeName, subtypeName))) ->
                        Assert.Equal(accountTypeString, typeName)
                        Assert.Equal(Some "Cash", subtypeName)
                    | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
                    | Ok _ -> Assert.Fail "Expected failure; got success"
                let! all = Account.fetchAll context false
                Assert.DoesNotContain(code, all |> List.map (Account.code >> AccountCode.value))
                return ()
            })
        |> railroadWrapper
