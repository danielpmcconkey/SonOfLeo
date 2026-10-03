module Business.FinancialServices.CashFlow.CashFlowAuditableAction

open App.Operation.IAuditableAction

type CashFlowAuditableAction = 
    | CashFlowCancelInstance
    | CashFlowCancelInvoice
    | CashFlowCreateAgreement
    | CashFlowCreateInstance
    | CashFlowCreateInvoice
    | CashFlowCreatePayment
    | CashFlowCreateUpcomingInstances
    | CashFlowDeletePayment
    | CashFlowTransitionPaymentsToPosted
    | CashFlowUpdateAgreement
    | CashFlowUpdateInvoice
    
    interface IAuditableAction with
        member this.CaseName = getUnionCaseName this

