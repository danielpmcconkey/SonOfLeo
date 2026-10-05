module Business.CrossDomainOrchestration.RealEstateOrchestration

open NodaTime
open App.Utility
open App.Utility.IAppError
open App.Utility.Result
open App.Utility.FieldUpdate
open App.Session
open Business.General.Person
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Positions
open Business.FinancialServices.Positions.PositionsError
open Business.FinancialServices.Positions.PositionsComponent
open Business.CrossDomainOrchestration.PositionsLedgerLinks

type NewProperty = {
    name: PropertyName
    propertyUse: PropertyUse
    owners: PersonName list
    ownedPeriod: OwnedPeriod
    purchaseBasis: PurchaseBasis
    assetAccountIds: AccountId list
    mortgageAccountIds: AccountId list
}

/// ownersUpdate, assetAccountIdsUpdate and mortgageAccountIdsUpdate each carry the complete new set.
type PropertyUpdate = {
    currentName: PropertyName
    nameUpdate: FieldUpdate<PropertyName>
    propertyUseUpdate: FieldUpdate<PropertyUse>
    ownersUpdate: FieldUpdate<PersonName list>
    acquisitionDateUpdate: FieldUpdate<LocalDate>
    disposalDateUpdate: FieldUpdate<LocalDate option>
    purchaseBasisUpdate: FieldUpdate<PurchaseBasis>
    assetAccountIdsUpdate: FieldUpdate<AccountId list>
    mortgageAccountIdsUpdate: FieldUpdate<AccountId list>
}

/// A Property as listed: its owners' names and its linked accounts' codes and names.
type PropertyView = {
    property: Property.Property
    ownerNames: string list
    assetAccountCodesAndNames: (string * string) list
    mortgageAccountCodesAndNames: (string * string) list
}

let fetchPropertyByName (context: Context.Context) (name: PropertyName) : Result<Property.Property, IAppError> =
    result {
        let! found = Property.fetchByName context name
        match found with
        | Some property -> return property
        | None -> return! error (PositionsPropertyNameDoesntMatch(name |> PropertyName.value))
    }

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

let private resolveOwners
    (context: Context.Context)
    (propertyName: PropertyName)
    (owners: PersonName list)
    : Result<Set<PersonId>, IAppError> =
    let shownName = propertyName |> PropertyName.value
    result {
        do! if owners |> List.isEmpty then error (PositionsPropertyHasNoOwners shownName) else Ok()
        do!
            match owners |> List.map PersonName.value |> List.countBy id |> List.tryFind (fun (_, count) -> count > 1) with
            | Some(repeated, _) -> error (PositionsPropertyOwnerRepeated(shownName, repeated))
            | None -> Ok()
        let! persons =
            owners |> List.map (PersonOrchestration.fetchPersonByName context) |> convertListOfResultsToResultsList
        return persons |> List.map personId |> Set.ofList
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

let private confirmNoAccountRepeated
    (context: Context.Context)
    (propertyName: PropertyName)
    (repeatedError: string * string -> PositionsError)
    (accountIds: AccountId list)
    : Result<unit, IAppError> =
    match accountIds |> List.countBy id |> List.tryFind (fun (_, count) -> count > 1) with
    | Some(repeated, _) ->
        result {
            let! code, _ = repeated |> ledgerAccountCodeAndName context
            return! error (repeatedError(propertyName |> PropertyName.value, code))
        }
    | None -> Ok()

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

/// Confirms each account may be linked to this Property, and returns them as a set.
let private resolveLinks
    (context: Context.Context)
    (self: PropertyId)
    (propertyName: PropertyName)
    (repeatedError: string * string -> PositionsError)
    (confirmLink: Context.Context -> PropertyId -> AccountId -> Result<unit, IAppError>)
    (accountIds: AccountId list)
    : Result<Set<AccountId>, IAppError> =
    result {
        do! confirmNoAccountRepeated context propertyName repeatedError accountIds
        do! accountIds |> List.map (confirmLink context self) |> convertListOfResultsToResultsList |> Result.map ignore
        return accountIds |> Set.ofList
    }

let createProperty (context: Context.Context) (newProperty: NewProperty) : Result<Property.Property, IAppError> =
    let instant = context |> Context.getInitiationInstant
    let propertyId = PropertyId.create ()
    result {
        do! confirmPropertyNameFree context newProperty.name None
        let! owners = resolveOwners context newProperty.name newProperty.owners
        do! confirmOnePrimaryResidence context None newProperty.name newProperty.propertyUse newProperty.ownedPeriod
        let! assetAccountIds =
            resolveLinks
                context propertyId newProperty.name PositionsPropertyAssetAccountRepeated confirmAssetLink
                newProperty.assetAccountIds
        let! mortgageAccountIds =
            resolveLinks
                context propertyId newProperty.name PositionsPropertyMortgageAccountRepeated confirmMortgageLink
                newProperty.mortgageAccountIds
        let property =
            Property.create
                propertyId
                newProperty.name
                newProperty.propertyUse
                newProperty.ownedPeriod
                newProperty.purchaseBasis
                assetAccountIds
                owners
                mortgageAccountIds
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

let updateProperty (context: Context.Context) (propertyUpdate: PropertyUpdate) : Result<Property.Property, IAppError> =
    result {
        let! property = fetchPropertyByName context propertyUpdate.currentName
        let self = property |> Property.propertyId
        do!
            match propertyUpdate.nameUpdate with
            | SetTo newName -> confirmPropertyNameFree context newName (Some self)
            | NoChange -> Ok()
        let resultingName = propertyUpdate.nameUpdate |> valueOrCurrent (property |> Property.propertyName)
        let! ownersUpdate =
            match propertyUpdate.ownersUpdate with
            | SetTo owners -> resolveOwners context resultingName owners |> Result.map SetTo
            | NoChange -> Ok NoChange
        let! ownedPeriodUpdate =
            match propertyUpdate.acquisitionDateUpdate, propertyUpdate.disposalDateUpdate with
            | NoChange, NoChange -> Ok NoChange
            | acquisitionUpdate, disposalUpdate ->
                let current = property |> Property.ownedPeriod
                result {
                    let! period =
                        OwnedPeriod.create
                            (acquisitionUpdate |> valueOrCurrent (current |> OwnedPeriod.acquisitionDate))
                            (disposalUpdate |> valueOrCurrent (current |> OwnedPeriod.disposalDate))
                    do! confirmPeriodKeepsValuations context property period
                    return SetTo period
                }
        do!
            confirmOnePrimaryResidence
                context
                (Some self)
                resultingName
                (propertyUpdate.propertyUseUpdate |> valueOrCurrent (property |> Property.propertyUse))
                (ownedPeriodUpdate |> valueOrCurrent (property |> Property.ownedPeriod))
        let! assetAccountIdsUpdate =
            propertyUpdate.assetAccountIdsUpdate
            |> convertFieldUpdateToNewTypeFallible (
                resolveLinks context self resultingName PositionsPropertyAssetAccountRepeated confirmAssetLink
            )
        let! mortgageAccountIdsUpdate =
            propertyUpdate.mortgageAccountIdsUpdate
            |> convertFieldUpdateToNewTypeFallible (
                resolveLinks context self resultingName PositionsPropertyMortgageAccountRepeated confirmMortgageLink
            )
        return!
            Property.update
                context
                { propertyIdToUpdate = self
                  propertyNameUpdate = propertyUpdate.nameUpdate
                  propertyUseUpdate = propertyUpdate.propertyUseUpdate
                  ownedPeriodUpdate = ownedPeriodUpdate
                  purchaseBasisUpdate = propertyUpdate.purchaseBasisUpdate
                  assetAccountIdsUpdate = assetAccountIdsUpdate
                  ownersUpdate = ownersUpdate
                  mortgageAccountIdsUpdate = mortgageAccountIdsUpdate }
    }

let viewProperty (context: Context.Context) (property: Property.Property) : Result<PropertyView, IAppError> =
    result {
        let! ownerNames = property |> Property.owners |> PersonOrchestration.personNamesOf context
        let codesAndNamesOf accountIds =
            accountIds
            |> Set.toList
            |> List.map (ledgerAccountCodeAndName context)
            |> convertListOfResultsToResultsList
            |> Result.map (List.sortBy fst)
        let! assetAccounts = property |> Property.assetAccountIds |> codesAndNamesOf
        let! mortgageAccounts = property |> Property.mortgageAccountIds |> codesAndNamesOf
        return
            { property = property
              ownerNames = ownerNames
              assetAccountCodesAndNames = assetAccounts
              mortgageAccountCodesAndNames = mortgageAccounts }
    }

let listProperties (context: Context.Context) : Result<PropertyView list, IAppError> =
    result {
        let! properties = Property.fetchAll context
        return!
            properties
            |> List.sortBy (Property.propertyName >> PropertyName.value)
            |> List.map (viewProperty context)
            |> convertListOfResultsToResultsList
    }

/// Deletes the Property with its owners and ledger links, and returns it as it stood before deletion.
let deleteProperty (context: Context.Context) (propertyName: PropertyName) : Result<Property.Property, IAppError> =
    result {
        let! property = fetchPropertyByName context propertyName
        let! valuations = property |> Property.propertyId |> Valuation.fetchByProperty context
        do!
            if valuations |> List.isEmpty then
                Ok()
            else
                error (PositionsPropertyHasValuations(propertyName |> PropertyName.value))
        do! property |> Property.propertyId |> Property.delete context
        return property
    }

let recordValuation
    (context: Context.Context)
    (propertyName: PropertyName)
    (valuationDate: LocalDate)
    (valuationValue: ValuationValue)
    (valuationBasis: ValuationBasis)
    : Result<Valuation.Valuation, IAppError> =
    let instant = context |> Context.getInitiationInstant
    let currentDate = instant |> Calendar.dateFromInstant
    let shownName = propertyName |> PropertyName.value
    result {
        let! property = fetchPropertyByName context propertyName
        let propertyId = property |> Property.propertyId
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
            let replacement =
                Valuation.create
                    (stored |> Valuation.valuationId)
                    propertyId
                    valuationDate
                    valuationValue
                    valuationBasis
                    (stored |> Valuation.createdAt)
                    instant
            do! replacement |> Valuation.replace context
            return replacement
        | None ->
            let valuation =
                Valuation.create (ValuationId.create ()) propertyId valuationDate valuationValue valuationBasis instant instant
            do! valuation |> Valuation.persist context
            return valuation
    }

/// Deletes the Valuation and returns it as it stood before deletion.
let deleteValuation
    (context: Context.Context)
    (propertyName: PropertyName)
    (valuationDate: LocalDate)
    : Result<Valuation.Valuation, IAppError> =
    result {
        let! property = fetchPropertyByName context propertyName
        let! existing = Valuation.fetchByPropertyAndDate context (property |> Property.propertyId) valuationDate
        match existing with
        | Some valuation ->
            do! valuation |> Valuation.valuationId |> Valuation.delete context
            return valuation
        | None -> return! error (PositionsValuationDoesntExist(propertyName |> PropertyName.value, valuationDate))
    }

/// A Property's Valuations in date order.
let listValuations (context: Context.Context) (propertyName: PropertyName) : Result<Valuation.Valuation list, IAppError> =
    fetchPropertyByName context propertyName
    |> Result.bind (Property.propertyId >> Valuation.fetchByProperty context)
