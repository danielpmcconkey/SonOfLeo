namespace Tests.Integrated.Model.Ledger

open System
open Model.Audit
open ModelOrchestrator
open Tests.Integrated
open Tests.Integrated.GenericTestProperties
open Utilities.ResultHelper
open Xunit
open Model.Ledger.Accounts
open Model.Ledger.Accounts.AccountComponent
open Utilities
open Utilities.AppError

[<Collection("SharedTestData")>]
type AccountTests(fixture: TestDataFixture) =
    
    [<Fact>]
    member _.``REQ-AC-1.4 REQ-AC-2.9 AccountCode must be unique`` () =
        let transaction = DAL.createDbTransaction() |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
        let duplicateCode = "F-1250"
        try
            let duplicateResult = 
                AccountCreation.constructNewAndSaveToDb
                    (duplicateCode |> AccountCode.create |> Result.defaultWith (fun e -> failwith (AppError.toMessage e)))
                    genericAccountName genericAccountType genericAccountActivityPeriod genericAccountSubtype
                    genericAccountParentId genericAccountReference genericAuditEnvelope (Some transaction)
            match duplicateResult with
                | Error (DalErrorDuringNonQueryExecution _) -> ()
                | Ok _ -> Assert.Fail("Expected failure; returned success.")
                | Error e -> Assert.Fail($"Wrong error type: {AppError.toMessage e}")
        finally
            DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore

    [<Fact>]
    member _.``REQ-AC-1.5 Account code is case sensitive.`` () =
        let transaction = DAL.createDbTransaction() |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
        let code = "f-1000"
        try
            let railroad = result {
                let! returned = 
                    AccountCreation.constructNewAndSaveToDb
                        (code |> AccountCode.create |> Result.defaultWith (fun e -> failwith (AppError.toMessage e)))
                        genericAccountName genericAccountType genericAccountActivityPeriod genericAccountSubtype
                        genericAccountParentId genericAccountReference genericAuditEnvelope (Some transaction)
                Assert.NotEqual(fixture.Data.assets1000Id, returned |> Account.accountId)
                return () }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail (AppError.toMessage e)
        finally
            DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore
            
    [<Fact>]
    member _.``REQ-AC-2.14 REQ-SYS-5.1 create account and fetch by ID returns identical record`` () =
        let transaction = DAL.createDbTransaction() |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
        let code = "AC-2.14"
        let name = "Create account and fetch by ID returns identical record"
        try
            let railroad = result {
                let! accountCode = code |> AccountCode.create
                let! accountName = name |> AccountName.create
                let! returned = 
                    AccountCreation.constructNewAndSaveToDb accountCode accountName
                         genericAccountType genericAccountActivityPeriod genericAccountSubtype
                         genericAccountParentId genericAccountReference genericAuditEnvelope (Some transaction)
                let returnedCode = returned |> Account.code
                let returnedName = returned |> Account.accountName
                Assert.Equal(accountCode, returnedCode)
                Assert.Equal(accountName, returnedName)
                return () }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail (AppError.toMessage e)
        finally
            DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore
    
    [<Fact>]
    member _.``REQ-AC-3.5 fetch by parent ID returns all children`` () =
        let parentId = fixture.Data.assets1000Id
        let expectedChildren =
            fixture.Data.accounts
            |> List.filter(fun x -> x|> Account.parentId = (parentId |> Some))
            |> List.map(fun x -> x |> Account.accountId)
        let expectedCount = expectedChildren |> List.length
        let railroad = result {
            let! fetched = Account.fetchByParentId None parentId
            Assert.Equal(expectedCount, List.length fetched)
            expectedChildren
            |> List.forall (fun id -> fetched |> List.exists (fun a -> Account.accountId a = id))
            |> Assert.True
            return () }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail (AppError.toMessage e)
    
    [<Fact>]
    member _.``REQ-AC-3.6 fetch by account type returns matching accounts`` () =
        let railroad = result {
            let! fetchType = AccountType.fromString "Equity"
            let! fetched = Account.fetchByAccountType None fetchType
            let expectedIds = [ fixture.Data.equity3000Id; fixture.Data.retirement3030Id ]
            expectedIds
            |> List.forall (fun id -> fetched |> List.exists (fun a -> Account.accountId a = id))
            |> Assert.True
            fetched
            |> List.forall (fun a -> Account.accountType a = fetchType)
            |> Assert.True
            return () }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail (AppError.toMessage e)
    
    [<Fact>]
    member _.``REQ-AC-3.7 fetch all fetches everything`` () =
        let expectedCount = fixture.Data.accounts |> List.length
        let railroad = result {
            let! fetched = Account.fetchAll false None
            Assert.Equal(expectedCount, fetched |> List.length)
            return () }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail (AppError.toMessage e)
    
    [<Fact>]
    member _.``REQ-AC-3.9 fetch all with active only fetches active accounts relative to system run time`` () =
        let today = Calendar.today()
        let activeAccounts =
            fixture.Data.accounts
            |> List.filter(fun a -> a |> Account.activityPeriod |> AccountActivityPeriod.isActive today)
        let expectedCount = activeAccounts |> List.length
        let railroad = result {
            let! fetched = Account.fetchAll true None
            Assert.Equal(expectedCount, fetched |> List.length)
            fixture.Data.closedBank1290Id
            |> fun closedId -> fetched |> List.exists (fun a -> Account.accountId a = closedId)
            |> Assert.False
            return () }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail (AppError.toMessage e)
    
    [<Fact>]
    member _.``REQ-AC-2.6 parent ID must reference existing account`` () =
        let parentId = Guid.NewGuid()
        let code = "AC-2.6"
        let transaction = DAL.createDbTransaction() |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
        try
            let result =
                let parentAccountId = parentId |> AccountId.fromGuid |> Some
                AccountCreation.constructNewAndSaveToDb
                    (code |> AccountCode.create |> Result.defaultWith (fun e -> failwith (AppError.toMessage e)))
                    genericAccountName genericAccountType genericAccountActivityPeriod genericAccountSubtype
                    parentAccountId genericAccountReference genericAuditEnvelope (Some transaction)
            match result with
            | Error (DalResultantRowsDidntMatchExpectation _) -> ()
            | Error e -> Assert.Fail($"Wrong error. {AppError.toMessage e}")
            | Ok _ -> Assert.Fail($"Expected failure; succeeded")
        finally
            DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore
    
    [<Fact>]
    member _.``REQ-AC-2.7 parent account must be active at AuditEnvelope instant--positive`` () =
        let transaction = DAL.createDbTransaction() |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
        let code = "AC-2.7-C"
        try
            let result =
                let parentAccountId = fixture.Data.revenue4000Id |> Some
                AccountCreation.constructNewAndSaveToDb
                    (code |> AccountCode.create |> Result.defaultWith (fun e -> failwith (AppError.toMessage e)))
                    genericAccountName genericAccountType genericAccountActivityPeriod genericAccountSubtype
                    parentAccountId genericAccountReference genericAuditEnvelope (Some transaction)
            match result with
            | Error e -> Assert.Fail(AppError.toMessage e)
            | Ok _ -> ()
        finally
            DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore
    
    [<Fact>]
    member _.``REQ-AC-2.7 parent account must be active at AuditEnvelope instant--negative`` () =
        let transaction = DAL.createDbTransaction() |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
        let code = "AC-2.7-C"
        try
            let result =
                let parentAccountId = fixture.Data.closedBank1290Id |> Some
                AccountCreation.constructNewAndSaveToDb
                    (code |> AccountCode.create |> Result.defaultWith (fun e -> failwith (AppError.toMessage e)))
                    genericAccountName genericAccountType genericAccountActivityPeriod genericAccountSubtype
                    parentAccountId genericAccountReference genericAuditEnvelope (Some transaction)
            match result with
            | Error (AccountParentIsInactive _) -> () 
            | Error e -> Assert.Fail($"Wrong error. {AppError.toMessage e}")
            | Ok _ -> Assert.Fail($"Expected failure; succeeded")
        finally
            DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore
    
    [<Fact>]
    member _.``REQ-AC-2.20 child AccountType must match parent AccountType`` () =
        let transaction = DAL.createDbTransaction() |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
        let code = "AC-2.7-C"
        try
            let result =
                let parentAccountId = fixture.Data.assets1000Id |> Some
                let accountType = "Liability" |> AccountType.fromString |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
                AccountCreation.constructNewAndSaveToDb
                    (code |> AccountCode.create |> Result.defaultWith (fun e -> failwith (AppError.toMessage e)))
                    genericAccountName accountType genericAccountActivityPeriod genericAccountSubtype
                    parentAccountId genericAccountReference genericAuditEnvelope (Some transaction)
            match result with
            | Error (AccountParentAndChildTypesDontMatch _) -> ()
            | Error e -> Assert.Fail($"Wrong error. {AppError.toMessage e}")
            | Ok _ -> Assert.Fail($"Expected failure; succeeded")
        finally
            DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore
    
    [<Fact>]
    member _.``REQ-AC-4.8 updateAccountName succeeds with valid accountName`` () =
        let envelope_rename = AuditEnvelope.create AccountUpdateName
        let goodAccountName = "fahrvergnügen"
        let transaction = DAL.createDbTransaction() |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
        try
            let railroad = result {
                let! renamedAccount =
                    Account.updateAccountNameById fixture.Data.moneyMarket1270Id goodAccountName envelope_rename (Some transaction)
                Assert.Equal(goodAccountName, (AccountName.value (Account.accountName renamedAccount)))
                return () }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail (AppError.toMessage e)
        finally
            DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore
    
    [<Fact>]
    member _.``REQ-AC-4.9 updateExternalReference succeeds with valid reference`` () =
        let envelope_update = AuditEnvelope.create AccountUpdateExtReference
        let goodReference = Some "Fliegende Ratte"
        let transaction = DAL.createDbTransaction() |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
        try
            let railroad = result {
                let! updatedAccount =
                    Account.updateExternalReferenceById fixture.Data.moneyMarket1270Id goodReference envelope_update (Some transaction)
                let newReference = Account.externalReference updatedAccount |> Option.map AccountExternalReference.value
                Assert.Equal(goodReference, newReference)
                return () }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail (AppError.toMessage e)
        finally
            DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore
    
    [<Fact>]
    member _.``REQ-AC-4.9 updateExternalReference can be updated to None`` () =
        let envelope_update = AuditEnvelope.create AccountUpdateExtReference
        let transaction = DAL.createDbTransaction() |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
        try
            let railroad = result {
                let! updatedAccount =
                    Account.updateExternalReferenceById fixture.Data.moneyMarket1270Id None envelope_update (Some transaction)
                let newReference = Account.externalReference updatedAccount |> Option.map AccountExternalReference.value
                Assert.Equal(None, newReference)
                return ()
            }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail (AppError.toMessage e)
        finally
            DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore
    
    [<Fact>]
    member _.``REQ-SYS-3.3 account update operations set modifiedAt from AuditEnvelope`` () =
        let transaction = DAL.createDbTransaction() |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
        try
            let railroad = result {
                System.Threading.Thread.Sleep(10)
                let envelope_update = AuditEnvelope.create AccountUpdateName
                let! updatedAccount =
                    Account.updateAccountNameById fixture.Data.moneyMarket1270Id "Blah blah blah" envelope_update (Some transaction)
                Assert.Equal(AuditEnvelope.instant envelope_update, Account.modifiedAt updatedAccount)
                return ()
            }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail (AppError.toMessage e)
        finally
            DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore
    
    [<Fact>]
    member _.``REQ-AC-4.19 update to deactivated account is permitted`` () =
        let envelope_update = AuditEnvelope.create AccountUpdateName
        let newName = "Blah blah blah"
        let transaction = DAL.createDbTransaction() |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
        try
            let railroad = result {
                let! original = Account.fetchById (Some transaction) fixture.Data.closedBank1290Id
                let isActive = original |> Account.activityPeriod |> AccountActivityPeriod.isActive (Calendar.today())
                Assert.False(isActive) // just confirming that you indeed start with an inactive account
                let! updatedAccount =
                    Account.updateAccountNameById fixture.Data.closedBank1290Id newName envelope_update (Some transaction)
                Assert.Equal(newName, AccountName.value (Account.accountName updatedAccount))
                return () }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail (AppError.toMessage e)
        finally
            DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore
