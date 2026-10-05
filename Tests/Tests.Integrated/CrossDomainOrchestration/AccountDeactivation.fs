namespace Tests.Integrated.CrossDomainOrchestration

open App.Session
open Business.General
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.CrossDomainOrchestration
open Ui.InterfaceBridge.CommandRoute
open App.Operation.CoreAuditableAction
open Business.FinancialServices.Ledger.LedgerAuditableAction
open Business.FinancialServices.DataIngestion.DataIngestionAuditableAction
open Business.FinancialServices.Classification.ClassificationAuditableAction
open Business.FinancialServices.CashFlow.CashFlowAuditableAction
open Business.General.ActivityPeriod
open Tests.Helpers
open Tests.Helpers.Railroad
open App.Utility.Result
open Xunit
open Business.FinancialServices.Ledger.Account
open Business.CrossDomainOrchestration.AccountDeactivation
open App.Utility
open App.Utility.IAppError
open Tests.Helpers.TestError
open Tests.Helpers.SadPath
open Business.FinancialServices.Ledger.LedgerError

[<Collection("SharedTestData")>]
type AccountDeactivationTests(fixture: TestDataFixture) =

    (* The tests below build, inside their own rolled-back transaction, the account they deactivate and a Liability to
       take the other side of its entries. Codes carry the requirement they serve. *)
    let today = Calendar.today ()

    let newAccount context code (activeEnd: NodaTime.LocalDate option) parent =
        EntityFunctions.createTestAccountFromPrimitives
            context code $"Deactivation test {code}" "Asset" (today.PlusYears(-1)) activeEnd (Some "Cash") parent None

    let counterAccount context code =
        EntityFunctions.createTestAccountFromPrimitives
            context code $"Deactivation counter {code}" "Liability" (today.PlusYears(-1)) None (Some "CurrentLiability") None None
        |> Result.map snd

    let post context entryDate (lines: (AccountComponent.AccountId * decimal * string * string option) list) =
        EntityFunctions.createTestJournalEntryFromPrimitives context "Deactivation test entry" None entryDate lines [] []
        |> Result.map snd

    let voidEntry context entryId =
        result {
            let! reason = "Voided before deactivation" |> JournalEntryComponent.CommentText.create
            let! _ = entryId |> JournalEntryVoiding.voidJournalEntry context None reason
            return ()
        }

    /// Deactivates the account at the requested end, then reads it back from the database and returns its active end.
    let deactivateAndReadBack context (requestedEnd: NodaTime.LocalDate) (accountId: AccountComponent.AccountId) =
        result {
            let! account = accountId |> Account.fetchById context
            let! _ = account |> deactivateAccount context (Some requestedEnd)
            let! readBack = accountId |> Account.fetchById context
            return readBack |> Account.activityPeriod |> activeEnd
        }


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
                let beginDate = original |> Account.activityPeriod |> activeBegin
                let! storedEnd = fixture.Data.moneyMarket1270Id |> deactivateAndReadBack context beginDate
                Assert.Equal(Some beginDate, storedEnd)
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

    [<Theory>]
    [<InlineData(0)>]
    [<InlineData(30)>]
    member _.``REQ-AC-4.3 deactivateAccount rejects a parent whose only child's active end is today or later, the child being active as of the current date``(childEndOffsetDays: int) =
        (* The requested end is tomorrow: judged as of the requested end, a child ending today would no longer be
           active, and a child with any active end is not open-ended, so only the current-date reading rejects. *)
        runCommandRouteAndAutoRollback AccountDeactivate (fun context ->
            result {
                let! parent, parentId = newAccount context "AC-4.3-P" None None
                let! _ = newAccount context "AC-4.3-C" (Some(today.PlusDays(childEndOffsetDays))) (Some parentId)
                do!
                    isCorrectError
                        (parent |> deactivateAccount context (Some(today.PlusDays(1))))
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

    [<Fact>]
    member _.``REQ-AC-4.3 deactivateAccount succeeds for a parent whose only child's active end is before today, its active end reading back as the requested date``() =
        runCommandRouteAndAutoRollback AccountDeactivate (fun context ->
            result {
                let! _, parentId = newAccount context "AC-4.3-P" None None
                let! _ = newAccount context "AC-4.3-C" (Some(today.PlusDays(-1))) (Some parentId)
                let! readBack = parentId |> deactivateAndReadBack context today
                Assert.Equal(Some today, readBack)
                return ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-AC-4.4 deactivateAccount succeeds when unvoided lines net to zero alongside a voided one-sided entry, its active end reading back as the requested date``() =
        runCommandRouteAndAutoRollback AccountDeactivate (fun context ->
            result {
                let! _, accountId = newAccount context "AC-4.4" None None
                let! counterId = counterAccount context "AC-4.4-L"
                let! _ = post context (today.PlusDays(-3)) [ (accountId, 20.00M, "Debit", None); (counterId, 20.00M, "Credit", None) ]
                let! _ = post context (today.PlusDays(-2)) [ (counterId, 20.00M, "Debit", None); (accountId, 20.00M, "Credit", None) ]
                let! oneSided = post context (today.PlusDays(-1)) [ (accountId, 50.00M, "Debit", None); (counterId, 50.00M, "Credit", None) ]
                do! voidEntry context oneSided
                let! readBack = accountId |> deactivateAndReadBack context today
                Assert.Equal(Some today, readBack)
                return ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-AC-4.5 deactivating an Account whose active end was scheduled in the future at creation is rejected with a typed error and the scheduled end is unchanged`` () =
        runCommandRouteAndAutoRollback AccountDeactivate (fun context ->
            result {
                let scheduledEnd = today.PlusDays(30)
                let! account, accountId = newAccount context "AC-4.5" (Some scheduledEnd) None
                let () =
                    match account |> deactivateAccount context (Some today) with
                    | Error (AsError (AccountAlreadyInactive (rejectedId, end'))) ->
                        Assert.Equal(accountId |> AccountComponent.AccountId.value, rejectedId)
                        Assert.Equal(scheduledEnd, end')
                    | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
                    | Ok _ -> Assert.Fail "Expected failure; got success"
                let! readBack = accountId |> Account.fetchById context
                Assert.Equal(Some scheduledEnd, readBack |> Account.activityPeriod |> activeEnd)
                return ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-AC-4.6 an Account referenced only by a line of a voided entry dated after the requested active end deactivates, its active end reading back as the requested date`` () =
        runCommandRouteAndAutoRollback AccountDeactivate (fun context ->
            result {
                let requestedEnd = today.PlusDays(-5)
                let! _, accountId = newAccount context "AC-4.6" None None
                let! counterId = counterAccount context "AC-4.6-L"
                let! after = post context (requestedEnd.PlusDays(3)) [ (accountId, 15.00M, "Debit", None); (counterId, 15.00M, "Credit", None) ]
                do! voidEntry context after
                let! readBack = accountId |> deactivateAndReadBack context requestedEnd
                Assert.Equal(Some requestedEnd, readBack)
                return ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-AC-4.6 an Account with an unvoided entry dated exactly on the requested active end deactivates, its active end reading back as the requested date`` () =
        runCommandRouteAndAutoRollback AccountDeactivate (fun context ->
            result {
                let requestedEnd = today.PlusDays(-5)
                let! _, accountId = newAccount context "AC-4.6" None None
                let! counterId = counterAccount context "AC-4.6-L"
                // two entries, so the balance is zero and only the entry dates are in question
                let! _ = post context (requestedEnd.PlusDays(-1)) [ (accountId, 15.00M, "Debit", None); (counterId, 15.00M, "Credit", None) ]
                let! _ = post context requestedEnd [ (counterId, 15.00M, "Debit", None); (accountId, 15.00M, "Credit", None) ]
                let! readBack = accountId |> deactivateAndReadBack context requestedEnd
                Assert.Equal(Some requestedEnd, readBack)
                return ()
            })
        |> railroadWrapper
