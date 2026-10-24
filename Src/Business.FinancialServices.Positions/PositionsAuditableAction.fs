module Business.FinancialServices.Positions.PositionsAuditableAction

open App.Operation.IAuditableAction

type PositionsAuditableAction =
    | PositionsCreateBalanceAccount
    | PositionsCreateDimensionValue
    | PositionsCreateHolding
    | PositionsCreateInvestmentAccount
    | PositionsCreateProperty
    | PositionsCreateSecurity
    | PositionsDeleteAccountSnapshot
    | PositionsDeleteBalanceAccount
    | PositionsDeleteBalanceAccountBalance
    | PositionsDeleteHolding
    | PositionsDeleteProperty
    | PositionsDeleteValuation
    | PositionsRecordAccountSnapshots
    | PositionsRecordBalanceAccountBalances
    | PositionsRecordInvestmentActivity
    | PositionsRecordValuation
    | PositionsRenameDimensionValue
    | PositionsUpdateBalanceAccount
    | PositionsUpdateHoldingBasisMethod
    | PositionsUpdateInvestmentAccount
    | PositionsUpdateProperty
    | PositionsUpdateSecurity

    interface IAuditableAction with
        member this.CaseName = getUnionCaseName this
