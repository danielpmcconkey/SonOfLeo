module Tests.Integrated.Model.Ledger.FiscalPeriod

open System
open Model.Audit
open Model.Ledger.FiscalPeriods
open Tests.Integrated.GenericTestProperties
open Xunit
open Utilities.ResultCE
open Utilities

[<Fact>]
let ``REQ-FP-2.1 creating a fiscal period must generate a UUID`` () =
    let transaction = DAL.createDbTransaction() |> Result.defaultWith failwith

    try
        let railroad = result {
            let! fp = FiscalPeriod.constructNewAndSaveToDb genericFiscalPeriodKey genericAuditEnvelope (Some transaction)
            let unique_id = FiscalPeriod.uniqueId fp
            Assert.NotEqual(unique_id, Guid.Empty)
            ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e
    finally
        DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore

[<Fact>]
let ``REQ-FP-1.3 REQ-FP-2.2 Period Key must be unique`` () =
    let transaction = DAL.createDbTransaction() |> Result.defaultWith failwith

    try
        let result1 = FiscalPeriod.constructNewAndSaveToDb genericFiscalPeriodKey genericAuditEnvelope (Some transaction)
        match result1 with
        | Error e -> Assert.Fail e // need to ensure that the first made it into the DB to check the unique constraint
        | Ok _ -> ()

        let result2 = FiscalPeriod.constructNewAndSaveToDb genericFiscalPeriodKey genericAuditEnvelope (Some transaction)
        Assert.True(Result.isError result2)
    finally
        DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore

[<Fact>]
let ``REQ-FP-2.4 REQ-FP-2.5 insertNewToDb happy path`` () =
    let expectedKey = "2026-10"
    let expectedStartMonth = 10
    let expectedStartDay = 1
    let expectedEndMonth = 10
    let expectedEndDay = 31
    let expectedIsOpen = true

    let transaction = DAL.createDbTransaction() |> Result.defaultWith failwith

    try
        let railroad = result {
            let! fp = FiscalPeriod.constructNewAndSaveToDb expectedKey genericAuditEnvelope (Some transaction)
            let startDate = FiscalPeriod.startDate fp
            let endDate = FiscalPeriod.endDate fp
            Assert.NotEqual(FiscalPeriod.uniqueId fp, Guid.Empty)
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
        DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore

[<Fact>]
let ``REQ-FP-2.6 is open is automatically true`` () =
    let expectedIsOpen = true

    let transaction = DAL.createDbTransaction() |> Result.defaultWith failwith

    try
        let railroad = result {
            let! fp = FiscalPeriod.constructNewAndSaveToDb genericFiscalPeriodKey genericAuditEnvelope (Some transaction)
            Assert.Equal(expectedIsOpen, FiscalPeriod.isOpen fp)
            ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e
    finally
        DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore

[<Fact>]
let ``REQ-FP-3.1 fetchById happy path`` () =
    let expectedKey = "2026-10"
    let expectedStartMonth = 10
    let expectedStartDay = 1
    let expectedEndMonth = 10
    let expectedEndDay = 31
    let expectedIsOpen = true
    let expectedCreated = AuditEnvelope.instant genericAuditEnvelope
    let expectedModified = AuditEnvelope.instant genericAuditEnvelope

    let transaction = DAL.createDbTransaction() |> Result.defaultWith failwith

    try
        let railroad = result {
            let! fp_create = FiscalPeriod.constructNewAndSaveToDb expectedKey genericAuditEnvelope (Some transaction)
            let uniqueId = FiscalPeriod.uniqueId fp_create
            let! fp_read = FiscalPeriod.fetchById (Some transaction) uniqueId

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
        DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore

[<Fact>]
let ``REQ-FP-3.4 fetchAll without filter happy path`` () =
    let explicitKey_1 = "2026-10"
    let explicitKey_2 = "2026-11"
    let explicitKey_3 = "2026-12"
    let explicitKey_4 = "2027-01"

    let transaction = DAL.createDbTransaction() |> Result.defaultWith failwith

    try
        let railroad = result {
            let! _ = FiscalPeriod.constructNewAndSaveToDb explicitKey_1 genericAuditEnvelope (Some transaction)
            let! _ = FiscalPeriod.constructNewAndSaveToDb explicitKey_2 genericAuditEnvelope (Some transaction)
            let! _ = FiscalPeriod.constructNewAndSaveToDb explicitKey_3 genericAuditEnvelope (Some transaction)
            let! _ = FiscalPeriod.constructNewAndSaveToDb explicitKey_4 genericAuditEnvelope (Some transaction)

            let! fetched = FiscalPeriod.fetchAll (Some transaction) false
            Assert.Equal(4, fetched |> List.length)

            ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e
    finally
        DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore

[<Fact>]
let ``REQ-FP-3.5 fetchAll with open only filters out closed periods`` () =
    let explicitKey_1 = "2026-10"
    let explicitKey_2 = "2026-11"
    let explicitKey_3 = "2026-12"
    let explicitKey_4 = "2027-01"

    let transaction = DAL.createDbTransaction() |> Result.defaultWith failwith

    try
        let railroad = result {
            let! _ = FiscalPeriod.constructNewAndSaveToDb explicitKey_1 genericAuditEnvelope (Some transaction)
            let! _ = FiscalPeriod.constructNewAndSaveToDb explicitKey_2 genericAuditEnvelope (Some transaction)
            let! fp_3 = FiscalPeriod.constructNewAndSaveToDb explicitKey_3 genericAuditEnvelope (Some transaction)
            let! _ = FiscalPeriod.constructNewAndSaveToDb explicitKey_4 genericAuditEnvelope (Some transaction)

            let id3 = fp_3 |> FiscalPeriod.uniqueId 
            let! _ = FiscalPeriod.closeFiscalPeriod id3 genericAuditEnvelope (Some transaction)

            let! fetched = FiscalPeriod.fetchAll (Some transaction) true
            Assert.Equal(3, fetched |> List.length)

            ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e
    finally
        DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore

[<Fact>]
let ``REQ-FP-4.1 closeFiscalPeriod happy path`` () =
    let explicitKey = "2024-08"

    let transaction = DAL.createDbTransaction() |> Result.defaultWith failwith

    try
        let railroad = result {
            let! fp = FiscalPeriod.constructNewAndSaveToDb explicitKey genericAuditEnvelope (Some transaction)
            let id = fp |> FiscalPeriod.uniqueId 
            let! fetched = FiscalPeriod.closeFiscalPeriod id genericAuditEnvelope (Some transaction)
            Assert.False(FiscalPeriod.isOpen fetched)

            ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e
    finally
        DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore

[<Fact>]
let ``REQ-FP-4.1.1 closeFiscalPeriod rejects already closed period`` () =
    let explicitKey = "1984-08"
    let expectedModified = AuditEnvelope.instant genericAuditEnvelope

    let transaction = DAL.createDbTransaction() |> Result.defaultWith failwith

    try
        let railroad = result {
            let! fp = FiscalPeriod.constructNewAndSaveToDb explicitKey genericAuditEnvelope (Some transaction)
            let id = fp |> FiscalPeriod.uniqueId 
            let! fetched = FiscalPeriod.closeFiscalPeriod id genericAuditEnvelope (Some transaction)
            Assert.False(FiscalPeriod.isOpen fetched) // make sure it's false

            System.Threading.Thread.Sleep(1000) // make sure there's some time so the modified_at isn't accidentally the same
            let attemptResult = FiscalPeriod.closeFiscalPeriod id (AuditEnvelope.create FiscalPeriodClose) (Some transaction)
            Assert.True(attemptResult.IsError)

            // fetch it again to make sure it didn't update the modified date or the flag
            let! fetched_2 = FiscalPeriod.fetchById (Some transaction) id
            Assert.False(FiscalPeriod.isOpen fetched_2)
            Assert.Equal(expectedModified, FiscalPeriod.modifiedAt fetched_2)

            ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e
    finally
        DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore

[<Fact>]
let ``REQ-FP-4.2 reopenFiscalPeriod happy path`` () =
    let explicitKey = "2008-09"

    let transaction = DAL.createDbTransaction() |> Result.defaultWith failwith

    try
        let railroad = result {
            let! fp = FiscalPeriod.constructNewAndSaveToDb explicitKey genericAuditEnvelope (Some transaction)
            let id = fp |> FiscalPeriod.uniqueId 
            let! fetched_1 = FiscalPeriod.closeFiscalPeriod id genericAuditEnvelope (Some transaction)
            Assert.False(FiscalPeriod.isOpen fetched_1) // make sure it's actually closed first

            let! reponed = FiscalPeriod.reopenFiscalPeriod id genericAuditEnvelope (Some transaction)
            Assert.True(FiscalPeriod.isOpen reponed)

            ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e
    finally
        DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore

[<Fact>]
let ``REQ-FP-4.2.1 reopenFiscalPeriod rejects already open period`` () =
    let explicitKey = "1979-02"
    let expectedModified = AuditEnvelope.instant genericAuditEnvelope

    let transaction = DAL.createDbTransaction() |> Result.defaultWith failwith

    try
        let railroad = result {
            let! fp = FiscalPeriod.constructNewAndSaveToDb explicitKey genericAuditEnvelope (Some transaction)
            let id = fp |> FiscalPeriod.uniqueId 
            System.Threading.Thread.Sleep(1000) // make sure there's some time so the modified_at isn't accidentally the same
            let attemptResult = FiscalPeriod.reopenFiscalPeriod id (AuditEnvelope.create FiscalPeriodReopen) (Some transaction)
            Assert.True(attemptResult.IsError)

            // fetch it again to make sure it didn't update the modified date or the flag
            let! fetched = FiscalPeriod.fetchById (Some transaction) id
            Assert.True(FiscalPeriod.isOpen fetched)
            Assert.Equal(expectedModified, FiscalPeriod.modifiedAt fetched)

            ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e
    finally
        DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore

[<Fact>]
let ``REQ-SYS-3.2 insertNewToDb sets create and modified timestamps`` () =
    let expected = AuditEnvelope.instant genericAuditEnvelope

    let transaction = DAL.createDbTransaction() |> Result.defaultWith failwith

    try
        let railroad = result {
            let! fp = FiscalPeriod.constructNewAndSaveToDb genericFiscalPeriodKey genericAuditEnvelope (Some transaction)
            Assert.Equal(expected, FiscalPeriod.createdAt fp)
            Assert.Equal(expected, FiscalPeriod.modifiedAt fp)
            ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e
    finally
        DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore
