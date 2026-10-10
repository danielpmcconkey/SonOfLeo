module Ui.InterfaceBridge.BoundaryConverters.PositionsFieldConverters

open App.Utility.IAppError
open App.Utility.Result
open App.Utility.FieldUpdate
open App.Session
open Business.General
open Business.FinancialServices
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Positions
open Business.FinancialServices.Positions.PositionsError
open Business.FinancialServices.Positions.PositionsComponent
open Business.CrossDomainOrchestration
open Business.CrossDomainOrchestration.SecurityOrchestration
open Business.CrossDomainOrchestration.InvestmentAccountOrchestration
open Business.CrossDomainOrchestration.HoldingOrchestration
open Business.CrossDomainOrchestration.AccountSnapshotOrchestration
open Business.CrossDomainOrchestration.RealEstateOrchestration
open Business.CrossDomainOrchestration.HoldingsAsOf
open Ui.InterfaceBridge.InterfaceContracts.PositionsContracts
open Ui.InterfaceBridge.BoundaryConverters.PersonFieldConverters
open Ui.InterfaceBridge.BoundaryConverters.AccountFieldConverters

let ``convert [string * string] to [LedgerAccountReturn]`` (codeAndName: string * string) : LedgerAccountReturn =
    let code, name = codeAndName
    { code = code; name = name }

let private ``convert [Map<Dimension, string>] to [DimensionValueReturn list]``
    (valueNames: Map<Dimension, string>)
    : DimensionValueReturn list =
    Dimension.all
    |> List.choose (fun d ->
        valueNames |> Map.tryFind d |> Option.map (fun name -> { dimension = d |> Dimension.toString; valueName = name }))

// ---- Name lookups ----

let ``convert [DimensionValueNameString] to [DimensionValueId]``
    (context: Context.Context)
    (dimension: Dimension)
    (nameString: string)
    : Result<DimensionValueId, IAppError> =
    result {
        let! name = nameString |> DimensionValueName.create
        let! found = DimensionValue.fetchByDimensionAndName context dimension name
        match found with
        | Some dimensionValue -> return dimensionValue |> DimensionValue.dimensionValueId
        | None ->
            return!
                error (
                    PositionsDimensionValueNameDoesntMatch(dimension |> Dimension.toString, name |> DimensionValueName.value)
                )
    }

let ``convert [SecurityNameString] to [SecurityId]`` (context: Context.Context) (nameString: string) : Result<SecurityId, IAppError> =
    result {
        let! name = nameString |> SecurityName.create
        let! found = Security.fetchByName context name
        match found with
        | Some security -> return security |> Security.securityId
        | None -> return! error (PositionsSecurityNameDoesntMatch(name |> SecurityName.value))
    }

let ``convert [InvestmentAccountNameString] to [InvestmentAccountId]``
    (context: Context.Context)
    (nameString: string)
    : Result<InvestmentAccountId, IAppError> =
    result {
        let! name = nameString |> InvestmentAccountName.create
        let! found = InvestmentAccount.fetchByName context name
        match found with
        | Some account -> return account |> InvestmentAccount.investmentAccountId
        | None -> return! error (PositionsInvestmentAccountNameDoesntMatch(name |> InvestmentAccountName.value))
    }

let ``convert [PropertyNameString] to [PropertyId]`` (context: Context.Context) (nameString: string) : Result<PropertyId, IAppError> =
    result {
        let! name = nameString |> PropertyName.create
        let! found = Property.fetchByName context name
        match found with
        | Some property -> return property |> Property.propertyId
        | None -> return! error (PositionsPropertyNameDoesntMatch(name |> PropertyName.value))
    }

// ---- Dimension Values ----

let ``convert [DimensionValue] to [DimensionValueReturn]`` (dimensionValue: DimensionValue.DimensionValue) : DimensionValueReturn =
    { dimension = dimensionValue |> DimensionValue.dimension |> Dimension.toString
      valueName = dimensionValue |> DimensionValue.dimensionValueName |> DimensionValueName.value }

// ---- Securities ----

let ``convert [SecurityView] to [SecurityReturn]`` (view: SecurityView) : SecurityReturn =
    { securityName = view.security |> Security.securityName |> SecurityName.value
      ticker = view.security |> Security.ticker |> Option.map Ticker.value
      dimensionValues = view.dimensionValueNames |> ``convert [Map<Dimension, string>] to [DimensionValueReturn list]``
      createdAt = view.security |> Security.createdAt
      modifiedAt = view.security |> Security.modifiedAt }

let ``convert [SecurityDimensionValueInput list] to [(Dimension * DimensionValueId) list]``
    (context: Context.Context)
    (values: SecurityDimensionValueInput list)
    : Result<(Dimension * DimensionValueId) list, IAppError> =
    values
    |> List.map (fun v ->
        result {
            let! dimension = v.dimension |> Dimension.fromString
            let! valueId = v.valueName |> ``convert [DimensionValueNameString] to [DimensionValueId]`` context dimension
            return dimension, valueId
        })
    |> convertListOfResultsToResultsList

let ``convert [DimensionValueUpdateInput list] to [(Dimension * DimensionValueId option) list]``
    (context: Context.Context)
    (updates: DimensionValueUpdateInput list)
    : Result<(Dimension * DimensionValueId option) list, IAppError> =
    updates
    |> List.map (fun u ->
        result {
            let! dimension = u.dimension |> Dimension.fromString
            let! valueId =
                u.valueName
                |> convertOptionToDesiredTypeWithFallibleConverter (
                    ``convert [DimensionValueNameString] to [DimensionValueId]`` context dimension
                )
            return dimension, valueId
        })
    |> convertListOfResultsToResultsList

// ---- Investment Accounts ----

let ``convert [InvestmentAccountView] to [InvestmentAccountReturn]`` (view: InvestmentAccountView) : InvestmentAccountReturn =
    let account = view.investmentAccount
    let period = account |> InvestmentAccount.activityPeriod
    { accountName = account |> InvestmentAccount.investmentAccountName |> InvestmentAccountName.value
      institution = account |> InvestmentAccount.institution |> Institution.value
      accountGroup = account |> InvestmentAccount.accountGroup |> AccountGroup.value
      taxTreatment = account |> InvestmentAccount.taxTreatment |> TaxTreatment.toString
      owners = view.ownerNames
      activeBegin = period |> ActivityPeriod.activeBegin
      activeEnd = period |> ActivityPeriod.activeEnd
      ledgerAccount = view.ledgerAccountCodeAndName |> Option.map ``convert [string * string] to [LedgerAccountReturn]``
      createdAt = account |> InvestmentAccount.createdAt
      modifiedAt = account |> InvestmentAccount.modifiedAt }

let ``convert [InvestmentAccountUpdateInput] to [InvestmentAccountFieldUpdates]``
    (context: Context.Context)
    (input: InvestmentAccountUpdateInput)
    : Result<InvestmentAccount.InvestmentAccountFieldUpdates, IAppError> =
    result {
        let! accountId = input.accountName |> ``convert [InvestmentAccountNameString] to [InvestmentAccountId]`` context
        let! account = accountId |> InvestmentAccount.fetchById context
        let! nameUpdate = input.accountNameUpdate |> convertFieldUpdateToNewTypeFallible InvestmentAccountName.create
        let resultingName = nameUpdate |> valueOrCurrent (account |> InvestmentAccount.investmentAccountName)
        let! institutionUpdate = input.institutionUpdate |> convertFieldUpdateToNewTypeFallible Institution.create
        let! accountGroupUpdate = input.accountGroupUpdate |> convertFieldUpdateToNewTypeFallible AccountGroup.create
        let! taxTreatmentUpdate = input.taxTreatmentUpdate |> convertFieldUpdateToNewTypeFallible TaxTreatment.fromString
        let! ownersUpdate =
            input.ownersUpdate
            |> convertFieldUpdateToNewTypeFallible (
                ``convert [PersonNameString list] to [PersonId list]`` context
                >> Result.bind (InvestmentAccountOrchestration.ownerSetOf context resultingName)
            )
        // the begin and end dates arrive separately; the account keeps whichever one isn't given
        let! activityPeriodUpdate =
            match input.activeBeginUpdate, input.activeEndUpdate with
            | NoChange, NoChange -> Ok NoChange
            | beginUpdate, endUpdate ->
                let current = account |> InvestmentAccount.activityPeriod
                ActivityPeriod.create
                    (beginUpdate |> valueOrCurrent (current |> ActivityPeriod.activeBegin))
                    (endUpdate |> valueOrCurrent (current |> ActivityPeriod.activeEnd))
                    ActivityPeriod.NotConsideredAvailableBeforeBeginDate
                |> Result.map SetTo
        let! ledgerAccountIdUpdate =
            input.ledgerAccountCodeUpdate
            |> convertFieldUpdateOptionToNewTypeOptionFallible (fallibleConverterAccountCodeToAccountId context)
        return
            { investmentAccountIdToUpdate = accountId
              investmentAccountNameUpdate = nameUpdate
              institutionUpdate = institutionUpdate
              accountGroupUpdate = accountGroupUpdate
              taxTreatmentUpdate = taxTreatmentUpdate
              ownersUpdate = ownersUpdate
              activityPeriodUpdate = activityPeriodUpdate
              ledgerAccountIdUpdate = ledgerAccountIdUpdate }
    }

// ---- Holdings ----

let ``convert [AccountSnapshotLot] to [AccountSnapshotLotReturn]``
    (lot: AccountSnapshotLot.AccountSnapshotLot)
    : AccountSnapshotLotReturn =
    { acquiredDate = lot |> AccountSnapshotLot.acquiredDate
      quantity = lot |> AccountSnapshotLot.quantity |> Quantity.amount
      reportedCostBasis = lot |> AccountSnapshotLot.reportedCostBasis |> Option.map Money.amount }

let ``convert [HoldingView] to [HoldingReturn]`` (view: HoldingView) : HoldingReturn =
    { accountName = view.investmentAccountName
      securityName = view.securityName
      basisMethod = view.holding |> Holding.basisMethod |> Option.map BasisMethod.toString
      createdAt = view.holding |> Holding.createdAt
      modifiedAt = view.holding |> Holding.modifiedAt }

let ``convert [HoldingsAsOfAccount] to [HoldingsAsOfAccountReturn]`` (account: HoldingsAsOfAccount) : HoldingsAsOfAccountReturn =
    { accountName = account.investmentAccountName
      institution = account.institution
      accountGroup = account.accountGroup
      taxTreatment = account.taxTreatment |> TaxTreatment.toString
      owners = account.ownerNames
      ledgerAccount = account.ledgerAccountCodeAndName |> Option.map ``convert [string * string] to [LedgerAccountReturn]``
      snapshotDate = account.snapshotDate
      provenance = account.provenance |> Provenance.toString
      contributionBasis = account.contributionBasis |> Option.map Money.amount
      lines =
        account.lines
        |> List.map (fun line ->
            { securityName = line.securityName
              ticker = line.ticker
              dimensionValues = line.dimensionValueNames |> ``convert [Map<Dimension, string>] to [DimensionValueReturn list]``
              basisMethod = line.basisMethod |> Option.map BasisMethod.toString
              quantity = line.quantity |> Quantity.amount
              price = line.price |> Price.amount
              marketValue = line.marketValue |> Money.amount
              reportedCostBasis = line.reportedCostBasis |> Option.map Money.amount
              lots = line.lots |> List.map ``convert [AccountSnapshotLot] to [AccountSnapshotLotReturn]`` }) }

// ---- Account Snapshots ----

let ``convert [AccountSnapshotInput] to [Snapshot]`` (context: Context.Context) (input: AccountSnapshotInput) : Result<Snapshot, IAppError> =
    result {
        let! accountId = input.accountName |> ``convert [InvestmentAccountNameString] to [InvestmentAccountId]`` context
        let! provenance = input.provenance |> Provenance.fromString
        let! contributionBasis =
            input.contributionBasis
            |> convertOptionToDesiredTypeWithFallibleConverter (Money.fromDecimal >> Result.bind ContributionBasis.create)
        let! lines =
            input.lines
            |> List.map (fun line ->
                result {
                    let! securityId = line.securityName |> ``convert [SecurityNameString] to [SecurityId]`` context
                    let! quantity = line.quantity |> Quantity.fromDecimal
                    let! price = line.price |> Price.fromDecimal
                    let! marketValue = line.marketValue |> Money.fromDecimal
                    let! costBasis = line.reportedCostBasis |> convertOptionToDesiredTypeWithFallibleConverter Money.fromDecimal
                    let! lots =
                        line.lots
                        |> List.map (fun lot ->
                            result {
                                let! lotQuantity = lot.quantity |> Quantity.fromDecimal
                                let! lotCostBasis =
                                    lot.reportedCostBasis |> convertOptionToDesiredTypeWithFallibleConverter Money.fromDecimal
                                return (lot.acquiredDate, lotQuantity, lotCostBasis): SnapshotLot
                            })
                        |> convertListOfResultsToResultsList
                    return (securityId, quantity, price, marketValue, costBasis, lots): SnapshotLine
                })
            |> convertListOfResultsToResultsList
        return (accountId, input.snapshotDate, provenance, contributionBasis, lines): Snapshot
    }

let ``convert [SnapshotView] to [AccountSnapshotReturn]`` (view: SnapshotView) : AccountSnapshotReturn =
    { accountName = view.investmentAccountName
      snapshotDate = view.header |> AccountSnapshotHeader.snapshotDate
      provenance = view.header |> AccountSnapshotHeader.provenance |> Provenance.toString
      contributionBasis =
        view.header |> AccountSnapshotHeader.contributionBasis |> Option.map (ContributionBasis.value >> Money.amount)
      lines =
        view.lines
        |> List.map (fun l ->
            ({ securityName = l.securityName
               quantity = l.line |> AccountSnapshotLine.quantity |> Quantity.amount
               price = l.line |> AccountSnapshotLine.price |> Price.amount
               marketValue = l.line |> AccountSnapshotLine.marketValue |> Money.amount
               reportedCostBasis = l.line |> AccountSnapshotLine.reportedCostBasis |> Option.map Money.amount
               lots = l.lots |> List.map ``convert [AccountSnapshotLot] to [AccountSnapshotLotReturn]`` }
             : AccountSnapshotLineReturn))
      createdAt = view.header |> AccountSnapshotHeader.createdAt
      modifiedAt = view.header |> AccountSnapshotHeader.modifiedAt }

// ---- Properties ----

let ``convert [PropertyView] to [PropertyReturn]`` (view: PropertyView) : PropertyReturn =
    let property = view.property
    let period = property |> Property.ownedPeriod
    { propertyName = property |> Property.propertyName |> PropertyName.value
      propertyUse = property |> Property.propertyUse |> PropertyUse.toString
      owners = view.ownerNames
      acquisitionDate = period |> OwnedPeriod.acquisitionDate
      disposalDate = period |> OwnedPeriod.disposalDate
      purchaseBasis = property |> Property.purchaseBasis |> PurchaseBasis.value |> Money.amount
      assetAccounts = view.assetAccountCodesAndNames |> List.map ``convert [string * string] to [LedgerAccountReturn]``
      mortgageAccounts = view.mortgageAccountCodesAndNames |> List.map ``convert [string * string] to [LedgerAccountReturn]``
      createdAt = property |> Property.createdAt
      modifiedAt = property |> Property.modifiedAt }

let ``convert [decimal] to [PurchaseBasis]`` (raw: decimal) : Result<PurchaseBasis, IAppError> =
    raw |> Money.fromDecimal |> Result.bind PurchaseBasis.create

let ``convert [AccountCodeString list] to [AccountId list]``
    (context: Context.Context)
    (codes: string list)
    : Result<AccountId list, IAppError> =
    codes |> List.map (fallibleConverterAccountCodeToAccountId context) |> convertListOfResultsToResultsList

let ``convert [PropertyUpdateInput] to [PropertyFieldUpdates]``
    (context: Context.Context)
    (input: PropertyUpdateInput)
    : Result<Property.PropertyFieldUpdates, IAppError> =
    result {
        let! propertyId = input.propertyName |> ``convert [PropertyNameString] to [PropertyId]`` context
        let! property = propertyId |> Property.fetchById context
        let! nameUpdate = input.propertyNameUpdate |> convertFieldUpdateToNewTypeFallible PropertyName.create
        let resultingName = nameUpdate |> valueOrCurrent (property |> Property.propertyName)
        let! useUpdate = input.propertyUseUpdate |> convertFieldUpdateToNewTypeFallible PropertyUse.fromString
        let! ownersUpdate =
            input.ownersUpdate
            |> convertFieldUpdateToNewTypeFallible (
                ``convert [PersonNameString list] to [PersonId list]`` context
                >> Result.bind (RealEstateOrchestration.ownerSetOf context resultingName)
            )
        // the acquisition and disposal dates arrive separately; the Property keeps whichever one isn't given
        let! ownedPeriodUpdate =
            match input.acquisitionDateUpdate, input.disposalDateUpdate with
            | NoChange, NoChange -> Ok NoChange
            | acquisitionUpdate, disposalUpdate ->
                let current = property |> Property.ownedPeriod
                OwnedPeriod.create
                    (acquisitionUpdate |> valueOrCurrent (current |> OwnedPeriod.acquisitionDate))
                    (disposalUpdate |> valueOrCurrent (current |> OwnedPeriod.disposalDate))
                |> Result.map SetTo
        let! purchaseBasisUpdate =
            input.purchaseBasisUpdate |> convertFieldUpdateToNewTypeFallible ``convert [decimal] to [PurchaseBasis]``
        let! assetAccountIdsUpdate =
            input.assetAccountCodesUpdate
            |> convertFieldUpdateToNewTypeFallible (
                ``convert [AccountCodeString list] to [AccountId list]`` context
                >> Result.bind (assetAccountSetOf context resultingName)
            )
        let! mortgageAccountIdsUpdate =
            input.mortgageAccountCodesUpdate
            |> convertFieldUpdateToNewTypeFallible (
                ``convert [AccountCodeString list] to [AccountId list]`` context
                >> Result.bind (mortgageAccountSetOf context resultingName)
            )
        return
            { propertyIdToUpdate = propertyId
              propertyNameUpdate = nameUpdate
              propertyUseUpdate = useUpdate
              ownedPeriodUpdate = ownedPeriodUpdate
              purchaseBasisUpdate = purchaseBasisUpdate
              assetAccountIdsUpdate = assetAccountIdsUpdate
              ownersUpdate = ownersUpdate
              mortgageAccountIdsUpdate = mortgageAccountIdsUpdate }
    }

// ---- Valuations ----

let ``convert [Valuation] to [ValuationReturn]`` (propertyName: string) (valuation: Valuation.Valuation) : ValuationReturn =
    { propertyName = propertyName
      valuationDate = valuation |> Valuation.valuationDate
      value = valuation |> Valuation.valuationValue |> ValuationValue.value |> Money.amount
      basis = valuation |> Valuation.valuationBasis |> ValuationBasis.value
      createdAt = valuation |> Valuation.createdAt
      modifiedAt = valuation |> Valuation.modifiedAt }
