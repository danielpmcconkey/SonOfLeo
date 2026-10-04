module Ui.InterfaceBridge.BoundaryConverters.PositionsFieldConverters

open App.Utility.IAppError
open App.Utility.Result
open App.Utility.FieldUpdate
open App.Session
open Business.General
open Business.FinancialServices
open Business.FinancialServices.Positions
open Business.FinancialServices.Positions.PositionsComponent
open Business.CrossDomainOrchestration
open Business.CrossDomainOrchestration.InvestmentOrchestration
open Business.CrossDomainOrchestration.AccountSnapshotOrchestration
open Business.CrossDomainOrchestration.RealEstateOrchestration
open Business.CrossDomainOrchestration.HoldingsAsOf
open Ui.InterfaceBridge.InterfaceContracts.PositionsContracts
open Ui.InterfaceBridge.BoundaryConverters.AccountFieldConverters
open Ui.InterfaceBridge.BoundaryConverters.PersonFieldConverters

let ``convert [string * string] to [LedgerAccountReturn]`` (codeAndName: string * string) : LedgerAccountReturn =
    let code, name = codeAndName
    { code = code; name = name }

let private ``convert [Map<Dimension, string>] to [DimensionValueReturn list]``
    (valueNames: Map<Dimension, string>)
    : DimensionValueReturn list =
    Dimension.all
    |> List.choose (fun d ->
        valueNames |> Map.tryFind d |> Option.map (fun name -> { dimension = d |> Dimension.toString; valueName = name }))

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

let ``convert [DimensionValueReturn list] to [(Dimension * DimensionValueName) list]``
    (values: DimensionValueReturn list)
    : Result<(Dimension * DimensionValueName) list, IAppError> =
    values
    |> List.map (fun v ->
        result {
            let! dimension = v.dimension |> Dimension.fromString
            let! name = v.valueName |> DimensionValueName.create
            return dimension, name
        })
    |> convertListOfResultsToResultsList

let ``convert [DimensionValueUpdateInput list] to [(Dimension * DimensionValueName option) list]``
    (updates: DimensionValueUpdateInput list)
    : Result<(Dimension * DimensionValueName option) list, IAppError> =
    updates
    |> List.map (fun u ->
        result {
            let! dimension = u.dimension |> Dimension.fromString
            let! name = u.valueName |> convertOptionToDesiredTypeWithFallibleConverter DimensionValueName.create
            return dimension, name
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

let ``convert [InvestmentAccountCreateInput] to [NewInvestmentAccount]``
    (context: Context.Context)
    (input: InvestmentAccountCreateInput)
    : Result<NewInvestmentAccount, IAppError> =
    result {
        let! name = input.accountName |> InvestmentAccountName.create
        let! institution = input.institution |> Institution.create
        let! accountGroup = input.accountGroup |> AccountGroup.create
        let! taxTreatment = input.taxTreatment |> TaxTreatment.fromString
        let! owners = input.owners |> ``convert [string list] to [PersonName list]``
        let! activityPeriod =
            ActivityPeriod.create input.activeBegin input.activeEnd ActivityPeriod.NotConsideredAvailableBeforeBeginDate
        let! ledgerAccountId =
            input.ledgerAccountCode
            |> convertOptionToDesiredTypeWithFallibleConverter (fallibleConverterAccountCodeToAccountId context)
        return
            { name = name
              institution = institution
              accountGroup = accountGroup
              taxTreatment = taxTreatment
              owners = owners
              activityPeriod = activityPeriod
              ledgerAccountId = ledgerAccountId }
    }

let ``convert [InvestmentAccountUpdateInput] to [InvestmentAccountUpdate]``
    (context: Context.Context)
    (input: InvestmentAccountUpdateInput)
    : Result<InvestmentAccountUpdate, IAppError> =
    result {
        let! currentName = input.accountName |> InvestmentAccountName.create
        let! nameUpdate = input.accountNameUpdate |> convertFieldUpdateToNewTypeFallible InvestmentAccountName.create
        let! institutionUpdate = input.institutionUpdate |> convertFieldUpdateToNewTypeFallible Institution.create
        let! accountGroupUpdate = input.accountGroupUpdate |> convertFieldUpdateToNewTypeFallible AccountGroup.create
        let! taxTreatmentUpdate = input.taxTreatmentUpdate |> convertFieldUpdateToNewTypeFallible TaxTreatment.fromString
        let! ownersUpdate = input.ownersUpdate |> convertFieldUpdateToNewTypeFallible ``convert [string list] to [PersonName list]``
        let! ledgerAccountIdUpdate =
            input.ledgerAccountCodeUpdate
            |> convertFieldUpdateOptionToNewTypeOptionFallible (fallibleConverterAccountCodeToAccountId context)
        return
            { currentName = currentName
              nameUpdate = nameUpdate
              institutionUpdate = institutionUpdate
              accountGroupUpdate = accountGroupUpdate
              taxTreatmentUpdate = taxTreatmentUpdate
              ownersUpdate = ownersUpdate
              activeBeginUpdate = input.activeBeginUpdate
              activeEndUpdate = input.activeEndUpdate
              ledgerAccountIdUpdate = ledgerAccountIdUpdate }
    }

// ---- Holdings ----

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
              reportedCostBasis = line.reportedCostBasis |> Option.map Money.amount }) }

// ---- Account Snapshots ----

let ``convert [AccountSnapshotInput] to [SnapshotInput]`` (input: AccountSnapshotInput) : Result<SnapshotInput, IAppError> =
    result {
        let! accountName = input.accountName |> InvestmentAccountName.create
        let! provenance = input.provenance |> Provenance.fromString
        let! contributionBasis =
            input.contributionBasis
            |> convertOptionToDesiredTypeWithFallibleConverter (Money.fromDecimal >> Result.bind ContributionBasis.create)
        let! lines =
            input.lines
            |> List.map (fun line ->
                result {
                    let! securityName = line.securityName |> SecurityName.create
                    let! quantity = line.quantity |> Quantity.fromDecimal
                    let! price = line.price |> Price.fromDecimal
                    let! marketValue = line.marketValue |> Money.fromDecimal
                    let! costBasis = line.reportedCostBasis |> convertOptionToDesiredTypeWithFallibleConverter Money.fromDecimal
                    let line: SnapshotLineInput =
                        { securityName = securityName
                          quantity = quantity
                          price = price
                          marketValue = marketValue
                          reportedCostBasis = costBasis }
                    return line
                })
            |> convertListOfResultsToResultsList
        return
            ({ investmentAccountName = accountName
               snapshotDate = input.snapshotDate
               provenance = provenance
               contributionBasis = contributionBasis
               lines = lines }: SnapshotInput)
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
            { securityName = l.securityName
              quantity = l.line |> AccountSnapshotLine.quantity |> Quantity.amount
              price = l.line |> AccountSnapshotLine.price |> Price.amount
              marketValue = l.line |> AccountSnapshotLine.marketValue |> Money.amount
              reportedCostBasis = l.line |> AccountSnapshotLine.reportedCostBasis |> Option.map Money.amount })
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
      assetAccount = view.ledgerAssetAccountCodeAndName |> Option.map ``convert [string * string] to [LedgerAccountReturn]``
      mortgageAccounts = view.mortgageAccountCodesAndNames |> List.map ``convert [string * string] to [LedgerAccountReturn]``
      createdAt = property |> Property.createdAt
      modifiedAt = property |> Property.modifiedAt }

let private ``convert [decimal] to [PurchaseBasis]`` (raw: decimal) : Result<PurchaseBasis, IAppError> =
    raw |> Money.fromDecimal |> Result.bind PurchaseBasis.create

let ``convert [PropertyCreateInput] to [NewProperty]``
    (context: Context.Context)
    (input: PropertyCreateInput)
    : Result<NewProperty, IAppError> =
    result {
        let! name = input.propertyName |> PropertyName.create
        let! propertyUse = input.propertyUse |> PropertyUse.fromString
        let! owners = input.owners |> ``convert [string list] to [PersonName list]``
        let! ownedPeriod = OwnedPeriod.create input.acquisitionDate input.disposalDate
        let! purchaseBasis = input.purchaseBasis |> ``convert [decimal] to [PurchaseBasis]``
        let! assetAccountId =
            input.assetAccountCode
            |> convertOptionToDesiredTypeWithFallibleConverter (fallibleConverterAccountCodeToAccountId context)
        let! mortgageAccountIds =
            input.mortgageAccountCodes
            |> List.map (fallibleConverterAccountCodeToAccountId context)
            |> convertListOfResultsToResultsList
        return
            { name = name
              propertyUse = propertyUse
              owners = owners
              ownedPeriod = ownedPeriod
              purchaseBasis = purchaseBasis
              ledgerAssetAccountId = assetAccountId
              mortgageAccountIds = mortgageAccountIds }
    }

let ``convert [PropertyUpdateInput] to [PropertyUpdate]``
    (context: Context.Context)
    (input: PropertyUpdateInput)
    : Result<PropertyUpdate, IAppError> =
    let accountIdsOf codes =
        codes |> List.map (fallibleConverterAccountCodeToAccountId context) |> convertListOfResultsToResultsList
    result {
        let! currentName = input.propertyName |> PropertyName.create
        let! nameUpdate = input.propertyNameUpdate |> convertFieldUpdateToNewTypeFallible PropertyName.create
        let! useUpdate = input.propertyUseUpdate |> convertFieldUpdateToNewTypeFallible PropertyUse.fromString
        let! ownersUpdate = input.ownersUpdate |> convertFieldUpdateToNewTypeFallible ``convert [string list] to [PersonName list]``
        let! purchaseBasisUpdate =
            input.purchaseBasisUpdate |> convertFieldUpdateToNewTypeFallible ``convert [decimal] to [PurchaseBasis]``
        let! assetAccountIdUpdate =
            input.assetAccountCodeUpdate
            |> convertFieldUpdateOptionToNewTypeOptionFallible (fallibleConverterAccountCodeToAccountId context)
        let! mortgageAccountIdsUpdate = input.mortgageAccountCodesUpdate |> convertFieldUpdateToNewTypeFallible accountIdsOf
        return
            { currentName = currentName
              nameUpdate = nameUpdate
              propertyUseUpdate = useUpdate
              ownersUpdate = ownersUpdate
              acquisitionDateUpdate = input.acquisitionDateUpdate
              disposalDateUpdate = input.disposalDateUpdate
              purchaseBasisUpdate = purchaseBasisUpdate
              ledgerAssetAccountIdUpdate = assetAccountIdUpdate
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
