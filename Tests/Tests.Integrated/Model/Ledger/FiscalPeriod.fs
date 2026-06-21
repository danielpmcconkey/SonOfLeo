module Tests.Integrated.Model.Ledger.FiscalPeriod

open System
open Model.Audit
open Model.Ledger
open Tests.Integrated.GenericTestProperties
open Xunit
open Model.Ledger.Account
open Model.Ledger.AccountComponent
open NodaTime
open Utilities.ResultCE
open Tests.Integrated._Cleanup
open Utilities.Clock

[<Fact>]
let ``REQ-FP-2.1 creating a fiscal period must generate a UUID`` () =     
    let mutable idToCleanUp = None
    try
        let railroad = result {
            let! fp = FiscalPeriod.constructNewAndSaveToDb genericFiscalPeriodKey genericAuditEnvelope
            let unique_id = FiscalPeriod.uniqueId fp
            idToCleanUp <- Some unique_id
            Assert.NotEqual(unique_id, Guid.Empty)
            ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e
    finally
        match cleanUpFiscalPeriodId idToCleanUp with
        | Ok () -> ()
        | Error e -> failwith e

[<Fact>]
let ``REQ-FP-1.3 REQ-FP-2.2 Period Key must be unique`` () = 
    let mutable idToCleanUp = None
    try
        let result1 = FiscalPeriod.constructNewAndSaveToDb genericFiscalPeriodKey genericAuditEnvelope
        match result1 with
        | Error e -> Assert.Fail e // need to ensure that the first made it into the DB to check the unique constraint
        | Ok fp -> idToCleanUp <- Some (FiscalPeriod.uniqueId fp)
        
        let result2 = FiscalPeriod.constructNewAndSaveToDb genericFiscalPeriodKey genericAuditEnvelope
        Assert.True(Result.isError result2)
    finally
        match cleanUpFiscalPeriodId idToCleanUp with
        | Ok () -> ()
        | Error e -> failwith e

[<Fact>]
let ``REQ-FP-2.4 REQ-FP-2.5 insertNewToDb happy path`` () =    
    let expectedKey = "2026-10"
    let expectedStartMonth = 10
    let expectedStartDay = 1
    let expectedEndMonth = 10
    let expectedEndDay = 31
    let expectedIsOpen = true
    let mutable idToCleanUp = None
    try
        let railroad = result {
            let! fp = FiscalPeriod.constructNewAndSaveToDb expectedKey genericAuditEnvelope
            let unique_id = FiscalPeriod.uniqueId fp
            idToCleanUp <- Some unique_id
            let startDate = FiscalPeriod.startDate fp
            let endDate = FiscalPeriod.endDate fp
            Assert.NotEqual(unique_id, Guid.Empty)
            Assert.Equal(expectedKey, PeriodKey.value (FiscalPeriod.periodKey fp))
            Assert.Equal(expectedStartMonth, startDate.Month)
            Assert.Equal(expectedStartDay, startDate.Day)
            Assert.Equal(expectedEndMonth, endDate.Month)
            Assert.Equal(expectedEndDay, endDate.Day)
            Assert.Equal(expectedIsOpen, FiscalPeriod.isOpen fp)
            ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e
    finally
        match cleanUpFiscalPeriodId idToCleanUp with
        | Ok () -> ()
        | Error e -> failwith e

[<Fact>]
let ``REQ-FP-2.6 is open is automatically true`` () =   
    let expectedIsOpen = true
    let mutable idToCleanUp = None
    try
        let railroad = result {
            let! fp = FiscalPeriod.constructNewAndSaveToDb genericFiscalPeriodKey genericAuditEnvelope
            let unique_id = FiscalPeriod.uniqueId fp
            idToCleanUp <- Some unique_id
            Assert.Equal(expectedIsOpen, FiscalPeriod.isOpen fp)
            ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e
    finally
        match cleanUpFiscalPeriodId idToCleanUp with
        | Ok () -> ()
        | Error e -> failwith e

[<Fact>]
let ``REQ-FP-3.1 REQ-FP-3.2 fetchByKey happy path`` () =    
    let expectedKey = "2026-10"
    let expectedStartMonth = 10
    let expectedStartDay = 1
    let expectedEndMonth = 10
    let expectedEndDay = 31
    let expectedIsOpen = true
    let expectedCreated = AuditEnvelope.instant genericAuditEnvelope
    let expectedModified = AuditEnvelope.instant genericAuditEnvelope
    let mutable idToCleanUp = None
    try
        let railroad = result {
            let! fp_create = FiscalPeriod.constructNewAndSaveToDb expectedKey genericAuditEnvelope
            let uniqueId = FiscalPeriod.uniqueId fp_create
            idToCleanUp <- Some uniqueId
            let key_create = PeriodKey.value (FiscalPeriod.periodKey fp_create)
            let! fp_read = FiscalPeriod.fetchByKey key_create
            
            let id_read = FiscalPeriod.uniqueId fp_read
            let key_read = PeriodKey.value (FiscalPeriod.periodKey fp_read)
            let startDate = FiscalPeriod.startDate fp_read
            let endDate = FiscalPeriod.endDate fp_read
            let isOpen = FiscalPeriod.isOpen fp_read
            let created = FiscalPeriod.createdAt fp_read
            let modified = FiscalPeriod.modifiedAt fp_read
            Assert.Equal(uniqueId, id_read)
            Assert.Equal(expectedKey, key_read)
            Assert.Equal(expectedStartMonth, startDate.Month)
            Assert.Equal(expectedStartDay, startDate.Day)
            Assert.Equal(expectedEndMonth, endDate.Month)
            Assert.Equal(expectedEndDay, endDate.Day)
            Assert.Equal(expectedIsOpen, isOpen)
            Assert.Equal(expectedCreated, created)
            Assert.Equal(expectedModified, modified)
            ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e
    finally
        match cleanUpFiscalPeriodId idToCleanUp with
        | Ok () -> ()
        | Error e -> failwith e

[<Fact>]
let ``REQ-FP-3.4 fetchAll without filter happy path`` () =
    let explicitKey_1 = "2026-10"
    let explicitKey_2 = "2026-11"
    let explicitKey_3 = "2026-12"
    let explicitKey_4 = "2027-01"
    let mutable idToCleanUp_1 = None
    let mutable idToCleanUp_2 = None
    let mutable idToCleanUp_3 = None
    let mutable idToCleanUp_4 = None
    try
        let railroad = result {
            let! fp_1 = FiscalPeriod.constructNewAndSaveToDb explicitKey_1 genericAuditEnvelope
            let uniqueId_1 = FiscalPeriod.uniqueId fp_1
            idToCleanUp_1 <- Some uniqueId_1
            
            let! fp_2 = FiscalPeriod.constructNewAndSaveToDb explicitKey_2 genericAuditEnvelope
            let uniqueId_2 = FiscalPeriod.uniqueId fp_2
            idToCleanUp_2 <- Some uniqueId_2
            
            let! fp_3 = FiscalPeriod.constructNewAndSaveToDb explicitKey_3 genericAuditEnvelope
            let uniqueId_3 = FiscalPeriod.uniqueId fp_3
            idToCleanUp_3 <- Some uniqueId_3
            
            let! fp_4 = FiscalPeriod.constructNewAndSaveToDb explicitKey_4 genericAuditEnvelope
            let uniqueId_4 = FiscalPeriod.uniqueId fp_4
            idToCleanUp_4 <- Some uniqueId_4
            
            let! fetched = FiscalPeriod.fetchAll(false)            
            Assert.Equal(4, fetched |> List.length)
            
            ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e
    finally
        match cleanUpFiscalPeriodIdsList [idToCleanUp_1;idToCleanUp_2;idToCleanUp_3;idToCleanUp_4;] with
        | Ok () -> ()
        | Error e -> failwith e

[<Fact>]
let ``REQ-FP-3.5 fetchAll with open only filters out closed periods`` () =
    let explicitKey_1 = "2026-10"
    let explicitKey_2 = "2026-11"
    let explicitKey_3 = "2026-12"
    let explicitKey_4 = "2027-01"
    let mutable idToCleanUp_1 = None
    let mutable idToCleanUp_2 = None
    let mutable idToCleanUp_3 = None
    let mutable idToCleanUp_4 = None
    try
        let railroad = result {
            let! fp_1 = FiscalPeriod.constructNewAndSaveToDb explicitKey_1 genericAuditEnvelope
            let uniqueId_1 = FiscalPeriod.uniqueId fp_1
            idToCleanUp_1 <- Some uniqueId_1
            
            let! fp_2 = FiscalPeriod.constructNewAndSaveToDb explicitKey_2 genericAuditEnvelope
            let uniqueId_2 = FiscalPeriod.uniqueId fp_2
            idToCleanUp_2 <- Some uniqueId_2
            
            let! fp_3 = FiscalPeriod.constructNewAndSaveToDb explicitKey_3 genericAuditEnvelope
            let uniqueId_3 = FiscalPeriod.uniqueId fp_3
            idToCleanUp_3 <- Some uniqueId_3
            
            let! fp_4 = FiscalPeriod.constructNewAndSaveToDb explicitKey_4 genericAuditEnvelope
            let uniqueId_4 = FiscalPeriod.uniqueId fp_4
            idToCleanUp_4 <- Some uniqueId_4
            
            let periodKey3 = fp_3 |> FiscalPeriod.periodKey |> PeriodKey.value
            let! _ = FiscalPeriod.closeFiscalPeriod periodKey3 genericAuditEnvelope 
            
            let! fetched = FiscalPeriod.fetchAll(true)
            Assert.Equal(3, fetched |> List.length)
            
            ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e
    finally
        match cleanUpFiscalPeriodIdsList [idToCleanUp_1;idToCleanUp_2;idToCleanUp_3;idToCleanUp_4;] with
        | Ok () -> ()
        | Error e -> failwith e

[<Fact>]
let ``REQ-FP-4.1 closeFiscalPeriod happy path`` () =
    let explicitKey = "2024-08"
    let mutable idToCleanUp_1 = None
    try
        let railroad = result {
            let! fp_1 = FiscalPeriod.constructNewAndSaveToDb explicitKey genericAuditEnvelope
            let uniqueId_1 = FiscalPeriod.uniqueId fp_1
            idToCleanUp_1 <- Some uniqueId_1            
            
            let! fetched = FiscalPeriod.closeFiscalPeriod explicitKey genericAuditEnvelope
            Assert.False(FiscalPeriod.isOpen fetched)
            
            ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e
    finally
        match cleanUpFiscalPeriodId idToCleanUp_1 with
        | Ok () -> ()
        | Error e -> failwith e

[<Fact>]
let ``REQ-FP-4.1.1 closeFiscalPeriod rejects already closed period`` () =
    let explicitKey = "1984-08"
    let expectedModified = AuditEnvelope.instant genericAuditEnvelope
    
    let mutable idToCleanUp_1 = None
    try
        let railroad = result {
            let! fp_1 = FiscalPeriod.constructNewAndSaveToDb explicitKey genericAuditEnvelope
            let uniqueId_1 = FiscalPeriod.uniqueId fp_1
            idToCleanUp_1 <- Some uniqueId_1            
            
            let! fetched = FiscalPeriod.closeFiscalPeriod explicitKey genericAuditEnvelope
            Assert.False(FiscalPeriod.isOpen fetched) // make sure it's false
            
            System.Threading.Thread.Sleep(1000) // make sure there's some time so the modified_at isn't accidentally the same
            let attemptResult = FiscalPeriod.closeFiscalPeriod explicitKey (AuditEnvelope.create FiscalPeriodClose) 
            Assert.True(attemptResult.IsError)
            
            // fetch it again to make sure it didn't update the modified date or the flag
            let! fetched_2 = FiscalPeriod.fetchByKey explicitKey
            Assert.False(FiscalPeriod.isOpen fetched_2)
            Assert.Equal(expectedModified, FiscalPeriod.modifiedAt fetched_2)
            
            ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e
    finally
        match cleanUpFiscalPeriodId idToCleanUp_1 with
        | Ok () -> ()
        | Error e -> failwith e

[<Fact>]
let ``REQ-FP-4.2 reopenFiscalPeriod happy path`` () =
    let explicitKey = "2008-09"
    let mutable idToCleanUp_1 = None
    try
        let railroad = result {
            let! fp_1 = FiscalPeriod.constructNewAndSaveToDb explicitKey genericAuditEnvelope
            let uniqueId_1 = FiscalPeriod.uniqueId fp_1
            idToCleanUp_1 <- Some uniqueId_1
            
            let! fetched_1 = FiscalPeriod.closeFiscalPeriod explicitKey genericAuditEnvelope
            Assert.False(FiscalPeriod.isOpen fetched_1) // make sure it's actually closed first
            
            let! reponed = FiscalPeriod.reopenFiscalPeriod explicitKey genericAuditEnvelope
            Assert.True(FiscalPeriod.isOpen reponed)
            
            ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e
    finally
        match cleanUpFiscalPeriodId idToCleanUp_1 with
        | Ok () -> ()
        | Error e -> failwith e

[<Fact>]
let ``REQ-FP-4.2.1 reopenFiscalPeriod rejects already open period`` () =
    let explicitKey = "1979-02"
    let expectedModified = AuditEnvelope.instant genericAuditEnvelope
    let mutable idToCleanUp_1 = None
    try
        let railroad = result {
            let! fp_1 = FiscalPeriod.constructNewAndSaveToDb explicitKey genericAuditEnvelope
            let uniqueId_1 = FiscalPeriod.uniqueId fp_1
            idToCleanUp_1 <- Some uniqueId_1             
            
            System.Threading.Thread.Sleep(1000) // make sure there's some time so the modified_at isn't accidentally the same
            let attemptResult = FiscalPeriod.reopenFiscalPeriod explicitKey (AuditEnvelope.create FiscalPeriodReopen) 
            Assert.True(attemptResult.IsError)
            
            // fetch it again to make sure it didn't update the modified date or the flag
            let! fetched = FiscalPeriod.fetchByKey explicitKey
            Assert.True(FiscalPeriod.isOpen fetched)
            Assert.Equal(expectedModified, FiscalPeriod.modifiedAt fetched)
            
            ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e
    finally
        match cleanUpFiscalPeriodId idToCleanUp_1 with
        | Ok () -> ()
        | Error e -> failwith e

[<Fact>]
let ``REQ-SYS-3.2 insertNewToDb sets create and modified timestamps`` () =
    let expected = AuditEnvelope.instant genericAuditEnvelope
    let mutable idToCleanUp = None
    try
        let railroad = result {
            let! fp = FiscalPeriod.constructNewAndSaveToDb genericFiscalPeriodKey genericAuditEnvelope
            let unique_id = FiscalPeriod.uniqueId fp
            idToCleanUp <- Some unique_id
            Assert.Equal(expected, FiscalPeriod.createdAt fp)
            Assert.Equal(expected, FiscalPeriod.modifiedAt fp)
            ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e
    finally
        match cleanUpFiscalPeriodId idToCleanUp with
        | Ok () -> ()
        | Error e -> failwith e

