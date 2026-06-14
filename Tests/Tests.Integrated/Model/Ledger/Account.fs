module Tests.Integrated.Model.Ledger.Account

open System
open Model.Audit
open Xunit
open Model.Ledger.Account
open Model.Ledger.AccountComponent
open NodaTime
open Utilities.ResultCE
open Tests.Integrated._Cleanup


[<Fact>]
let ``REQ-AC-1.4 AccountCode must be unique`` () =    
    let code1 = "REQ-AC-1.4"
    let code2 = code1
    let name1 = "AccountCode must be unique"
    let name2 = "AccountCode must still be unique"
    let accountType = "Asset"
    let activeBegin = Instant.FromDateTimeOffset(DateTimeOffset.Now)
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
        match idToCleanUp with
        | None -> ()
        | Some x ->
            match CleanUpAccountId x with
            | Ok () -> ()
            | Error e -> failwith e
        