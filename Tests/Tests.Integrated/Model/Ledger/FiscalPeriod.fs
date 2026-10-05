namespace Tests.Integrated.Model.Ledger

open App.Session
open Business.General
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.CrossDomainOrchestration
open System
open App.DataAccessLayer.DbTransaction
open Ui.InterfaceBridge.CommandRoute
open App.Operation.AuditEnvelope
open Business.FinancialServices.Ledger.FiscalPeriodComponent
open Tests.Helpers
open Tests.Helpers.GenericTestProperties
open Tests.Helpers.Railroad
open App.Utility.Result
open Xunit
open App.Utility.IAppError
open Tests.Helpers.TestError
open Tests.Helpers.SadPath
open App.Operation.CoreAuditableAction
open Business.FinancialServices.Ledger.LedgerAuditableAction
open Business.FinancialServices.DataIngestion.DataIngestionAuditableAction
open Business.FinancialServices.Classification.ClassificationAuditableAction
open Business.FinancialServices.CashFlow.CashFlowAuditableAction
open App.DataAccessLayer.DalError
open Business.FinancialServices.Ledger.LedgerError


[<Collection("SharedTestData")>]
type FiscalPeriodTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-FP-2.1 two fiscal periods created in one transaction get distinct generated IDs, each stored against its own key``() =
        runCommandRouteAndAutoRollback AccountCreate (fun context ->
            result {
                let! firstKey = "2051-01" |> FiscalPeriodKey.fromString
                let! secondKey = "2051-02" |> FiscalPeriodKey.fromString
                let! first = firstKey |> FiscalPeriodCreation.constructNewAndPersist context
                let! second = secondKey |> FiscalPeriodCreation.constructNewAndPersist context
                Assert.NotEqual(first |> FiscalPeriod.fiscalPeriodId, second |> FiscalPeriod.fiscalPeriodId)
                let fixtureIds = fixture.Data.fiscalPeriods |> List.map FiscalPeriod.fiscalPeriodId
                Assert.DoesNotContain(first |> FiscalPeriod.fiscalPeriodId, fixtureIds)
                Assert.DoesNotContain(second |> FiscalPeriod.fiscalPeriodId, fixtureIds)
                let! storedFirst = FiscalPeriod.fetchIdByKey context "2051-01"
                let! storedSecond = FiscalPeriod.fetchIdByKey context "2051-02"
                Assert.Equal(first |> FiscalPeriod.fiscalPeriodId, storedFirst)
                Assert.Equal(second |> FiscalPeriod.fiscalPeriodId, storedSecond)
                ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-FP-1.3 REQ-FP-2.2 Period Key must be unique``() =
        runCommandRouteAndAutoRollback AccountCreate (fun context ->
            result {
                let! existingPeriod = FiscalPeriod.fetchById context (fixture.Data.openFiscalPeriodIds |> List.head)
                let existingKey = FiscalPeriod.periodKey existingPeriod
                do!
                    isCorrectError
                        (existingKey |> FiscalPeriodCreation.constructNewAndPersist context)
                        DalErrorDuringNonQueryExecution
                        None
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-FP-2.4 REQ-FP-2.5 persist happy path``() =
        let expectedKey =
            "2050-10"
            |> FiscalPeriodKey.fromString
            |> Result.defaultWith(fun (e: IAppError) -> failwith(e.ToMessage()))
        let expectedYear = 2050
        let expectedStartMonth = 10
        let expectedStartDay = 1
        let expectedEndMonth = 10
        let expectedEndDay = 31
        let expectedIsOpen = true
        runCommandRouteAndAutoRollback AccountCreate (fun context ->
            result {
                let! fp = expectedKey |> FiscalPeriodCreation.constructNewAndPersist context
                let startDate = FiscalPeriod.startDate fp
                let endDate = FiscalPeriod.endDate fp
                let uuid = FiscalPeriod.fiscalPeriodId fp |> FiscalPeriodId.value
                Assert.NotEqual(uuid, Guid.Empty)
                Assert.Equal(expectedKey, FiscalPeriod.periodKey fp)
                Assert.Equal(expectedYear, startDate.Year)
                Assert.Equal(expectedStartMonth, startDate.Month)
                Assert.Equal(expectedStartDay, startDate.Day)
                Assert.Equal(expectedYear, endDate.Year)
                Assert.Equal(expectedEndMonth, endDate.Month)
                Assert.Equal(expectedEndDay, endDate.Day)
                Assert.Equal(expectedIsOpen, FiscalPeriod.isOpen fp)
                return ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-FP-2.6 is open is automatically true``() =
        let expectedIsOpen = true
        runCommandRouteAndAutoRollback AccountCreate (fun context ->
            result {
                let! fp = genericFiscalPeriodKey |> FiscalPeriodCreation.constructNewAndPersist context
                Assert.Equal(expectedIsOpen, FiscalPeriod.isOpen fp)
                ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-FP-3.1 fetchById returns a period carrying every property the fixture created it with, not just its id and open flag``() =
        let expectedId = fixture.Data.openFiscalPeriodIds |> List.head
        (* Expectations come from the period the fixture built in memory, never from a second
           read, so a read path that dropped or garbled a column cannot agree with itself.
           Dates and timestamps were previously asserted only on the in-memory value returned
           by creation -- never on a period that had made the round trip through the database. *)
        let expected =
            fixture.Data.fiscalPeriods
            |> List.find (fun fp -> fp |> FiscalPeriod.fiscalPeriodId = expectedId)
        let context = Context.create NoTransaction FetchOnly
        result {
            let! fetched = FiscalPeriod.fetchById context expectedId
            Assert.Equal(expectedId, fetched |> FiscalPeriod.fiscalPeriodId)
            Assert.Equal(expected |> FiscalPeriod.periodKey, fetched |> FiscalPeriod.periodKey)
            Assert.Equal(expected |> FiscalPeriod.startDate, fetched |> FiscalPeriod.startDate)
            Assert.Equal(expected |> FiscalPeriod.endDate, fetched |> FiscalPeriod.endDate)
            Assert.Equal(expected |> FiscalPeriod.createdAt, fetched |> FiscalPeriod.createdAt)
            Assert.Equal(expected |> FiscalPeriod.modifiedAt, fetched |> FiscalPeriod.modifiedAt)
            Assert.Equal(expected |> FiscalPeriod.isOpen, fetched |> FiscalPeriod.isOpen)
            // the period drawn from openFiscalPeriodIds really is open, or the line above is a
            // pair of matching falses
            Assert.True(fetched |> FiscalPeriod.isOpen)
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-FP-3.4 fetchAll without filter returns exactly the fixture's periods, open and closed``() =
        let context = Context.create NoTransaction FetchOnly
        let expectedKeys =
            fixture.Data.fiscalPeriods
            |> List.map(FiscalPeriod.periodKey >> FiscalPeriodKey.value)
            |> List.sort
        result {
            let! fetched = FiscalPeriod.fetchAll context false
            Assert.Equal<string list>(expectedKeys, fetched |> List.map(FiscalPeriod.periodKey >> FiscalPeriodKey.value) |> List.sort)
            ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-FP-3.5 fetchAll with open only returns exactly the fixture's open periods``() =
        let context = Context.create NoTransaction FetchOnly
        let expectedKeys =
            fixture.Data.fiscalPeriods
            |> List.filter FiscalPeriod.isOpen
            |> List.map(FiscalPeriod.periodKey >> FiscalPeriodKey.value)
            |> Set.ofList
        result {
            let! fetched = FiscalPeriod.fetchAll context true
            Assert.DoesNotContain(fixture.Data.closedFiscalPeriodId, fetched |> List.map FiscalPeriod.fiscalPeriodId)
            Assert.Equal<Set<string>>(expectedKeys, fetched |> List.map(FiscalPeriod.periodKey >> FiscalPeriodKey.value) |> Set.ofList)
            ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-FP-4.1 closeFiscalPeriod happy path``() =
        runCommandRouteAndAutoRollback AccountCreate (fun context ->
            result {
                let id = fixture.Data.openFiscalPeriodIds |> List.head
                let expectedKey =
                    fixture.Data.fiscalPeriods |> List.find (fun fp -> FiscalPeriod.fiscalPeriodId fp = id) |> FiscalPeriod.periodKey
                let! closed = id |> FiscalPeriod.closeFiscalPeriod context
                Assert.False(FiscalPeriod.isOpen closed)
                Assert.Equal(id, closed |> FiscalPeriod.fiscalPeriodId)
                Assert.Equal(expectedKey, closed |> FiscalPeriod.periodKey)
                ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-FP-4.1.1 closeFiscalPeriod rejects already closed period``() =
        runCommandRouteAndAutoRollback AccountCreate (fun context ->
            result {
                let! original = FiscalPeriod.fetchById context fixture.Data.closedFiscalPeriodId
                let originalModified = FiscalPeriod.modifiedAt original
                Assert.False(FiscalPeriod.isOpen original)
                System.Threading.Thread.Sleep(10) // this is here to ensure that we haven't updated the modified date
                do!
                    isCorrectErrorEmpty
                        (FiscalPeriod.closeFiscalPeriod context fixture.Data.closedFiscalPeriodId)
                        FiscalPeriodToggleOpenNoOp
                        None
                let! fetched = FiscalPeriod.fetchById context fixture.Data.closedFiscalPeriodId
                Assert.False(FiscalPeriod.isOpen fetched)
                Assert.Equal(originalModified, FiscalPeriod.modifiedAt fetched)
                ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-FP-4.2 reopenFiscalPeriod happy path``() =
        runCommandRouteAndAutoRollback AccountCreate (fun context ->
            result {
                let id = fixture.Data.closedFiscalPeriodId
                let expectedKey =
                    fixture.Data.fiscalPeriods |> List.find (fun fp -> FiscalPeriod.fiscalPeriodId fp = id) |> FiscalPeriod.periodKey
                let! reopened = id |> FiscalPeriod.reopenFiscalPeriod context
                Assert.True(FiscalPeriod.isOpen reopened)
                Assert.Equal(id, reopened |> FiscalPeriod.fiscalPeriodId)
                Assert.Equal(expectedKey, reopened |> FiscalPeriod.periodKey)
                ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-FP-4.2.1 reopenFiscalPeriod rejects already open period``() =
        runCommandRouteAndAutoRollback AccountCreate (fun context ->
            result {
                let id = fixture.Data.openFiscalPeriodIds |> List.head
                let! original = FiscalPeriod.fetchById context id
                let originalModified = FiscalPeriod.modifiedAt original
                Assert.True(FiscalPeriod.isOpen original)
                System.Threading.Thread.Sleep(10) // this is here to ensure that we haven't updated the modified date
                do!
                    isCorrectErrorEmpty
                        (id |> FiscalPeriod.reopenFiscalPeriod context)
                        FiscalPeriodToggleOpenNoOp
                        None
                let! fetched = FiscalPeriod.fetchById context id
                Assert.True(FiscalPeriod.isOpen fetched)
                Assert.Equal(originalModified, FiscalPeriod.modifiedAt fetched)
                ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-SYS-3.2 persist sets create and modified timestamps``() =
        runCommandRouteAndAutoRollback AccountCreate (fun context ->
            let expected = context |> Context.getInitiationInstant
            result {
                let! fp = genericFiscalPeriodKey |> FiscalPeriodCreation.constructNewAndPersist context
                Assert.Equal(expected, FiscalPeriod.createdAt fp)
                Assert.Equal(expected, FiscalPeriod.modifiedAt fp)
                ()
            })
        |> railroadWrapper
