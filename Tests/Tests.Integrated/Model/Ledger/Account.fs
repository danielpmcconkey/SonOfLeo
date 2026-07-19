namespace Tests.Integrated.Model.Ledger

open System
open Model.Audit
open Tests.Integrated
open Xunit
open Model.Ledger.Accounts
open Model.Ledger.Accounts.AccountComponent
open Utilities
//
// [<Collection("SharedTestData")>]
// type AccountTests(fixture: TestDataFixture) =
//
//     [<Fact>]
//     member _.``REQ-AC-1.4 REQ-AC-2.9 AccountCode must be unique`` () =
//         let transaction = DAL.createDbTransaction() |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
//
//         try
//             let duplicateResult =
//                 Account.constructNewAndSaveToDb "F-1250" genericAccountNameString genericAccountTypeString
//                     genericAccountActiveBegin genericAccountActiveEnd genericAccountSubtype genericAccountParentId
//                     genericAccountReference genericAuditEnvelope (Some transaction)
//             Assert.True(Result.isError duplicateResult)
//         finally
//             DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore
//
//     [<Fact>]
//     member _.``REQ-AC-1.5 Account code is case sensitive.`` () =
//         let transaction = DAL.createDbTransaction() |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
//
//         try
//             let railroad = result {
//                 let! lowercaseAccount =
//                     Account.constructNewAndSaveToDb "f-1000" genericAccountNameString genericAccountTypeString
//                         genericAccountActiveBegin genericAccountActiveEnd genericAccountSubtype genericAccountParentId
//                         genericAccountReference genericAuditEnvelope (Some transaction)
//                 Assert.NotEqual(fixture.Data.assets1000Id, Account.accountId lowercaseAccount)
//                 return ()
//             }
//             match railroad with
//             | Ok _ -> ()
//             | Error e -> Assert.Fail (AppError.toMessage e)
//         finally
//             DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore
//
//     // =============================================================================
//     // Create + Read round-trips
//     // =============================================================================
//
//     [<Fact>]
//     member _.``REQ-AC-2.14 REQ-SYS-5.1 create account and fetch by ID returns identical record`` () =
//         let code = "AC-2.14"
//         let accountName = "Create account and fetch by ID returns identical record"
//         let accountType = "Asset"
//         let activeBegin = genericAccountActiveBegin
//         let activeEnd = Some (activeBegin.PlusDays(60))
//         let subtype = Some "FixedAsset"
//         let parentId = None
//         let reference = Some "test ext ref"
//         let envelope = AuditEnvelope.create AccountCreate
//
//         let transaction = DAL.createDbTransaction() |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
//
//         try
//             let railroad = result {
//                 let! pushResult =
//                     Account.constructNewAndSaveToDb code accountName accountType
//                         activeBegin activeEnd subtype parentId reference envelope (Some transaction)
//                 let pushId = Account.accountId pushResult
//                 let! pullResult = Account.fetchById (Some transaction) pushId
//                 Assert.Equal(pushId, (Account.accountId pullResult))
//                 Assert.Equal(code, AccountCode.value(Account.code pullResult))
//                 Assert.Equal(accountName, AccountName.value(Account.accountName pullResult))
//                 Assert.Equal(accountType, AccountType.toString(Account.accountType pullResult))
//                 Assert.Equal(activeBegin, Account.activeBegin pullResult)
//                 Assert.Equal(activeEnd, Account.activeEnd pullResult)
//                 let! pullSubtype =
//                     match Account.accountSubType pullResult with
//                     | None -> Error "pulled subtype was null when it shouldn't have been"
//                     | Some x -> Ok (AccountSubtype.toString x)
//                 Assert.Equal(Option.get subtype, pullSubtype)
//                 Assert.Null(Account.parentId pullResult)
//                 let! pullReference =
//                     match Account.externalReference pullResult with
//                     | None -> Error "pulled external reference was null when it shouldn't have been"
//                     | Some x -> Ok (AccountExternalReference.value x)
//                 Assert.Equal(Option.get reference, pullReference)
//                 return ()
//             }
//             match railroad with
//             | Ok _ -> ()
//             | Error e -> Assert.Fail (AppError.toMessage e)
//         finally
//             DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore
//
//     [<Fact>]
//     member _.``REQ-AC-3.5 fetch by parent ID returns all children`` () =
//         let parentId = fixture.Data.assets1000Id
//         let expectedChildren =
//             [ fixture.Data.rothIra1250Id
//               fixture.Data.moneyMarket1270Id
//               fixture.Data.closedBank1290Id ]
//
//         let railroad = result {
//             let! fetched = Account.fetchByParentId None parentId
//             Assert.Equal(3, List.length fetched)
//             expectedChildren
//             |> List.forall (fun id -> fetched |> List.exists (fun a -> Account.accountId a = id))
//             |> Assert.True
//             return ()
//         }
//         match railroad with
//         | Ok _ -> ()
//         | Error e -> Assert.Fail (AppError.toMessage e)
//
//     [<Fact>]
//     member _.``REQ-AC-3.6 fetch by account type returns matching accounts`` () =
//         let railroad = result {
//             let! fetchType = AccountType.fromString "Equity"
//             let! fetched = Account.fetchByAccountType None fetchType
//
//             let expectedIds = [ fixture.Data.equity3000Id; fixture.Data.retirement3030Id ]
//             expectedIds
//             |> List.forall (fun id -> fetched |> List.exists (fun a -> Account.accountId a = id))
//             |> Assert.True
//
//             fetched
//             |> List.forall (fun a -> Account.accountType a = fetchType)
//             |> Assert.True
//             return ()
//         }
//         match railroad with
//         | Ok _ -> ()
//         | Error e -> Assert.Fail (AppError.toMessage e)
//
//     [<Fact>]
//     member _.``REQ-AC-3.7 fetch all fetches everything`` () =
//         let railroad = result {
//             let! fetched = Account.fetchAll false None
//
//             let expectedIds =
//                 [ fixture.Data.assets1000Id; fixture.Data.liabilities2000Id
//                   fixture.Data.equity3000Id; fixture.Data.revenue4000Id
//                   fixture.Data.expenses5000Id; fixture.Data.closedBank1290Id ]
//             expectedIds
//             |> List.forall (fun id -> fetched |> List.exists (fun a -> Account.accountId a = id))
//             |> Assert.True
//             return ()
//         }
//         match railroad with
//         | Ok _ -> ()
//         | Error e -> Assert.Fail (AppError.toMessage e)
//
//     [<Fact>]
//     member _.``REQ-AC-3.9 fetch all with active only fetches active accounts relative to system run time`` () =
//         let railroad = result {
//             let! fetched = Account.fetchAll true None
//
//             fixture.Data.closedBank1290Id
//             |> fun closedId -> fetched |> List.exists (fun a -> Account.accountId a = closedId)
//             |> Assert.False
//
//             let activeIds =
//                 [ fixture.Data.assets1000Id; fixture.Data.moneyMarket1270Id
//                   fixture.Data.food5350Id ]
//             activeIds
//             |> List.forall (fun id -> fetched |> List.exists (fun a -> Account.accountId a = id))
//             |> Assert.True
//             return ()
//         }
//         match railroad with
//         | Ok _ -> ()
//         | Error e -> Assert.Fail (AppError.toMessage e)
//
//     // =============================================================================
//     // Create validations (DB-dependent)
//     // =============================================================================
//
//     [<Fact>]
//     member _.``REQ-AC-2.6 parent ID must reference existing account`` () =
//         let parentId = Some (Guid.NewGuid())
//
//         let transaction = DAL.createDbTransaction() |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
//
//         try
//             let result =
//                     Account.constructNewAndSaveToDb genericAccountCodeString genericAccountNameString genericAccountTypeString
//                         genericAccountActiveBegin genericAccountActiveEnd genericAccountSubtype parentId
//                         genericAccountReference genericAuditEnvelope (Some transaction)
//             let didFail =
//                 match result with
//                 | Error _ -> true
//                 | Ok _ -> false
//             Assert.True(didFail, "Account creation was allowed to succeed with invalid parent")
//         finally
//             DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore
//
//     [<Fact>]
//     member _.``REQ-AC-2.7 parent account must be active at AuditEnvelope instant--positive`` () =
//         let transaction = DAL.createDbTransaction() |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
//
//         try
//             let railroad = result {
//                 let! _ =
//                     Account.constructNewAndSaveToDb "AC-2.7-C" genericAccountNameString "Asset"
//                         genericAccountActiveBegin genericAccountActiveEnd (Some "Cash") (Some (fixture.Data.assets1000Id |> AccountId.value))
//                         genericAccountReference genericAuditEnvelope (Some transaction)
//                 return ()
//             }
//             match railroad with
//             | Ok _ -> ()
//             | Error e -> Assert.Fail (AppError.toMessage e)
//         finally
//             DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore
//
//     [<Fact>]
//     member _.``REQ-AC-2.7 parent account must be active at AuditEnvelope instant--negative`` () =
//         let transaction = DAL.createDbTransaction() |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
//
//         try
//             let childResult =
//                 Account.constructNewAndSaveToDb "AC-2.7-C" genericAccountNameString "Asset"
//                     genericAccountActiveBegin genericAccountActiveEnd (Some "Cash") (Some (fixture.Data.closedBank1290Id |> AccountId.value))
//                     genericAccountReference genericAuditEnvelope (Some transaction)
//             Assert.True(Result.isError childResult, "Child account creation was allowed to succeed with inactive parent")
//         finally
//             DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore
//
//     [<Fact>]
//     member _.``REQ-AC-2.20 child AccountType must match parent AccountType`` () =
//         let transaction = DAL.createDbTransaction() |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
//
//         try
//             let childResult =
//                 Account.constructNewAndSaveToDb "AC-2.20-C" genericAccountNameString "Liability"
//                     genericAccountActiveBegin genericAccountActiveEnd genericAccountSubtype (Some (fixture.Data.assets1000Id |> AccountId.value))
//                     genericAccountReference genericAuditEnvelope (Some transaction)
//             Assert.True(Result.isError childResult, "Child account creation was allowed to succeed with different account type from parent")
//         finally
//             DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore
//
//     // =============================================================================
//     // Updates
//     // =============================================================================
//
//     [<Fact>]
//     member _.``REQ-AC-4.8 updateAccountName succeeds with valid accountName`` () =
//         let envelope_rename = AuditEnvelope.create AccountUpdateName
//         let goodAccountName = "fahrvergnügen"
//
//         let transaction = DAL.createDbTransaction() |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
//
//         try
//             let railroad = result {
//                 let! renamedAccount =
//                     Account.updateAccountNameById fixture.Data.moneyMarket1270Id goodAccountName envelope_rename (Some transaction)
//                 Assert.Equal(goodAccountName, (AccountName.value (Account.accountName renamedAccount)))
//                 return ()
//             }
//             match railroad with
//             | Ok _ -> ()
//             | Error e -> Assert.Fail (AppError.toMessage e)
//         finally
//             DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore
//
//     [<Fact>]
//     member _.``REQ-AC-4.8 REQ-SYS-2.1 updateAccountName rejects invalid name`` () =
//         let result1 = AccountName.create "      "
//         Assert.True(Result.isError result1)
//         let result2 = AccountName.create (String('A', 101))
//         Assert.True(Result.isError result2)
//
//     [<Fact>]
//     member _.``REQ-AC-4.9 updateExternalReference succeeds with valid reference`` () =
//         let envelope_update = AuditEnvelope.create AccountUpdateExtReference
//         let goodReference = Some "Fliegende Ratte"
//
//         let transaction = DAL.createDbTransaction() |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
//
//         try
//             let railroad = result {
//                 let! updatedAccount =
//                     Account.updateExternalReferenceById fixture.Data.moneyMarket1270Id goodReference envelope_update (Some transaction)
//                 let newReference = Account.externalReference updatedAccount |> Option.map AccountExternalReference.value
//                 Assert.Equal(goodReference, newReference)
//                 return ()
//             }
//             match railroad with
//             | Ok _ -> ()
//             | Error e -> Assert.Fail (AppError.toMessage e)
//         finally
//             DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore
//
//     [<Fact>]
//     member _.``REQ-AC-4.9 updateExternalReference can be updated to None`` () =
//         let envelope_update = AuditEnvelope.create AccountUpdateExtReference
//
//         let transaction = DAL.createDbTransaction() |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
//
//         try
//             let railroad = result {
//                 let! updatedAccount =
//                     Account.updateExternalReferenceById fixture.Data.moneyMarket1270Id None envelope_update (Some transaction)
//                 let newReference = Account.externalReference updatedAccount |> Option.map AccountExternalReference.value
//                 Assert.Equal(None, newReference)
//                 return ()
//             }
//             match railroad with
//             | Ok _ -> ()
//             | Error e -> Assert.Fail (AppError.toMessage e)
//         finally
//             DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore
//
//     [<Fact>]
//     member _.``REQ-SYS-3.3 account update operations set modifiedAt from AuditEnvelope`` () =
//         let transaction = DAL.createDbTransaction() |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
//
//         try
//             let railroad = result {
//                 System.Threading.Thread.Sleep(10)
//                 let envelope_update = AuditEnvelope.create AccountUpdateName
//                 let! updatedAccount =
//                     Account.updateAccountNameById fixture.Data.moneyMarket1270Id "Blah blah blah" envelope_update (Some transaction)
//                 Assert.Equal(AuditEnvelope.instant envelope_update, Account.modifiedAt updatedAccount)
//                 return ()
//             }
//             match railroad with
//             | Ok _ -> ()
//             | Error e -> Assert.Fail (AppError.toMessage e)
//         finally
//             DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore
//
//     [<Fact>]
//     member _.``REQ-AC-4.19 update to deactivated account is permitted`` () =
//         let envelope_update = AuditEnvelope.create AccountUpdateName
//         let newName = "Blah blah blah"
//
//         let transaction = DAL.createDbTransaction() |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
//
//         try
//             let railroad = result {
//                 let! original = Account.fetchById (Some transaction) fixture.Data.closedBank1290Id
//                 Assert.False(Account.isActive (Calendar.today()) original)
//
//                 let! updatedAccount =
//                     Account.updateAccountNameById fixture.Data.closedBank1290Id newName envelope_update (Some transaction)
//                 Assert.Equal(newName, AccountName.value (Account.accountName updatedAccount))
//                 return ()
//             }
//             match railroad with
//             | Ok _ -> ()
//             | Error e -> Assert.Fail (AppError.toMessage e)
//         finally
//             DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore
