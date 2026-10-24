module Business.CrossDomainOrchestration.RealEstateOrchestration

open NodaTime
open App.Utility
open App.Utility.IAppError
open App.Utility.Result
open App.Utility.FieldUpdate
open App.Session
open Business.General
open Business.General.PersonComponent
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Positions
open Business.FinancialServices.Positions.PositionsError
open Business.FinancialServices.Positions.PositionsComponent
open Business.CrossDomainOrchestration.PositionsLedgerLinks

let private confirmPropertyNameFree
    (context: Context.Context)
    (name: PropertyName)
    (self: PropertyId option)
    : Result<unit, IAppError> =
    result {
        let! found = Property.fetchByName context name
        match found with
        | Some property when Some(property |> Property.propertyId) <> self ->
            return! error (PositionsPropertyNameAlreadyExists(name |> PropertyName.value))
        | _ -> return ()
    }

/// Confirms the owners given for a Property: at least one, no Person twice, each an existing Person. Returns them as
/// the Property's set of owners.
let ownerSetOf (context: Context.Context) (propertyName: PropertyName) (owners: PersonId list) : Result<Set<PersonId>, IAppError> =
    let shownName = propertyName |> PropertyName.value
    result {
        do! if owners |> List.isEmpty then error (PositionsPropertyHasNoOwners shownName) else Ok()
        let! persons = owners |> List.map (Person.fetchById context) |> convertListOfResultsToResultsList
        do!
            match
                persons
                |> List.map (Person.personName >> PersonName.value)
                |> List.countBy id
                |> List.tryFind (fun (_, count) -> count > 1)
            with
            | Some(repeated, _) -> error (PositionsPropertyOwnerRepeated(shownName, repeated))
            | None -> Ok()
        return owners |> Set.ofList
    }

// Owned periods are half-open (the disposal date is not owned), so one residence may be acquired on the day another is
// disposed of.
let private confirmOnePrimaryResidence
    (context: Context.Context)
    (self: PropertyId option)
    (name: PropertyName)
    (propertyUse: PropertyUse)
    (ownedPeriod: OwnedPeriod)
    : Result<unit, IAppError> =
    match propertyUse with
    | PropertyUse.Rental -> Ok()
    | PropertyUse.PrimaryResidence ->
        result {
            let! properties = Property.fetchAll context
            let clash =
                properties
                |> List.filter (fun p -> Some(p |> Property.propertyId) <> self)
                |> List.filter (fun p -> p |> Property.propertyUse = PropertyUse.PrimaryResidence)
                |> List.sortBy (Property.propertyName >> PropertyName.value)
                |> List.tryFind (fun p -> p |> Property.ownedPeriod |> OwnedPeriod.overlaps ownedPeriod)
            match clash with
            | Some other ->
                return!
                    error (
                        PositionsPrimaryResidencesOverlap(
                            name |> PropertyName.value,
                            other |> Property.propertyName |> PropertyName.value
                        )
                    )
            | None -> return ()
        }

let private accountSetOf
    (context: Context.Context)
    (propertyName: PropertyName)
    (repeatedError: string * string -> PositionsError)
    (accountIds: AccountId list)
    : Result<Set<AccountId>, IAppError> =
    match accountIds |> List.countBy id |> List.tryFind (fun (_, count) -> count > 1) with
    | Some(repeated, _) ->
        result {
            let! account = repeated |> fetchLedgerAccount context
            return! error (repeatedError(propertyName |> PropertyName.value, codeOf account))
        }
    | None -> Ok(accountIds |> Set.ofList)

/// Confirms no ledger account is given twice among a Property's asset accounts, and returns them as a set.
let assetAccountSetOf (context: Context.Context) (propertyName: PropertyName) (accountIds: AccountId list) =
    accountSetOf context propertyName PositionsPropertyAssetAccountRepeated accountIds

/// Confirms no ledger account is given twice among a Property's mortgage accounts, and returns them as a set.
let mortgageAccountSetOf (context: Context.Context) (propertyName: PropertyName) (accountIds: AccountId list) =
    accountSetOf context propertyName PositionsPropertyMortgageAccountRepeated accountIds

/// Confirms no Balance Account is given twice among a Property's mortgage Balance Accounts, and returns them as a set.
let mortgageBalanceAccountSetOf
    (context: Context.Context)
    (propertyName: PropertyName)
    (balanceAccountIds: BalanceAccountId list)
    : Result<Set<BalanceAccountId>, IAppError> =
    match balanceAccountIds |> List.countBy id |> List.tryFind (fun (_, count) -> count > 1) with
    | Some(repeated, _) ->
        result {
            let! balanceAccount = repeated |> BalanceAccount.fetchById context
            let shownName = balanceAccount |> BalanceAccount.balanceAccountName |> BalanceAccountName.value
            return! error (PositionsPropertyMortgageBalanceAccountRepeated(propertyName |> PropertyName.value, shownName))
        }
    | None -> Ok(balanceAccountIds |> Set.ofList)

let private confirmAssetLink (context: Context.Context) (self: PropertyId) (accountId: AccountId) : Result<unit, IAppError> =
    result {
        let! account = accountId |> fetchLedgerAccount context
        do!
            match account |> Account.accountType, account |> Account.accountSubType with
            | AccountType.Asset, Some AccountSubtype.FixedAsset -> Ok()
            | _ ->
                let accountType, subtype = describeType account
                error (PositionsPropertyLedgerAccountNotAssetFixedAsset(codeOf account, accountType, subtype))
        let! linked = accountId |> Property.fetchByAssetAccountId context
        match linked with
        | Some other when other |> Property.propertyId <> self ->
            let name = other |> Property.propertyName |> PropertyName.value
            return! error (PositionsAssetAccountAlreadyLinked(codeOf account, name))
        | _ -> return ()
    }

let private confirmMortgageLink (context: Context.Context) (self: PropertyId) (accountId: AccountId) : Result<unit, IAppError> =
    result {
        let! account = accountId |> fetchLedgerAccount context
        do!
            match account |> Account.accountType with
            | AccountType.Liability -> Ok()
            | other -> error (PositionsMortgageAccountNotLiability(codeOf account, other |> AccountType.toString))
        let! linked = accountId |> Property.fetchByMortgageAccountId context
        match linked with
        | Some other when other |> Property.propertyId <> self ->
            let name = other |> Property.propertyName |> PropertyName.value
            return! error (PositionsMortgageAccountAlreadyLinked(codeOf account, name))
        | _ -> return ()
    }

let private confirmMortgageBalanceAccountLink
    (context: Context.Context)
    (self: PropertyId)
    (balanceAccountId: BalanceAccountId)
    : Result<unit, IAppError> =
    result {
        let! balanceAccount = balanceAccountId |> BalanceAccount.fetchById context
        let shownName = balanceAccount |> BalanceAccount.balanceAccountName |> BalanceAccountName.value
        do!
            match balanceAccount |> BalanceAccount.kind with
            | BalanceAccountKind.Debt -> Ok()
            | other -> error (PositionsMortgageBalanceAccountNotDebt(shownName, other |> BalanceAccountKind.toString))
        let! linked = balanceAccountId |> Property.fetchByMortgageBalanceAccountId context
        match linked with
        | Some other when other |> Property.propertyId <> self ->
            let name = other |> Property.propertyName |> PropertyName.value
            return! error (PositionsMortgageBalanceAccountAlreadyLinked(shownName, name))
        | _ -> return ()
    }

let private confirmLinks
    (context: Context.Context)
    (self: PropertyId)
    (confirmLink: Context.Context -> PropertyId -> 'id -> Result<unit, IAppError>)
    (ids: Set<'id>)
    : Result<unit, IAppError> =
    ids
    |> Set.toList
    |> List.map (confirmLink context self)
    |> convertListOfResultsToResultsList
    |> Result.map ignore

let constructNewAndPersist
    (context: Context.Context)
    (name: PropertyName)
    (propertyUse: PropertyUse)
    (owners: PersonId list)
    (ownedPeriod: OwnedPeriod)
    (purchaseBasis: PurchaseBasis)
    (assetAccountIds: AccountId list)
    (mortgageAccountIds: AccountId list)
    (mortgageBalanceAccountIds: BalanceAccountId list)
    : Result<Property.Property, IAppError> =
    let instant = context |> Context.getInitiationInstant
    let propertyId = PropertyId.create ()
    result {
        do! confirmPropertyNameFree context name None
        let! ownerSet = ownerSetOf context name owners
        do! confirmOnePrimaryResidence context None name propertyUse ownedPeriod
        let! assetAccountSet = assetAccountSetOf context name assetAccountIds
        do! assetAccountSet |> confirmLinks context propertyId confirmAssetLink
        let! mortgageAccountSet = mortgageAccountSetOf context name mortgageAccountIds
        do! mortgageAccountSet |> confirmLinks context propertyId confirmMortgageLink
        let! mortgageBalanceAccountSet = mortgageBalanceAccountSetOf context name mortgageBalanceAccountIds
        do! mortgageBalanceAccountSet |> confirmLinks context propertyId confirmMortgageBalanceAccountLink
        let property =
            Property.create
                propertyId
                name
                propertyUse
                ownedPeriod
                purchaseBasis
                assetAccountSet
                ownerSet
                mortgageAccountSet
                mortgageBalanceAccountSet
                instant
                instant
        do! property |> Property.persist context
        return property
    }

let private confirmPeriodKeepsValuations
    (context: Context.Context)
    (property: Property.Property)
    (ownedPeriod: OwnedPeriod)
    : Result<unit, IAppError> =
    result {
        let! valuations = property |> Property.propertyId |> Valuation.fetchByProperty context
        let offendingDates =
            valuations
            |> List.map Valuation.valuationDate
            |> List.filter (fun d -> not (ownedPeriod |> OwnedPeriod.admitsValuationOn d))
        if not (offendingDates |> List.isEmpty) then
            return!
                error (
                    PositionsOwnershipExcludesValuations(
                        property |> Property.propertyName |> PropertyName.value,
                        offendingDates |> List.min,
                        offendingDates |> List.max
                    )
                )
    }

/// ownersUpdate, assetAccountIdsUpdate, mortgageAccountIdsUpdate and mortgageBalanceAccountIdsUpdate each carry the
/// complete new set.
let updateProperty
    (context: Context.Context)
    (fieldUpdates: Property.PropertyFieldUpdates)
    : Result<Property.Property, IAppError> =
    result {
        let self = fieldUpdates.propertyIdToUpdate
        let! property = self |> Property.fetchById context
        do!
            match fieldUpdates.propertyNameUpdate with
            | SetTo newName -> confirmPropertyNameFree context newName (Some self)
            | NoChange -> Ok()
        let resultingName = fieldUpdates.propertyNameUpdate |> valueOrCurrent (property |> Property.propertyName)
        do!
            match fieldUpdates.ownersUpdate with
            | SetTo owners -> owners |> Set.toList |> ownerSetOf context resultingName |> Result.map ignore
            | NoChange -> Ok()
        do!
            match fieldUpdates.ownedPeriodUpdate with
            | SetTo period -> confirmPeriodKeepsValuations context property period
            | NoChange -> Ok()
        do!
            confirmOnePrimaryResidence
                context
                (Some self)
                resultingName
                (fieldUpdates.propertyUseUpdate |> valueOrCurrent (property |> Property.propertyUse))
                (fieldUpdates.ownedPeriodUpdate |> valueOrCurrent (property |> Property.ownedPeriod))
        do!
            match fieldUpdates.assetAccountIdsUpdate with
            | SetTo accountIds -> accountIds |> confirmLinks context self confirmAssetLink
            | NoChange -> Ok()
        do!
            match fieldUpdates.mortgageAccountIdsUpdate with
            | SetTo accountIds -> accountIds |> confirmLinks context self confirmMortgageLink
            | NoChange -> Ok()
        do!
            match fieldUpdates.mortgageBalanceAccountIdsUpdate with
            | SetTo balanceAccountIds -> balanceAccountIds |> confirmLinks context self confirmMortgageBalanceAccountLink
            | NoChange -> Ok()
        return! Property.update context fieldUpdates
    }

/// Every Property, ordered by name.
let listProperties (context: Context.Context) : Result<Property.Property list, IAppError> =
    Property.fetchAll context |> Result.map (List.sortBy (Property.propertyName >> PropertyName.value))

/// Deletes the Property with its owners, ledger links and mortgage Balance Account links, and returns it as it stood
/// before deletion.
let deleteProperty (context: Context.Context) (propertyId: PropertyId) : Result<Property.Property, IAppError> =
    result {
        let! property = propertyId |> Property.fetchById context
        let! valuations = property |> Property.propertyId |> Valuation.fetchByProperty context
        do!
            if valuations |> List.isEmpty then
                Ok()
            else
                error (PositionsPropertyHasValuations(property |> Property.propertyName |> PropertyName.value))
        do! property |> Property.propertyId |> Property.delete context
        return property
    }

let recordValuation
    (context: Context.Context)
    (propertyId: PropertyId)
    (valuationDate: LocalDate)
    (valuationValue: ValuationValue)
    (valuationBasis: ValuationBasis)
    : Result<Valuation.Valuation, IAppError> =
    let instant = context |> Context.getInitiationInstant
    let currentDate = instant |> Calendar.dateFromInstant
    result {
        let! property = propertyId |> Property.fetchById context
        let shownName = property |> Property.propertyName |> PropertyName.value
        do!
            if not (property |> Property.ownedPeriod |> OwnedPeriod.admitsValuationOn valuationDate) then
                error (PositionsValuationDateOutsideOwnership(shownName, valuationDate))
            elif valuationDate > currentDate then
                error (PositionsValuationDateLaterThanCurrentDate(shownName, valuationDate))
            else
                Ok()
        let! existing = Valuation.fetchByPropertyAndDate context propertyId valuationDate
        match existing with
        | Some stored ->
            return!
                Valuation.update
                    context
                    { valuationIdToUpdate = stored |> Valuation.valuationId
                      valuationValueUpdate = SetTo valuationValue
                      valuationBasisUpdate = SetTo valuationBasis }
        | None ->
            let valuation =
                Valuation.create (ValuationId.create ()) propertyId valuationDate valuationValue valuationBasis instant instant
            do! valuation |> Valuation.persist context
            return valuation
    }

/// Deletes the Valuation and returns it as it stood before deletion.
let deleteValuation
    (context: Context.Context)
    (propertyId: PropertyId)
    (valuationDate: LocalDate)
    : Result<Valuation.Valuation, IAppError> =
    result {
        let! property = propertyId |> Property.fetchById context
        let! existing = Valuation.fetchByPropertyAndDate context propertyId valuationDate
        match existing with
        | Some valuation ->
            do! valuation |> Valuation.valuationId |> Valuation.delete context
            return valuation
        | None ->
            return! error (PositionsValuationDoesntExist(property |> Property.propertyName |> PropertyName.value, valuationDate))
    }

/// A Property's Valuations in date order.
let listValuations (context: Context.Context) (propertyId: PropertyId) : Result<Valuation.Valuation list, IAppError> =
    propertyId |> Property.fetchById context |> Result.bind (fun _ -> Valuation.fetchByProperty context propertyId)
