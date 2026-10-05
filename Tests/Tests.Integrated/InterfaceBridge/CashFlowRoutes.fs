module Tests.Integrated.InterfaceBridge.CashFlowRoutes

open System
open App.DataAccessLayer.DbTransaction
open App.DataAccessLayer.ExecuteNonQuery
open App.DataAccessLayer.ExecuteReader
open App.DataAccessLayer.QueryParameter
open App.Operation.CoreAuditableAction
open App.Session
open App.Utility
open App.Utility.IAppError
open App.Utility.Json
open App.Utility.Result
open Business.General
open Business.FinancialServices.CashFlow
open Business.FinancialServices.CashFlow.CashFlowComponent
open NodaTime
open Tests.Helpers
open Tests.Helpers.Railroad
open Tests.Helpers.RouteResolver
open Xunit

module Contracts = Ui.InterfaceBridge.InterfaceContracts.CashFlowContracts

(* The CreateUpcomingInstances route: a horizon in days goes in, every open Instance comes back. The route commits,
   and the sweep it runs reads every active agreement, the fixture's included. So the test that runs a sweep snapshots
   every agreement's Instances, Invoices, next-instance date and modified-at first, makes its own Daily agreement, and
   in a finally deletes its agreement, deletes what the sweep added to any other agreement, puts back each other
   agreement's next-instance date and modified-at, and checks the database matches the snapshot again. *)

let private fresh () = Context.create NoTransaction FetchOnly

let private sweepRoute (horizon: int) =
    ({ projectionHorizonInDays = horizon } : Contracts.CreateUpcomingInstancesInput)
    |> Json.toJson
    |> Result.bind (routeUiCommandForTesting "CashFlow" "CreateUpcomingInstances" [])

/// Every agreement with its next-instance date and modified-at, and every Instance and Invoice, sorted by ID.
let private snapshot () =
    result {
        let context = fresh ()
        let! masters = MasterAgreement.fetchAll context
        let agreements =
            masters
            |> List.map (fun m ->
                (m |> MasterAgreement.agreementID |> MasterAgreementId.value),
                (m |> MasterAgreement.cadence |> Cadence.nextInstance).nextInstance,
                (m |> MasterAgreement.modifiedAt))
            |> List.sort
        let! instances =
            if masters |> List.isEmpty then Ok []
            else masters |> List.map MasterAgreement.agreementID |> Instance.fetchByMasterAgreementIdList context
        let instances = instances |> List.sortBy (Instance.instanceId >> InstanceId.value)
        let! invoices =
            if instances |> List.isEmpty then Ok []
            else instances |> List.map Instance.instanceId |> Invoice.fetchByInstanceIdList context
        let invoices = invoices |> List.sortBy (Invoice.invoiceId >> InvoiceId.value)
        return agreements, instances, invoices
    }

let private execute (query: string) (parameters: QueryParameter list) =
    executeNonQuery ((fresh ()) |> Context.getDatabaseTransaction) query parameters AnyQuantityIsAcceptable
    |> Result.map ignore

/// Deletes the Instances (with their Invoices and Payments) not in `before`, and puts back every agreement's
/// next-instance date and modified-at as `before` holds them.
let private restore (before: (Guid * LocalDate * Instant) list * Instance.Instance list * Invoice.Invoice list) =
    result {
        let beforeAgreements, beforeInstances, _ = before
        let! _, afterInstances, _ = snapshot ()
        let beforeIds = beforeInstances |> List.map (Instance.instanceId >> InstanceId.value) |> Set.ofList
        let added =
            afterInstances
            |> List.map (Instance.instanceId >> InstanceId.value)
            |> List.filter (fun id -> beforeIds.Contains id |> not)
        let deletions =
            added
            |> List.collect (fun id ->
                let parameters = [ { name = "@instance_id"; value = UniqueId id } ]
                [ ("""delete from cashflow.payment WHERE invoice_id IN
                        (select unique_id from cashflow.invoice where instance_id = @instance_id);""", parameters)
                  ("""delete from cashflow.invoice WHERE instance_id = @instance_id;""", parameters)
                  ("""delete from cashflow.instance WHERE unique_id = @instance_id;""", parameters) ])
        let resets =
            beforeAgreements
            |> List.map (fun (id, nextInstance, modifiedAt) ->
                ("""update cashflow.master_agreement set next_instance = @next_instance, modified_at = @modified_at
                    WHERE unique_id = @agreement_id;""",
                 [ { name = "@agreement_id"; value = UniqueId id }
                   { name = "@next_instance"; value = DbLocalDate nextInstance }
                   { name = "@modified_at"; value = DbInstant modifiedAt } ]))
        do!
            deletions @ resets
            |> List.fold (fun acc (query, parameters) -> acc |> Result.bind (fun () -> execute query parameters)) (Ok())
        return ()
    }

[<Collection("SharedTestData")>]
type CashFlowRouteTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-CF-7.1 REQ-CF-7.14 a CreateUpcomingInstances payload with a one-day horizon creates a Daily agreement's Instances for today and tomorrow, and returns every open Instance there is after the sweep`` () =
        let name = $"CF-7.1 route sweep {Guid.NewGuid():N}"
        let today = Calendar.today ()
        let mutable before = None
        let cleanUpFailures = ResizeArray<string>()
        try
            result {
                let! taken = snapshot ()
                before <- Some taken
                let! createJson =
                    ({ agreementName = name
                       direction = "Outgo"
                       cadence = { cadenceType = Contracts.Daily; nextInstance = today }
                       counterparty = "Route sweep test counterparty"
                       activeBegin = today.PlusDays(-30)
                       activeEnd = None
                       memo = None
                       paymentAgreements =
                         [ { paymentAgreementName = $"{name} leg"
                             debitAccountCode = "F-2230"
                             creditAccountCode = "F-1280"
                             expectedAmount = Some 100.00M
                             daysDueAfterInvoiceDate = Some 0
                             memo = None } ] } : Contracts.CreateAgreementInput)
                    |> Json.toJson
                let! _ = routeUiCommandForTesting "CashFlow" "CreateAgreement" [] createJson
                let! returnedJson = sweepRoute 1
                let! returned = Json.fromJson<Contracts.InstanceCompositeReturn list> returnedJson
                let! openAfter = Instance.fetchOpen (fresh ())
                let mine =
                    returned
                    |> List.filter (fun c -> c.instance.masterAgreementName = name)
                    |> List.map (fun c -> c.instance.instanceDate)
                    |> List.sort
                Assert.Equal<LocalDate list>([ today; today.PlusDays(1) ], mine)
                Assert.Equal<Set<Guid>>(
                    openAfter |> List.map (Instance.instanceId >> InstanceId.value) |> Set.ofList,
                    returned |> List.map (fun c -> c.instance.instanceId) |> Set.ofList)
            }
            |> railroadWrapper
        finally
            let stored =
                ({ agreementIds = None; activeAgreementsOnly = false } : Business.CrossDomainOrchestration.FetchFilters.AgreementFilter)
                |> Business.CrossDomainOrchestration.AgreementOrchestration.fetchFiltered (fresh ()) AnyQuantityIsAcceptable
            match stored with
            | Ok agreements ->
                for a in agreements do
                    let master = a |> Business.CrossDomainOrchestration.AgreementOrchestration.masterAgreement
                    if master |> MasterAgreement.agreementName |> AgreementName.value = name then
                        match Cleanup.cleanUpMasterAgreementTree (Some(master |> MasterAgreement.agreementID |> MasterAgreementId.value)) with
                        | Ok () -> ()
                        | Error e -> cleanUpFailures.Add(e.ToMessage())
            | Error e -> cleanUpFailures.Add(e.ToMessage())
            match before with
            | None -> ()
            | Some taken ->
                match restore taken |> Result.bind (fun () -> snapshot ()) with
                | Ok restored -> if restored <> taken then cleanUpFailures.Add "the database does not match the snapshot taken before the sweep"
                | Error e -> cleanUpFailures.Add(e.ToMessage())
        Assert.Empty(cleanUpFailures)

    [<Theory>]
    [<InlineData(0)>]
    [<InlineData(366)>]
    member _.``REQ-CF-7.1 for each of 0 and 366, a CreateUpcomingInstances payload with that horizon is rejected with a typed error naming the horizon and the bound, and nothing is written`` (horizon: int) =
        result {
            let! before = snapshot ()
            let attempt = sweepRoute horizon
            let! after = snapshot ()
            let () =
                match attempt with
                | Error (AsError (CashFlowError.CashflowProjectionHorizonInDaysBelowMin(raw, bound))) when horizon = 0 ->
                    Assert.Equal((0, 1), (raw, bound))
                | Error (AsError (CashFlowError.CashflowProjectionHorizonInDaysExceededMax(raw, bound))) when horizon = 366 ->
                    Assert.Equal((366, 365), (raw, bound))
                | Error e -> Assert.Fail $"Wrong error. {e.DomainName}.{e.CaseName}: {e.ToMessage()}"
                | Ok _ -> Assert.Fail "Expected failure; got success"
            Assert.True((before = after), "the database changed")
        }
        |> railroadWrapper

    // Placeholders committed before the Src was read (audit 2026-10-04a remediation)

    [<Fact>]
    member _.``REQ-CF-14.8 an UpdateAgreement payload naming a Payment Agreement of a different Master Agreement is rejected with a typed error, and both agreements are unchanged`` () =
        Assert.Fail "Not yet implemented"
