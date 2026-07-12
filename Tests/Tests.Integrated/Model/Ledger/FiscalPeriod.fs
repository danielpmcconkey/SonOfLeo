namespace Tests.Integrated.Model.Ledger

open System
open Model.Audit
open Model.Ledger.FiscalPeriods
open Tests.Integrated
open Tests.Integrated.GenericTestProperties
open Xunit
open Utilities.ResultCE
open Utilities

[<Collection("SharedTestData")>]
type FiscalPeriodTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-FP-2.1 creating a fiscal period must generate a UUID`` () =
        let transaction = DAL.createDbTransaction() |> Result.defaultWith failwith

        try
            let railroad = result {
                let! fp = FiscalPeriod.constructNewAndSaveToDb genericFiscalPeriodKey genericAuditEnvelope (Some transaction)
                let unique_id = FiscalPeriod.fiscalPeriodId fp |> FiscalPeriodId.value
                Assert.NotEqual(unique_id, Guid.Empty)
                ()
            }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail e
        finally
            DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore

    [<Fact>]
    member _.``REQ-FP-1.3 REQ-FP-2.2 Period Key must be unique`` () =
        let transaction = DAL.createDbTransaction() |> Result.defaultWith failwith

        try
            let railroad = result {
                let! existingPeriod = FiscalPeriod.fetchById (Some transaction) (fixture.Data.fiscalPeriodIds |> List.head)
                let existingKey = FiscalPeriod.periodKey existingPeriod |> FiscalPeriodKey.value
                let duplicateResult = FiscalPeriod.constructNewAndSaveToDb existingKey genericAuditEnvelope (Some transaction)
                Assert.True(Result.isError duplicateResult)
                ()
            }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail e
        finally
            DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore

    [<Fact>]
    member _.``REQ-FP-2.4 REQ-FP-2.5 insertNewToDb happy path`` () =
        let expectedKey = "2050-10"
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
                let uuid = FiscalPeriod.fiscalPeriodId fp |> FiscalPeriodId.value
                Assert.NotEqual(uuid, Guid.Empty)
                Assert.Equal(expectedKey, FiscalPeriodKey.value (FiscalPeriod.periodKey fp))
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
    member _.``REQ-FP-2.6 is open is automatically true`` () =
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
    member _.``REQ-FP-3.1 fetchById happy path`` () =
        let expectedId = fixture.Data.fiscalPeriodIds |> List.head

        let railroad = result {
            let! fetched = FiscalPeriod.fetchById None expectedId
            Assert.Equal(expectedId, FiscalPeriod.fiscalPeriodId fetched)
            Assert.True(FiscalPeriod.isOpen fetched)
            ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e

    [<Fact>]
    member _.``REQ-FP-3.4 fetchAll without filter happy path`` () =
        let railroad = result {
            let! fetched = FiscalPeriod.fetchAll None false
            fixture.Data.fiscalPeriodIds
            |> List.forall (fun id -> fetched |> List.exists (fun fp -> FiscalPeriod.fiscalPeriodId fp = id))
            |> Assert.True
            ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e

    [<Fact>]
    member _.``REQ-FP-3.5 fetchAll with open only filters out closed periods`` () =
        let railroad = result {
            let! fetched = FiscalPeriod.fetchAll None true

            fetched
            |> List.exists (fun fp -> FiscalPeriod.fiscalPeriodId fp = fixture.Data.closedFiscalPeriodId)
            |> Assert.False

            fixture.Data.fiscalPeriodIds
            |> List.forall (fun id -> fetched |> List.exists (fun fp -> FiscalPeriod.fiscalPeriodId fp = id))
            |> Assert.True
            ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e

    [<Fact>]
    member _.``REQ-FP-4.1 closeFiscalPeriod happy path`` () =
        let transaction = DAL.createDbTransaction() |> Result.defaultWith failwith

        try
            let railroad = result {
                let id = fixture.Data.fiscalPeriodIds |> List.head
                let! closed = FiscalPeriod.closeFiscalPeriod id genericAuditEnvelope (Some transaction)
                Assert.False(FiscalPeriod.isOpen closed)
                ()
            }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail e
        finally
            DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore

    [<Fact>]
    member _.``REQ-FP-4.1.1 closeFiscalPeriod rejects already closed period`` () =
        let transaction = DAL.createDbTransaction() |> Result.defaultWith failwith

        try
            let railroad = result {
                let! original = FiscalPeriod.fetchById (Some transaction) fixture.Data.closedFiscalPeriodId
                let originalModified = FiscalPeriod.modifiedAt original
                Assert.False(FiscalPeriod.isOpen original)

                System.Threading.Thread.Sleep(10)
                let attemptResult = FiscalPeriod.closeFiscalPeriod fixture.Data.closedFiscalPeriodId (AuditEnvelope.create FiscalPeriodClose) (Some transaction)
                Assert.True(attemptResult.IsError)

                let! fetched = FiscalPeriod.fetchById (Some transaction) fixture.Data.closedFiscalPeriodId
                Assert.False(FiscalPeriod.isOpen fetched)
                Assert.Equal(originalModified, FiscalPeriod.modifiedAt fetched)
                ()
            }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail e
        finally
            DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore

    [<Fact>]
    member _.``REQ-FP-4.2 reopenFiscalPeriod happy path`` () =
        let transaction = DAL.createDbTransaction() |> Result.defaultWith failwith

        try
            let railroad = result {
                let! reopened = FiscalPeriod.reopenFiscalPeriod fixture.Data.closedFiscalPeriodId genericAuditEnvelope (Some transaction)
                Assert.True(FiscalPeriod.isOpen reopened)
                ()
            }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail e
        finally
            DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore

    [<Fact>]
    member _.``REQ-FP-4.2.1 reopenFiscalPeriod rejects already open period`` () =
        let transaction = DAL.createDbTransaction() |> Result.defaultWith failwith

        try
            let railroad = result {
                let id = fixture.Data.fiscalPeriodIds |> List.head
                let! original = FiscalPeriod.fetchById (Some transaction) id
                let originalModified = FiscalPeriod.modifiedAt original
                Assert.True(FiscalPeriod.isOpen original)

                System.Threading.Thread.Sleep(10)
                let attemptResult = FiscalPeriod.reopenFiscalPeriod id (AuditEnvelope.create FiscalPeriodReopen) (Some transaction)
                Assert.True(attemptResult.IsError)

                let! fetched = FiscalPeriod.fetchById (Some transaction) id
                Assert.True(FiscalPeriod.isOpen fetched)
                Assert.Equal(originalModified, FiscalPeriod.modifiedAt fetched)
                ()
            }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail e
        finally
            DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore

    [<Fact>]
    member _.``REQ-SYS-3.2 insertNewToDb sets create and modified timestamps`` () =
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
