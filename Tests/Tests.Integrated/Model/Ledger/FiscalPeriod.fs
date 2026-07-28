namespace Tests.Integrated.Model.Ledger

open System
open DataAccessLayer.DbTransaction
open Logger.Audit
open Model.Ledger.FiscalPeriods
open ModelOrchestrator
open Tests.Integrated
open Tests.Integrated.GenericTestProperties
open Tests.Integrated.InterfaceBridge._routeResolver
open Tests.Integrated.Railroad
open Utilities.ResultHelper
open Xunit
open Utilities.AppError
open Context.Context

[<Collection("SharedTestData")>]
type FiscalPeriodTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-FP-2.1 creating a fiscal period must generate a UUID``() =
        runFuncAndAutoRollback AccountCreate (fun context ->
            result {
                let! fp = genericFiscalPeriodKey |> FiscalPeriodCreation.constructNewAndSaveToDb context
                let unique_id = FiscalPeriod.fiscalPeriodId fp |> FiscalPeriodId.value
                Assert.NotEqual(unique_id, Guid.Empty)
                ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-FP-1.3 REQ-FP-2.2 Period Key must be unique``() =
        runFuncAndAutoRollback AccountCreate (fun context ->
                result {
                    let! existingPeriod = FiscalPeriod.fetchById context (fixture.Data.openFiscalPeriodIds |> List.head)
                    let existingKey = FiscalPeriod.periodKey existingPeriod
                    let duplicateResult = existingKey |> FiscalPeriodCreation.constructNewAndSaveToDb context
                    do!
                        match duplicateResult with
                        | Error(DalErrorDuringNonQueryExecution _) -> Ok()
                        | Ok _ -> Error(TestingError "Expected failure; returned success.")
                        | Error e -> Error(TestingError $"Wrong error type: {AppError.toMessage e}")
                    Assert.True(Result.isError duplicateResult)
                    ()
                })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-FP-2.4 REQ-FP-2.5 insertNewToDb happy path``() =
        let expectedKey =
            "2050-10"
            |> FiscalPeriodKey.fromString
            |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
        let expectedStartMonth = 10
        let expectedStartDay = 1
        let expectedEndMonth = 10
        let expectedEndDay = 31
        let expectedIsOpen = true
        runFuncAndAutoRollback AccountCreate (fun context ->
            result {
                let! fp = expectedKey |> FiscalPeriodCreation.constructNewAndSaveToDb context
                let startDate = FiscalPeriod.startDate fp
                let endDate = FiscalPeriod.endDate fp
                let uuid = FiscalPeriod.fiscalPeriodId fp |> FiscalPeriodId.value
                Assert.NotEqual(uuid, Guid.Empty)
                Assert.Equal(expectedKey, FiscalPeriod.periodKey fp)
                Assert.Equal(expectedStartMonth, startDate.Month)
                Assert.Equal(expectedStartDay, startDate.Day)
                Assert.Equal(expectedEndMonth, endDate.Month)
                Assert.Equal(expectedEndDay, endDate.Day)
                Assert.Equal(expectedIsOpen, FiscalPeriod.isOpen fp)
                return ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-FP-2.6 is open is automatically true``() =
        let expectedIsOpen = true
        runFuncAndAutoRollback AccountCreate (fun context ->
            result {
                let! fp = genericFiscalPeriodKey |> FiscalPeriodCreation.constructNewAndSaveToDb context
                Assert.Equal(expectedIsOpen, FiscalPeriod.isOpen fp)
                ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-FP-3.1 fetchById happy path``() =
        let expectedId = fixture.Data.openFiscalPeriodIds |> List.head
        let context = create NoTransaction FetchOnly
        result {
            let! fetched = FiscalPeriod.fetchById context expectedId
            Assert.Equal(expectedId, FiscalPeriod.fiscalPeriodId fetched)
            Assert.True(FiscalPeriod.isOpen fetched)
            ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-FP-3.4 fetchAll without filter happy path``() =
        let context = create NoTransaction FetchOnly
        result {
            let! fetched = FiscalPeriod.fetchAll context false
            fixture.Data.openFiscalPeriodIds
            |> List.forall(fun id -> fetched |> List.exists(fun fp -> FiscalPeriod.fiscalPeriodId fp = id))
            |> Assert.True
            fetched
            |> List.exists(fun fp -> FiscalPeriod.fiscalPeriodId fp = fixture.Data.closedFiscalPeriodId)
            |> Assert.True
            ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-FP-3.5 fetchAll with open only filters out closed periods``() =
        let context = create NoTransaction FetchOnly
        result {
            let! fetched = FiscalPeriod.fetchAll context true
            fetched
            |> List.exists(fun fp -> FiscalPeriod.fiscalPeriodId fp = fixture.Data.closedFiscalPeriodId)
            |> Assert.False
            fixture.Data.openFiscalPeriodIds
            |> List.forall(fun id -> fetched |> List.exists(fun fp -> FiscalPeriod.fiscalPeriodId fp = id))
            |> Assert.True
            ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-FP-4.1 closeFiscalPeriod happy path``() =
        runFuncAndAutoRollback AccountCreate (fun context ->
            result {
                let id = fixture.Data.openFiscalPeriodIds |> List.head
                let! closed = id |> FiscalPeriod.closeFiscalPeriod context
                Assert.False(FiscalPeriod.isOpen closed)
                ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-FP-4.1.1 closeFiscalPeriod rejects already closed period``() =
        runFuncAndAutoRollback AccountCreate (fun context ->
            result {
                let! original = FiscalPeriod.fetchById context fixture.Data.closedFiscalPeriodId
                let originalModified = FiscalPeriod.modifiedAt original
                Assert.False(FiscalPeriod.isOpen original)
                System.Threading.Thread.Sleep(10) // this is here to ensure that we haven't updated the modified date
                let attemptResult =
                    FiscalPeriod.closeFiscalPeriod
                        context
                        fixture.Data.closedFiscalPeriodId
                do!
                    match attemptResult with
                    | Error(DalResultantRowsDidntMatchExpectation _) -> Ok() // todo: create a no op error for this
                    | Ok _ -> Error(TestingError "Expected failure; returned success.")
                    | Error e -> Error(TestingError $"Wrong error type: {AppError.toMessage e}")
                let! fetched = FiscalPeriod.fetchById context fixture.Data.closedFiscalPeriodId
                Assert.False(FiscalPeriod.isOpen fetched)
                Assert.Equal(originalModified, FiscalPeriod.modifiedAt fetched)
                ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-FP-4.2 reopenFiscalPeriod happy path``() =
        runFuncAndAutoRollback AccountCreate (fun context ->
            result {
                let! reopened = fixture.Data.closedFiscalPeriodId |> FiscalPeriod.reopenFiscalPeriod context
                Assert.True(FiscalPeriod.isOpen reopened)
                ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-FP-4.2.1 reopenFiscalPeriod rejects already open period``() =
        runFuncAndAutoRollback AccountCreate (fun context ->
            result {
                let id = fixture.Data.openFiscalPeriodIds |> List.head
                let! original = FiscalPeriod.fetchById context id
                let originalModified = FiscalPeriod.modifiedAt original
                Assert.True(FiscalPeriod.isOpen original)
                System.Threading.Thread.Sleep(10) // this is here to ensure that we haven't updated the modified date
                let attemptResult = id |> FiscalPeriod.reopenFiscalPeriod context
                do!
                    match attemptResult with
                    | Error(DalResultantRowsDidntMatchExpectation _) -> Ok() // todo: create a no op error for this
                    | Ok _ -> Error(TestingError "Expected failure; returned success.")
                    | Error e -> Error(TestingError $"Wrong error type: {AppError.toMessage e}")
                let! fetched = FiscalPeriod.fetchById context id
                Assert.True(FiscalPeriod.isOpen fetched)
                Assert.Equal(originalModified, FiscalPeriod.modifiedAt fetched)
                ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-SYS-3.2 insertNewToDb sets create and modified timestamps``() =
        runFuncAndAutoRollback AccountCreate (fun context ->
            let expected = context |> getInitiationInstant
            result {
                let! fp =
                    genericFiscalPeriodKey |> FiscalPeriodCreation.constructNewAndSaveToDb context
                Assert.Equal(expected, FiscalPeriod.createdAt fp)
                Assert.Equal(expected, FiscalPeriod.modifiedAt fp)
                ()
            })
        |> railroadWrapper
