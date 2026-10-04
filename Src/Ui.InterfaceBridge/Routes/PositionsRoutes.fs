module Ui.InterfaceBridge.Routes.PositionsRoutes

open App.Utility.IAppError
open App.Utility.Result
open App.Utility.Json
open App.Utility.FieldUpdate
open App.Operation.CoreAuditableAction
open App.DataAccessLayer.DbTransaction
open App.Session
open Business.FinancialServices.Positions
open Business.FinancialServices.Positions.PositionsAuditableAction
open Business.FinancialServices.Positions.PositionsComponent
open Business.CrossDomainOrchestration
open Business.CrossDomainOrchestration.HoldingsAsOf
open Ui.InterfaceBridge.InterfaceContracts.SharedContracts
open Ui.InterfaceBridge.InterfaceContracts.PositionsContracts
open Ui.InterfaceBridge.BoundaryConverters.PositionsFieldConverters
open Ui.InterfaceBridge.CommandRoute

let private readOnly (func: Context.Context -> Result<string, IAppError>) : Result<string, IAppError> =
    Context.create NoTransaction FetchOnly |> func

// ---- Dimension Values ----

let private createDimensionValue payload _ =
    runCommandRouteAndAutoCompleteTransaction PositionsCreateDimensionValue (fun context ->
        result {
            let! input = Json.fromJson<DimensionValueCreateInput> payload
            let! dimension = input.dimension |> Dimension.fromString
            let! name = input.valueName |> DimensionValueName.create
            let! dimensionValue = InvestmentOrchestration.createDimensionValue context dimension name
            return! dimensionValue |> ``convert [DimensionValue] to [DimensionValueReturn]`` |> Json.toJson<DimensionValueReturn>
        })

let private renameDimensionValue payload _ =
    runCommandRouteAndAutoCompleteTransaction PositionsRenameDimensionValue (fun context ->
        result {
            let! input = Json.fromJson<DimensionValueRenameInput> payload
            let! dimension = input.dimension |> Dimension.fromString
            let! currentName = input.currentName |> DimensionValueName.create
            let! newName = input.newName |> DimensionValueName.create
            let! dimensionValue = InvestmentOrchestration.renameDimensionValue context dimension currentName newName
            return! dimensionValue |> ``convert [DimensionValue] to [DimensionValueReturn]`` |> Json.toJson<DimensionValueReturn>
        })

let private listDimensionValues payload _ =
    readOnly (fun context ->
        result {
            let! input = Json.fromJson<DimensionValueListInput> payload
            let! dimension = input.dimension |> Dimension.fromString
            let! values = InvestmentOrchestration.listDimensionValues context dimension
            return!
                values
                |> List.map ``convert [DimensionValue] to [DimensionValueReturn]``
                |> Json.toJson<DimensionValueReturn list>
        })

// ---- Securities ----

let private securityReturn context security =
    InvestmentOrchestration.viewSecurity context security
    |> Result.map ``convert [SecurityView] to [SecurityReturn]``
    |> Result.bind Json.toJson<SecurityReturn>

let private createSecurity payload _ =
    runCommandRouteAndAutoCompleteTransaction PositionsCreateSecurity (fun context ->
        result {
            let! input = Json.fromJson<SecurityCreateInput> payload
            let! name = input.securityName |> SecurityName.create
            let! ticker = input.ticker |> convertOptionToDesiredTypeWithFallibleConverter Ticker.create
            let! dimensionValues = input.dimensionValues |> ``convert [DimensionValueReturn list] to [(Dimension * DimensionValueName) list]``
            let! security = InvestmentOrchestration.createSecurity context name ticker dimensionValues
            return! securityReturn context security
        })

let private updateSecurity payload _ =
    runCommandRouteAndAutoCompleteTransaction PositionsUpdateSecurity (fun context ->
        result {
            let! input = Json.fromJson<SecurityUpdateInput> payload
            let! currentName = input.securityName |> SecurityName.create
            let! nameUpdate = input.securityNameUpdate |> convertFieldUpdateToNewTypeFallible SecurityName.create
            let! tickerUpdate = input.tickerUpdate |> convertFieldUpdateOptionToNewTypeOptionFallible Ticker.create
            let! dimensionUpdates =
                input.dimensionValueUpdates
                |> ``convert [DimensionValueUpdateInput list] to [(Dimension * DimensionValueName option) list]``
            let! security =
                InvestmentOrchestration.updateSecurity context currentName nameUpdate tickerUpdate dimensionUpdates
            return! securityReturn context security
        })

let private listSecurities _ _ =
    readOnly (fun context ->
        result {
            let! views = InvestmentOrchestration.listSecurities context
            return! views |> List.map ``convert [SecurityView] to [SecurityReturn]`` |> Json.toJson<SecurityReturn list>
        })

// ---- Investment Accounts ----

let private investmentAccountReturn context account =
    InvestmentOrchestration.viewInvestmentAccount context account
    |> Result.map ``convert [InvestmentAccountView] to [InvestmentAccountReturn]``
    |> Result.bind Json.toJson<InvestmentAccountReturn>

let private createInvestmentAccount payload _ =
    runCommandRouteAndAutoCompleteTransaction PositionsCreateInvestmentAccount (fun context ->
        result {
            let! input = Json.fromJson<InvestmentAccountCreateInput> payload
            let! newAccount = input |> ``convert [InvestmentAccountCreateInput] to [NewInvestmentAccount]`` context
            let! account = InvestmentOrchestration.createInvestmentAccount context newAccount
            return! investmentAccountReturn context account
        })

let private updateInvestmentAccount payload _ =
    runCommandRouteAndAutoCompleteTransaction PositionsUpdateInvestmentAccount (fun context ->
        result {
            let! input = Json.fromJson<InvestmentAccountUpdateInput> payload
            let! accountUpdate = input |> ``convert [InvestmentAccountUpdateInput] to [InvestmentAccountUpdate]`` context
            let! account = InvestmentOrchestration.updateInvestmentAccount context accountUpdate
            return! investmentAccountReturn context account
        })

let private listInvestmentAccounts _ _ =
    readOnly (fun context ->
        result {
            let! views = InvestmentOrchestration.listInvestmentAccounts context
            return!
                views
                |> List.map ``convert [InvestmentAccountView] to [InvestmentAccountReturn]``
                |> Json.toJson<InvestmentAccountReturn list>
        })

// ---- Holdings ----

let private holdingReturn context holding =
    InvestmentOrchestration.viewHolding context holding
    |> Result.map ``convert [HoldingView] to [HoldingReturn]``
    |> Result.bind Json.toJson<HoldingReturn>

let private createHolding payload _ =
    runCommandRouteAndAutoCompleteTransaction PositionsCreateHolding (fun context ->
        result {
            let! input = Json.fromJson<HoldingCreateInput> payload
            let! accountName = input.accountName |> InvestmentAccountName.create
            let! securityName = input.securityName |> SecurityName.create
            let! basisMethod = input.basisMethod |> convertOptionToDesiredTypeWithFallibleConverter BasisMethod.fromString
            let! holding = InvestmentOrchestration.createHolding context accountName securityName basisMethod
            return! holdingReturn context holding
        })

let private updateHoldingBasisMethod payload _ =
    runCommandRouteAndAutoCompleteTransaction PositionsUpdateHoldingBasisMethod (fun context ->
        result {
            let! input = Json.fromJson<HoldingUpdateBasisMethodInput> payload
            let! accountName = input.accountName |> InvestmentAccountName.create
            let! securityName = input.securityName |> SecurityName.create
            let! basisMethod = input.basisMethod |> convertOptionToDesiredTypeWithFallibleConverter BasisMethod.fromString
            let! holding = InvestmentOrchestration.changeHoldingBasisMethod context accountName securityName basisMethod
            return! holdingReturn context holding
        })

let private listHoldings payload _ =
    readOnly (fun context ->
        result {
            let! input = Json.fromJson<HoldingListInput> payload
            let! accountName = input.accountName |> convertOptionToDesiredTypeWithFallibleConverter InvestmentAccountName.create
            let! views = InvestmentOrchestration.listHoldings context accountName
            return! views |> List.map ``convert [HoldingView] to [HoldingReturn]`` |> Json.toJson<HoldingReturn list>
        })

let private fetchHoldingsAsOf payload _ =
    readOnly (fun context ->
        result {
            let! input = Json.fromJson<HoldingFetchAsOfInput> payload
            let! accounts = fetchHoldingsAsOf context input.asOf
            return!
                accounts
                |> List.map ``convert [HoldingsAsOfAccount] to [HoldingsAsOfAccountReturn]``
                |> Json.toJson<HoldingsAsOfAccountReturn list>
        })

// ---- Account Snapshots ----

let private recordAccountSnapshots payload _ =
    // one transaction, so a failure in any snapshot records none of them
    runCommandRouteAndAutoCompleteTransaction PositionsRecordAccountSnapshots (fun context ->
        result {
            let! input = Json.fromJson<AccountSnapshotRecordInput> payload
            let! snapshots =
                input.snapshots
                |> List.map ``convert [AccountSnapshotInput] to [SnapshotInput]``
                |> convertListOfResultsToResultsList
            let! recorded = AccountSnapshotOrchestration.recordSnapshots context snapshots
            return!
                recorded
                |> List.map (fun r ->
                    { snapshot = r.snapshot |> ``convert [SnapshotView] to [AccountSnapshotReturn]``
                      replacedExisting = r.replacedExisting })
                |> Json.toJson<RecordedAccountSnapshotReturn list>
        })

let private deleteAccountSnapshot payload _ =
    runCommandRouteAndAutoCompleteTransaction PositionsDeleteAccountSnapshot (fun context ->
        result {
            let! input = Json.fromJson<AccountSnapshotDeleteInput> payload
            let! accountName = input.accountName |> InvestmentAccountName.create
            let! deleted = AccountSnapshotOrchestration.deleteSnapshot context accountName input.snapshotDate
            return! deleted |> ``convert [SnapshotView] to [AccountSnapshotReturn]`` |> Json.toJson<AccountSnapshotReturn>
        })

let private fetchAccountSnapshot payload _ =
    readOnly (fun context ->
        result {
            let! input = Json.fromJson<AccountSnapshotFetchInput> payload
            let! accountName = input.accountName |> InvestmentAccountName.create
            let! snapshot = AccountSnapshotOrchestration.fetchSnapshot context accountName input.snapshotDate
            return! snapshot |> ``convert [SnapshotView] to [AccountSnapshotReturn]`` |> Json.toJson<AccountSnapshotReturn>
        })

let private listAccountSnapshotDates payload _ =
    readOnly (fun context ->
        result {
            let! input = Json.fromJson<AccountSnapshotListDatesInput> payload
            let! accountName = input.accountName |> InvestmentAccountName.create
            let! dates = AccountSnapshotOrchestration.listSnapshotDates context accountName input.beginDate input.endDate
            return!
                dates
                |> List.map (fun (date, provenance) -> { snapshotDate = date; provenance = provenance |> Provenance.toString })
                |> Json.toJson<AccountSnapshotDateReturn list>
        })

// ---- Properties ----

let private propertyReturn context property =
    RealEstateOrchestration.viewProperty context property
    |> Result.map ``convert [PropertyView] to [PropertyReturn]``
    |> Result.bind Json.toJson<PropertyReturn>

let private createProperty payload _ =
    runCommandRouteAndAutoCompleteTransaction PositionsCreateProperty (fun context ->
        result {
            let! input = Json.fromJson<PropertyCreateInput> payload
            let! newProperty = input |> ``convert [PropertyCreateInput] to [NewProperty]`` context
            let! property = RealEstateOrchestration.createProperty context newProperty
            return! propertyReturn context property
        })

let private updateProperty payload _ =
    runCommandRouteAndAutoCompleteTransaction PositionsUpdateProperty (fun context ->
        result {
            let! input = Json.fromJson<PropertyUpdateInput> payload
            let! propertyUpdate = input |> ``convert [PropertyUpdateInput] to [PropertyUpdate]`` context
            let! property = RealEstateOrchestration.updateProperty context propertyUpdate
            return! propertyReturn context property
        })

let private listProperties _ _ =
    readOnly (fun context ->
        result {
            let! views = RealEstateOrchestration.listProperties context
            return! views |> List.map ``convert [PropertyView] to [PropertyReturn]`` |> Json.toJson<PropertyReturn list>
        })

// ---- Valuations ----

let private recordValuation payload _ =
    runCommandRouteAndAutoCompleteTransaction PositionsRecordValuation (fun context ->
        result {
            let! input = Json.fromJson<ValuationRecordInput> payload
            let! propertyName = input.propertyName |> PropertyName.create
            let! value = input.value |> Business.FinancialServices.Money.fromDecimal |> Result.bind ValuationValue.create
            let! basis = input.basis |> ValuationBasis.create
            let! valuation = RealEstateOrchestration.recordValuation context propertyName input.valuationDate value basis
            return!
                valuation
                |> ``convert [Valuation] to [ValuationReturn]`` (propertyName |> PropertyName.value)
                |> Json.toJson<ValuationReturn>
        })

let private deleteValuation payload _ =
    runCommandRouteAndAutoCompleteTransaction PositionsDeleteValuation (fun context ->
        result {
            let! input = Json.fromJson<ValuationDeleteInput> payload
            let! propertyName = input.propertyName |> PropertyName.create
            let! deleted = RealEstateOrchestration.deleteValuation context propertyName input.valuationDate
            return!
                deleted
                |> ``convert [Valuation] to [ValuationReturn]`` (propertyName |> PropertyName.value)
                |> Json.toJson<ValuationReturn>
        })

let private listValuations payload _ =
    readOnly (fun context ->
        result {
            let! input = Json.fromJson<ValuationListInput> payload
            let! propertyName = input.propertyName |> PropertyName.create
            let! valuations = RealEstateOrchestration.listValuations context propertyName
            return!
                valuations
                |> List.map (``convert [Valuation] to [ValuationReturn]`` (propertyName |> PropertyName.value))
                |> Json.toJson<ValuationReturn list>
        })

let positionsDomainCommandRoutes =
    [ { domain = "DimensionValue"
        verb = "Create"
        description =
          "Create a value in one of the seven allocation dimensions (InvestmentType, MarketCap, IndexType, Sector, Region, Objective, Benchmark). Names are unique within a dimension."
        inputContract = typeof<DimensionValueCreateInput>.Name
        outputContract = typeof<DimensionValueReturn>.Name
        handler = createDimensionValue }
      { domain = "DimensionValue"
        verb = "Rename"
        description = "Rename a Dimension Value, addressed by its dimension and current name."
        inputContract = typeof<DimensionValueRenameInput>.Name
        outputContract = typeof<DimensionValueReturn>.Name
        handler = renameDimensionValue }
      { domain = "DimensionValue"
        verb = "List"
        description = "List the values of one dimension, ordered by name. Read-only."
        inputContract = typeof<DimensionValueListInput>.Name
        outputContract = typeof<DimensionValueReturn list>.Name
        handler = listDimensionValues }
      { domain = "Security"
        verb = "Create"
        description =
          "Create a Security with an optional ticker and at most one Dimension Value per dimension, each value given by dimension and name."
        inputContract = typeof<SecurityCreateInput>.Name
        outputContract = typeof<SecurityReturn>.Name
        handler = createSecurity }
      { domain = "Security"
        verb = "Update"
        description =
          "Update a Security's name, ticker (set or clear) and the value of any dimension (set or clear), the Security addressed by its current name."
        inputContract = typeof<SecurityUpdateInput>.Name
        outputContract = typeof<SecurityReturn>.Name
        handler = updateSecurity }
      { domain = "Security"
        verb = "List"
        description = "List every Security ordered by name, with its ticker and its value in each dimension. Read-only."
        inputContract = typeof<NoInput>.Name
        outputContract = typeof<SecurityReturn list>.Name
        handler = listSecurities }
      { domain = "InvestmentAccount"
        verb = "Create"
        description =
          "Create an Investment Account: name, institution, account group, tax treatment (Taxable, TaxDeferred, Roth, Hsa), owners by Person name, active begin and end, and an optional linked ledger account by code (Asset, subtype Investment)."
        inputContract = typeof<InvestmentAccountCreateInput>.Name
        outputContract = typeof<InvestmentAccountReturn>.Name
        handler = createInvestmentAccount }
      { domain = "InvestmentAccount"
        verb = "Update"
        description =
          "Update any of an Investment Account's fields, the account addressed by its current name. Owners are given as the complete new set; active end and the ledger link can be cleared."
        inputContract = typeof<InvestmentAccountUpdateInput>.Name
        outputContract = typeof<InvestmentAccountReturn>.Name
        handler = updateInvestmentAccount }
      { domain = "InvestmentAccount"
        verb = "List"
        description =
          "List every Investment Account ordered by name, with its owners' names, its linked ledger account's code and name, and its active period. Read-only."
        inputContract = typeof<NoInput>.Name
        outputContract = typeof<InvestmentAccountReturn list>.Name
        handler = listInvestmentAccounts }
      { domain = "Holding"
        verb = "Create"
        description =
          "Create a Holding of a Security in an Investment Account, both by name. A Taxable account's Holding takes a basis method (AverageCost or SpecificLot); any other account's takes none."
        inputContract = typeof<HoldingCreateInput>.Name
        outputContract = typeof<HoldingReturn>.Name
        handler = createHolding }
      { domain = "Holding"
        verb = "UpdateBasisMethod"
        description = "Change a Holding's basis method, subject to the same rule as Create."
        inputContract = typeof<HoldingUpdateBasisMethodInput>.Name
        outputContract = typeof<HoldingReturn>.Name
        handler = updateHoldingBasisMethod }
      { domain = "Holding"
        verb = "List"
        description =
          "List Holdings, optionally of one Investment Account, ordered by account name then Security name, each with its basis method. Read-only."
        inputContract = typeof<HoldingListInput>.Name
        outputContract = typeof<HoldingReturn list>.Name
        handler = listHoldings }
      { domain = "Holding"
        verb = "FetchAsOf"
        description =
          "Holdings as of a date: for every Investment Account active on it with a snapshot on or before it, that account's latest such snapshot with its lines. Accounts ordered by name, lines by Security name. Read-only."
        inputContract = typeof<HoldingFetchAsOfInput>.Name
        outputContract = typeof<HoldingsAsOfAccountReturn list>.Name
        handler = fetchHoldingsAsOf }
      { domain = "AccountSnapshot"
        verb = "Record"
        description =
          "Record one or more Account Snapshots in one operation: all are recorded or none is. A snapshot for an account and date that already has one replaces it entirely. Lines name Securities the account already holds. Returns each snapshot as stored, marked new or replacement."
        inputContract = typeof<AccountSnapshotRecordInput>.Name
        outputContract = typeof<RecordedAccountSnapshotReturn list>.Name
        handler = recordAccountSnapshots }
      { domain = "AccountSnapshot"
        verb = "Delete"
        description =
          "Delete the Account Snapshot for an Investment Account and date, with its lines. Returns the snapshot as it stood before deletion. This is a hard delete."
        inputContract = typeof<AccountSnapshotDeleteInput>.Name
        outputContract = typeof<AccountSnapshotReturn>.Name
        handler = deleteAccountSnapshot }
      { domain = "AccountSnapshot"
        verb = "Fetch"
        description = "Fetch the Account Snapshot for an Investment Account and date, with its lines. Read-only."
        inputContract = typeof<AccountSnapshotFetchInput>.Name
        outputContract = typeof<AccountSnapshotReturn>.Name
        handler = fetchAccountSnapshot }
      { domain = "AccountSnapshot"
        verb = "ListDates"
        description =
          "List an Investment Account's snapshot dates, with each one's provenance, between two dates inclusive, in date order. Read-only."
        inputContract = typeof<AccountSnapshotListDatesInput>.Name
        outputContract = typeof<AccountSnapshotDateReturn list>.Name
        handler = listAccountSnapshotDates }
      { domain = "Property"
        verb = "Create"
        description =
          "Create a Property: name, use (PrimaryResidence or Rental), owners by Person name, acquisition and optional disposal date, purchase basis, an optional asset account by code (Asset, subtype FixedAsset) and any mortgage accounts by code (Liability)."
        inputContract = typeof<PropertyCreateInput>.Name
        outputContract = typeof<PropertyReturn>.Name
        handler = createProperty }
      { domain = "Property"
        verb = "Update"
        description =
          "Update any of a Property's fields, the Property addressed by its current name. Owners and mortgage accounts are given as complete new sets; disposal date and asset account can be cleared."
        inputContract = typeof<PropertyUpdateInput>.Name
        outputContract = typeof<PropertyReturn>.Name
        handler = updateProperty }
      { domain = "Property"
        verb = "List"
        description =
          "List every Property ordered by name, with its owners' names and its linked accounts' codes and names. Read-only."
        inputContract = typeof<NoInput>.Name
        outputContract = typeof<PropertyReturn list>.Name
        handler = listProperties }
      { domain = "Valuation"
        verb = "Record"
        description =
          "Record what a Property was worth on a date and on what basis. A Valuation for a Property and date that already has one replaces its value and basis."
        inputContract = typeof<ValuationRecordInput>.Name
        outputContract = typeof<ValuationReturn>.Name
        handler = recordValuation }
      { domain = "Valuation"
        verb = "Delete"
        description =
          "Delete a Property's Valuation for a date. Returns the Valuation as it stood before deletion. This is a hard delete."
        inputContract = typeof<ValuationDeleteInput>.Name
        outputContract = typeof<ValuationReturn>.Name
        handler = deleteValuation }
      { domain = "Valuation"
        verb = "List"
        description = "List a Property's Valuations in date order. Read-only."
        inputContract = typeof<ValuationListInput>.Name
        outputContract = typeof<ValuationReturn list>.Name
        handler = listValuations } ]
