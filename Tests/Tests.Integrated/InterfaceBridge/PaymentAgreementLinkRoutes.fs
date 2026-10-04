module Tests.Integrated.InterfaceBridge.PaymentAgreementLinkRoutes

open System
open App.DataAccessLayer.DbTransaction
open App.DataAccessLayer.ExecuteReader
open App.Operation.CoreAuditableAction
open App.Session
open App.Utility
open App.Utility.FieldUpdate
open App.Utility.IAppError
open App.Utility.Json
open App.Utility.Result
open Business.General
open Business.FinancialServices
open Business.FinancialServices.Ledger.JournalEntryComponent
open Business.FinancialServices.Classification.ClassificationComponent
open Business.FinancialServices.DataIngestion
open Business.FinancialServices.DataIngestion.StageEntryComponent
open Business.FinancialServices.CashFlow
open Business.FinancialServices.CashFlow.CashFlowComponent
open Business.FinancialServices.CashFlow.CashFlowAuditableAction
open Business.CrossDomainOrchestration
open Business.CrossDomainOrchestration.FetchFilters
open Ui.InterfaceBridge.CommandRoute
open NodaTime
open Tests.Helpers
open Tests.Helpers.EntityFunctions
open Tests.Helpers.Railroad
open Tests.Helpers.RouteResolver
open Xunit

module CashFlowContracts = Ui.InterfaceBridge.InterfaceContracts.CashFlowContracts
module ClassificationContracts = Ui.InterfaceBridge.InterfaceContracts.ClassificationContracts

(* The Classification domain's Payment Agreement verbs, each through its route: CreatePaymentAgreementLink,
   UpdatePaymentAgreementLink and DeletePaymentAgreementLink address a Payment Agreement by name and a link by ID;
   ClassifyPaymentAgreements links and matches. The routes commit, so every test builds its own Daily Outgo agreement
   (debit F-2230, credit F-1280, 100.00 legs) and its own Ingested staged entry (Debit F-2230 and Credit F-1280,
   100.00, dated today), each committed, and a finally deletes, in order, the rule's matches and the rule, the agreement
   with its links, Instances, Invoices and Payments, then the staged entries. A ClassifyPaymentAgreements run reads
   every staged entry in the database; the test checks that everything the run decided concerns its own line. *)

let private fresh () = Context.create NoTransaction FetchOnly

let private send (verb: string) (input: 'a) =
    input |> Json.toJson |> Result.bind (routeUiCommandForTesting "Classification" verb [])

let private linkReturnOf (payload: string) = Json.fromJson<ClassificationContracts.PaymentAgreementLinkReturn> payload

/// Builds what a test needs, each piece committed, and remembers it for clean-up.
type private World(fixture: TestDataFixture) =
    let testBank =
        fixture.Data.ingestionSources
        |> List.find (fun source -> source |> IngestionSource.name |> JournalRefFinancialInstitution.value = "TestBank")
    let agreements = ResizeArray<Guid>()
    let stageEntries = ResizeArray<StageEntryHeaderId>()
    let rules = ResizeArray<ClassificationRuleId>()

    member _.today = Calendar.today ()

    /// A Daily Outgo agreement, next instance today, with a 100.00 leg of each name. Returns its ID and the leg IDs.
    member this.agreement (name: string) (legNames: string list) =
        result {
            let! json =
                ({ agreementName = name
                   direction = "Outgo"
                   cadence = { cadenceType = CashFlowContracts.Daily; nextInstance = this.today }
                   counterparty = "Link route test counterparty"
                   activeBegin = this.today.PlusDays(-30)
                   activeEnd = None
                   memo = None
                   paymentAgreements =
                     legNames
                     |> List.map (fun legName ->
                         { paymentAgreementName = legName
                           debitAccountCode = "F-2230"
                           creditAccountCode = "F-1280"
                           expectedAmount = Some 100.00M
                           daysDueAfterInvoiceDate = Some 0
                           memo = None } : CashFlowContracts.CreatePaymentAgreementFieldsInput) }
                 : CashFlowContracts.CreateAgreementInput)
                |> Json.toJson
            let! _ = routeUiCommandForTesting "CashFlow" "CreateAgreement" [] json
            let! stored =
                ({ agreementIds = None; activeAgreementsOnly = false } : AgreementFilter)
                |> AgreementOrchestration.fetchFiltered (fresh ()) AnyQuantityIsAcceptable
            let agreement =
                stored
                |> List.find (fun a -> a |> AgreementOrchestration.masterAgreement |> MasterAgreement.agreementName |> AgreementName.value = name)
            let agreementId = agreement |> AgreementOrchestration.masterAgreement |> MasterAgreement.agreementID
            agreements.Add(agreementId |> MasterAgreementId.value)
            let legIdOf legName =
                agreement
                |> AgreementOrchestration.paymentAgreements
                |> List.find (fun pa -> pa |> PaymentAgreement.paymentAgreementName |> PaymentAgreementName.value = legName)
                |> PaymentAgreement.paymentAgreementId
            return agreementId, legNames |> List.map legIdOf
        }

    /// An Ingested staged entry whose description carries the tag. Returns its Debit line.
    member this.stagedLine (tag: string) =
        runCommandRouteAndAutoCompleteTransaction CashFlowCreatePayment (fun context ->
            result {
                let start = Clock.now ()
                let! staged =
                    createStageEntryForTest context "/tmp/link-route-test.dat" $"Link route test {tag}"
                        (Guid.NewGuid().ToString()) testBank this.today
                        [ (100.00M, "Debit", Some "F-2230", None, None); (100.00M, "Credit", Some "F-1280", None, None) ]
                        [ (None, "Ingested", start, "StageIngestion") ]
                stageEntries.Add(staged |> StageEntryOrchestration.stageEntryHeader |> StageEntryHeader.stageEntryHeaderId)
                return
                    staged
                    |> StageEntryOrchestration.seLines
                    |> List.find (fun l -> l |> StageEntryLine.lineType = JournalEntryLineType.Debit)
                    |> StageEntryLine.stageEntryLineId
            })

    /// An Instance today holding one 100.00 Invoice, due today, on the leg. Returns the Instance and Invoice IDs.
    member this.openInvoice (agreementId: MasterAgreementId) (legId: PaymentAgreementId) =
        runCommandRouteAndAutoCompleteTransaction CashFlowCreateInstance (fun context ->
            result {
                let! amount = Money.fromDecimal 100.00M
                let! created =
                    InstanceOrchestration.constructNewAndPersist context agreementId this.today
                        [ (legId, None, InvoiceDate.create this.today, DueDate.create this.today, InvoiceAmount.create amount,
                           InvoiceReceived, None, None, []) ]
                let instanceId = created |> InstanceOrchestration.instance |> Instance.instanceId
                let invoiceId =
                    created |> InstanceOrchestration.invoiceComposites |> List.exactlyOne |> InstanceOrchestration.invoice |> Invoice.invoiceId
                return instanceId, invoiceId
            })

    /// A Payment Agreement rule, through its route, claiming the Debit line of any entry whose description has the tag.
    member _.rule (legName: string) (tag: string) =
        result {
            let! payload =
                send "NewClassificationRule"
                    ({ classificationRuleName = $"Link route rule {tag}"
                       claimantAtMatch = ClassificationContracts.ClassificationClaimantInput.PaymentAgreement legName
                       priority = 37
                       ruleGroups =
                         [ { connector = "And"
                             chainOne =
                               ({ chain =
                                   [ ClassificationContracts.FieldMatchContract.Description tag
                                     ClassificationContracts.FieldMatchContract.LineType "Debit" ] }
                                : ClassificationContracts.FieldMatchChainContract)
                             chainTwo = None } ] } : ClassificationContracts.NewClassificationRuleInput)
            let! created = Json.fromJson<ClassificationContracts.ClassificationRuleReturn> payload
            let ruleId = created.classificationRuleId |> ClassificationRuleId.fromGuid
            rules.Add ruleId
            return ruleId
        }

    member _.cleanUp () =
        [ for ruleId in rules do
            yield Cleanup.cleanUpRuleMatchesOfRuleId (Some ruleId)
            yield Cleanup.cleanUpClassificationRuleId (Some ruleId)
          for id in agreements do
            yield Cleanup.cleanUpMasterAgreementTree (Some id)
          for headerId in stageEntries do
            yield Cleanup.cleanUpStageEntryHeaderId (Some headerId) ]
        |> List.choose (function
            | Ok () -> None
            | Error e -> Some(e.ToMessage()))

let private withWorld (fixture: TestDataFixture) (test: World -> Result<unit, IAppError>) =
    let w = World(fixture)
    let mutable cleanUpFailures = []
    try
        test w |> railroadWrapper
    finally
        cleanUpFailures <- w.cleanUp ()
    Assert.Empty(cleanUpFailures)

let private storedLink (linkId: Guid) =
    linkId |> PaymentAgreementLinkId.fromGuid |> PaymentAgreementLink.fetchById (fresh ())

[<Collection("SharedTestData")>]
type PaymentAgreementLinkRouteTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-CF-12.7 a CreatePaymentAgreementLink payload naming a Payment Agreement links the staged line to it, and an UpdatePaymentAgreementLink payload naming another re-points the same link, each returned and stored as sent`` () =
        withWorld fixture (fun w ->
            result {
                let name = $"CF-12.7 link route {Guid.NewGuid():N}"
                let firstLeg, secondLeg = $"{name} first", $"{name} second"
                let! _, legIds = w.agreement name [ firstLeg; secondLeg ]
                let! line = w.stagedLine (Guid.NewGuid().ToString("N"))
                let! createdPayload =
                    send "CreatePaymentAgreementLink"
                        ({ paymentAgreementName = firstLeg; stageEntryLineId = line |> StageEntryLineId.value }
                         : ClassificationContracts.CreatePaymentAgreementLinkInput)
                let! created = linkReturnOf createdPayload
                let! afterCreate = storedLink created.paymentAgreementLinkId
                let! repointedPayload =
                    send "UpdatePaymentAgreementLink"
                        ({ paymentAgreementLinkId = created.paymentAgreementLinkId; paymentAgreementNameUpdate = SetTo secondLeg }
                         : ClassificationContracts.UpdatePaymentAgreementLinkInput)
                let! repointed = linkReturnOf repointedPayload
                let! afterRepoint = storedLink created.paymentAgreementLinkId
                Assert.Equal((firstLeg, line |> StageEntryLineId.value), (created.paymentAgreementName, created.stageEntryLineId))
                Assert.Equal((legIds[0], line), (afterCreate |> PaymentAgreementLink.paymentAgreementId, afterCreate |> PaymentAgreementLink.stageEntryLineId))
                Assert.Equal(
                    (created.paymentAgreementLinkId, secondLeg, line |> StageEntryLineId.value),
                    (repointed.paymentAgreementLinkId, repointed.paymentAgreementName, repointed.stageEntryLineId))
                Assert.Equal((legIds[1], line), (afterRepoint |> PaymentAgreementLink.paymentAgreementId, afterRepoint |> PaymentAgreementLink.stageEntryLineId))
            })

    [<Fact>]
    member _.``REQ-CF-12.7 a CreatePaymentAgreementLink payload naming no stored Payment Agreement is rejected with a typed error naming the name, and no link is stored for the line`` () =
        withWorld fixture (fun w ->
            result {
                let unknown = $"CF-12.7 no such leg {Guid.NewGuid():N}"
                let! line = w.stagedLine (Guid.NewGuid().ToString("N"))
                let attempt =
                    send "CreatePaymentAgreementLink"
                        ({ paymentAgreementName = unknown; stageEntryLineId = line |> StageEntryLineId.value }
                         : ClassificationContracts.CreatePaymentAgreementLinkInput)
                let! links = [ line ] |> PaymentAgreementLink.fetchByStageEntryLineIdList (fresh ())
                let () =
                    match attempt with
                    | Error (AsError (CashFlowError.CashflowPaymentAgreementNameDoesntMatchId name)) -> Assert.Equal(unknown, name)
                    | Error e -> Assert.Fail $"Wrong error. {e.DomainName}.{e.CaseName}: {e.ToMessage()}"
                    | Ok _ -> Assert.Fail "Expected failure; got success"
                Assert.Empty(links)
            })

    [<Fact>]
    member _.``REQ-CF-12.7 a DeletePaymentAgreementLink payload deletes the link and returns it as it stood before the delete`` () =
        withWorld fixture (fun w ->
            result {
                let name = $"CF-12.7 delete route {Guid.NewGuid():N}"
                let legName = $"{name} leg"
                let! _ = w.agreement name [ legName ]
                let! line = w.stagedLine (Guid.NewGuid().ToString("N"))
                let! createdPayload =
                    send "CreatePaymentAgreementLink"
                        ({ paymentAgreementName = legName; stageEntryLineId = line |> StageEntryLineId.value }
                         : ClassificationContracts.CreatePaymentAgreementLinkInput)
                let! created = linkReturnOf createdPayload
                let! deletedPayload =
                    send "DeletePaymentAgreementLink"
                        ({ paymentAgreementLinkId = created.paymentAgreementLinkId } : ClassificationContracts.DeletePaymentAgreementLinkInput)
                let! deleted = linkReturnOf deletedPayload
                let! links = [ line ] |> PaymentAgreementLink.fetchByStageEntryLineIdList (fresh ())
                Assert.Equal(created, deleted)
                Assert.Empty(links)
            })

    [<Fact>]
    member _.``REQ-CF-12.3 REQ-CF-13.9 a ClassifyPaymentAgreements request links the staged line a Payment Agreement rule claims and returns the link, the decision naming the agreement, link and rule, the Payment created on the open Invoice, and every open Instance after matching`` () =
        withWorld fixture (fun w ->
            result {
                let tag = Guid.NewGuid().ToString("N")
                let name = $"CF-13.9 classify route {tag}"
                let legName = $"{name} leg"
                let! agreementId, legIds = w.agreement name [ legName ]
                let! line = w.stagedLine tag
                let! instanceId, invoiceId = w.openInvoice agreementId legIds[0]
                let! ruleId = w.rule legName tag
                let! payload = routeUiCommandForTesting "Classification" "ClassifyPaymentAgreements" [] ""
                let! run = Json.fromJson<ClassificationContracts.PaymentAgreementClassificationResultReturn> payload
                let! openAfter = Instance.fetchOpen (fresh ())
                let! payments = [ invoiceId ] |> Payment.fetchByInvoiceIdList (fresh ())
                let lineUuid = line |> StageEntryLineId.value
                // everything the run decided concerns this test's line
                Assert.Equal<Set<Guid>>(
                    set [ lineUuid ],
                    (run.linksCreated |> List.map (fun l -> l.stageEntryLineId)) @ (run.decisionLog |> List.map (fun d -> d.stageEntryLineId))
                    |> Set.ofList)
                let link = Assert.Single(run.linksCreated)
                Assert.Equal((legName, lineUuid), (link.paymentAgreementName, link.stageEntryLineId))
                let decision = Assert.Single(run.decisionLog)
                Assert.Equal(
                    (Some legName, Some link.paymentAgreementLinkId, [ ruleId |> ClassificationRuleId.value ], "Linked"),
                    (decision.paymentAgreementName, decision.paymentAgreementLinkId, decision.ruleIds, decision.outcome))
                Assert.Equal<ClassificationContracts.InvoiceDecisionReturn list>(
                    [ ({ invoiceId = invoiceId |> InvoiceId.value
                         outcome = ClassificationContracts.InvoiceDecisionOutcomeReturn.PaymentCreated lineUuid }
                       : ClassificationContracts.InvoiceDecisionReturn) ],
                    run.invoiceDecisionLog)
                let payment = Assert.Single(payments)
                Assert.Equal(Staged line, payment |> Payment.transactionPointer)
                Assert.Equal<Set<Guid>>(
                    openAfter |> List.map (Instance.instanceId >> InstanceId.value) |> Set.ofList,
                    run.openInstances |> List.map (fun c -> c.instance.instanceId) |> Set.ofList)
                Assert.DoesNotContain(instanceId |> InstanceId.value, run.openInstances |> List.map (fun c -> c.instance.instanceId))
            })
