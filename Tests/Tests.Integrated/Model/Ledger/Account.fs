namespace Tests.Integrated.Model.Ledger

open App.Session
open App.DataAccessLayer
open Business.General
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.CrossDomainOrchestration
open System
open App.DataAccessLayer.DbTransaction
open Ui.InterfaceBridge.CommandRoute
open App.Operation.AuditEnvelope
open Tests.Helpers
open Tests.Helpers.Cleanup
open Tests.Helpers.GenericTestProperties
open App.Utility
open App.Utility.Result
open Xunit
open Business.FinancialServices.Ledger.Account
open Business.FinancialServices.Ledger.AccountComponent
open App.Utility.IAppError
open Tests.Helpers.TestError
open Tests.Helpers.SadPath
open Tests.Helpers.Railroad
open App.Operation.CoreAuditableAction
open Business.FinancialServices.Ledger.LedgerAuditableAction
open Business.FinancialServices.DataIngestion.DataIngestionAuditableAction
open Business.FinancialServices.Classification.ClassificationAuditableAction
open Business.FinancialServices.CashFlow.CashFlowAuditableAction
open Business.FinancialServices.Ledger.LedgerError
open App.DataAccessLayer.DalError

[<Collection("SharedTestData")>]
type AccountTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-AC-1.4 REQ-AC-2.9 creating over an account code already in use is refused and leaves that account the only holder of the code``() =
        (* NoTransaction rather than the usual rollback: a failed statement aborts an open
           transaction, so what the refusal left behind could not be read back afterwards.
           This test only ever adds a row, and deletes it in the finally if the create wrongly
           succeeds. *)
        let mutable idToCleanUp = None
        let duplicateCode = "F-1250"
        let incumbent =
            fixture.Data.accounts
            |> List.find(fun a -> a |> Account.code |> AccountCode.value = duplicateCode)
        let context = Context.create NoTransaction AccountCreate
        try
            result {
                do!
                    AccountCreation.constructNewAndPersist
                        context
                        (duplicateCode |> AccountCode.create |> Result.defaultWith(fun (e: IAppError) -> failwith(e.ToMessage())))
                        genericAccountName
                        genericAccountType
                        genericActivityPeriod
                        genericAccountSubtype
                        genericAccountParentId
                        genericAccountReference
                    |> fun r ->
                        (match r with
                         | Ok created -> idToCleanUp <- Some(created |> Account.accountId)
                         | Error _ -> ())
                        isCorrectError r DalErrorDuringNonQueryExecution None
                (* The error alone would also be satisfied by an implementation that resolved
                   the collision by overwriting the account already holding the code. *)
                let! all = Account.fetchAll context false
                let holders = all |> List.filter(fun a -> a |> Account.code |> AccountCode.value = duplicateCode)
                Assert.Equal(1, holders |> List.length)
                let survivor = holders |> List.exactlyOne
                Assert.Equal(incumbent |> Account.accountId, survivor |> Account.accountId)
                Assert.Equal(
                    incumbent |> Account.accountName |> AccountName.value,
                    survivor |> Account.accountName |> AccountName.value)
                Assert.Equal(incumbent |> Account.accountType, survivor |> Account.accountType)
            }
            |> railroadWrapper
        finally
            cleanUpAccountId idToCleanUp |> ignore

    [<Fact>]
    member _.``REQ-AC-1.5 an account created under the lower-cased form of an existing account's code is a distinct account, and each code resolves to its own account``() =
        let incumbentId = fixture.Data.assets1000Id
        let incumbentCode =
            fixture.Data.accounts |> List.find(fun a -> a |> Account.accountId = incumbentId) |> Account.code |> AccountCode.value
        let lowered = incumbentCode.ToLowerInvariant()
        Assert.NotEqual<string>(incumbentCode, lowered)
        runCommandRouteAndAutoRollback AccountCreate (fun context ->
            result {
                let! created =
                    AccountCreation.constructNewAndPersist
                        context
                        (lowered |> AccountCode.create |> Result.defaultWith(fun (e: IAppError) -> failwith(e.ToMessage())))
                        genericAccountName
                        genericAccountType
                        genericActivityPeriod
                        genericAccountSubtype
                        genericAccountParentId
                        genericAccountReference
                let! all = Account.fetchAll context false
                let holderOf code = all |> List.filter(fun a -> a |> Account.code |> AccountCode.value = code) |> List.map Account.accountId
                Assert.Equal<AccountId list>([ incumbentId ], holderOf incumbentCode)
                Assert.Equal<AccountId list>([ created |> Account.accountId ], holderOf lowered)
                return ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-AC-2.14 REQ-SYS-5.1 create account and fetch by ID returns identical record``() =
        runCommandRouteAndAutoRollback AccountCreate (fun context ->
            let code = "AC-2.14"
            let name = "Create account and fetch by ID returns identical record"
            let reference = "AC-2.14 external reference"
            result {
                let! accountCode = code |> AccountCode.create
                let! accountName = name |> AccountName.create
                // Asset, so that the non-null Cash subtype and the F-1000 Assets parent are both legal
                let! accountType = "Asset" |> AccountType.fromString
                let! externalReference = reference |> AccountExternalReference.create
                let subType = Some genericAccountSubtypeNonNull
                let parentId = Some fixture.Data.assets1000Id
                let! created =
                    AccountCreation.constructNewAndPersist
                        context
                        accountCode
                        accountName
                        accountType
                        genericActivityPeriod
                        subType
                        parentId
                        (Some externalReference)
                let! fetched = created |> Account.accountId |> Account.fetchById context
                Assert.Equal(created |> Account.accountId, fetched |> Account.accountId)
                Assert.Equal(accountCode, fetched |> Account.code)
                Assert.Equal(accountName, fetched |> Account.accountName)
                Assert.Equal(accountType, fetched |> Account.accountType)
                Assert.Equal(genericActivityPeriod, fetched |> Account.activityPeriod)
                Assert.Equal<AccountSubtype option>(subType, fetched |> Account.accountSubType)
                Assert.Equal<AccountId option>(parentId, fetched |> Account.parentId)
                Assert.Equal<AccountExternalReference option>(
                    Some externalReference,
                    fetched |> Account.externalReference
                )
                Assert.Equal(created |> Account.createdAt, fetched |> Account.createdAt)
                Assert.Equal(created |> Account.modifiedAt, fetched |> Account.modifiedAt)
                return ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-AC-3.5 fetch by parent ID returns all children``() =
        let parentId = fixture.Data.assets1000Id
        let expectedChildren =
            fixture.Data.accounts
            |> List.filter(fun x -> x |> Account.parentId = (parentId |> Some))
            |> List.map(fun x -> x |> Account.accountId)
        let expectedCount = expectedChildren |> List.length
        let context = Context.create NoTransaction FetchOnly
        result {
            let! fetched = Account.fetchByParentId context parentId
            Assert.Equal(expectedCount, List.length fetched)
            expectedChildren
            |> List.forall(fun id -> fetched |> List.exists(fun a -> Account.accountId a = id))
            |> Assert.True
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-AC-3.6 fetch by account type returns exactly the accounts of that type``() =
        let context = Context.create NoTransaction FetchOnly
        let expectedIds =
            fixture.Data.accounts
            |> List.filter(fun a -> a |> Account.accountType = AccountType.Equity)
            |> List.map Account.accountId
        result {
            let! fetched = Account.fetchByAccountType context AccountType.Equity
            Assert.Equal<Set<AccountId>>(expectedIds |> Set.ofList, fetched |> List.map Account.accountId |> Set.ofList)
            Assert.Equal(expectedIds |> List.length, fetched |> List.length)
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-AC-3.7 fetch all returns exactly the fixture's accounts``() =
        let expectedIds = fixture.Data.accounts |> List.map Account.accountId
        let context = Context.create NoTransaction FetchOnly
        result {
            let! fetched = Account.fetchAll context false
            Assert.Equal<Set<AccountId>>(expectedIds |> Set.ofList, fetched |> List.map Account.accountId |> Set.ofList)
            Assert.Equal(expectedIds |> List.length, fetched |> List.length)
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-AC-3.9 fetch all with active only returns exactly the accounts whose activity window holds today``() =
        let today = Calendar.today()
        // REQ-AC-1.50 in plain comparisons on the fixture's stored dates
        let expectedIds =
            fixture.Data.accounts
            |> List.filter(fun a ->
                let period = a |> Account.activityPeriod
                let begins = period |> ActivityPeriod.activeBegin
                let ends = period |> ActivityPeriod.activeEnd
                begins <= today && (ends.IsNone || ends.Value >= today))
            |> List.map Account.accountId
        Assert.DoesNotContain(fixture.Data.closedBank1290Id, expectedIds)
        let context = Context.create NoTransaction FetchOnly
        result {
            let! fetched = Account.fetchAll context true
            Assert.Equal<Set<AccountId>>(expectedIds |> Set.ofList, fetched |> List.map Account.accountId |> Set.ofList)
            Assert.Equal(expectedIds |> List.length, fetched |> List.length)
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-AC-1.40 REQ-AC-2.6 parent ID must reference existing account``() =
        runCommandRouteAndAutoRollback AccountCreate (fun context ->
            let parentId = Guid.NewGuid()
            let code = "AC-2.6"
            let result =
                let parentAccountId = parentId |> AccountId.fromGuid |> Some
                AccountCreation.constructNewAndPersist
                    context
                    (code |> AccountCode.create |> Result.defaultWith(fun (e: IAppError) -> failwith(e.ToMessage())))
                    genericAccountName
                    genericAccountType
                    genericActivityPeriod
                    genericAccountSubtype
                    parentAccountId
                    genericAccountReference
            match result with
            | Error (AsError (AccountIdDoesntMatch uuid)) -> Assert.Equal(parentId, uuid); Ok()
            | Error e -> Error(TestingError $"Wrong error. {e.ToMessage()}")
            | Ok _ -> Error(TestingError $"Expected failure; succeeded"))
        |> railroadWrapper

    [<Theory>]
    [<InlineData(0)>]
    [<InlineData(30)>]
    member _.``REQ-AC-2.7 creating a child under a parent whose active end is today or later succeeds and the stored child carries that parent``(parentEndOffsetDays: int) =
        runCommandRouteAndAutoRollback AccountCreate (fun context ->
            result {
                let parentEnd = Calendar.today().PlusDays(parentEndOffsetDays)
                let! _, parentId =
                    EntityFunctions.createTestAccountFromPrimitives
                        context "AC-2.7-P" "Parent ending today or later" genericAccountTypeString genericActiveBegin
                        (Some parentEnd) genericAccountSubtype None None
                let! child =
                    AccountCreation.constructNewAndPersist
                        context
                        ("AC-2.7-C" |> AccountCode.create |> Result.defaultWith(fun (e: IAppError) -> failwith(e.ToMessage())))
                        genericAccountName
                        genericAccountType
                        genericActivityPeriod
                        genericAccountSubtype
                        (Some parentId)
                        genericAccountReference
                let! stored = child |> Account.accountId |> Account.fetchById context
                Assert.Equal(Some parentId, stored |> Account.parentId)
                return ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-AC-2.7 parent account must be active at AuditEnvelope instant--negative``() =
        runCommandRouteAndAutoRollback AccountCreate (fun context ->
            let code = "AC-2.7-C"
            let result =
                let parentAccountId = fixture.Data.closedBank1290Id |> Some
                AccountCreation.constructNewAndPersist
                    context
                    (code |> AccountCode.create |> Result.defaultWith(fun (e: IAppError) -> failwith(e.ToMessage())))
                    genericAccountName
                    genericAccountType
                    genericActivityPeriod
                    genericAccountSubtype
                    parentAccountId
                    genericAccountReference
            match result with
            | Error (AsError (AccountParentIsInactive _)) -> Ok()
            | Error e -> Error(TestingError $"Wrong error. {e.ToMessage()}")
            | Ok _ -> Error(TestingError $"Expected failure; succeeded"))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-AC-2.20 child AccountType must match parent AccountType``() =
        runCommandRouteAndAutoRollback AccountCreate (fun context ->
            let code = "AC-2.7-C"
            let result =
                let parentAccountId = fixture.Data.assets1000Id |> Some
                let accountType =
                    "Liability"
                    |> AccountType.fromString
                    |> Result.defaultWith(fun (e: IAppError) -> failwith(e.ToMessage()))
                AccountCreation.constructNewAndPersist
                    context
                    (code |> AccountCode.create |> Result.defaultWith(fun (e: IAppError) -> failwith(e.ToMessage())))
                    genericAccountName
                    accountType
                    genericActivityPeriod
                    genericAccountSubtype
                    parentAccountId
                    genericAccountReference
            match result with
            | Error (AsError (AccountParentAndChildTypesDontMatch _)) -> Ok()
            | Error e -> Error(TestingError $"Wrong error. {e.ToMessage()}")
            | Ok _ -> Error(TestingError $"Expected failure; succeeded"))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-AC-4.8 updateAccountName succeeds with valid accountName``() =
        runCommandRouteAndAutoRollback AccountUpdateName (fun context ->
            let goodAccountName = "fahrvergnügen"
            result {
                let! renamedAccount =
                    Account.updateAccountNameById context fixture.Data.moneyMarket1270Id goodAccountName
                Assert.Equal(goodAccountName, AccountName.value(Account.accountName renamedAccount))
                return ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-AC-4.9 updateExternalReference succeeds with valid reference``() =
        runCommandRouteAndAutoRollback AccountUpdateExtReference (fun context ->
            let goodReference = Some "Fliegende Ratte"
            result {
                let! updatedAccount =
                    Account.updateExternalReferenceById context fixture.Data.moneyMarket1270Id goodReference
                let newReference =
                    Account.externalReference updatedAccount |> Option.map AccountExternalReference.value
                Assert.Equal(goodReference, newReference)
                return ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-AC-4.9 updateExternalReference can be updated to None``() =
        runCommandRouteAndAutoRollback AccountUpdateExtReference (fun context ->
            result {
                let! updatedAccount = Account.updateExternalReferenceById context fixture.Data.moneyMarket1270Id None
                let newReference =
                    Account.externalReference updatedAccount |> Option.map AccountExternalReference.value
                Assert.Equal(None, newReference)
                return ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-SYS-3.3 account update operations set modifiedAt from AuditEnvelope``() =
        runCommandRouteAndAutoRollback AccountUpdateName (fun context ->
            result {
                let! updatedAccount =
                    Account.updateAccountNameById context fixture.Data.moneyMarket1270Id "Blah blah blah"
                Assert.Equal(context |> Context.getInitiationInstant, Account.modifiedAt updatedAccount)
                return ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-AC-4.19 update to deactivated account is permitted``() =
        runCommandRouteAndAutoRollback AccountUpdateName (fun context ->
            let newName = "Blah blah blah"
            result {
                let! original = Account.fetchById context fixture.Data.closedBank1290Id
                let isActive = original |> Account.activityPeriod |> ActivityPeriod.isActive(Calendar.today())
                Assert.False(isActive) // just confirming that you indeed start with an inactive account
                let! updatedAccount = Account.updateAccountNameById context fixture.Data.closedBank1290Id newName
                Assert.Equal(newName, AccountName.value(Account.accountName updatedAccount))
                return ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-AC-3.3 fetchById returns account matching provided ID``() =
        let context = Context.create NoTransaction FetchOnly
        let expectedId = fixture.Data.mortgage2210Id
        (* The ID is the locator, so asserting it back proves only that the query returned a
           row. The account's own properties are what prove it returned the right one. *)
        let expectedAccount =
            fixture.Data.accounts
            |> List.find(fun a -> a |> Account.accountId = expectedId)
        result {
            let! account = Account.fetchById context expectedId
            Assert.Equal(expectedId, account |> Account.accountId)
            Assert.Equal(
                expectedAccount |> Account.code |> AccountCode.value,
                account |> Account.code |> AccountCode.value)
            Assert.Equal(
                expectedAccount |> Account.accountName |> AccountName.value,
                account |> Account.accountName |> AccountName.value)
            Assert.Equal(expectedAccount |> Account.accountType, account |> Account.accountType)
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-AC-3.4 fetching by account code returns the account carrying that code``() =
        let context = Context.create NoTransaction FetchOnly
        let expectedAccount =
            fixture.Data.accounts
            |> List.find(fun a -> a |> Account.accountId = fixture.Data.mortgage2210Id)
        let expectedCode = expectedAccount |> Account.code
        result {
            let! id = expectedCode |> AccountCode.value |> Business.FinancialServices.Ledger.Account.codeToId.fetch (context |> Context.getDatabaseTransaction)
            let! account = id |> AccountId.fromGuid |> Account.fetchById context
            Assert.Equal(expectedCode, account |> Account.code)
            Assert.Equal(expectedAccount |> Account.accountId, account |> Account.accountId)
            Assert.Equal(expectedAccount |> Account.accountName, account |> Account.accountName)
            return ()
        }
        |> railroadWrapper
