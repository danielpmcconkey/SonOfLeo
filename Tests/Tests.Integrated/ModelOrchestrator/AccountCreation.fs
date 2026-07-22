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