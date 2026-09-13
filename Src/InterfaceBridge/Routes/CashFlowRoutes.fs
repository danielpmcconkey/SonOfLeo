module InterfaceBridge.Routes.CashFlowRoutes

open InterfaceBridge.InterfaceContracts.CashFlowContracts
open InterfaceBridge.InterfaceContracts.SharedContracts
open InterfaceBridge.CommandRoute

let private createUpcomingInstances _ _ =
    raise (System.NotImplementedException())

let private classifyPaymentAgreements _ _ =
    raise (System.NotImplementedException())

let private createAgreement _ _ =
    raise (System.NotImplementedException())

let private updateAgreement _ _ =
    raise (System.NotImplementedException())

let private createInstance _ _ =
    raise (System.NotImplementedException())

let private createInvoice _ _ =
    raise (System.NotImplementedException())

let private updateInvoice _ _ =
    raise (System.NotImplementedException())

let private createPayment _ _ =
    raise (System.NotImplementedException())

let private createPaymentAgreementLink _ _ =
    raise (System.NotImplementedException())

let private updatePaymentAgreementLink _ _ =
    raise (System.NotImplementedException())

let private deletePaymentAgreementLink _ _ =
    raise (System.NotImplementedException())

let private fetchAgreementSummary _ _ =
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
        description = "Add an invoice, and any payments already known for it, to an existing instance."
        inputContract = typeof<CreateInvoiceInput>.Name
        outputContract = typeof<InvoiceCompositeReturn>.Name
        handler = createInvoice }

      { domain = "CashFlow"
        verb = "UpdateInvoice"
        description = "Update any of an invoice's fields, including each half of its life cycle state."
        inputContract = typeof<UpdateInvoiceInput>.Name
        outputContract = typeof<InvoiceCompositeReturn>.Name
        handler = updateInvoice }

      { domain = "CashFlow"
        verb = "CreatePayment"
        description = "Record a payment against an invoice, pointing at either the staged entry line or the journal entry line that moved the money."
        inputContract = typeof<CreatePaymentInput>.Name
        outputContract = typeof<PaymentReturn>.Name
        handler = createPayment }

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
