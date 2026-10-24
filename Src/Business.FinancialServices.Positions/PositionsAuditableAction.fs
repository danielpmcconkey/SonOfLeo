module Business.FinancialServices.Positions.PositionsAuditableAction

open App.Operation.IAuditableAction

type PositionsAuditableAction =
    | PositionsCreateDimensionValue
    | PositionsCreateHolding
    | PositionsCreateInvestmentAccount
    | PositionsCreateProperty
    | PositionsCreateSecurity
    | PositionsDeleteAccountSnapshot
    | PositionsDeleteHolding
    | PositionsDeleteProperty
    | PositionsDeleteValuation
    | PositionsRecordAccountSnapshots
    | PositionsRecordInvestmentActivity
    | PositionsRecordValuation
    | PositionsRenameDimensionValue
    | PositionsUpdateHoldingBasisMethod
    | PositionsUpdateInvestmentAccount
    | PositionsUpdateProperty
    | PositionsUpdateSecurity

    interface IAuditableAction with
        member this.CaseName = getUnionCaseName this
