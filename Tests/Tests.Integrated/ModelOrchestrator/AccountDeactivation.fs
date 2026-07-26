namespace Tests.Integrated.ModelOrchestrator

open Model.Audit
open Tests.Integrated
open Tests.Integrated.Rollback
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
    member _.``REQ-AC-4.1 deactivateAccount sets active end and returns inactive account``() =
        let envelope = AuditEnvelope.create AccountDeactivate
        let explicitDeactivationDate = Some(Calendar.today().PlusDays(-1))
        withRollback(fun tran ->
            let railroad =
                result {
                    let! original = Account.fetchById tran fixture.Data.moneyMarket1270Id
                    Assert.True(original |> Account.activityPeriod |> AccountActivityPeriod.isActive(Calendar.today()))
                    let! account = fixture.Data.moneyMarket1270Id |> Account.fetchById tran
                    let! deactivated = account |> deactivateAccount tran envelope explicitDeactivationDate

                    Assert.Equal(fixture.Data.moneyMarket1270Id, Account.accountId deactivated)
                    Assert.False(
                        deactivated |> Account.activityPeriod |> AccountActivityPeriod.isActive(Calendar.today())
                    )
                    return ()
                }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail(AppError.toMessage e))

    [<Fact>]
    member _.``REQ-AC-4.2 deactivateAccount rejects end earlier than begin``() =
        let envelope = AuditEnvelope.create AccountDeactivate
        withRollback(fun tran ->
            let railroad =
                result {
                    let! original = Account.fetchById tran fixture.Data.moneyMarket1270Id
                    let badActiveEnd =
                        (original |> Account.activityPeriod |> AccountActivityPeriod.activeBegin).PlusDays(-1)
                    let! account = fixture.Data.moneyMarket1270Id |> Account.fetchById tran
                    let deactivationResult = account |> deactivateAccount tran envelope (Some badActiveEnd)
                    do!
                        match deactivationResult with
                        | Ok _ -> Error(TestingError "Expected failure; returned success.")
                        | Error(AccountDeactivationProposedDateIsInvalid _) -> Ok()
                        | Error e -> Error(TestingError $"Wrong error type: {AppError.toMessage e}")
                    return ()
                }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail(AppError.toMessage e))

    [<Fact>]
    member _.``REQ-AC-4.2 deactivateAccount accepts end equal to begin``() =
        let envelope = AuditEnvelope.create AccountDeactivate
        withRollback(fun tran ->
            let railroad =
                result {
                    let! original = Account.fetchById tran fixture.Data.moneyMarket1270Id
                    let equalEnd = Some(original |> Account.activityPeriod |> AccountActivityPeriod.activeBegin)
                    let! account = fixture.Data.moneyMarket1270Id |> Account.fetchById tran
                    let! _ = account |> deactivateAccount tran envelope equalEnd
                    return ()
                }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail(AppError.toMessage e))

    [<Fact>]
    member _.``REQ-AC-4.3 deactivateAccount rejects when active children exist``() =
        let envelope = AuditEnvelope.create AccountDeactivate
        let goodActiveEnd = Some(Calendar.today())
        withRollback(fun tran ->
            let railroad =
                result {
                    let! account = fixture.Data.assets1000Id |> Account.fetchById tran
                    let deactivationResult = account |> deactivateAccount tran envelope goodActiveEnd
                    do!
                        match deactivationResult with
                        | Ok _ -> Error(TestingError "Expected failure; returned success.")
                        | Error(AccountActiveChildrenBeforeDeactivation _) -> Ok()
                        | Error e -> Error(TestingError $"Wrong error type: {AppError.toMessage e}")
                    return ()
                }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail(AppError.toMessage e))

    [<Fact>]
    member _.``REQ-AC-4.4 deactivateAccount rejects when balance is non-zero``() =
        let envelope = AuditEnvelope.create AccountDeactivate
        let goodActiveEnd = Some(Calendar.today())
        withRollback(fun tran ->
            let railroad =
                result {
                    let! account = fixture.Data.mortgage2210Id |> Account.fetchById tran
                    let deactivationResult = account |> deactivateAccount tran envelope goodActiveEnd
                    do!
                        match deactivationResult with
                        | Ok _ -> Error(TestingError "Expected failure; returned success.")
                        | Error(AccountNonZeroBalanceBeforeDeactivation _) -> Ok()
                        | Error e -> Error(TestingError $"Wrong error type: {AppError.toMessage e}")
                    return ()
                }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail(AppError.toMessage e))

    [<Fact>]
    member _.``REQ-AC-4.5 deactivateAccount rejects already deactivated account``() =
        let envelope = AuditEnvelope.create AccountDeactivate
        let goodActiveEnd = Some(Calendar.today().PlusDays(1))
        withRollback(fun tran ->
            let railroad =
                result {
                    let! account = fixture.Data.closedBank1290Id |> Account.fetchById tran
                    let deactivationResult = account |> deactivateAccount tran envelope goodActiveEnd
                    do!
                        match deactivationResult with
                        | Ok _ -> Error(TestingError "Expected failure; returned success.")
                        | Error(AccountAlreadyInactive _) -> Ok()
                        | Error e -> Error(TestingError $"Wrong error type: {AppError.toMessage e}")
                    return ()
                }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail(AppError.toMessage e))

// todo: we need a test for AccountDeactivationWithJournalEntriesDatedAfterDeactivationDate
