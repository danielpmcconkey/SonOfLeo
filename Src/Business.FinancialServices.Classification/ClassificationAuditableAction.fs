module Business.FinancialServices.Classification.ClassificationAuditableAction

open App.Operation.IAuditableAction

type ClassificationAuditableAction = 
    | ClassifyAccounts
    | ClassifyPaymentAgreements
    | CreatePaymentAgreementLink
    | DeletePaymentAgreementLink
    | UpdatePaymentAgreementLink
    | ClassificationNewRule
    | ClassificationUpdateRule
    
    interface IAuditableAction with
        member this.CaseName = getUnionCaseName this

