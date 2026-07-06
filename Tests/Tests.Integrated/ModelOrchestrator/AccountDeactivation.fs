namespace Tests.Integrated.ModelOrchestrator

open Model.Audit
open Tests.Integrated
open Tests.Integrated.GenericTestProperties
open Xunit
open Model.Ledger.Accounts
open Model.Ledger.Accounts.AccountComponent
open ModelOrchestrator.AccountDeactivation
open Utilities.ResultCE
open Utilities

[<Collection("SharedTestData")>]
type AccountDeactivationTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-AC-4.1 deactivateAccount sets active end and returns inactive account`` () =
        let envelope = AuditEnvelope.create AccountDeactivation
        let explicitDeactivationDate = Some (Calendar.today().PlusDays(-1))

        let transaction = DAL.createDbTransaction() |> Result.defaultWith failwith

        try
            let railroad = result {
                let! original = Account.fetchById (Some transaction) fixture.Data.moneyMarket1270Id
                Assert.True(Account.isActive (Calendar.today()) original)

                let! deactivated =
                    fixture.Data.moneyMarket1270Id
                    |> deactivateAccountById explicitDeactivationDate envelope (Some transaction)

                Assert.Equal(fixture.Data.moneyMarket1270Id, Account.uniqueId deactivated)
                Assert.False(Account.isActive (Calendar.today()) deactivated)
                return ()
            }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail e
        finally
            DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore

    [<Fact>]
    member _.``REQ-AC-4.2 deactivateAccount rejects end earlier than begin`` () =
        let envelope = AuditEnvelope.create AccountDeactivation

        let transaction = DAL.createDbTransaction() |> Result.defaultWith failwith

        try
            let railroad = result {
                let! original = Account.fetchById (Some transaction) fixture.Data.moneyMarket1270Id
                let badActiveEnd = Some ((Account.activeBegin original).PlusDays(-1))

                let deactivationResult =
                    fixture.Data.moneyMarket1270Id
                    |> deactivateAccountById badActiveEnd envelope (Some transaction)

                Assert.True(Result.isError deactivationResult,
                    "Account deactivation was allowed to succeed with an earlier end than begin")
                return ()
            }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail e
        finally
            DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore

    [<Fact>]
    member _.``REQ-AC-4.2 deactivateAccount accepts end equal to begin`` () =
        let envelope = AuditEnvelope.create AccountDeactivation

        let transaction = DAL.createDbTransaction() |> Result.defaultWith failwith

        try
            let railroad = result {
                let! original = Account.fetchById (Some transaction) fixture.Data.moneyMarket1270Id
                let equalEnd = Some (Account.activeBegin original)

                let! _ =
                    fixture.Data.moneyMarket1270Id
                    |> deactivateAccountById equalEnd envelope (Some transaction)
                return ()
            }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail e
        finally
            DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore

    [<Fact>]
    member _.``REQ-AC-4.3 deactivateAccount rejects when active children exist`` () =
        let envelope = AuditEnvelope.create AccountDeactivation
        let goodActiveEnd = Some (Calendar.today())

        let transaction = DAL.createDbTransaction() |> Result.defaultWith failwith

        try
            let deactivationResult =
                fixture.Data.assets1000Id
                |> deactivateAccountById goodActiveEnd envelope (Some transaction)
            Assert.True(Result.isError deactivationResult,
                "Account deactivation was allowed to succeed with an active child")
        finally
            DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore

    [<Fact>]
    member _.``REQ-AC-4.4 deactivateAccount rejects when balance is non-zero`` () =
        let envelope = AuditEnvelope.create AccountDeactivation
        let goodActiveEnd = Some (Calendar.today())

        let transaction = DAL.createDbTransaction() |> Result.defaultWith failwith

        try
            let deactivationResult =
                fixture.Data.mortgage2210Id
                |> deactivateAccountById goodActiveEnd envelope (Some transaction)
            Assert.True(Result.isError deactivationResult,
                "Account deactivation was allowed to succeed with a non-zero balance")
        finally
            DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore

    [<Fact>]
    member _.``REQ-AC-4.5 deactivateAccount rejects already deactivated account`` () =
        let envelope = AuditEnvelope.create AccountDeactivation
        let activeEnd = Some (Calendar.today().PlusDays(1))

        let transaction = DAL.createDbTransaction() |> Result.defaultWith failwith

        try
            let deactivationResult =
                fixture.Data.closedBank1290Id
                |> deactivateAccountById activeEnd envelope (Some transaction)
            Assert.True(Result.isError deactivationResult,
                "Account deactivation was allowed to succeed with an already inactive account")
        finally
            DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore
