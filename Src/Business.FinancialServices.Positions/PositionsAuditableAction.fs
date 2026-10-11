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
    | PositionsDeletePreLedgerBalance
    | PositionsDeleteProperty
    | PositionsDeleteValuation
    | PositionsRecordAccountSnapshots
    | PositionsRecordInvestmentActivity
    | PositionsRecordPreLedgerBalances
    | PositionsRecordValuation
    | PositionsRenameDimensionValue
    | PositionsUpdateHoldingBasisMethod
    | PositionsUpdateInvestmentAccount
    | PositionsUpdateProperty
    | PositionsUpdateSecurity

    interface IAuditableAction with
        member this.CaseName = getUnionCaseName this
