module Tests.Integrated.Model.Ledger.Account

open System
open Model.Audit
open Xunit
open Model.Ledger.Account
open Model.Ledger.AccountComponent
open NodaTime
open Utilities.ResultCE
open Tests.Integrated._Cleanup
open Utilities.Clock

[<Fact>]
let ``REQ-AC-1.4 REQ-AC-2.9 AccountCode must be unique`` () =    
    let code1 = "REQ-AC-1.4"
    let code2 = code1
    let name1 = "AccountCode must be unique"
    let name2 = "AccountCode must still be unique"
    let accountType = "Asset"
    let activeBegin = Clock.now()
    let activeEnd = None
    let subtype = None
    let parentId = None
    let reference = None
    let envelope1 = AuditEnvelope.create AccountCreate
    let envelope2 = AuditEnvelope.create AccountCreate
    
    let mutable idToCleanUp = None
    try
        let result1 = Account.constructNewAndSaveToDb code1 name1 accountType activeBegin activeEnd subtype parentId reference envelope1
        match result1 with
        | Error e -> Assert.Fail e // need to ensure that the first made it into the DB to check the unique constraint
        | Ok a -> idToCleanUp <- Some (Account.id a)
        
        let result2 = Account.constructNewAndSaveToDb code2 name2 accountType activeBegin activeEnd subtype parentId reference envelope2
        Assert.True(Result.isError result2)
    finally
        match cleanUpAccountId idToCleanUp with
        | Ok () -> ()
        | Error e -> failwith e

[<Fact>]
let ``REQ-AC-1.5 Account code is case sensitive.`` () =    
    let code1 = "REQ-AC-1.5"
    let code2 = "req-ac-1.5"
    let name = "AccountCode is case sensitive"
    let accountType = "Asset"
    let activeBegin = Clock.now()
    let activeEnd = None
    let subtype = None
    let parentId = None
    let reference = None
    let envelope1 = AuditEnvelope.create AccountCreate
    let envelope2 = AuditEnvelope.create AccountCreate
    
    let mutable idToCleanUp = None
    let mutable idToCleanUp2 = None
    try
        let railroad = result {
            let! account1 = Account.constructNewAndSaveToDb code1 name accountType activeBegin activeEnd subtype parentId reference envelope1
            idToCleanUp <- Some (Account.id account1)
            let! account2 = Account.constructNewAndSaveToDb code2 name accountType activeBegin activeEnd subtype parentId reference envelope2
            idToCleanUp2 <- Some (Account.id account2)
            let dbCode1 = AccountCode.value (Account.code account1)
            let dbCode2 = AccountCode.value (Account.code account2)
            Assert.Equal(code1, dbCode1)
            Assert.Equal(code2, dbCode2)
            Assert.True(dbCode1 <> dbCode2) // Assert.NotEqual doesn't work here
            return ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e
    finally
        [idToCleanUp; idToCleanUp2]
        |> cleanUpAccountList
        |> function
            | Ok _ -> ()
            | Error e -> failwith e 

// =============================================================================
// Create + Read round-trips
// =============================================================================

[<Fact>]
let ``REQ-AC-2.14 REQ-SYS-5.1 create account and fetch by ID returns identical record`` () =
    let code = "AC-2.14"
    let name = "Create account and fetch by ID returns identical record"
    let accountType = "Asset"
    let activeBegin = Clock.now()
    let activeEnd = Some (activeBegin.Plus(Duration.FromDays(60)))
    let subtype = Some "FixedAsset"
    let parentId = None
    let reference = Some "test ext ref"
    let envelope = AuditEnvelope.create AccountCreate
    
    let mutable idToCleanUp = None
    try
        let railroad = result {
            let! pushResult = Account.constructNewAndSaveToDb code name accountType activeBegin activeEnd subtype parentId reference envelope
            let pushId = Account.id pushResult
            idToCleanUp <- Some pushId
            let! pullResult = Account.fetchById pushId
            Assert.Equal(pushId, (Account.id pullResult))
            Assert.Equal(code, AccountCode.value(Account.code pullResult))
            Assert.Equal(name, AccountName.value(Account.name pullResult))
            Assert.Equal(accountType, AccountType.toString(Account.accountType pullResult))
            Assert.Equal(activeBegin, Account.activeBegin pullResult)
            Assert.Equal(activeEnd, Account.activeEnd pullResult)
            let! pullSubtype =
                match Account.accountSubType pullResult with
                | None -> Error "pulled subtype was null when it shouldn't have been"
                | Some x -> Ok (AccountSubtype.toString x)
            Assert.Equal(Option.get subtype, pullSubtype)
            Assert.Null(Account.parentId pullResult)
            let! pullReference =
                match Account.externalReference pullResult with
                | None -> Error "pulled external reference was null when it shouldn't have been"
                | Some x -> Ok (AccountExternalReference.value x)
            Assert.Equal(Option.get reference, pullReference)
            return ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e
    finally
        match cleanUpAccountId idToCleanUp with
        | Ok () -> ()
        | Error e -> failwith e

[<Fact>]
let ``REQ-AC-3.4 fetch by code returns correct account`` () =
    let code = "REQ-AC-3.4"
    let name = "fetch by code returns correct account"
    let accountType = "Asset"
    let activeBegin = Clock.now()
    let activeEnd = None
    let subtype = None
    let parentId = None
    let reference = None
    let envelope = AuditEnvelope.create AccountCreate
    
    let mutable idToCleanUp = None
    try
        let railroad = result {
            let! pushResult = Account.constructNewAndSaveToDb code name accountType activeBegin activeEnd subtype parentId reference envelope
            let pushId = Account.id pushResult
            idToCleanUp <- Some pushId
            let! pullResult = Account.fetchByCode code
            Assert.Equal(pushId, (Account.id pullResult))
            return ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e
    finally
        match cleanUpAccountId idToCleanUp with
        | Ok () -> ()
        | Error e -> failwith e

[<Fact>]
let ``REQ-AC-3.5 fetch by parent ID returns all children`` () =
    let code_parent = "AC-3.5-P"
    let name_parent = "fetch by parent ID returns all children"
    let accountType_parent = "Asset"
    let activeBegin_parent = Clock.now()
    let activeEnd_parent = None
    let subtype_parent = None
    let parentId_parent = None
    let reference_parent= None
    let envelope_parent = AuditEnvelope.create AccountCreate
    
    let code_child1 = "AC-3.5-C1"
    let name_child1 = "fetch by parent ID returns all children"
    let accountType_child1 = "Asset"
    let activeBegin_child1 = Clock.now()
    let activeEnd_child1 = None
    let subtype_child1 = None    
    let reference_child1= None
    let envelope_child1 = AuditEnvelope.create AccountCreate    
    
    let code_child2 = "AC-3.5-C2"
    let name_child2 = "fetch by parent ID returns all children"
    let accountType_child2 = "Asset"
    let activeBegin_child2 = Clock.now()
    let activeEnd_child2 = None
    let subtype_child2 = None
    let reference_child2= None
    let envelope_child2 = AuditEnvelope.create AccountCreate  
    
    let code_child3 = "AC-3.5-C3"
    let name_child3 = "fetch by parent ID returns all children"
    let accountType_child3 = "Asset"
    let activeBegin_child3 = Clock.now()
    let activeEnd_child3 = None
    let subtype_child3 = None
    let reference_child3= None
    let envelope_child3 = AuditEnvelope.create AccountCreate
    
    let mutable idToCleanUp_parent = None
    let mutable idToCleanUp_child1 = None
    let mutable idToCleanUp_child2 = None
    let mutable idToCleanUp_child3 = None
    try
        let railroad = result {
            let! account_parent = Account.constructNewAndSaveToDb
                                      code_parent name_parent accountType_parent activeBegin_parent activeEnd_parent
                                      subtype_parent parentId_parent reference_parent envelope_parent
            let parentId = Account.id account_parent
            idToCleanUp_parent <- Some parentId
            
            let! account_child1 = Account.constructNewAndSaveToDb
                                      code_child1 name_child1 accountType_child1 activeBegin_child1 activeEnd_child1
                                      subtype_child1 (Some parentId) reference_child1 envelope_child1
            let id_child1 = Account.id account_child1
            idToCleanUp_child1 <- Some id_child1
            
            let! account_child2 = Account.constructNewAndSaveToDb
                                      code_child2 name_child2 accountType_child2 activeBegin_child2 activeEnd_child2
                                      subtype_child2 (Some parentId) reference_child2 envelope_child2
            let id_child2 = Account.id account_child2
            idToCleanUp_child2 <- Some id_child2
            
            let! account_child3 = Account.constructNewAndSaveToDb
                                      code_child3 name_child3 accountType_child3 activeBegin_child3 activeEnd_child3
                                      subtype_child3 (Some parentId) reference_child3 envelope_child3
            let id_child3 = Account.id account_child3
            idToCleanUp_child3 <- Some id_child3
            
            let! fetched = Account.fetchByParentId parentId
            
            Assert.Equal(3, List.length fetched)
            
            [Option.get idToCleanUp_child1;
             Option.get idToCleanUp_child2;
             Option.get idToCleanUp_child3]
            |> List.forall(fun id -> fetched |> List.exists (fun a -> Account.id a = id))
            |> Assert.True
            return ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e
    finally
        match cleanUpParentIdAndChildren idToCleanUp_parent [idToCleanUp_child1; idToCleanUp_child2; idToCleanUp_child3] with
        | Ok () -> ()
        | Error e -> failwith e

[<Fact>]
let ``REQ-AC-3.6 fetch by account type returns matching accounts`` () =
    let code_1 = "AC-3.6-1"
    let name_1 = "fetch by account type returns matching accounts"
    let accountType_1 = "Equity"
    let activeBegin_1 = Clock.now()
    let activeEnd_1 = None
    let subtype_1 = None    
    let reference_1= None
    let envelope_1 = AuditEnvelope.create AccountCreate    
    
    let code_2 = "AC-3.6-2"
    let name_2 = "fetch by account type returns matching accounts"
    let accountType_2 = "Equity"
    let activeBegin_2 = Clock.now()
    let activeEnd_2 = None
    let subtype_2 = None
    let reference_2= None
    let envelope_2 = AuditEnvelope.create AccountCreate  
    
    let code_3 = "AC-3.6-3"
    let name_3 = "fetch by account type returns matching accounts"
    let accountType_3 = "Equity"
    let activeBegin_3 = Clock.now()
    let activeEnd_3 = None
    let subtype_3 = None
    let reference_3= None
    let envelope_3 = AuditEnvelope.create AccountCreate
    
    let mutable idToCleanUp_1 = None
    let mutable idToCleanUp_2 = None
    let mutable idToCleanUp_3 = None
    try
        let railroad = result {
            let! account_1 = Account.constructNewAndSaveToDb
                                      code_1 name_1 accountType_1 activeBegin_1 activeEnd_1
                                      subtype_1 None reference_1 envelope_1
            let id_1 = Account.id account_1
            idToCleanUp_1 <- Some id_1
            
            let! account_2 = Account.constructNewAndSaveToDb
                                      code_2 name_2 accountType_2 activeBegin_2 activeEnd_2
                                      subtype_2 None reference_2 envelope_2
            let id_2 = Account.id account_2
            idToCleanUp_2 <- Some id_2
            
            let! account_3 = Account.constructNewAndSaveToDb
                                      code_3 name_3 accountType_3 activeBegin_3 activeEnd_3
                                      subtype_3 None reference_3 envelope_3
            let id_3 = Account.id account_3
            idToCleanUp_3 <- Some id_3
            
            let! fetchType = AccountType.fromString "Equity"
            let! fetched = Account.fetchByAccountType fetchType
            Assert.Equal(3, List.length fetched)
            
            [Option.get idToCleanUp_1;
             Option.get idToCleanUp_2;
             Option.get idToCleanUp_3]
            |> List.forall(fun id -> fetched |> List.exists (fun a -> Account.id a = id))
            |> Assert.True
            return ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e
    finally
        match cleanUpAccountList [idToCleanUp_1; idToCleanUp_2; idToCleanUp_3] with
        | Ok () -> ()
        | Error e -> failwith e
        
// =============================================================================
// Create validations (DB-dependent)
// =============================================================================

[<Fact>]
let ``REQ-AC-2.6 parent ID must reference existing account`` () =    
    let code = "AC-2.6"
    let name = "parent ID must reference existing account"
    let accountType = "Liability"
    let activeBegin = Clock.now()
    let activeEnd = None
    let subtype = None
    let parentId = Some (Guid.NewGuid())
    let reference = None
    let envelope = AuditEnvelope.create AccountCreate
    
    let mutable idToCleanUp = None
    try
        let result = (Account.constructNewAndSaveToDb code name accountType activeBegin activeEnd subtype parentId reference envelope)
        let didFail =
            match result with
            | Error e -> true
            | Ok x ->
                idToCleanUp <- Some (Account.id x)
                false
        Assert.True(didFail, "Account creation was allowed to succeed with invalid parent")
    finally
        match cleanUpAccountId idToCleanUp with
        | Ok () -> ()
        | Error e -> failwith e

[<Fact>]
let ``REQ-AC-2.7 parent account must be active at AuditEnvelope instant--positive`` () =
    let code_parent = "AC-2.7-P"
    let name_parent = "REQ-AC-2.7 parent account must be active at AuditEnvelope instant--positive"
    let accountType_parent = "Expense"
    let activeBegin_parent = (Clock.now()).Plus(Duration.FromDays(-700))
    let activeEnd_parent = None
    let subtype_parent = None
    let parentId_parent = None
    let reference_parent= None
    let envelope_parent = AuditEnvelope.create AccountCreate
    
    let code_child1 = "AC-2.7-C1"
    let name_child1 = "REQ-AC-2.7 parent account must be active at AuditEnvelope instant--positive"
    let accountType_child1 = "Expense"
    let activeBegin_child1 = Clock.now()
    let activeEnd_child1 = None
    let subtype_child1 = None    
    let reference_child1= None
    let envelope_child1 = AuditEnvelope.create AccountCreate
    
    let mutable idToCleanUp_parent = None
    let mutable idToCleanUp_child1 = None
    try
        let railroad = result {
            let! account_parent = Account.constructNewAndSaveToDb
                                      code_parent name_parent accountType_parent activeBegin_parent activeEnd_parent
                                      subtype_parent parentId_parent reference_parent envelope_parent
            let parentId = Account.id account_parent
            idToCleanUp_parent <- Some parentId
            
            let! account_child1 = Account.constructNewAndSaveToDb
                                      code_child1 name_child1 accountType_child1 activeBegin_child1 activeEnd_child1
                                      subtype_child1 (Some parentId) reference_child1 envelope_child1
            let id_child1 = Account.id account_child1
            idToCleanUp_child1 <- Some id_child1
            
            return ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e
    finally
        match cleanUpParentIdAndChildren idToCleanUp_parent [idToCleanUp_child1;] with
        | Ok () -> ()
        | Error e -> failwith e

[<Fact>]
let ``REQ-AC-2.7 parent account must be active at AuditEnvelope instant--negative`` () =
    let code_parent = "AC-2.7-P"
    let name_parent = "REQ-AC-2.7 parent account must be active at AuditEnvelope instant--negative"
    let accountType_parent = "Expense"
    let activeBegin_parent = (Clock.now()).Plus(Duration.FromDays(-700))
    let activeEnd_parent = (Clock.now()).Plus(Duration.FromDays(-1))
    let subtype_parent = None
    let parentId_parent = None
    let reference_parent= None
    let envelope_parent = AuditEnvelope.create AccountCreate
    
    let code_child1 = "AC-2.7-C1"
    let name_child1 = "REQ-AC-2.7 parent account must be active at AuditEnvelope instant--negative"
    let accountType_child1 = "Expense"
    let activeBegin_child1 = Clock.now()
    let activeEnd_child1 = None
    let subtype_child1 = None    
    let reference_child1= None
    let envelope_child1 = AuditEnvelope.create AccountCreate
    
    let mutable idToCleanUp_parent = None
    let mutable idToCleanUp_child1 = None
    try
        let railroad = result {
            let! account_parent = Account.constructNewAndSaveToDb
                                      code_parent name_parent accountType_parent activeBegin_parent (Some activeEnd_parent)
                                      subtype_parent parentId_parent reference_parent envelope_parent
            let parentId = Account.id account_parent
            idToCleanUp_parent <- Some parentId
            
            let account_child1 = Account.constructNewAndSaveToDb
                                      code_child1 name_child1 accountType_child1 activeBegin_child1 activeEnd_child1
                                      subtype_child1 (Some parentId) reference_child1 envelope_child1
            
            let! checkResult =
                match account_child1 with
                | Error _ -> Ok ()
                | Ok a ->
                    idToCleanUp_child1 <- Some (Account.id a)
                    Error "Child account creation was allowed to succeed with inactive parent"
            
            return ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e
    finally
        match cleanUpParentIdAndChildren idToCleanUp_parent [idToCleanUp_child1;] with
        | Ok () -> ()
        | Error e -> failwith e

[<Fact>]
let ``REQ-AC-2.19 child AccountType must match parent AccountType`` () =
    let code_parent = "AC-2.19-P"
    let name_parent = "child AccountType must match parent AccountType"
    let accountType_parent = "Expense"
    let activeBegin_parent = (Clock.now()).Plus(Duration.FromDays(-700))
    let activeEnd_parent = None
    let subtype_parent = None
    let parentId_parent = None
    let reference_parent= None
    let envelope_parent = AuditEnvelope.create AccountCreate
    
    let code_child1 = "AC-2.19-C1"
    let name_child1 = "child AccountType must match parent AccountType"
    let accountType_child1 = "Liability"
    let activeBegin_child1 = Clock.now()
    let activeEnd_child1 = None
    let subtype_child1 = None    
    let reference_child1= None
    let envelope_child1 = AuditEnvelope.create AccountCreate
    
    let mutable idToCleanUp_parent = None
    let mutable idToCleanUp_child1 = None
    try
        let railroad = result {
            let! account_parent = Account.constructNewAndSaveToDb
                                      code_parent name_parent accountType_parent activeBegin_parent activeEnd_parent
                                      subtype_parent parentId_parent reference_parent envelope_parent
            let parentId = Account.id account_parent
            idToCleanUp_parent <- Some parentId
            
            let account_child1 = Account.constructNewAndSaveToDb
                                      code_child1 name_child1 accountType_child1 activeBegin_child1 activeEnd_child1
                                      subtype_child1 (Some parentId) reference_child1 envelope_child1
            
            let! checkResult =
                match account_child1 with
                | Error _ -> Ok ()
                | Ok a ->
                    idToCleanUp_child1 <- Some (Account.id a)
                    Error "Child account creation was allowed to succeed with different account type from parent"
            
            return ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e
    finally
        match cleanUpParentIdAndChildren idToCleanUp_parent [idToCleanUp_child1;] with
        | Ok () -> ()
        | Error e -> failwith e

// =============================================================================
// Deactivation
// =============================================================================

[<Fact>]
let ``REQ-AC-4.1 deactivateAccount sets active end and returns inactive account`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-AC-4.2 deactivateAccount rejects end earlier than or equal to begin`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-AC-4.3 deactivateAccount rejects when active children exist`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-AC-4.5 deactivateAccount rejects already deactivated account`` () =
    Assert.Fail "not implemented"

// =============================================================================
// Updates
// =============================================================================

[<Fact>]
let ``REQ-AC-4.8 updateAccountName succeeds with valid name`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-AC-4.8 REQ-SYS-2.1 updateAccountName rejects invalid name`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-AC-4.9 updateExternalReference succeeds with valid reference`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-AC-4.9 updateExternalReference can clear to None`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-SYS-3.3 update operations set modifiedAt from AuditEnvelope`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-AC-4.19 update to deactivated account is permitted`` () =
    Assert.Fail "not implemented"
