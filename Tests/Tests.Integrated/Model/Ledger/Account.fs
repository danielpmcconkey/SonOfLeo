namespace Tests.Integrated.Model.Ledger

open System
open DataAccessLayer.DbTransaction
open InterfaceBridge.CommandRoute
open Logger.Audit
open Model
open ModelOrchestrator
open Tests.Helpers
open Tests.Helpers.GenericTestProperties
open Utilities
open Utilities.ResultHelper
open Xunit
open Model.Ledger.Accounts
open Model.Ledger.Accounts.AccountComponent
open Utilities.AppError
open Tests.Helpers.SadPath

open Tests.Helpers.Railroad

[<Collection("SharedTestData")>]
type AccountTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-AC-1.4 REQ-AC-2.9 AccountCode must be unique``() =
        runCommandRouteAndAutoRollback AccountCreate (fun context ->
            let duplicateCode = "F-1250"
            let duplicateResult =
                AccountCreation.constructNewAndSaveToDb
                    context
                    (duplicateCode |> AccountCode.create |> Result.defaultWith(fun e -> failwith(AppError.toMessage e)))
                    genericAccountName
                    genericAccountType
                    genericAccountActivityPeriod
                    genericAccountSubtype
                    genericAccountParentId
                    genericAccountReference
            isCorrectError duplicateResult DalErrorDuringNonQueryExecution None)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-AC-1.5 Account code is case sensitive.``() =
        runCommandRouteAndAutoRollback AccountCreate (fun context ->
            let code = "f-1000"
            result {
                let! returned =
                    AccountCreation.constructNewAndSaveToDb
                        context
                        (code |> AccountCode.create |> Result.defaultWith(fun e -> failwith(AppError.toMessage e)))
                        genericAccountName
                        genericAccountType
                        genericAccountActivityPeriod
                        genericAccountSubtype
                        genericAccountParentId
                        genericAccountReference
                Assert.NotEqual(fixture.Data.assets1000Id, returned |> Account.accountId)
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
                    AccountCreation.constructNewAndSaveToDb
                        context
                        accountCode
                        accountName
                        accountType
                        genericAccountActivityPeriod
                        subType
                        parentId
                        (Some externalReference)
                let! fetched = created |> Account.accountId |> Account.fetchById context
                Assert.Equal(created |> Account.accountId, fetched |> Account.accountId)
                Assert.Equal(accountCode, fetched |> Account.code)
                Assert.Equal(accountName, fetched |> Account.accountName)
                Assert.Equal(accountType, fetched |> Account.accountType)
                Assert.Equal(genericAccountActivityPeriod, fetched |> Account.activityPeriod)
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
    member _.``REQ-AC-3.6 fetch by account type returns matching accounts``() =
        let context = Context.create NoTransaction FetchOnly
        result {
            let! fetchType = AccountType.fromString "Equity"
            let! fetched = Account.fetchByAccountType context fetchType
            let expectedIds = [ fixture.Data.equity3000Id; fixture.Data.retirement3030Id ]
            expectedIds
            |> List.forall(fun id -> fetched |> List.exists(fun a -> Account.accountId a = id))
            |> Assert.True
            fetched |> List.forall(fun a -> Account.accountType a = fetchType) |> Assert.True
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-AC-3.7 fetch all fetches everything``() =
        let expectedCount = fixture.Data.accounts |> List.length
        let context = Context.create NoTransaction FetchOnly
        result {
            let! fetched = Account.fetchAll context false
            Assert.Equal(expectedCount, fetched |> List.length)
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-AC-3.9 fetch all with active only fetches active accounts relative to system run time``() =
        let today = Calendar.today()
        let activeAccounts =
            fixture.Data.accounts
            |> List.filter(fun a -> a |> Account.activityPeriod |> AccountActivityPeriod.isActive today)
        let expectedCount = activeAccounts |> List.length
        let context = Context.create NoTransaction FetchOnly
        result {
            let! fetched = Account.fetchAll context true
            Assert.Equal(expectedCount, fetched |> List.length)
            fixture.Data.closedBank1290Id
            |> fun closedId -> fetched |> List.exists(fun a -> Account.accountId a = closedId)
            |> Assert.False
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-AC-2.6 parent ID must reference existing account``() =
        runCommandRouteAndAutoRollback AccountCreate (fun context ->
            let parentId = Guid.NewGuid()
            let code = "AC-2.6"
            let result =
                let parentAccountId = parentId |> AccountId.fromGuid |> Some
                AccountCreation.constructNewAndSaveToDb
                    context
                    (code |> AccountCode.create |> Result.defaultWith(fun e -> failwith(AppError.toMessage e)))
                    genericAccountName
                    genericAccountType
                    genericAccountActivityPeriod
                    genericAccountSubtype
                    parentAccountId
                    genericAccountReference
            match result with
            | Error(DalResultantRowsDidntMatchExpectation _) -> Ok()
            | Error e -> Error(TestingError $"Wrong error. {AppError.toMessage e}")
            | Ok _ -> Error(TestingError $"Expected failure; succeeded"))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-AC-2.7 parent account must be active at AuditEnvelope instant--positive``() =
        runCommandRouteAndAutoRollback AccountCreate (fun context ->
            let code = "AC-2.7-C"
            let parentAccountId = fixture.Data.revenue4000Id |> Some
            AccountCreation.constructNewAndSaveToDb
                context
                (code |> AccountCode.create |> Result.defaultWith(fun e -> failwith(AppError.toMessage e)))
                genericAccountName
                genericAccountType
                genericAccountActivityPeriod
                genericAccountSubtype
                parentAccountId
                genericAccountReference)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-AC-2.7 parent account must be active at AuditEnvelope instant--negative``() =
        runCommandRouteAndAutoRollback AccountCreate (fun context ->
            let code = "AC-2.7-C"
            let result =
                let parentAccountId = fixture.Data.closedBank1290Id |> Some
                AccountCreation.constructNewAndSaveToDb
                    context
                    (code |> AccountCode.create |> Result.defaultWith(fun e -> failwith(AppError.toMessage e)))
                    genericAccountName
                    genericAccountType
                    genericAccountActivityPeriod
                    genericAccountSubtype
                    parentAccountId
                    genericAccountReference
            match result with
            | Error(AccountParentIsInactive _) -> Ok()
            | Error e -> Error(TestingError $"Wrong error. {AppError.toMessage e}")
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
                    |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
                AccountCreation.constructNewAndSaveToDb
                    context
                    (code |> AccountCode.create |> Result.defaultWith(fun e -> failwith(AppError.toMessage e)))
                    genericAccountName
                    accountType
                    genericAccountActivityPeriod
                    genericAccountSubtype
                    parentAccountId
                    genericAccountReference
            match result with
            | Error(AccountParentAndChildTypesDontMatch _) -> Ok()
            | Error e -> Error(TestingError $"Wrong error. {AppError.toMessage e}")
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
                let isActive = original |> Account.activityPeriod |> AccountActivityPeriod.isActive(Calendar.today())
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
            let! id = expectedCode |> AccountCode.value |> LookupCache.accountCodeToId.fetch context
            let! account = id |> AccountId.fromGuid |> Account.fetchById context
            Assert.Equal(expectedCode, account |> Account.code)
            Assert.Equal(expectedAccount |> Account.accountId, account |> Account.accountId)
            Assert.Equal(expectedAccount |> Account.accountName, account |> Account.accountName)
            return ()
        }
        |> railroadWrapper
