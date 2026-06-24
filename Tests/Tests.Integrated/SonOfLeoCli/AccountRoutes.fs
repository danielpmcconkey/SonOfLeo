module Tests.Integrated.SonOfLeoCli.AccountRoutes

open Model.Ledger.Accounts
open Model.Ledger.Accounts.AccountComponent
open Model.UI.InterfaceContractTypes
open Model.UI.Json
open Tests.Integrated.GenericTestProperties
open Tests.Integrated.SonOfLeoCli.CliExecutor
open Utilities
open Xunit
open Utilities.ResultCE
open Tests.Integrated._Cleanup

let private createAccountInput codeToUse =
    { code = codeToUse; name = genericAccountNameString; accountTypeSt = genericAccountTypeString
      activeBegin =  genericAccountActiveBegin; activeEnd = genericAccountActiveEnd; subType = genericAccountSubtype
      parentCode = genericAccountParentCode; reference = genericAccountReference }

let private createAccountInDb codeToUse = 
    Account.constructNewAndSaveToDbUsingParentId codeToUse genericAccountNameString genericAccountTypeString
                    genericAccountActiveBegin genericAccountActiveEnd genericAccountSubtype genericAccountParentId
                    genericAccountReference genericAuditEnvelope None


// =============================================================================
// Create
// =============================================================================


[<Fact>]
let ``REQ-AC-2.21 Account Create happy path`` () =    
    let accountInput = createAccountInput genericAccountCodeString
    let args = ["Account"; "Create"]
    let payload = accountInput |> toJson<AccountCreateInput> |> Result.defaultWith failwith
    let code, a, e = runCli args payload
    match code with
    | 0 ->
        let accountReturn:AccountReturn = fromJson<AccountReturn> a |> Result.defaultWith failwith
        let cleanUpId = accountReturn.code |> Account.fetchIdByCode None |> Result.defaultWith failwith
        cleanUpAccountId (Some cleanUpId) |> Result.defaultWith failwith
    | _ ->
        Assert.Fail $"Create Account happy path returned a non-zero value: {e}"

[<Fact>]
let ``REQ-NGUI-1.5 Account Create fails with invalid parent code`` () =
    let badAccountCode = Some "BullS**t"
    let expectedReturnCode = 1
    let expectedError = "Execute scalar returned null in fetchIdByCode"
    let railroad = result {
        let accountInput =
            { code = genericAccountCodeString; name = genericAccountNameString; accountTypeSt = genericAccountTypeString
              activeBegin =  genericAccountActiveBegin; activeEnd = genericAccountActiveEnd; subType = genericAccountSubtype
              parentCode = badAccountCode; reference = genericAccountReference }
        let! payload = accountInput |> toJson<AccountCreateInput>  
        let args = ["Account"; "Create"]
        let code, _, e = runCli args payload
        Assert.Equal(expectedReturnCode, code)
        Assert.Contains(expectedError, e)
        return ()
    }
    match railroad with
    | Ok _ -> ()
    | Error e -> Assert.Fail e

// =============================================================================
// Read
// =============================================================================

[<Fact>]
let ``REQ-AC-3.4 Account FetchByCode happy path`` () =
    let explicitCode = "PrinceAli"
    let accountResult = createAccountInDb explicitCode
    match accountResult with
    | Error e -> Assert.Fail $"Account setup failed: {e}"
    | Ok account ->
        let accountId = Account.uniqueId account
        let payload = { code = explicitCode } |> toJson<AccountFetchByCodeInput>  |> Result.defaultWith failwith
        let args = ["Account"; "FetchByCode"]
        let code, _, e = runCli args payload
        cleanUpAccountId (Some accountId) |> Result.defaultWith failwith // clean up regardless of outcome because the account's been written
        match code with
        | 0 -> ()
        | _ -> Assert.Fail $"Account FetchByCode happy path returned a non-zero value: {e}"

[<Fact>]
let ``REQ-AC-3.10 Account FetchByParentCode happy path`` () =
    let code_parent = "AC-3.10-P"
    let code_child1 = "AC-3.10-C1"
    let explicitName = "Fabulous he, Ali Ababwa"
    let mutable idToCleanUp_parent = None
    let mutable idToCleanUp_child1 = None
    try
        let railroad = result {
            let! account_parent = 
                Account.constructNewAndSaveToDbUsingParentId code_parent genericAccountNameString genericAccountTypeString
                    genericAccountActiveBegin genericAccountActiveEnd genericAccountSubtype genericAccountParentId
                    genericAccountReference genericAuditEnvelope None
            let parentCode = Account.code account_parent
            let parentCodeString = parentCode |> AccountCode.value
            let parentId = Account.uniqueId account_parent
            idToCleanUp_parent <- Some parentId
            
            let! account_child1 = 
                Account.constructNewAndSaveToDbUsingParentId code_child1 explicitName genericAccountTypeString
                    genericAccountActiveBegin genericAccountActiveEnd genericAccountSubtype (Some parentId)
                    genericAccountReference genericAuditEnvelope None
            let id_child1 = Account.uniqueId account_child1
            idToCleanUp_child1 <- Some id_child1
            
            let! payload = { parentCode = parentCodeString } |> toJson<AccountFetchByParentCodeInput> 
            let args = ["Account"; "FetchByParentCode"]
            let code, fetchedAccountRecords, e = runCli args payload
            
            do! if code <> 0
                then Error $"FetchByParentCode CLI returned {code}: {e}"
                else Ok ()
            
            let! fetchedChildren = fromJson<AccountReturn list> fetchedAccountRecords
            let fetchedChild = Assert.Single(fetchedChildren)
            Assert.Equal(explicitName, fetchedChild.name)
            
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
let ``REQ-NGUI-1.5 Account FetchByParentCode fails with invalid code`` () =
    let badAccountCode = "HorseS**t"
    let expectedReturnCode = 1
    let expectedError = "Execute scalar returned null in fetchIdByCode"
    let railroad = result {
        let! payload = { parentCode = badAccountCode } |> toJson<AccountFetchByParentCodeInput> 
        let args = ["Account"; "FetchByParentCode"]
        let code, _, e = runCli args payload
        Assert.Equal(expectedReturnCode, code)
        Assert.Contains(expectedError, e)
        return ()
    }
    match railroad with
    | Ok _ -> ()
    | Error e -> Assert.Fail e

[<Fact>]
let ``REQ-AC-3.6 Account FetchByAccountType happy path`` () =
    let code_1 = "AC-3.6-1"
    let code_2 = "AC-3.6-2"
    let code_3 = "AC-3.6-3"
    let explicitType = "Revenue"
    let mutable idToCleanUp_1 = None
    let mutable idToCleanUp_2 = None
    let mutable idToCleanUp_3 = None
    try
        let railroad = result {
            let! account_1 = 
                Account.constructNewAndSaveToDbUsingParentId code_1 genericAccountNameString explicitType
                    genericAccountActiveBegin genericAccountActiveEnd genericAccountSubtype genericAccountParentId
                    genericAccountReference genericAuditEnvelope None
            idToCleanUp_1 <- account_1 |> Account.uniqueId |> Some
            
            let! account_2 = 
                Account.constructNewAndSaveToDbUsingParentId code_2 genericAccountNameString explicitType
                    genericAccountActiveBegin genericAccountActiveEnd genericAccountSubtype genericAccountParentId
                    genericAccountReference genericAuditEnvelope None
            idToCleanUp_2 <- account_2 |> Account.uniqueId |> Some
            
            let! account_3 = 
                Account.constructNewAndSaveToDbUsingParentId code_3 genericAccountNameString explicitType
                    genericAccountActiveBegin genericAccountActiveEnd genericAccountSubtype genericAccountParentId
                    genericAccountReference genericAuditEnvelope None
            idToCleanUp_3 <- account_3 |> Account.uniqueId |> Some
            
            let! payload = { accountTypeSt = explicitType } |> toJson<AccountFetchByAccountTypeInput> 
            let args = ["Account"; "FetchByAccountType"]
            let code, fetchedAccountRecords, e = runCli args payload
            
            do! if code <> 0
                then Error $"FetchByAccountType CLI returned {code}: {e}"
                else Ok ()
            
            let! fetchedAccounts = fromJson<AccountReturn list> fetchedAccountRecords            
            Assert.Equal(3, fetchedAccounts |> List.length)
            
            let filtered = fetchedAccounts |> List.filter(fun x -> x.accountTypeSt = explicitType)
            Assert.Equal(3, filtered |> List.length)
            
            return ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e
    finally
        match cleanUpAccountList [idToCleanUp_1; idToCleanUp_2; idToCleanUp_3;] with
        | Ok () -> ()
        | Error e -> failwith e

[<Fact>]
let ``REQ-AC-3.7 Account FetchAll happy path`` () =
    let code_1 = "AC-3.7-1"
    let code_2 = "AC-3.7-2"
    let code_3 = "AC-3.7-3"
    let explicitType1 = "Revenue"
    let explicitType2 = "Expense"
    let explicitType3 = "Equity"
    let mutable idToCleanUp_1 = None
    let mutable idToCleanUp_2 = None
    let mutable idToCleanUp_3 = None
    try
        let railroad = result {
            let! account_1 = 
                Account.constructNewAndSaveToDbUsingParentId code_1 genericAccountNameString explicitType1
                    genericAccountActiveBegin genericAccountActiveEnd genericAccountSubtype genericAccountParentId
                    genericAccountReference genericAuditEnvelope None
            idToCleanUp_1 <- account_1 |> Account.uniqueId |> Some
            
            let! account_2 = 
                Account.constructNewAndSaveToDbUsingParentId code_2 genericAccountNameString explicitType2
                    genericAccountActiveBegin genericAccountActiveEnd genericAccountSubtype genericAccountParentId
                    genericAccountReference genericAuditEnvelope None
            idToCleanUp_2 <- account_2 |> Account.uniqueId |> Some
            
            let! account_3 = 
                Account.constructNewAndSaveToDbUsingParentId code_3 genericAccountNameString explicitType3
                    genericAccountActiveBegin genericAccountActiveEnd genericAccountSubtype genericAccountParentId
                    genericAccountReference genericAuditEnvelope None
            idToCleanUp_3 <- account_3 |> Account.uniqueId |> Some
            
            let! payload = { activeOnly = false } |> toJson<AccountFetchAllInput> 
            let args = ["Account"; "FetchAll"]
            let code, fetchedAccountRecords, e = runCli args payload
            
            do! if code <> 0
                then Error $"FetchAll CLI returned {code}: {e}"
                else Ok ()
            
            let! fetchedAccounts = fromJson<AccountReturn list> fetchedAccountRecords
            Assert.Equal(3, fetchedAccounts |> List.length)
            
            let filtered1 = fetchedAccounts |> List.filter(fun x -> x.accountTypeSt = explicitType1)
            Assert.Equal(1, filtered1 |> List.length)
            let filtered2 = fetchedAccounts |> List.filter(fun x -> x.accountTypeSt = explicitType2)
            Assert.Equal(1, filtered2 |> List.length)
            let filtered3 = fetchedAccounts |> List.filter(fun x -> x.accountTypeSt = explicitType3)
            Assert.Equal(1, filtered3 |> List.length)
            
            return ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e
    finally
        match cleanUpAccountList [idToCleanUp_1; idToCleanUp_2; idToCleanUp_3;] with
        | Ok () -> ()
        | Error e -> failwith e

// =============================================================================
// Update
// =============================================================================

[<Fact>]
let ``REQ-AC-4.1 Account Deactivate happy path`` () =
    let now = Calendar.today()
    let endDate = now.PlusDays(-1)
    let mutable idToCleanUp_1 = None
    try
        let railroad = result {
            let! account = createAccountInDb genericAccountCodeString
            let accountId = Account.uniqueId account
            idToCleanUp_1 <- Some accountId
            let! payload = { code = genericAccountCodeString; activeEnd = endDate } |> toJson<AccountDeactivationInput>
            let args = ["Account"; "Deactivate"]
            let code, accountReturnString, e = runCli args payload
            do! if code = 1 then Error e else Ok ()
            let! accountReturn = accountReturnString |> fromJson<AccountReturn>            
            Assert.Equal(Some endDate, accountReturn.activeEnd)
            return ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e
    finally
        match cleanUpAccountId idToCleanUp_1 with
        | Ok () -> ()
        | Error e -> failwith e

[<Fact>]
let ``REQ-NGUI-1.5 Account Deactivate fails with invalid code`` () =
    let badAccountCode = "BatS**t"
    let now = Calendar.today()
    let activeEndInstant = now.PlusDays(-1)
    let expectedReturnCode = 1
    let expectedError = "Execute scalar returned null in fetchIdByCode"
    let railroad = result {
        let! payload = { code = badAccountCode; activeEnd = activeEndInstant } |> toJson<AccountDeactivationInput>
        let args = ["Account"; "Deactivate"]
        let code, _, e = runCli args payload
        Assert.Equal(expectedReturnCode, code)
        Assert.Contains(expectedError, e)
        return ()
    }
    match railroad with
    | Ok _ -> ()
    | Error e -> Assert.Fail e

[<Fact>]
let ``REQ-AC-4.8 Account UpdateName happy path`` () =
    let newName = "He's got the monkeys, let's see the monkeys"
    let mutable idToCleanUp_1 = None
    try
        let railroad = result {
            let! account = createAccountInDb genericAccountCodeString
            let accountId = Account.uniqueId account
            idToCleanUp_1 <- Some accountId
            let! payload = { code = genericAccountCodeString; newName = newName } |> toJson<AccountUpdateNameInput>
            let args = ["Account"; "UpdateName"]
            let code, accountReturnString, _ = runCli args payload
            Assert.Equal(0, code)
            let! accountReturn = accountReturnString |> fromJson<AccountReturn>
            Assert.Equal(newName, accountReturn.name)
            return ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e
    finally
        match cleanUpAccountId idToCleanUp_1 with
        | Ok () -> ()
        | Error e -> failwith e

[<Fact>]
let ``REQ-NGUI-1.5 Account UpdateName fails with invalid code`` () =
    let badAccountCode = "ApeS**t"
    let newName = "I picked the wrong day to quit sniffing glue"
    let expectedReturnCode = 1
    let expectedError = "Execute scalar returned null in fetchIdByCode"
    let railroad = result {
        let! payload = { code = badAccountCode; newName = newName } |> toJson<AccountUpdateNameInput>
        let args = ["Account"; "UpdateName"]
        let code, _, e = runCli args payload
        Assert.Equal(expectedReturnCode, code)
        Assert.Contains(expectedError, e)
        return ()
    }
    match railroad with
    | Ok _ -> ()
    | Error e -> Assert.Fail e

[<Fact>]
let ``REQ-AC-4.9 Account UpdateExternalReference happy path`` () =
    let newReference = Some "Genuflect, show some respect"
    let mutable idToCleanUp_1 = None
    try
        let railroad = result {
            let! account = createAccountInDb genericAccountCodeString
            let accountId = Account.uniqueId account
            idToCleanUp_1 <- Some accountId
            let! payload = { code = genericAccountCodeString; newReference = newReference } |> toJson<AccountUpdateExternalReferenceInput>
            let args = ["Account"; "UpdateExternalReference"]
            let code, accountReturnString, _ = runCli args payload
            Assert.Equal(0, code)
            let! accountReturn = accountReturnString |> fromJson<AccountReturn>
            Assert.Equal(newReference, accountReturn.reference)
            return ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e
    finally
        match cleanUpAccountId idToCleanUp_1 with
        | Ok () -> ()
        | Error e -> failwith e

[<Fact>]
let ``REQ-NGUI-1.5 Account UpdateExternalReference fails with invalid code`` () =
    let badAccountCode = "DogS**t"
    let newReference = Some "I'm not bad; I'm just drawn that way"
    let expectedReturnCode = 1
    let expectedError = "Execute scalar returned null in fetchIdByCode"
    let railroad = result {
        let! payload = { code = badAccountCode; newReference = newReference } |> toJson<AccountUpdateExternalReferenceInput>
        let args = ["Account"; "UpdateExternalReference"]
        let code, _, e = runCli args payload
        Assert.Equal(expectedReturnCode, code)
        Assert.Contains(expectedError, e)
        return ()
    }
    match railroad with
    | Ok _ -> ()
    | Error e -> Assert.Fail e
