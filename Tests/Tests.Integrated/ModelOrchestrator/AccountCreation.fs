module Tests.Integrated.ModelOrchestrator.AccountCreation

open System
open Model.Audit
open Model.Ledger.Accounts.AccountComponent.AccountType
open Model.Ledger.FiscalPeriods
open Model.Ledger.Journaling
open Model.Ledger.Journaling.JournalEntryComponent
open ModelOrchestrator
open ModelOrchestrator.JournalEntries
open Utilities.DAL
open Utilities.ResultHelper
open Xunit
open Model.Ledger.Accounts
open Model.Ledger.Accounts.AccountComponent
open Utilities
open Utilities.AppError
open Tests.Integrated.GenericTestProperties

// [<Fact>]
// let MyShitsBroke () =
//     let today = Calendar.today()
//     let lastYear = today.PlusYears(-1)
//     let envelope = AuditEnvelope.create AccountCreate
//     let railroad = result {
//         let! assets1000Id = createTestAccountFromPrimitives "F-1000" "Assets" "Asset" lastYear None None None None envelope None
//         let! liabilities2000Id = createTestAccountFromPrimitives "F-2000" "Liabilities" "Liability" lastYear None None None None envelope None
//         let! fiscalPeriods =
//             [-4..4]
//             |> List.map (fun x ->
//                 let date = x |> today.PlusMonths
//                 let monthF = date.Month.ToString("D2")
//                 let key = $"{date.Year}-{monthF}" |> FiscalPeriodKey.fromString |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
//                 FiscalPeriodCreation.constructNewAndSaveToDb key envelope None)
//             |> convertListOfResultsToResultsList
//         let jeEnvelope = AuditEnvelope.create JournalEntryPostNew
//      
//         let! basicJe, basicJeId =
//             createTestJournalEntryFromPrimitives "Basic journal entry" None today 
//                 [ (assets1000Id, 100.00M, "Debit", None)
//                   (liabilities2000Id, 100.00M, "Credit", (Some "Grocery run")) ]
//                 [  ]
//                 [ (None, "Fixture comment for testing") ]
//                 jeEnvelope
//         let! closedFiscalPeriod =
//                let date = today.PlusMonths(-5)
//                let monthF = date.Month.ToString("D2")
//                let key = $"{date.Year}-{monthF}" |> FiscalPeriodKey.fromString |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
//                FiscalPeriodCreation.constructNewAndSaveToDb key envelope None
//  
//         let closedFiscalPeriodId = closedFiscalPeriod |> FiscalPeriod.fiscalPeriodId
//  
//         let jeEnvelope = AuditEnvelope.create JournalEntryPostNew
//
//         let! basicJe, basicJeId =
//             createTestJournalEntryFromPrimitives "Basic journal entry" None today 
//                 [ (assets1000Id, 100.00M, "Debit", None)
//                   (liabilities2000Id, 100.00M, "Credit", (Some "Grocery run")) ]
//                 [  ]
//                 [ (None, "Fixture comment for testing") ]
//                 jeEnvelope
//  
//         let closedPeriodEntryDate = today.PlusMonths(-5).PlusDays(14)
//
//         let! jeInClosedPeriod, jeInClosedPeriodId =
//             createTestJournalEntryFromPrimitives "Fixture JE in closed period" None closedPeriodEntryDate 
//                 [ (assets1000Id, 25.00M, "Debit", None)
//                   (liabilities2000Id, 25.00M, "Credit", None) ]
//                 [  ]
//                 []
//                 jeEnvelope
//  
//         let! jeToVoid, jeToVoidId =
//             createTestJournalEntryFromPrimitives "Fixture voided JE" None today 
//                 [ (assets1000Id, 75.00M, "Debit", None)
//                   (liabilities2000Id, 75.00M, "Credit", None) ]
//                 [  ]
//                 []
//                 jeEnvelope
//  
//         let! _ = FiscalPeriod.closeFiscalPeriod closedFiscalPeriodId envelope None
//  
//         let voidEnvelope = AuditEnvelope.create JournalEntryVoid
//         let! commentText = "Fixture voiding reason" |> CommentText.create
//         let! voidedJe = jeToVoidId |> JournalEntryVoiding.voidJournalEntry voidEnvelope None commentText
//         let voidedJeId = voidedJe |> JournalEntry.header |> JournalEntryHeader.journalEntryHeaderId
//         return ()
//     }
//     match railroad with
//     | Ok x -> ()
//     | Error e -> Assert.Fail (AppError.toMessage e)
    
    
    
[<Fact>]
let ``REQ-AC-2.13 REQ-SYS-3.2 constructNew generates UUID`` () =
    let transaction = createDbTransaction() |> Result.defaultWith (fun e -> failwith (AppError.toMessage e)) // if this fails, nothing can proceed
    try
        let code = "abc1" |> AccountCode.create |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
        AccountCreation.constructNewAndSaveToDb
            code genericAccountName genericAccountType genericAccountActivityPeriod genericAccountSubtype
            genericAccountParentId genericAccountReference genericAuditEnvelope (Some transaction)
        |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
        |> Account.accountId
        |> AccountId.value
        |> fun id -> Assert.NotEqual(Guid.Empty, id)
    finally
        rollbackDbTransactionAndDisposeConnection transaction |> ignore
    
[<Fact>]
let ``REQ-AC-2.13 REQ-SYS-3.2 constructNew sets timestamps from AuditEnvelope`` () =
    let transaction = createDbTransaction() |> Result.defaultWith (fun e -> failwith (AppError.toMessage e)) // if this fails, nothing can proceed
    try
        let code = "abc2" |> AccountCode.create |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
        let expected = AuditEnvelope.instant genericAuditEnvelope
        let account =
            AccountCreation.constructNewAndSaveToDb
                code genericAccountName genericAccountType genericAccountActivityPeriod genericAccountSubtype
                genericAccountParentId genericAccountReference genericAuditEnvelope (Some transaction)
            |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
        Assert.Equal(expected, Account.createdAt account)
        Assert.Equal(expected, Account.modifiedAt account)
    finally
        rollbackDbTransactionAndDisposeConnection transaction |> ignore