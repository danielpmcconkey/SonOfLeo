module InterfaceBridge.Routes.CashFlowRoutes

open App.DataAccessLayer.DbTransaction
open InterfaceBridge.BoundaryConverters.CashFlowFieldConverters
open InterfaceBridge.BoundaryConverters.CashFlowLookupConverters
open InterfaceBridge.InterfaceContracts.CashFlowContracts
open InterfaceBridge.InterfaceContracts.SharedContracts
open InterfaceBridge.CommandRoute
open Logger.Audit
open Model
open Business.FinancialServices.CashFlow
open Business.FinancialServices.CashFlow.CashFlowComponent
open ModelOrchestrator
open Utilities
open App.Utility.Json
open App.Utility.Result

let private createUpcomingInstances payload _ =
    runCommandRouteAndAutoCompleteTransaction CashFlowCreateUpcomingInstances (fun context ->
        result {
            let! input = Json.fromJson<CreateUpcomingInstancesInput> payload
            let! horizon = input.projectionHorizonInDays |> ProjectionHorizonInDays.create
            let! openInstances = horizon |> CashFlowOps.createUpcomingInstances context
            let! converted =
                openInstances |> ``convert [InstanceComposite list] to [InstanceCompositeReturn list]`` context
            return! Json.toJson<InstanceCompositeReturn list> converted
        })

let private classifyPaymentAgreements _ _ =
    runCommandRouteAndAutoCompleteTransaction CashFlowClassifyPaymentAgreements (fun context ->
        result {
            let! classificationResult = CashFlowOps.classifyPaymentAgreements context
            let! converted =
                classificationResult
                |> ``convert [PaymentAgreementClassificationResult] to [PaymentAgreementClassificationResultReturn]`` context
            return! Json.toJson<PaymentAgreementClassificationResultReturn> converted
        })

let private transitionPaymentsToPosted _ _ =
    runCommandRouteAndAutoCompleteTransaction CashFlowTransitionPaymentsToPosted (fun context ->
        result {
            let! transitions = CashFlowOps.transitionPaymentsToPosted context
            let converted =
                transitions |> ``convert [PaymentPostingTransition list] to [PaymentPostingTransitionReturn list]``
            return! Json.toJson<PaymentPostingTransitionReturn list> converted
        })

let private projectCashFlow payload _ =
    let context = Context.create NoTransaction FetchOnly
    result {
        let! input = Json.fromJson<ProjectCashFlowInput> payload
        let! horizon = input.projectionHorizonInDays |> ProjectionHorizonInDays.create
        let! projection = horizon |> CashFlowOps.projectCashFlowNDaysForward context
        let converted = projection |> ``convert [CashFlowProjection] to [CashFlowProjectionReturn]``
        return! Json.toJson<CashFlowProjectionReturn> converted
    }

let private createAgreement payload _ =
    runCommandRouteAndAutoCompleteTransaction CashFlowCreateAgreement (fun context ->
        result {
            let! input = Json.fromJson<CreateAgreementInput> payload
            let! agreementName = input.agreementName |> AgreementName.create
            let! direction = input.direction |> FlowDirection.fromString
            let! cadenceType = input.cadence.cadenceType |> ``convert [CadenceTypeContract] to [CadenceType]``
            let firstInstance : Cadence.CadenceNextInstance = { nextInstance = input.cadence.nextInstance }
            let! counterparty = input.counterparty |> Counterparty.create
            let! activityPeriod =
                ActivityPeriod.create input.activeBegin input.activeEnd
                    ActivityPeriod.ConsideredAvailableBeforeBeginDate
            let! memo = input.memo |> convertOptionToDesiredTypeWithFallibleConverter AgreementMemo.create
            let! paymentAgreements =
                input.paymentAgreements
                |> ``convert [CreatePaymentAgreementFieldsInput list] to [PaymentAgreementPrimitives list]`` context
            let! agreement =
                AgreementOrchestration.constructNewAndPersist
                    context agreementName direction cadenceType firstInstance counterparty activityPeriod memo
                    paymentAgreements
            let! converted = agreement |> ``convert [Agreement] to [AgreementReturn]`` context
            return! Json.toJson<AgreementReturn> converted
        })

let private updateAgreement payload _ =
    runCommandRouteAndAutoCompleteTransaction CashFlowUpdateAgreement (fun context ->
        result {
            let! input = Json.fromJson<UpdateAgreementInput> payload
            let! masterAgreementUpdates =
                input |> ``convert [UpdateAgreementInput] to [MasterAgreementFieldUpdates]`` context
            let! agreement = masterAgreementUpdates |> AgreementOrchestration.updateAgreement context [] [] [] []
            let! converted = agreement |> ``convert [Agreement] to [AgreementReturn]`` context
            return! Json.toJson<AgreementReturn> converted
        })

let private createInstance payload _ =
    runCommandRouteAndAutoCompleteTransaction CashFlowCreateInstance (fun context ->
        result {
            let! input = Json.fromJson<CreateInstanceInput> payload
            let! masterAgreementId =
                input.masterAgreementName |> ``convert [AgreementNameString] to [MasterAgreementId]`` context
            let! invoices =
                input.invoices |> ``convert [CreateInvoiceFieldsInput list] to [InvoiceCompositePrimitives list]`` context
            let! instanceComposite =
                InstanceOrchestration.createInstanceCompositeAndSaveToDb
                    context masterAgreementId input.instanceDate input.isFulfilled invoices
            let! converted = instanceComposite |> ``convert [InstanceComposite] to [InstanceCompositeReturn]`` context
            return! Json.toJson<InstanceCompositeReturn> converted
        })

let private createInvoice payload _ =
    runCommandRouteAndAutoCompleteTransaction CashFlowCreateInvoice (fun context ->
        result {
            let! input = Json.fromJson<CreateInvoiceInput> payload
            let! compositeUpdate = input |> ``convert [CreateInvoiceInput] to [InstanceCompositeUpdate]`` context
            let! instanceComposite = compositeUpdate |> InstanceOrchestration.updateInstanceComposite context
            let! converted = instanceComposite |> ``convert [InstanceComposite] to [InstanceCompositeReturn]`` context
            return! Json.toJson<InstanceCompositeReturn> converted
        })

let private updateInvoice payload _ =
    runCommandRouteAndAutoCompleteTransaction CashFlowUpdateInvoice (fun context ->
        result {
            let! input = Json.fromJson<UpdateInvoiceInput> payload
            let! compositeUpdate = input |> ``convert [UpdateInvoiceInput] to [InstanceCompositeUpdate]`` context
            let! instanceComposite = compositeUpdate |> InstanceOrchestration.updateInstanceComposite context
            let! converted = instanceComposite |> ``convert [InstanceComposite] to [InstanceCompositeReturn]`` context
            return! Json.toJson<InstanceCompositeReturn> converted
        })

let private createPayment payload _ =
    runCommandRouteAndAutoCompleteTransaction CashFlowCreatePayment (fun context ->
        result {
            let! input = Json.fromJson<CreatePaymentInput> payload
            let! compositeUpdate = input |> ``convert [CreatePaymentInput] to [InstanceCompositeUpdate]`` context
            let! instanceComposite = compositeUpdate |> InstanceOrchestration.updateInstanceComposite context
            let! converted = instanceComposite |> ``convert [InstanceComposite] to [InstanceCompositeReturn]`` context
            return! Json.toJson<InstanceCompositeReturn> converted
        })

let private deletePayment payload _ =
    runCommandRouteAndAutoCompleteTransaction CashFlowDeletePayment (fun context ->
        result {
            let! input = Json.fromJson<DeletePaymentInput> payload
            let paymentId = input.paymentId |> PaymentId.fromGuid
            let! instanceComposite = paymentId |> CashFlowOps.deletePaymentAndItsLinkage context
            let! converted = instanceComposite |> ``convert [InstanceComposite] to [InstanceCompositeReturn]`` context
            return! Json.toJson<InstanceCompositeReturn> converted
        })

let private createPaymentAgreementLink payload _ =
    runCommandRouteAndAutoCompleteTransaction CashFlowCreatePaymentAgreementLink (fun context ->
        result {
            let! input = Json.fromJson<CreatePaymentAgreementLinkInput> payload
            let! paymentAgreementId =
                input.paymentAgreementName |> ``convert [PaymentAgreementNameString] to [PaymentAgreementId]`` context
            let stageEntryLineId =
                input.stageEntryLineId |> DataIngestion.StageEntryComponent.StageEntryLineId.fromGuid
            let! link =
                stageEntryLineId |> CashFlowOps.constructNewPaymentAgreementLinkAndPersist context paymentAgreementId
            let! converted = link |> ``convert [PaymentAgreementLink] to [PaymentAgreementLinkReturn]`` context
            return! Json.toJson<PaymentAgreementLinkReturn> converted
        })

let private updatePaymentAgreementLink payload _ =
    runCommandRouteAndAutoCompleteTransaction CashFlowUpdatePaymentAgreementLink (fun context ->
        result {
            let! input = Json.fromJson<UpdatePaymentAgreementLinkInput> payload
            let! fieldUpdates =
                input |> ``convert [UpdatePaymentAgreementLinkInput] to [PaymentAgreementLinkFieldUpdates]`` context
            let! link = fieldUpdates |> PaymentAgreementLink.update context
            let! converted = link |> ``convert [PaymentAgreementLink] to [PaymentAgreementLinkReturn]`` context
            return! Json.toJson<PaymentAgreementLinkReturn> converted
        })

let private deletePaymentAgreementLink payload _ =
    runCommandRouteAndAutoCompleteTransaction CashFlowDeletePaymentAgreementLink (fun context ->
        result {
            let! input = Json.fromJson<DeletePaymentAgreementLinkInput> payload
            let linkId = input.paymentAgreementLinkId |> PaymentAgreementLinkId.fromGuid
            let! link = linkId |> PaymentAgreementLink.fetchById context
            do! linkId |> PaymentAgreementLink.delete context
            let! converted = link |> ``convert [PaymentAgreementLink] to [PaymentAgreementLinkReturn]`` context
            return! Json.toJson<PaymentAgreementLinkReturn> converted
        })

let private fetchAgreementSummary payload _ =
    let context = Context.create NoTransaction FetchOnly
    result {
        let! input = Json.fromJson<FetchAgreementSummaryInput> payload
        let! agreementId = input.agreementName |> ``convert [AgreementNameString] to [MasterAgreementId]`` context
        let! agreement = agreementId |> AgreementOrchestration.fetchByMasterAgreementId context
        let! converted = agreement |> ``convert [Agreement] to [AgreementReturn]`` context
        return! Json.toJson<AgreementReturn> converted
    }

let cashFlowDomainCommandRoutes: CommandRoute list =
    [
      { domain = "CashFlow"
        verb = "CreateUpcomingInstances"
        description = "Walk every active agreement's cadence and create the instances, and any fixed-amount invoices, that fall within the horizon. Returns every instance that is not yet fulfilled."
        inputContract = typeof<CreateUpcomingInstancesInput>.Name
        outputContract = typeof<InstanceCompositeReturn list>.Name
        handler = createUpcomingInstances }

      { domain = "CashFlow"
        verb = "ClassifyPaymentAgreements"
        description = "Match staged entry lines to payment agreements, record the linkage for each uncontested match, create the payments that follow from it, and return both decision logs alongside the open instances."
        inputContract = typeof<NoInput>.Name
        outputContract = typeof<PaymentAgreementClassificationResultReturn>.Name
        handler = classifyPaymentAgreements }

      { domain = "CashFlow"
        verb = "TransitionPaymentsToPosted"
        description = "Repoint every payment whose staged entry line has since been posted at the journal entry line it became, and re-derive the posted state of each invoice that touches. Returns the payments moved."
        inputContract = typeof<NoInput>.Name
        outputContract = typeof<PaymentPostingTransitionReturn list>.Name
        handler = transitionPaymentsToPosted }

      { domain = "CashFlow"
        verb = "ProjectCashFlow"
        description = "Compute the projected cash position over the horizon for every managed cash account: its posted balance, the unpaid invoices landing on it, and where that leaves it. Also returns the instances still waiting on a bill, whose amounts no arithmetic can include."
        inputContract = typeof<ProjectCashFlowInput>.Name
        outputContract = typeof<CashFlowProjectionReturn>.Name
        handler = projectCashFlow }

      { domain = "CashFlow"
        verb = "CreateAgreement"
        description = "Create a master agreement and its payment agreements as one unit."
        inputContract = typeof<CreateAgreementInput>.Name
        outputContract = typeof<AgreementReturn>.Name
        handler = createAgreement }

      { domain = "CashFlow"
        verb = "UpdateAgreement"
        description = "Update any of a master agreement's fields. Its payment agreements are not touched."
        inputContract = typeof<UpdateAgreementInput>.Name
        outputContract = typeof<AgreementReturn>.Name
        handler = updateAgreement }

      { domain = "CashFlow"
        verb = "CreateInstance"
        description = "Create one instance of an agreement's obligation, along with any invoices and payments already known for it."
        inputContract = typeof<CreateInstanceInput>.Name
        outputContract = typeof<InstanceCompositeReturn>.Name
        handler = createInstance }

      { domain = "CashFlow"
        verb = "CreateInvoice"
        description = "Add an invoice, and any payments already known for it, to an existing instance. Returns the whole instance, whose fulfillment the new invoice may have changed."
        inputContract = typeof<CreateInvoiceInput>.Name
        outputContract = typeof<InstanceCompositeReturn>.Name
        handler = createInvoice }

      { domain = "CashFlow"
        verb = "UpdateInvoice"
        description = "Update an invoice's fields and its invoice state or blocker. Payment state and posted state are derived, so a package that sets either is rejected. Returns the whole instance."
        inputContract = typeof<UpdateInvoiceInput>.Name
        outputContract = typeof<InstanceCompositeReturn>.Name
        handler = updateInvoice }

      { domain = "CashFlow"
        verb = "CreatePayment"
        description = "Record a payment against an invoice, pointing at either the staged entry line or the journal entry line that moved the money. Returns the whole instance, with the invoice's payment and posted states re-derived."
        inputContract = typeof<CreatePaymentInput>.Name
        outputContract = typeof<InstanceCompositeReturn>.Name
        handler = createPayment }

      { domain = "CashFlow"
        verb = "DeletePayment"
        description = "Remove a payment that never should have been recorded, along with the payment agreement linkage that produced it, and return the whole instance with its invoice states and fulfillment re-derived. This is a hard delete, not a void."
        inputContract = typeof<DeletePaymentInput>.Name
        outputContract = typeof<InstanceCompositeReturn>.Name
        handler = deletePayment }

      { domain = "CashFlow"
        verb = "CreatePaymentAgreementLink"
        description = "Link a staged entry line to the payment agreement it satisfies, where classification left the decision to the operator."
        inputContract = typeof<CreatePaymentAgreementLinkInput>.Name
        outputContract = typeof<PaymentAgreementLinkReturn>.Name
        handler = createPaymentAgreementLink }

      { domain = "CashFlow"
        verb = "UpdatePaymentAgreementLink"
        description = "Repoint an existing linkage at a different payment agreement."
        inputContract = typeof<UpdatePaymentAgreementLinkInput>.Name
        outputContract = typeof<PaymentAgreementLinkReturn>.Name
        handler = updatePaymentAgreementLink }

      { domain = "CashFlow"
        verb = "DeletePaymentAgreementLink"
        description = "Remove a linkage outright, for a staged entry line that is not an obligation at all. Returns the row as it stood before deletion. This is a hard delete, not a void."
        inputContract = typeof<DeletePaymentAgreementLinkInput>.Name
        outputContract = typeof<PaymentAgreementLinkReturn>.Name
        handler = deletePaymentAgreementLink }


      { domain = "CashFlow"
        verb = "FetchAgreementSummary"
        description = "Read one master agreement's whole tree: its payment agreements, instances, invoices, and payments."
        inputContract = typeof<FetchAgreementSummaryInput>.Name
        outputContract = typeof<AgreementReturn>.Name
        handler = fetchAgreementSummary }
    ]
