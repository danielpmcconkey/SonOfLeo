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
open Business.CrossDomainOrchestration.InvestmentActivityOrchestration
open Business.CrossDomainOrchestration.UnitRollForward
open Business.CrossDomainOrchestration.BalanceAccountOrchestration
open Business.CrossDomainOrchestration.RealEstateOrchestration
open Business.CrossDomainOrchestration.HoldingsAsOf
open Ui.InterfaceBridge.InterfaceContracts.PositionsContracts
open Ui.InterfaceBridge.BoundaryConverters.PersonFieldConverters
open Ui.InterfaceBridge.BoundaryConverters.AccountFieldConverters

let private ``convert [Map<Dimension, string>] to [DimensionValueReturn list]``
    (valueNames: Map<Dimension, string>)
    : DimensionValueReturn list =
    Dimension.all
    |> List.choose (fun d ->
        valueNames |> Map.tryFind d |> Option.map (fun name -> { dimension = d |> Dimension.toString; valueName = name }))

let private ``convert [Map<Dimension, DimensionValueId>] to [Map<Dimension, string>]``
    (context: Context.Context)
    (valueIds: Map<Dimension, DimensionValueId>)
    : Result<Map<Dimension, string>, IAppError> =
    valueIds
    |> Map.toList
    |> List.map (fun (d, id) -> id |> DimensionValue.idToName context |> Result.map (fun n -> d, n |> DimensionValueName.value))
    |> convertListOfResultsToResultsList
    |> Result.map Map.ofList

/// The owners' names, sorted.
let ownerNamesOf (context: Context.Context) (ownerIds: Set<PersonComponent.PersonId>) : Result<string list, IAppError> =
    ownerIds
    |> Set.toList
    |> List.map (Person.idToName context >> Result.map PersonComponent.PersonName.value)
    |> convertListOfResultsToResultsList
    |> Result.map List.sort

let private ``convert [AccountId] to [LedgerAccountReturn]``
    (context: Context.Context)
    (accountId: AccountId)
    : Result<LedgerAccountReturn, IAppError> =
    result {
        let! code = accountId |> ``convert AccountId to AccountCodeString`` context
        let! name = accountId |> ``convert AccountId to AccountNameString`` context
        return { code = code; name = name }
    }

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

let ``convert [BalanceAccountNameString] to [BalanceAccountId]``
    (context: Context.Context)
    (nameString: string)
    : Result<BalanceAccountId, IAppError> =
    result {
        let! name = nameString |> BalanceAccountName.create
        let! found = BalanceAccount.fetchByName context name
        match found with
        | Some balanceAccount -> return balanceAccount |> BalanceAccount.balanceAccountId
        | None -> return! error (PositionsBalanceAccountNameDoesntMatch(name |> BalanceAccountName.value))
    }

// ---- Dimension Values ----

let ``convert [DimensionValue] to [DimensionValueReturn]`` (dimensionValue: DimensionValue.DimensionValue) : DimensionValueReturn =
    { dimension = dimensionValue |> DimensionValue.dimension |> Dimension.toString
      valueName = dimensionValue |> DimensionValue.dimensionValueName |> DimensionValueName.value }

// ---- Securities ----

let ``convert [Security] to [SecurityReturn]`` (context: Context.Context) (security: Security.Security) : Result<SecurityReturn, IAppError> =
    security
    |> Security.dimensionValues
    |> ``convert [Map<Dimension, DimensionValueId>] to [Map<Dimension, string>]`` context
    |> Result.map (fun valueNames ->
        { securityName = security |> Security.securityName |> SecurityName.value
          ticker = security |> Security.ticker |> Option.map Ticker.value
          dimensionValues = valueNames |> ``convert [Map<Dimension, string>] to [DimensionValueReturn list]``
          createdAt = security |> Security.createdAt
          modifiedAt = security |> Security.modifiedAt })

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

let ``convert [InvestmentAccount] to [InvestmentAccountReturn]``
    (context: Context.Context)
    (account: InvestmentAccount.InvestmentAccount)
    : Result<InvestmentAccountReturn, IAppError> =
    result {
        let period = account |> InvestmentAccount.activityPeriod
        let! owners = account |> InvestmentAccount.owners |> ownerNamesOf context
        let! ledgerAccount =
            account
            |> InvestmentAccount.ledgerAccountId
            |> convertOptionToDesiredTypeWithFallibleConverter (``convert [AccountId] to [LedgerAccountReturn]`` context)
        return
            { accountName = account |> InvestmentAccount.investmentAccountName |> InvestmentAccountName.value
              institution = account |> InvestmentAccount.institution |> Institution.value
              accountGroup = account |> InvestmentAccount.accountGroup |> AccountGroup.value
              taxTreatment = account |> InvestmentAccount.taxTreatment |> TaxTreatment.toString
              owners = owners
              activeBegin = period |> ActivityPeriod.activeBegin
              activeEnd = period |> ActivityPeriod.activeEnd
              ledgerAccount = ledgerAccount
              createdAt = account |> InvestmentAccount.createdAt
              modifiedAt = account |> InvestmentAccount.modifiedAt }
    }

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

let ``convert [Holding] to [HoldingReturn]`` (context: Context.Context) (holding: Holding.Holding) : Result<HoldingReturn, IAppError> =
    result {
        let! accountName = holding |> Holding.investmentAccountId |> InvestmentAccount.idToName context
        let! securityName = holding |> Holding.securityId |> Security.idToName context
        return
            { accountName = accountName |> InvestmentAccountName.value
              securityName = securityName |> SecurityName.value
              basisMethod = holding |> Holding.basisMethod |> Option.map BasisMethod.toString
              createdAt = holding |> Holding.createdAt
              modifiedAt = holding |> Holding.modifiedAt }
    }

let ``convert [HoldingsAsOfLine] to [HoldingsAsOfLineReturn]``
    (context: Context.Context)
    (line: HoldingsAsOfLine)
    : Result<HoldingsAsOfLineReturn, IAppError> =
    result {
        let! security = line.securityId |> Security.fetchById context
        let! dimensionValueNames =
            security |> Security.dimensionValues |> ``convert [Map<Dimension, DimensionValueId>] to [Map<Dimension, string>]`` context
        return
            { securityName = security |> Security.securityName |> SecurityName.value
              ticker = security |> Security.ticker |> Option.map Ticker.value
              dimensionValues = dimensionValueNames |> ``convert [Map<Dimension, string>] to [DimensionValueReturn list]``
              basisMethod = line.basisMethod |> Option.map BasisMethod.toString
              quantity = line.quantity |> Quantity.amount
              price = line.price |> Price.amount
              marketValue = line.marketValue |> Money.amount
              reportedCostBasis = line.reportedCostBasis |> Option.map Money.amount
              lots =
                line.lots
                |> List.map (fun (acquiredDate, quantity, reportedCostBasis) ->
                    { acquiredDate = acquiredDate
                      quantity = quantity |> Quantity.amount
                      reportedCostBasis = reportedCostBasis |> Option.map Money.amount }) }
    }

let ``convert [HoldingsAsOfAccount] to [HoldingsAsOfAccountReturn]``
    (context: Context.Context)
    (account: HoldingsAsOfAccount)
    : Result<HoldingsAsOfAccountReturn, IAppError> =
    result {
        let! accountName = account.investmentAccountId |> InvestmentAccount.idToName context
        let! owners = account.ownerIds |> ownerNamesOf context
        let! ledgerAccount =
            account.ledgerAccountId
            |> convertOptionToDesiredTypeWithFallibleConverter (``convert [AccountId] to [LedgerAccountReturn]`` context)
        let! lines = account.lines |> List.map (``convert [HoldingsAsOfLine] to [HoldingsAsOfLineReturn]`` context) |> convertListOfResultsToResultsList
        return
            { accountName = accountName |> InvestmentAccountName.value
              institution = account.institution |> Institution.value
              accountGroup = account.accountGroup |> AccountGroup.value
              taxTreatment = account.taxTreatment |> TaxTreatment.toString
              owners = owners
              ledgerAccount = ledgerAccount
              snapshotDate = account.snapshotDate
              provenance = account.provenance |> Provenance.toString
              contributionBasis = account.contributionBasis |> Option.map (ContributionBasis.value >> Money.amount)
              lines = lines }
    }

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

let ``convert [SnapshotView] to [AccountSnapshotReturn]``
    (context: Context.Context)
    (view: SnapshotView)
    : Result<AccountSnapshotReturn, IAppError> =
    result {
        let! accountName = view.header |> AccountSnapshotHeader.investmentAccountId |> InvestmentAccount.idToName context
        let! lines =
            view.lines
            |> List.map (fun l ->
                l.securityId
                |> Security.idToName context
                |> Result.map (fun securityName ->
                    ({ securityName = securityName |> SecurityName.value
                       quantity = l.line |> AccountSnapshotLine.quantity |> Quantity.amount
                       price = l.line |> AccountSnapshotLine.price |> Price.amount
                       marketValue = l.line |> AccountSnapshotLine.marketValue |> Money.amount
                       reportedCostBasis = l.line |> AccountSnapshotLine.reportedCostBasis |> Option.map Money.amount
                       lots = l.lots |> List.map ``convert [AccountSnapshotLot] to [AccountSnapshotLotReturn]`` }
                     : AccountSnapshotLineReturn)))
            |> convertListOfResultsToResultsList
        return
            { accountName = accountName |> InvestmentAccountName.value
              snapshotDate = view.header |> AccountSnapshotHeader.snapshotDate
              provenance = view.header |> AccountSnapshotHeader.provenance |> Provenance.toString
              contributionBasis =
                view.header |> AccountSnapshotHeader.contributionBasis |> Option.map (ContributionBasis.value >> Money.amount)
              lines = lines
              createdAt = view.header |> AccountSnapshotHeader.createdAt
              modifiedAt = view.header |> AccountSnapshotHeader.modifiedAt }
    }

let ``convert [RecordedSnapshot] to [RecordedAccountSnapshotReturn]``
    (context: Context.Context)
    (recorded: RecordedSnapshot)
    : Result<RecordedAccountSnapshotReturn, IAppError> =
    result {
        let! snapshot = recorded.snapshot |> ``convert [SnapshotView] to [AccountSnapshotReturn]`` context
        let! replacedLineLots =
            recorded.replacedLineLots
            |> List.map (fun replaced ->
                replaced.securityId
                |> Security.idToName context
                |> Result.map (fun securityName ->
                    ({ security = securityName |> SecurityName.value; lotCount = replaced.lotCount }: ReplacedLineLotsReturn)))
            |> convertListOfResultsToResultsList
        return { snapshot = snapshot; replacedExisting = recorded.replacedExisting; replacedLineLots = replacedLineLots }
    }

// ---- Investment Activity ----

let ``convert [InvestmentActivityRangeInput] to [ActivityRange]``
    (context: Context.Context)
    (input: InvestmentActivityRangeInput)
    : Result<ActivityRange, IAppError> =
    result {
        let! accountId = input.accountName |> ``convert [InvestmentAccountNameString] to [InvestmentAccountId]`` context
        let! activities =
            input.activities
            |> List.map (fun activity ->
                result {
                    let! kind = activity.kind |> ActivityKind.fromString
                    let! description = activity.description |> ActivityDescription.create
                    let! source = activity.source |> convertOptionToDesiredTypeWithFallibleConverter ActivitySource.create
                    let! securityId =
                        activity.securityName
                        |> convertOptionToDesiredTypeWithFallibleConverter (``convert [SecurityNameString] to [SecurityId]`` context)
                    let! quantity = activity.quantity |> convertOptionToDesiredTypeWithFallibleConverter Quantity.fromDecimal
                    let! price = activity.price |> convertOptionToDesiredTypeWithFallibleConverter Price.fromDecimal
                    let! amount = activity.amount |> Money.fromDecimal
                    return (activity.activityDate, kind, description, source, securityId, quantity, price, amount): ActivityInput
                })
            |> convertListOfResultsToResultsList
        return (accountId, input.beginDate, input.endDate, activities): ActivityRange
    }

let ``convert [RecordedRange] to [RecordedActivityRangeReturn]``
    (context: Context.Context)
    (recorded: RecordedRange)
    : Result<RecordedActivityRangeReturn, IAppError> =
    recorded.investmentAccountId
    |> InvestmentAccount.idToName context
    |> Result.map (fun accountName ->
        { accountName = accountName |> InvestmentAccountName.value
          beginDate = recorded.beginDate
          endDate = recorded.endDate
          removed = recorded.removed
          recorded = recorded.recorded })

let ``convert [ActivityView] to [InvestmentActivityReturn]``
    (context: Context.Context)
    (view: ActivityView)
    : Result<InvestmentActivityReturn, IAppError> =
    let activity = view.activity
    result {
        let! accountName = activity |> InvestmentActivity.investmentAccountId |> InvestmentAccount.idToName context
        let! securityName =
            view.securityId |> convertOptionToDesiredTypeWithFallibleConverter (Security.idToName context)
        return
            { accountName = accountName |> InvestmentAccountName.value
              activityDate = activity |> InvestmentActivity.activityDate
              kind = activity |> InvestmentActivity.kind |> ActivityKind.toString
              description = activity |> InvestmentActivity.description |> ActivityDescription.value
              source = activity |> InvestmentActivity.source |> Option.map ActivitySource.value
              securityName = securityName |> Option.map SecurityName.value
              quantity = activity |> InvestmentActivity.quantity |> Option.map Quantity.amount
              price = activity |> InvestmentActivity.price |> Option.map Price.amount
              amount = activity |> InvestmentActivity.amount |> Money.amount
              createdAt = activity |> InvestmentActivity.createdAt
              modifiedAt = activity |> InvestmentActivity.modifiedAt }
    }

let ``convert [ActivityCoverageGap] to [InvestmentActivityCoverageGapReturn]`` (gap: ActivityCoverageGap) : InvestmentActivityCoverageGapReturn =
    { beginDate = gap.firstUncoveredDate; endDate = gap.lastUncoveredDate }

// ---- Unit roll-forward ----

let ``convert [RollForward] to [RollForwardReturn]``
    (context: Context.Context)
    (rollForward: RollForward)
    : Result<RollForwardReturn, IAppError> =
    result {
        let! accountName = rollForward.investmentAccountId |> InvestmentAccount.idToName context
        let! rows =
            rollForward.rows
            |> List.map (fun row ->
                row.securityId
                |> Security.idToName context
                |> Result.map (fun securityName ->
                    { securityName = securityName |> SecurityName.value
                      startQuantity = row.startQuantity |> Quantity.amount
                      unitsIn = row.unitsIn
                      unitsOut = row.unitsOut
                      expected = row.expected
                      endQuantity = row.endQuantity |> Quantity.amount
                      difference = row.difference }))
            |> convertListOfResultsToResultsList
        return
            { accountName = accountName |> InvestmentAccountName.value
              firstDate = rollForward.firstDate
              secondDate = rollForward.secondDate
              rows = rows }
    }

// ---- Balance Accounts ----

let ``convert [BalanceAccount] to [BalanceAccountReturn]``
    (context: Context.Context)
    (balanceAccount: BalanceAccount.BalanceAccount)
    : Result<BalanceAccountReturn, IAppError> =
    result {
        let! owners = balanceAccount |> BalanceAccount.owners |> ownerNamesOf context
        return
            { balanceAccountName = balanceAccount |> BalanceAccount.balanceAccountName |> BalanceAccountName.value
              kind = balanceAccount |> BalanceAccount.kind |> BalanceAccountKind.toString
              institution = balanceAccount |> BalanceAccount.institution |> Institution.value
              owners = owners
              createdAt = balanceAccount |> BalanceAccount.createdAt
              modifiedAt = balanceAccount |> BalanceAccount.modifiedAt }
    }

let ``convert [BalanceAccountUpdateInput] to [BalanceAccountFieldUpdates]``
    (context: Context.Context)
    (input: BalanceAccountUpdateInput)
    : Result<BalanceAccount.BalanceAccountFieldUpdates, IAppError> =
    result {
        let! balanceAccountId = input.balanceAccountName |> ``convert [BalanceAccountNameString] to [BalanceAccountId]`` context
        let! balanceAccount = balanceAccountId |> BalanceAccount.fetchById context
        let! nameUpdate = input.balanceAccountNameUpdate |> convertFieldUpdateToNewTypeFallible BalanceAccountName.create
        let resultingName = nameUpdate |> valueOrCurrent (balanceAccount |> BalanceAccount.balanceAccountName)
        let! institutionUpdate = input.institutionUpdate |> convertFieldUpdateToNewTypeFallible Institution.create
        let! ownersUpdate =
            input.ownersUpdate
            |> convertFieldUpdateToNewTypeFallible (
                ``convert [PersonNameString list] to [PersonId list]`` context
                >> Result.bind (BalanceAccountOrchestration.ownerSetOf context resultingName)
            )
        return
            { balanceAccountIdToUpdate = balanceAccountId
              balanceAccountNameUpdate = nameUpdate
              institutionUpdate = institutionUpdate
              ownersUpdate = ownersUpdate }
    }

// ---- Balance Account Balances ----

let ``convert [BalanceAccountBalanceEntryInput] to [BalanceInput]``
    (context: Context.Context)
    (input: BalanceAccountBalanceEntryInput)
    : Result<BalanceAccountOrchestration.BalanceInput, IAppError> =
    result {
        let! balanceAccountId = input.balanceAccountName |> ``convert [BalanceAccountNameString] to [BalanceAccountId]`` context
        let! amount = input.amount |> Money.fromDecimal
        return balanceAccountId, input.balanceDate, amount
    }

let ``convert [BalanceAccountBalance] to [BalanceAccountBalanceReturn]``
    (balanceAccountName: BalanceAccountName)
    (balance: BalanceAccountBalance.BalanceAccountBalance)
    : BalanceAccountBalanceReturn =
    { balanceAccountName = balanceAccountName |> BalanceAccountName.value
      balanceDate = balance |> BalanceAccountBalance.balanceDate
      amount = balance |> BalanceAccountBalance.amount |> Money.amount
      createdAt = balance |> BalanceAccountBalance.createdAt
      modifiedAt = balance |> BalanceAccountBalance.modifiedAt }

// ---- Properties ----

let ``convert [Property] to [PropertyReturn]`` (context: Context.Context) (property: Property.Property) : Result<PropertyReturn, IAppError> =
    let period = property |> Property.ownedPeriod
    // a Property's accounts are shown in code order
    let ledgerAccountsOf accountIds =
        accountIds
        |> Set.toList
        |> List.map (``convert [AccountId] to [LedgerAccountReturn]`` context)
        |> convertListOfResultsToResultsList
        |> Result.map (List.sortBy (fun a -> a.code))
    result {
        let! owners = property |> Property.owners |> ownerNamesOf context
        let! assetAccounts = property |> Property.assetAccountIds |> ledgerAccountsOf
        let! mortgageAccounts = property |> Property.mortgageAccountIds |> ledgerAccountsOf
        return
            { propertyName = property |> Property.propertyName |> PropertyName.value
              propertyUse = property |> Property.propertyUse |> PropertyUse.toString
              owners = owners
              acquisitionDate = period |> OwnedPeriod.acquisitionDate
              disposalDate = period |> OwnedPeriod.disposalDate
              purchaseBasis = property |> Property.purchaseBasis |> PurchaseBasis.value |> Money.amount
              assetAccounts = assetAccounts
              mortgageAccounts = mortgageAccounts
              createdAt = property |> Property.createdAt
              modifiedAt = property |> Property.modifiedAt }
    }

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
