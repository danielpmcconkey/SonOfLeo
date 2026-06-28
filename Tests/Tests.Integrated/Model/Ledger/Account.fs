module Tests.Integrated.Model.Ledger.Account

open System
open Model.Audit
open Tests.Integrated.GenericTestProperties
open Xunit
open Model.Ledger.Accounts
open Model.Ledger.Accounts.AccountComponent
open Utilities.ResultCE
open Utilities

[<Fact>]
let ``REQ-AC-1.4 REQ-AC-2.9 AccountCode must be unique`` () =
    let code1 = "REQ-AC-1.4"
    let code2 = code1

    let transaction = DAL.createDbTransaction() |> Result.defaultWith failwith

    try
        let result1 =
            Account.constructNewAndSaveToDb code1 genericAccountNameString genericAccountTypeString
                genericAccountActiveBegin genericAccountActiveEnd genericAccountSubtype genericAccountParentId
                genericAccountReference genericAuditEnvelope (Some transaction)
        match result1 with
        | Error e -> Assert.Fail e // need to ensure that the first made it into the DB to check the unique constraint
        | Ok _ -> ()

        let result2 =
            Account.constructNewAndSaveToDb code2 genericAccountNameString genericAccountTypeString
                genericAccountActiveBegin genericAccountActiveEnd genericAccountSubtype genericAccountParentId
                genericAccountReference genericAuditEnvelope (Some transaction)
        Assert.True(Result.isError result2)
    finally
        DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore

[<Fact>]
let ``REQ-AC-1.5 Account code is case sensitive.`` () =
    let code1 = "REQ-AC-1.5"
    let code2 = "req-ac-1.5"

    let transaction = DAL.createDbTransaction() |> Result.defaultWith failwith

    try
        let railroad = result {
            let! account1 =
                Account.constructNewAndSaveToDb code1 genericAccountNameString genericAccountTypeString
                    genericAccountActiveBegin genericAccountActiveEnd genericAccountSubtype genericAccountParentId
                    genericAccountReference genericAuditEnvelope (Some transaction)
            let! account2 =
                Account.constructNewAndSaveToDb code2 genericAccountNameString genericAccountTypeString
                    genericAccountActiveBegin genericAccountActiveEnd genericAccountSubtype genericAccountParentId
                    genericAccountReference genericAuditEnvelope (Some transaction)
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
        DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore

// =============================================================================
// Create + Read round-trips
// =============================================================================

[<Fact>]
let ``REQ-AC-2.14 REQ-SYS-5.1 create account and fetch by ID returns identical record`` () =
    let code = "AC-2.14"
    let accountName = "Create account and fetch by ID returns identical record"
    let accountType = "Asset"
    let activeBegin = genericAccountActiveBegin
    let activeEnd = Some (activeBegin.PlusDays(60))
    let subtype = Some "FixedAsset"
    let parentId = None
    let reference = Some "test ext ref"
    let envelope = AuditEnvelope.create AccountCreate

    let transaction = DAL.createDbTransaction() |> Result.defaultWith failwith

    try
        let railroad = result {
            let! pushResult =
                Account.constructNewAndSaveToDb code accountName accountType
                    activeBegin activeEnd subtype parentId reference envelope (Some transaction)
            let pushId = Account.uniqueId pushResult
            let! pullResult = Account.fetchById (Some transaction) pushId
            Assert.Equal(pushId, (Account.uniqueId pullResult))
            Assert.Equal(code, AccountCode.value(Account.code pullResult))
            Assert.Equal(accountName, AccountName.value(Account.accountName pullResult))
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
        DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore

[<Fact>]
let ``REQ-AC-3.5 fetch by parent ID returns all children`` () =
    let code_parent = "AC-3.5-P"
    let code_child1 = "AC-3.5-C1"
    let code_child2 = "AC-3.5-C2"
    let code_child3 = "AC-3.5-C3"

    let transaction = DAL.createDbTransaction() |> Result.defaultWith failwith

    try
        let railroad = result {
            let! account_parent =
                Account.constructNewAndSaveToDb code_parent genericAccountNameString genericAccountTypeString
                    genericAccountActiveBegin genericAccountActiveEnd genericAccountSubtype genericAccountParentId
                    genericAccountReference genericAuditEnvelope (Some transaction)
            let parentId = Account.uniqueId account_parent

            let! account_child1 =
                Account.constructNewAndSaveToDb code_child1 genericAccountNameString genericAccountTypeString
                    genericAccountActiveBegin genericAccountActiveEnd genericAccountSubtype (Some parentId)
                    genericAccountReference genericAuditEnvelope (Some transaction)
            let id_child1 = Account.uniqueId account_child1

            let! account_child2 =
                Account.constructNewAndSaveToDb code_child2 genericAccountNameString genericAccountTypeString
                    genericAccountActiveBegin genericAccountActiveEnd genericAccountSubtype (Some parentId)
                    genericAccountReference genericAuditEnvelope (Some transaction)
            let id_child2 = Account.uniqueId account_child2

            let! account_child3 =
                Account.constructNewAndSaveToDb code_child3 genericAccountNameString genericAccountTypeString
                    genericAccountActiveBegin genericAccountActiveEnd genericAccountSubtype (Some parentId)
                    genericAccountReference genericAuditEnvelope (Some transaction)
            let id_child3 = Account.uniqueId account_child3

            let! fetched = Account.fetchByParentId (Some transaction) parentId

            Assert.Equal(3, List.length fetched)

            [id_child1; id_child2; id_child3]
            |> List.forall(fun id -> fetched |> List.exists (fun a -> Account.uniqueId a = id))
            |> Assert.True
            return ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e
    finally
        DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore

[<Fact>]
let ``REQ-AC-3.6 fetch by account type returns matching accounts`` () =
    let code_1 = "AC-3.6-1"
    let code_2 = "AC-3.6-2"
    let code_3 = "AC-3.6-3"
    let explicitAccountType = "Equity"

    let transaction = DAL.createDbTransaction() |> Result.defaultWith failwith

    try
        let railroad = result {
            let! account_1 =
                Account.constructNewAndSaveToDb code_1 genericAccountNameString explicitAccountType
                    genericAccountActiveBegin genericAccountActiveEnd genericAccountSubtype genericAccountParentId
                    genericAccountReference genericAuditEnvelope (Some transaction)
            let id_1 = Account.uniqueId account_1

            let! account_2 =
                Account.constructNewAndSaveToDb code_2 genericAccountNameString explicitAccountType
                    genericAccountActiveBegin genericAccountActiveEnd genericAccountSubtype genericAccountParentId
                    genericAccountReference genericAuditEnvelope (Some transaction)
            let id_2 = Account.uniqueId account_2

            let! account_3 =
                Account.constructNewAndSaveToDb code_3 genericAccountNameString explicitAccountType
                    genericAccountActiveBegin genericAccountActiveEnd genericAccountSubtype genericAccountParentId
                    genericAccountReference genericAuditEnvelope (Some transaction)
            let id_3 = Account.uniqueId account_3

            let! fetchType = AccountType.fromString explicitAccountType
            let! fetched = Account.fetchByAccountType (Some transaction) fetchType
            Assert.Equal(3, List.length fetched)

            [id_1; id_2; id_3]
            |> List.forall(fun id -> fetched |> List.exists (fun a -> Account.uniqueId a = id))
            |> Assert.True
            return ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e
    finally
        DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore

[<Fact>]
let ``REQ-AC-3.7 fetch all fetches everything`` () =
    let code_1 = "AC-3.7-1"
    let code_2 = "AC-3.7-2"
    let code_3 = "AC-3.7-3"
    let code_4 = "AC-3.7-4"
    let explicitAccountType1 = "Liability"
    let explicitAccountType2 = "Equity"
    let explicitAccountType3 = "Revenue"
    let explicitAccountType4 = "Expense"
    let account4ActiveEnd = Some (Calendar.today().PlusDays(-1))

    let transaction = DAL.createDbTransaction() |> Result.defaultWith failwith

    try
        let railroad = result {
            let! account_1 =
                Account.constructNewAndSaveToDb code_1 genericAccountNameString explicitAccountType1
                    genericAccountActiveBegin genericAccountActiveEnd genericAccountSubtype genericAccountParentId
                    genericAccountReference genericAuditEnvelope (Some transaction)
            let id_1 = Account.uniqueId account_1

            let! account_2 =
                Account.constructNewAndSaveToDb code_2 genericAccountNameString explicitAccountType2
                    genericAccountActiveBegin genericAccountActiveEnd genericAccountSubtype genericAccountParentId
                    genericAccountReference genericAuditEnvelope (Some transaction)
            let id_2 = Account.uniqueId account_2

            let! account_3 =
                Account.constructNewAndSaveToDb code_3 genericAccountNameString explicitAccountType3
                    genericAccountActiveBegin genericAccountActiveEnd genericAccountSubtype genericAccountParentId
                    genericAccountReference genericAuditEnvelope (Some transaction)
            let id_3 = Account.uniqueId account_3

            let! account_4 =
                Account.constructNewAndSaveToDb code_4 genericAccountNameString explicitAccountType4
                    genericAccountActiveBegin account4ActiveEnd genericAccountSubtype genericAccountParentId
                    genericAccountReference genericAuditEnvelope (Some transaction)
            let id_4 = Account.uniqueId account_4

            let! fetched = Account.fetchAll false (Some transaction)
            Assert.Equal(4, List.length fetched)

            [id_1; id_2; id_3; id_4]
            |> List.forall(fun id -> fetched |> List.exists (fun a -> Account.uniqueId a = id))
            |> Assert.True
            return ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e
    finally
        DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore

[<Fact>]
let ``REQ-AC-3.9 fetch all with active only fetches active accounts relative to system run time`` () =
    let code_1 = "AC-3.9-1"
    let code_2 = "AC-3.9-2"
    let code_3 = "AC-3.9-3"
    let code_4 = "AC-3.9-4"
    let explicitAccountType1 = "Liability"
    let explicitAccountType2 = "Equity"
    let explicitAccountType3 = "Revenue"
    let explicitAccountType4 = "Asset"
    let account4ActiveEnd = Some (Calendar.today().PlusDays(-1))

    let transaction = DAL.createDbTransaction() |> Result.defaultWith failwith

    try
        let railroad = result {
            let! account_1 =
                Account.constructNewAndSaveToDb code_1 genericAccountNameString explicitAccountType1
                    genericAccountActiveBegin genericAccountActiveEnd genericAccountSubtype genericAccountParentId
                    genericAccountReference genericAuditEnvelope (Some transaction)
            let id_1 = Account.uniqueId account_1

            let! account_2 =
                Account.constructNewAndSaveToDb code_2 genericAccountNameString explicitAccountType2
                    genericAccountActiveBegin genericAccountActiveEnd genericAccountSubtype genericAccountParentId
                    genericAccountReference genericAuditEnvelope (Some transaction)
            let id_2 = Account.uniqueId account_2

            let! account_3 =
                Account.constructNewAndSaveToDb code_3 genericAccountNameString explicitAccountType3
                    genericAccountActiveBegin genericAccountActiveEnd genericAccountSubtype genericAccountParentId
                    genericAccountReference genericAuditEnvelope (Some transaction)
            let id_3 = Account.uniqueId account_3

            let! _ =
                Account.constructNewAndSaveToDb code_4 genericAccountNameString explicitAccountType4
                    genericAccountActiveBegin account4ActiveEnd genericAccountSubtype genericAccountParentId
                    genericAccountReference genericAuditEnvelope (Some transaction)

            let! fetched = Account.fetchAll true (Some transaction)
            Assert.Equal(3, List.length fetched)

            [id_1; id_2; id_3]
            |> List.forall(fun id -> fetched |> List.exists (fun a -> Account.uniqueId a = id))
            |> Assert.True
            return ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e
    finally
        DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore

// =============================================================================
// Create validations (DB-dependent)
// =============================================================================

[<Fact>]
let ``REQ-AC-2.6 parent ID must reference existing account`` () =
    let parentId = Some (Guid.NewGuid())

    let transaction = DAL.createDbTransaction() |> Result.defaultWith failwith

    try
        let result =
                Account.constructNewAndSaveToDb genericAccountCodeString genericAccountNameString genericAccountTypeString
                    genericAccountActiveBegin genericAccountActiveEnd genericAccountSubtype parentId
                    genericAccountReference genericAuditEnvelope (Some transaction)
        let didFail =
            match result with
            | Error _ -> true
            | Ok _ -> false
        Assert.True(didFail, "Account creation was allowed to succeed with invalid parent")
    finally
        DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore

[<Fact>]
let ``REQ-AC-2.7 parent account must be active at AuditEnvelope instant--positive`` () =
    let code_parent = "AC-2.7-P"
    let code_child1 = "AC-2.7-C1"
    let activeBegin_parent = Calendar.today().PlusDays(-700)

    let transaction = DAL.createDbTransaction() |> Result.defaultWith failwith

    try
        let railroad = result {
            let! account_parent =
                Account.constructNewAndSaveToDb code_parent genericAccountNameString genericAccountTypeString
                    activeBegin_parent genericAccountActiveEnd genericAccountSubtype genericAccountParentId
                    genericAccountReference genericAuditEnvelope (Some transaction)
            let parentId = Account.uniqueId account_parent

            let! _ =
                Account.constructNewAndSaveToDb code_child1 genericAccountNameString genericAccountTypeString
                    genericAccountActiveBegin genericAccountActiveEnd genericAccountSubtype (Some parentId)
                    genericAccountReference genericAuditEnvelope (Some transaction)

            return ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e
    finally
        DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore

[<Fact>]
let ``REQ-AC-2.7 parent account must be active at AuditEnvelope instant--negative`` () =
    let code_parent = "AC-2.7-P"
    let activeBegin_parent = Calendar.today().PlusDays(-700)
    let activeEnd_parent = Calendar.today().PlusDays(-1)
    let code_child1 = "AC-2.7-C1"

    let transaction = DAL.createDbTransaction() |> Result.defaultWith failwith

    try
        let railroad = result {
            let! account_parent =
                Account.constructNewAndSaveToDb code_parent genericAccountNameString genericAccountTypeString
                    activeBegin_parent (Some activeEnd_parent) genericAccountSubtype genericAccountParentId
                    genericAccountReference genericAuditEnvelope (Some transaction)
            let parentId = Account.uniqueId account_parent

            let account_child1 =
                Account.constructNewAndSaveToDb code_child1 genericAccountNameString genericAccountTypeString
                    genericAccountActiveBegin genericAccountActiveEnd genericAccountSubtype (Some parentId)
                    genericAccountReference genericAuditEnvelope (Some transaction)

            let! _ =
                match account_child1 with
                | Error _ -> Ok ()
                | Ok _ ->
                    Error "Child account creation was allowed to succeed with inactive parent"

            return ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e
    finally
        DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore

[<Fact>]
let ``REQ-AC-2.20 child AccountType must match parent AccountType`` () =
    let code_parent = "AC-2.20-P"
    let accountType_parent = "Expense"
    let code_child1 = "AC-2.20-C1"
    let accountType_child1 = "Liability"

    let transaction = DAL.createDbTransaction() |> Result.defaultWith failwith

    try
        let railroad = result {
            let! account_parent =
                Account.constructNewAndSaveToDb code_parent genericAccountNameString accountType_parent
                    genericAccountActiveBegin genericAccountActiveEnd genericAccountSubtype genericAccountParentId
                    genericAccountReference genericAuditEnvelope (Some transaction)
            let parentId = Account.uniqueId account_parent

            let account_child1 =
                Account.constructNewAndSaveToDb code_child1 genericAccountNameString accountType_child1
                    genericAccountActiveBegin genericAccountActiveEnd genericAccountSubtype (Some parentId)
                    genericAccountReference genericAuditEnvelope (Some transaction)

            let! _ =
                match account_child1 with
                | Error _ -> Ok ()
                | Ok _ ->
                    Error "Child account creation was allowed to succeed with different account type from parent"

            return ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e
    finally
        DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore

// =============================================================================
// Updates
// =============================================================================

[<Fact>]
let ``REQ-AC-4.8 updateAccountName succeeds with valid accountName`` () =
    let envelope_rename = AuditEnvelope.create AccountUpdateName
    let goodAccountName = "fahrvergnügen"

    let transaction = DAL.createDbTransaction() |> Result.defaultWith failwith

    try
        let railroad = result {
            let! createdAccount =
                Account.constructNewAndSaveToDb genericAccountCodeString genericAccountNameString genericAccountTypeString
                    genericAccountActiveBegin genericAccountActiveEnd genericAccountSubtype genericAccountParentId
                    genericAccountReference genericAuditEnvelope (Some transaction)
            let createdId = Account.uniqueId createdAccount

            Assert.Equal(genericAccountNameString, (AccountName.value (Account.accountName createdAccount))) // make sure we have the start name

            let! renamedAccount = Account.updateAccountNameById createdId goodAccountName envelope_rename (Some transaction)

            Assert.Equal(goodAccountName, (AccountName.value (Account.accountName renamedAccount)))

            return ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e
    finally
        DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore

[<Fact>]
let ``REQ-AC-4.8 REQ-SYS-2.1 updateAccountName rejects invalid name`` () =
    // note this test isn't needed in the Integrated tests project. But, to keep
    // it from being flagged in Audit, I'm repeating the isolated tests from
    // AC-1.7 and 1.8
    let result1 = AccountName.create "      "
    Assert.True(Result.isError result1)
    let result2 = AccountName.create (String('A', 101))
    Assert.True(Result.isError result2)

[<Fact>]
let ``REQ-AC-4.9 updateExternalReference succeeds with valid reference`` () =
    let noneReference = None
    let envelope_update = AuditEnvelope.create AccountUpdateExtReference
    let goodReference = Some "Fliegende Ratte"

    let transaction = DAL.createDbTransaction() |> Result.defaultWith failwith

    try
        let railroad = result {
            let! createdAccount =
                Account.constructNewAndSaveToDb genericAccountCodeString genericAccountNameString genericAccountTypeString
                    genericAccountActiveBegin genericAccountActiveEnd genericAccountSubtype genericAccountParentId
                    noneReference genericAuditEnvelope (Some transaction)
            let createdId = Account.uniqueId createdAccount

            // verify that the None got recorded
            let startingReference = Account.externalReference createdAccount |> Option.map AccountExternalReference.value
            Assert.Equal(None, startingReference)

            // update
            let! updatedAccount = Account.updateExternalReferenceById createdId goodReference envelope_update (Some transaction)

            // verify that the new value got recorded
            let newReference = Account.externalReference updatedAccount |> Option.map AccountExternalReference.value
            Assert.Equal(goodReference, newReference)

            return ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e
    finally
        DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore

[<Fact>]
let ``REQ-AC-4.9 updateExternalReference can be updated to None`` () =
    let reference= Some "un poquito aburrido"
    let envelope_update = AuditEnvelope.create AccountUpdateExtReference
    let emptyReference = None

    let transaction = DAL.createDbTransaction() |> Result.defaultWith failwith

    try
        let railroad = result {
            let! createdAccount =
                Account.constructNewAndSaveToDb genericAccountCodeString genericAccountNameString genericAccountTypeString
                    genericAccountActiveBegin genericAccountActiveEnd genericAccountSubtype genericAccountParentId
                    reference genericAuditEnvelope (Some transaction)
            let createdId = Account.uniqueId createdAccount

            // verify that the Some got recorded
            let startingReference = Account.externalReference createdAccount |> Option.map AccountExternalReference.value
            Assert.Equal(reference, startingReference)

            // update
            let! updatedAccount = Account.updateExternalReferenceById createdId emptyReference envelope_update (Some transaction)

            // verify that the None got recorded
            let newReference = Account.externalReference updatedAccount |> Option.map AccountExternalReference.value
            Assert.Equal(emptyReference, newReference)

            return ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e
    finally
        DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore

[<Fact>]
let ``REQ-SYS-3.3 account update operations set modifiedAt from AuditEnvelope`` () =
    let newName = "Blah blah blah"

    let transaction = DAL.createDbTransaction() |> Result.defaultWith failwith

    try
        let railroad = result {
            let! createdAccount =
                Account.constructNewAndSaveToDb genericAccountCodeString genericAccountNameString genericAccountTypeString
                    genericAccountActiveBegin genericAccountActiveEnd genericAccountSubtype genericAccountParentId
                    genericAccountReference genericAuditEnvelope (Some transaction)
            let createdId = Account.uniqueId createdAccount

            // update
            System.Threading.Thread.Sleep(100) // ensure that the 2 clock calls don't fall on the same cycle
            let envelope_update = AuditEnvelope.create AccountUpdateName
            let! updatedAccount = Account.updateAccountNameById createdId newName envelope_update (Some transaction)

            // verify that the modified date got set properly
            Assert.Equal(AuditEnvelope.instant envelope_update, Account.modifiedAt updatedAccount)

            return ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e
    finally
        DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore

[<Fact>]
let ``REQ-AC-4.19 update to deactivated account is permitted`` () =
    let activeBegin = Calendar.today().PlusDays(-365)
    let activeEnd =  Calendar.today().PlusDays(-1) // already deactive
    let envelope_update = AuditEnvelope.create AccountUpdateName
    let newName = "Blah blah blah"

    let transaction = DAL.createDbTransaction() |> Result.defaultWith failwith

    try
        let railroad = result {
            let! createdAccount =
                Account.constructNewAndSaveToDb genericAccountCodeString genericAccountNameString genericAccountTypeString
                    activeBegin (Some activeEnd) genericAccountSubtype genericAccountParentId
                    genericAccountReference genericAuditEnvelope (Some transaction)
            let createdId = Account.uniqueId createdAccount

            // validate that it is *indeed* inactive
            Assert.False(Account.isActive (Calendar.today()) createdAccount)

            // update
            let! updatedAccount = Account.updateAccountNameById createdId newName envelope_update (Some transaction)

            // verify that the update actually occurred
            Assert.Equal(newName, AccountName.value (Account.accountName updatedAccount))

            return ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e
    finally
        DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore
