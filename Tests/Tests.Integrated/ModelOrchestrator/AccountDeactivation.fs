module Tests.Integrated.ModelOrchestrator.AccountDeactivation


open System
open Model.Audit
open Tests.Integrated.GenericTestProperties
open Xunit
open Model.Ledger.Accounts
open Model.Ledger.Accounts.AccountComponent
open ModelOrchestrator.AccountDeactivation
open Utilities.ResultCE
open Utilities

[<Fact>]
let ``REQ-AC-4.1 deactivateAccount sets active end and returns inactive account`` () =

    let envelope1 = AuditEnvelope.create AccountCreate
    let envelope2 = AuditEnvelope.create AccountDeactivation
    let dateRef1 = (AuditEnvelope.instant envelope1) |> Calendar.dateFromInstant
    let dateRef2 = (AuditEnvelope.instant envelope2) |> Calendar.dateFromInstant
    let explicitDeactivationDate = Some (Calendar.today().PlusDays(-1))

    let transaction = DAL.createDbTransaction() |> Result.defaultWith failwith

    try
        let railroad = result {
            let! pushAccount =
                Account.constructNewAndSaveToDb genericAccountCodeString genericAccountNameString genericAccountTypeString
                    genericAccountActiveBegin genericAccountActiveEnd genericAccountSubtype genericAccountParentId
                    genericAccountReference envelope1 (Some transaction)
            let pushId = Account.uniqueId pushAccount

            let! pullAccount = pushId |> deactivateAccountById explicitDeactivationDate envelope2 (Some transaction)
            let pullId = Account.uniqueId pullAccount

            Assert.Equal(pushId, pullId)
            Assert.True(Account.isActive dateRef1 pushAccount)
            Assert.False(Account.isActive dateRef2 pullAccount)

            return ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e
    finally
        DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore

[<Fact>]
let ``REQ-AC-4.2 deactivateAccount rejects end earlier than begin`` () =
    let envelope1 = AuditEnvelope.create AccountCreate
    let envelope2 = AuditEnvelope.create AccountDeactivation
    let activeBegin = Calendar.today().PlusDays(-1)
    let badActiveEnd = Some (activeBegin.PlusDays(-1))

    let transaction = DAL.createDbTransaction() |> Result.defaultWith failwith

    try
        let railroad = result {
            let! pushAccount =
                Account.constructNewAndSaveToDb genericAccountCodeString genericAccountNameString genericAccountTypeString
                    activeBegin genericAccountActiveEnd genericAccountSubtype genericAccountParentId
                    genericAccountReference envelope1 (Some transaction)
            let pushId = Account.uniqueId pushAccount

            let deactivationResult = pushId |> deactivateAccountById badActiveEnd envelope2 (Some transaction)

            let! _ =
                match deactivationResult with
                | Error _ -> Ok ()
                | Ok _ ->
                    Error "Account deactivation was allowed to succeed with an earlier end than begin"

            return ()

        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e
    finally
        DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore

[<Fact>]
let ``REQ-AC-4.2 deactivateAccount accepts end equal to begin`` () =
    let envelope1 = AuditEnvelope.create AccountCreate
    let envelope2 = AuditEnvelope.create AccountDeactivation
    let activeBegin = Calendar.today().PlusDays(-1)
    let goodActiveEnd = Some activeBegin

    let transaction = DAL.createDbTransaction() |> Result.defaultWith failwith

    try
        let railroad = result {
            let! pushAccount =
                Account.constructNewAndSaveToDb genericAccountCodeString genericAccountNameString genericAccountTypeString
                    activeBegin genericAccountActiveEnd genericAccountSubtype genericAccountParentId
                    genericAccountReference envelope1 (Some transaction)
            let pushId = Account.uniqueId pushAccount

            let deactivationResult = pushId |> deactivateAccountById goodActiveEnd envelope2 (Some transaction)

            let! _ =
                match deactivationResult with
                | Error _ -> Error "Account deactivation failed with an equal end and begin"
                | Ok _ -> Ok ()

            return ()

        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e
    finally
        DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore

[<Fact>]
let ``REQ-AC-4.3 deactivateAccount rejects when active children exist`` () =
    let code_parent = "AC-4.3-P"
    let code_child1 = "AC-4.3-C1"
    let envelope_deactivation = AuditEnvelope.create AccountDeactivation
    let goodActiveEnd = Some (Calendar.today())

    let transaction = DAL.createDbTransaction() |> Result.defaultWith failwith

    try
        let railroad = result {
            let! account_parent =
                Account.constructNewAndSaveToDb code_parent genericAccountNameString genericAccountTypeString
                    genericAccountActiveBegin genericAccountActiveEnd genericAccountSubtype genericAccountParentId
                    genericAccountReference genericAuditEnvelope (Some transaction)
            let parentId = Account.uniqueId account_parent

            let! _ =
                Account.constructNewAndSaveToDb code_child1 genericAccountNameString genericAccountTypeString
                    genericAccountActiveBegin genericAccountActiveEnd genericAccountSubtype (Some parentId)
                    genericAccountReference genericAuditEnvelope (Some transaction)

            let deactivationResult = parentId |> deactivateAccountById goodActiveEnd envelope_deactivation (Some transaction)

            let! _ =
                match deactivationResult with
                | Error _ -> Ok ()
                | Ok _ ->
                    Error "Account deactivation was allowed to succeed with an active child"

            return ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e
    finally
        DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore

[<Fact>]
let ``REQ-AC-4.5 deactivateAccount rejects already deactivated account`` () =
    let envelope_deactivation1 = AuditEnvelope.create AccountDeactivation
    let goodActiveEnd = Some (Calendar.today().PlusDays(-1))

    let transaction = DAL.createDbTransaction() |> Result.defaultWith failwith

    try
        let railroad = result {
            let! activeAccount =
                Account.constructNewAndSaveToDb genericAccountCodeString genericAccountNameString genericAccountTypeString
                    genericAccountActiveBegin genericAccountActiveEnd genericAccountSubtype genericAccountParentId
                    genericAccountReference genericAuditEnvelope (Some transaction)
            let activeId = Account.uniqueId activeAccount

            Assert.True(Account.isActive (Calendar.today()) activeAccount) // account starts as active

            let! inactiveAccount = activeId |> deactivateAccountById goodActiveEnd envelope_deactivation1 (Some transaction)

            Assert.False(Account.isActive (Calendar.today()) inactiveAccount) // first deactivation succeeds

            let envelope_deactivation2 = AuditEnvelope.create AccountDeactivation
            let betterActiveEnd = Some (Calendar.today().PlusDays(1))
            let deactivationResult2 =
                activeId |> deactivateAccountById betterActiveEnd envelope_deactivation2 (Some transaction) // should fail

            let! _ =
                match deactivationResult2 with
                | Error _ -> Ok ()
                | Ok _ ->
                    Error "Account deactivation was allowed to succeed with an already inactive account"

            return ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e
    finally
        DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore
        