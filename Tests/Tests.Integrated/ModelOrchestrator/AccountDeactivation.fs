namespace Tests.Integrated.ModelOrchestrator

open InterfaceBridge.CommandRoute
open Logger.Audit
open Model
open Model.ActivityPeriod
open Tests.Helpers
open Tests.Helpers.Railroad
open Utilities.ResultHelper
open Xunit
open Model.Ledger.Account
open ModelOrchestrator.AccountDeactivation
open Utilities
open Utilities.AppError
open Tests.Helpers.SadPath

[<Collection("SharedTestData")>]
type AccountDeactivationTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-AC-4.1 deactivateAccount sets active end and returns inactive account``() =
        let explicitDeactivationDate = Some(Calendar.today().PlusDays(-1))
        runCommandRouteAndAutoRollback AccountDeactivate (fun context ->
            result {
                let! original = Account.fetchById context fixture.Data.moneyMarket1270Id
                Assert.True(original |> Account.activityPeriod |> isActive(Calendar.today()))
                let! account = fixture.Data.moneyMarket1270Id |> Account.fetchById context
                let! deactivated = account |> deactivateAccount context explicitDeactivationDate

                Assert.Equal(fixture.Data.moneyMarket1270Id, Account.accountId deactivated)
                Assert.False(deactivated |> Account.activityPeriod |> isActive(Calendar.today()))
                (* isActive would still be false for any past date, so the name's first claim
                   needs the date itself. *)
                Assert.Equal<NodaTime.LocalDate option>(
                    explicitDeactivationDate,
                    deactivated |> Account.activityPeriod |> activeEnd)
                return ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-AC-4.2 deactivateAccount rejects end earlier than begin``() =
        runCommandRouteAndAutoRollback AccountDeactivate (fun context ->
            result {
                let! original = Account.fetchById context fixture.Data.moneyMarket1270Id
                let badActiveEnd =
                    (original |> Account.activityPeriod |> activeBegin).PlusDays(-1)
                let! account = fixture.Data.moneyMarket1270Id |> Account.fetchById context
                do!
                    isCorrectError
                        (account |> deactivateAccount context (Some badActiveEnd))
                        AccountDeactivationProposedDateIsInvalid
                        None
                return ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-AC-4.2 deactivateAccount accepts end equal to begin``() =
        runCommandRouteAndAutoRollback AccountDeactivate (fun context ->
            result {
                let! original = Account.fetchById context fixture.Data.moneyMarket1270Id
                let equalEnd = Some(original |> Account.activityPeriod |> activeBegin)
                let! account = fixture.Data.moneyMarket1270Id |> Account.fetchById context
                let! _ = account |> deactivateAccount context equalEnd
                return ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-AC-4.3 deactivateAccount rejects when active children exist``() =
        let goodActiveEnd = Some(Calendar.today())
        runCommandRouteAndAutoRollback AccountDeactivate (fun context ->
            result {
                let! account = fixture.Data.assets1000Id |> Account.fetchById context
                do!
                    isCorrectError
                        (account |> deactivateAccount context goodActiveEnd)
                        AccountActiveChildrenBeforeDeactivation
                        None
                return ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-AC-4.4 deactivateAccount rejects when balance is non-zero``() =
        let goodActiveEnd = Some(Calendar.today())
        runCommandRouteAndAutoRollback AccountDeactivate (fun context ->
            result {
                let! account = fixture.Data.mortgage2210Id |> Account.fetchById context
                do!
                    isCorrectError
                        (account |> deactivateAccount context goodActiveEnd)
                        AccountNonZeroBalanceBeforeDeactivation
                        None
                return ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-AC-4.5 deactivateAccount rejects already deactivated account``() =
        let goodActiveEnd = Some(Calendar.today().PlusDays(1))
        runCommandRouteAndAutoRollback AccountDeactivate (fun context ->
            result {
                let! account = fixture.Data.closedBank1290Id |> Account.fetchById context
                do!
                    isCorrectError
                        (account |> deactivateAccount context goodActiveEnd)
                        AccountAlreadyInactive
                        None
                return ()
            })
        |> railroadWrapper

// todo: we need a test for AccountDeactivationWithJournalEntriesDatedAfterDeactivationDate
