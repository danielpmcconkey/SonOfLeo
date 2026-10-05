module Ui.InterfaceBridge.Routes.PositionsRoutes

open App.Utility.IAppError
open App.Utility.Result
open App.Utility.Json
open App.Utility.FieldUpdate
open App.Operation.CoreAuditableAction
open App.DataAccessLayer.DbTransaction
open App.Session
open Business.General
open Business.FinancialServices.Positions
open Business.FinancialServices.Positions.PositionsAuditableAction
open Business.FinancialServices.Positions.PositionsComponent
open Business.CrossDomainOrchestration
open Business.CrossDomainOrchestration.HoldingsAsOf
open Ui.InterfaceBridge.InterfaceContracts.SharedContracts
open Ui.InterfaceBridge.InterfaceContracts.PositionsContracts
open Ui.InterfaceBridge.BoundaryConverters.PersonFieldConverters
open Ui.InterfaceBridge.BoundaryConverters.AccountFieldConverters
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
            let! dimensionValue = DimensionValueOrchestration.constructNewAndPersist context dimension name
            return! dimensionValue |> ``convert [DimensionValue] to [DimensionValueReturn]`` |> Json.toJson<DimensionValueReturn>
        })

let private renameDimensionValue payload _ =
    runCommandRouteAndAutoCompleteTransaction PositionsRenameDimensionValue (fun context ->
        result {
            let! input = Json.fromJson<DimensionValueRenameInput> payload
            let! dimension = input.dimension |> Dimension.fromString
            let! dimensionValueId =
                input.currentName |> ``convert [DimensionValueNameString] to [DimensionValueId]`` context dimension
            let! newName = input.newName |> DimensionValueName.create
            let! dimensionValue =
                DimensionValueOrchestration.renameDimensionValue
                    context
                    { dimensionValueIdToUpdate = dimensionValueId; dimensionValueNameUpdate = SetTo newName }
            return! dimensionValue |> ``convert [DimensionValue] to [DimensionValueReturn]`` |> Json.toJson<DimensionValueReturn>
        })

let private listDimensionValues payload _ =
    readOnly (fun context ->
        result {
            let! input = Json.fromJson<DimensionValueListInput> payload
            let! dimension = input.dimension |> Dimension.fromString
            let! values = DimensionValueOrchestration.listDimensionValues context dimension
            return!
                values
                |> List.map ``convert [DimensionValue] to [DimensionValueReturn]``
                |> Json.toJson<DimensionValueReturn list>
        })

// ---- Securities ----

let private securityReturn context security =
    SecurityOrchestration.viewSecurity context security
    |> Result.map ``convert [SecurityView] to [SecurityReturn]``
    |> Result.bind Json.toJson<SecurityReturn>

let private createSecurity payload _ =
    runCommandRouteAndAutoCompleteTransaction PositionsCreateSecurity (fun context ->
        result {
            let! input = Json.fromJson<SecurityCreateInput> payload
            let! name = input.securityName |> SecurityName.create
            let! ticker = input.ticker |> convertOptionToDesiredTypeWithFallibleConverter Ticker.create
            let! dimensionValues =
                input.dimensionValues
                |> ``convert [SecurityDimensionValueInput list] to [(Dimension * DimensionValueId) list]`` context
            let! security = SecurityOrchestration.constructNewAndPersist context name ticker dimensionValues
            return! securityReturn context security
        })

let private updateSecurity payload _ =
    runCommandRouteAndAutoCompleteTransaction PositionsUpdateSecurity (fun context ->
        result {
            let! input = Json.fromJson<SecurityUpdateInput> payload
            let! securityId = input.securityName |> ``convert [SecurityNameString] to [SecurityId]`` context
            let! nameUpdate = input.securityNameUpdate |> convertFieldUpdateToNewTypeFallible SecurityName.create
            let! tickerUpdate = input.tickerUpdate |> convertFieldUpdateOptionToNewTypeOptionFallible Ticker.create
            let! dimensionUpdates =
                input.dimensionValueUpdates
                |> ``convert [DimensionValueUpdateInput list] to [(Dimension * DimensionValueId option) list]`` context
            let! security = SecurityOrchestration.updateSecurity context securityId nameUpdate tickerUpdate dimensionUpdates
            return! securityReturn context security
        })

let private listSecurities _ _ =
    readOnly (fun context ->
        result {
            let! views = SecurityOrchestration.listSecurities context
            return! views |> List.map ``convert [SecurityView] to [SecurityReturn]`` |> Json.toJson<SecurityReturn list>
        })

// ---- Investment Accounts ----

let private investmentAccountReturn context account =
    InvestmentAccountOrchestration.viewInvestmentAccount context account
    |> Result.map ``convert [InvestmentAccountView] to [InvestmentAccountReturn]``
    |> Result.bind Json.toJson<InvestmentAccountReturn>

let private createInvestmentAccount payload _ =
    runCommandRouteAndAutoCompleteTransaction PositionsCreateInvestmentAccount (fun context ->
        result {
            let! input = Json.fromJson<InvestmentAccountCreateInput> payload
            let! name = input.accountName |> InvestmentAccountName.create
            let! institution = input.institution |> Institution.create
            let! accountGroup = input.accountGroup |> AccountGroup.create
            let! taxTreatment = input.taxTreatment |> TaxTreatment.fromString
            let! owners = input.owners |> ``convert [PersonNameString list] to [PersonId list]`` context
            let! activityPeriod =
                ActivityPeriod.create input.activeBegin input.activeEnd ActivityPeriod.NotConsideredAvailableBeforeBeginDate
            let! ledgerAccountId =
                input.ledgerAccountCode
                |> convertOptionToDesiredTypeWithFallibleConverter (fallibleConverterAccountCodeToAccountId context)
            let! account =
                InvestmentAccountOrchestration.constructNewAndPersist
                    context name institution accountGroup taxTreatment owners activityPeriod ledgerAccountId
            return! investmentAccountReturn context account
        })

let private updateInvestmentAccount payload _ =
    runCommandRouteAndAutoCompleteTransaction PositionsUpdateInvestmentAccount (fun context ->
        result {
            let! input = Json.fromJson<InvestmentAccountUpdateInput> payload
            let! fieldUpdates = input |> ``convert [InvestmentAccountUpdateInput] to [InvestmentAccountFieldUpdates]`` context
            let! account = InvestmentAccountOrchestration.updateInvestmentAccount context fieldUpdates
            return! investmentAccountReturn context account
        })

let private listInvestmentAccounts _ _ =
    readOnly (fun context ->
        result {
            let! views = InvestmentAccountOrchestration.listInvestmentAccounts context
            return!
                views
                |> List.map ``convert [InvestmentAccountView] to [InvestmentAccountReturn]``
                |> Json.toJson<InvestmentAccountReturn list>
        })

// ---- Holdings ----

let private holdingReturn context holding =
    HoldingOrchestration.viewHolding context holding
    |> Result.map ``convert [HoldingView] to [HoldingReturn]``
    |> Result.bind Json.toJson<HoldingReturn>

let private createHolding payload _ =
    runCommandRouteAndAutoCompleteTransaction PositionsCreateHolding (fun context ->
        result {
            let! input = Json.fromJson<HoldingCreateInput> payload
            let! accountId = input.accountName |> ``convert [InvestmentAccountNameString] to [InvestmentAccountId]`` context
            let! securityId = input.securityName |> ``convert [SecurityNameString] to [SecurityId]`` context
            let! basisMethod = input.basisMethod |> convertOptionToDesiredTypeWithFallibleConverter BasisMethod.fromString
            let! holding = HoldingOrchestration.constructNewAndPersist context accountId securityId basisMethod
            return! holdingReturn context holding
        })

let private updateHoldingBasisMethod payload _ =
    runCommandRouteAndAutoCompleteTransaction PositionsUpdateHoldingBasisMethod (fun context ->
        result {
            let! input = Json.fromJson<HoldingUpdateBasisMethodInput> payload
            let! accountId = input.accountName |> ``convert [InvestmentAccountNameString] to [InvestmentAccountId]`` context
            let! securityId = input.securityName |> ``convert [SecurityNameString] to [SecurityId]`` context
            let! basisMethod = input.basisMethod |> convertOptionToDesiredTypeWithFallibleConverter BasisMethod.fromString
            let! holding = HoldingOrchestration.changeHoldingBasisMethod context accountId securityId basisMethod
            return! holdingReturn context holding
        })

let private deleteHolding payload _ =
    runCommandRouteAndAutoCompleteTransaction PositionsDeleteHolding (fun context ->
        result {
            let! input = Json.fromJson<HoldingDeleteInput> payload
            let! accountId = input.accountName |> ``convert [InvestmentAccountNameString] to [InvestmentAccountId]`` context
            let! securityId = input.securityName |> ``convert [SecurityNameString] to [SecurityId]`` context
            let! deleted = HoldingOrchestration.deleteHolding context accountId securityId
            return! holdingReturn context deleted
        })

let private listHoldings payload _ =
    readOnly (fun context ->
        result {
            let! input = Json.fromJson<HoldingListInput> payload
            let! accountId =
                input.accountName
                |> convertOptionToDesiredTypeWithFallibleConverter (
                    ``convert [InvestmentAccountNameString] to [InvestmentAccountId]`` context
                )
            let! views = HoldingOrchestration.listHoldings context accountId
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
                |> List.map (``convert [AccountSnapshotInput] to [Snapshot]`` context)
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
            let! accountId = input.accountName |> ``convert [InvestmentAccountNameString] to [InvestmentAccountId]`` context
            let! deleted = AccountSnapshotOrchestration.deleteSnapshot context accountId input.snapshotDate
            return! deleted |> ``convert [SnapshotView] to [AccountSnapshotReturn]`` |> Json.toJson<AccountSnapshotReturn>
        })

let private fetchAccountSnapshot payload _ =
    readOnly (fun context ->
        result {
            let! input = Json.fromJson<AccountSnapshotFetchInput> payload
            let! accountId = input.accountName |> ``convert [InvestmentAccountNameString] to [InvestmentAccountId]`` context
            let! snapshot = AccountSnapshotOrchestration.fetchSnapshot context accountId input.snapshotDate
            return! snapshot |> ``convert [SnapshotView] to [AccountSnapshotReturn]`` |> Json.toJson<AccountSnapshotReturn>
        })

let private listAccountSnapshotDates payload _ =
    readOnly (fun context ->
        result {
            let! input = Json.fromJson<AccountSnapshotListDatesInput> payload
            let! accountId = input.accountName |> ``convert [InvestmentAccountNameString] to [InvestmentAccountId]`` context
            let! dates = AccountSnapshotOrchestration.listSnapshotDates context accountId input.beginDate input.endDate
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
            let! name = input.propertyName |> PropertyName.create
            let! propertyUse = input.propertyUse |> PropertyUse.fromString
            let! owners = input.owners |> ``convert [PersonNameString list] to [PersonId list]`` context
            let! ownedPeriod = OwnedPeriod.create input.acquisitionDate input.disposalDate
            let! purchaseBasis = input.purchaseBasis |> ``convert [decimal] to [PurchaseBasis]``
            let! assetAccountIds = input.assetAccountCodes |> ``convert [AccountCodeString list] to [AccountId list]`` context
            let! mortgageAccountIds =
                input.mortgageAccountCodes |> ``convert [AccountCodeString list] to [AccountId list]`` context
            let! property =
                RealEstateOrchestration.constructNewAndPersist
                    context name propertyUse owners ownedPeriod purchaseBasis assetAccountIds mortgageAccountIds
            return! propertyReturn context property
        })

let private updateProperty payload _ =
    runCommandRouteAndAutoCompleteTransaction PositionsUpdateProperty (fun context ->
        result {
            let! input = Json.fromJson<PropertyUpdateInput> payload
            let! fieldUpdates = input |> ``convert [PropertyUpdateInput] to [PropertyFieldUpdates]`` context
            let! property = RealEstateOrchestration.updateProperty context fieldUpdates
            return! propertyReturn context property
        })

let private deleteProperty payload _ =
    runCommandRouteAndAutoCompleteTransaction PositionsDeleteProperty (fun context ->
        result {
            let! input = Json.fromJson<PropertyDeleteInput> payload
            let! propertyId = input.propertyName |> ``convert [PropertyNameString] to [PropertyId]`` context
            let! deleted = RealEstateOrchestration.deleteProperty context propertyId
            return! propertyReturn context deleted
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
            let! propertyId = input.propertyName |> ``convert [PropertyNameString] to [PropertyId]`` context
            let! value = input.value |> Business.FinancialServices.Money.fromDecimal |> Result.bind ValuationValue.create
            let! basis = input.basis |> ValuationBasis.create
            let! valuation = RealEstateOrchestration.recordValuation context propertyId input.valuationDate value basis
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
            let! propertyId = input.propertyName |> ``convert [PropertyNameString] to [PropertyId]`` context
            let! deleted = RealEstateOrchestration.deleteValuation context propertyId input.valuationDate
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
            let! propertyId = input.propertyName |> ``convert [PropertyNameString] to [PropertyId]`` context
            let! valuations = RealEstateOrchestration.listValuations context propertyId
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
        verb = "Delete"
        description =
          "Delete the Holding of a Security in an Investment Account, given by account and Security name. Refused while any Account Snapshot line references it. Returns the Holding as it stood."
        inputContract = typeof<HoldingDeleteInput>.Name
        outputContract = typeof<HoldingReturn>.Name
        handler = deleteHolding }
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
          "Create a Property: name, use (PrimaryResidence or Rental), owners by Person name, acquisition and optional disposal date, purchase basis, any asset accounts by code (Asset, subtype FixedAsset) and any mortgage accounts by code (Liability)."
        inputContract = typeof<PropertyCreateInput>.Name
        outputContract = typeof<PropertyReturn>.Name
        handler = createProperty }
      { domain = "Property"
        verb = "Update"
        description =
          "Update any of a Property's fields, the Property addressed by its current name. Owners, asset accounts and mortgage accounts are given as complete new sets; the disposal date can be cleared."
        inputContract = typeof<PropertyUpdateInput>.Name
        outputContract = typeof<PropertyReturn>.Name
        handler = updateProperty }
      { domain = "Property"
        verb = "Delete"
        description =
          "Delete a Property, addressed by name, with its owners and ledger links. Refused while it has any Valuation. Returns the Property as it stood."
        inputContract = typeof<PropertyDeleteInput>.Name
        outputContract = typeof<PropertyReturn>.Name
        handler = deleteProperty }
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
