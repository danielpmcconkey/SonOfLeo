namespace Tests.Integrated.ModelOrchestrator

open Model.Audit
open Tests.Integrated
open Utilities.ResultHelper
open Xunit
open Model.Ledger.Accounts
open Model.Ledger.Accounts.AccountComponent
open ModelOrchestrator.AccountDeactivation
open Utilities
open Utilities.AppError

[<Collection("SharedTestData")>]
type AccountDeactivationTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-AC-4.1 deactivateAccount sets active end and returns inactive account`` () =
        let envelope = AuditEnvelope.create AccountDeactivate
        let explicitDeactivationDate = Some (Calendar.today().PlusDays(-1))
        let transaction = DAL.createDbTransaction() |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))

        try
            let railroad = result {
                let! original = Account.fetchById (Some transaction) fixture.Data.moneyMarket1270Id
                Assert.True(original |> Account.activityPeriod |> AccountActivityPeriod.isActive (Calendar.today()))
                let! account = fixture.Data.moneyMarket1270Id |> Account.fetchById (Some transaction)
                let! deactivated = account |> deactivateAccount (Some transaction) envelope explicitDeactivationDate 

                Assert.Equal(fixture.Data.moneyMarket1270Id, Account.accountId deactivated)
                Assert.False(deactivated |> Account.activityPeriod |> AccountActivityPeriod.isActive (Calendar.today()))
                return ()
            }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail (AppError.toMessage e)
        finally
            DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore

    [<Fact>]
    member _.``REQ-AC-4.2 deactivateAccount rejects end earlier than begin`` () =
        let envelope = AuditEnvelope.create AccountDeactivate
        let transaction = DAL.createDbTransaction() |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
        try
            let railroad = result {
                let! original = Account.fetchById (Some transaction) fixture.Data.moneyMarket1270Id
                let badActiveEnd = (original |> Account.activityPeriod |> AccountActivityPeriod.activeBegin).PlusDays(-1)
                let! account = fixture.Data.moneyMarket1270Id |> Account.fetchById (Some transaction)
                let deactivationResult =
                    account |> deactivateAccount (Some transaction) envelope (Some badActiveEnd)
                do! match deactivationResult with
                    | Ok _ -> Error(TestingError "Expected failure; returned success.")
                    | Error (AccountDeactivationProposedDateIsInvalid _) -> Ok ()
                    | Error e -> Error(TestingError $"Wrong error type: {AppError.toMessage e}")
                return () }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail (AppError.toMessage e)
        finally
            DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore

    [<Fact>]
    member _.``REQ-AC-4.2 deactivateAccount accepts end equal to begin`` () =
        let envelope = AuditEnvelope.create AccountDeactivate
        let transaction = DAL.createDbTransaction() |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
        try
            let railroad = result {
                let! original = Account.fetchById (Some transaction) fixture.Data.moneyMarket1270Id
                let equalEnd = Some (original |> Account.activityPeriod |> AccountActivityPeriod.activeBegin)
                let! account = fixture.Data.moneyMarket1270Id |> Account.fetchById (Some transaction)
                let! _ = account |> deactivateAccount (Some transaction) envelope equalEnd 
                return () }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail (AppError.toMessage e)
        finally
            DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore

    [<Fact>]
    member _.``REQ-AC-4.3 deactivateAccount rejects when active children exist`` () =
        let envelope = AuditEnvelope.create AccountDeactivate
        let goodActiveEnd = Some (Calendar.today())
        let transaction = DAL.createDbTransaction() |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
        try
            let railroad = result {
                let! account = fixture.Data.assets1000Id |> Account.fetchById (Some transaction)
                let deactivationResult =
                    account |> deactivateAccount (Some transaction) envelope goodActiveEnd
                do! match deactivationResult with
                    | Ok _ -> Error(TestingError "Expected failure; returned success.")
                    | Error (AccountActiveChildrenBeforeDeactivation _) -> Ok ()
                    | Error e -> Error(TestingError $"Wrong error type: {AppError.toMessage e}")
                return () }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail (AppError.toMessage e)
        finally
            DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore

    [<Fact>]
    member _.``REQ-AC-4.4 deactivateAccount rejects when balance is non-zero`` () =
        let envelope = AuditEnvelope.create AccountDeactivate
        let goodActiveEnd = Some (Calendar.today())
        let transaction = DAL.createDbTransaction() |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
        try
            let railroad = result {
                let! account = fixture.Data.mortgage2210Id |> Account.fetchById (Some transaction)
                let deactivationResult =
                    account |> deactivateAccount (Some transaction) envelope goodActiveEnd
                do! match deactivationResult with
                    | Ok _ -> Error(TestingError "Expected failure; returned success.")
                    | Error (AccountNonZeroBalanceBeforeDeactivation _) -> Ok ()
                    | Error e -> Error(TestingError $"Wrong error type: {AppError.toMessage e}")
                return () }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail (AppError.toMessage e)
        finally
            DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore

    [<Fact>]
    member _.``REQ-AC-4.5 deactivateAccount rejects already deactivated account`` () =
        let envelope = AuditEnvelope.create AccountDeactivate
        let goodActiveEnd = Some (Calendar.today().PlusDays(1))
        let transaction = DAL.createDbTransaction() |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
        try
            let railroad = result {
                let! account = fixture.Data.closedBank1290Id |> Account.fetchById (Some transaction)
                let deactivationResult =
                    account |> deactivateAccount (Some transaction) envelope goodActiveEnd
                do! match deactivationResult with
                    | Ok _ -> Error(TestingError "Expected failure; returned success.")
                    | Error (AccountAlreadyInactive _) -> Ok ()
                    | Error e -> Error(TestingError $"Wrong error type: {AppError.toMessage e}")
                return () }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail (AppError.toMessage e)
        finally
            DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore

// todo: we need a test for AccountDeactivationWithJournalEntriesDatedAfterDeactivationDate
