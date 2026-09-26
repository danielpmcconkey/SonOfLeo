module App.Operation.CoreAuditableAction

open App.Operation.IAuditableAction

type CoreAuditableAction = 
    | FetchOnly
    
    interface IAuditableAction with
        member this.CaseName = getUnionCaseName this
