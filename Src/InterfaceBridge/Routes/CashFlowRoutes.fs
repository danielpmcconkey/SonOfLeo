module InterfaceBridge.Routes.CashFlowRoutes

open InterfaceBridge.InterfaceContracts.CashFlowContracts
open InterfaceBridge.InterfaceContracts.SharedContracts
open InterfaceBridge.CommandRoute

let private createUpcomingInstances _ _ =
    raise (System.NotImplementedException())

let private classifyPaymentAgreements _ _ =
    raise (System.NotImplementedException())

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
    ]
