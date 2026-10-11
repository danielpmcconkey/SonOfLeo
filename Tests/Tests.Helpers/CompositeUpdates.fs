module Tests.Helpers.CompositeUpdates

open Business.CrossDomainOrchestration.InstanceOrchestration

// the composite update records are private, so the copy-and-update tests relied on lives here
let withInvoiceUpdates invoiceUpdates (update: InvoiceCompositeUpdate) =
    InvoiceCompositeUpdate.create
        invoiceUpdates
        (update |> InvoiceCompositeUpdate.paymentUpdates)
        (update |> InvoiceCompositeUpdate.paymentIdsToDelete)
        (update |> InvoiceCompositeUpdate.newPayments)

let withPaymentIdsToDelete paymentIdsToDelete (update: InvoiceCompositeUpdate) =
    InvoiceCompositeUpdate.create
        (update |> InvoiceCompositeUpdate.invoiceUpdates)
        (update |> InvoiceCompositeUpdate.paymentUpdates)
        paymentIdsToDelete
        (update |> InvoiceCompositeUpdate.newPayments)

let withNewPayments newPayments (update: InvoiceCompositeUpdate) =
    InvoiceCompositeUpdate.create
        (update |> InvoiceCompositeUpdate.invoiceUpdates)
        (update |> InvoiceCompositeUpdate.paymentUpdates)
        (update |> InvoiceCompositeUpdate.paymentIdsToDelete)
        newPayments

let withInvoiceCompositeUpdates invoiceCompositeUpdates (update: InstanceCompositeUpdate) =
    InstanceCompositeUpdate.create
        (update |> InstanceCompositeUpdate.instanceUpdates)
        invoiceCompositeUpdates
        (update |> InstanceCompositeUpdate.newInvoices)

let withNewInvoices newInvoices (update: InstanceCompositeUpdate) =
    InstanceCompositeUpdate.create
        (update |> InstanceCompositeUpdate.instanceUpdates)
        (update |> InstanceCompositeUpdate.invoiceCompositeUpdates)
        newInvoices

let mapInvoiceUpdates change (update: InvoiceCompositeUpdate) =
    update |> withInvoiceUpdates (update |> InvoiceCompositeUpdate.invoiceUpdates |> change)

let mapInvoiceCompositeUpdates change (update: InstanceCompositeUpdate) =
    update |> withInvoiceCompositeUpdates (update |> InstanceCompositeUpdate.invoiceCompositeUpdates |> List.map change)
