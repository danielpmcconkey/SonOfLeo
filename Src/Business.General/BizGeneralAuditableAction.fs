module Business.General.BizGeneralAuditableAction

open App.Operation.IAuditableAction

type BizGeneralAuditableAction =
    | PersonCreate
    | PersonUpdate

    interface IAuditableAction with
        member this.CaseName = getUnionCaseName this
