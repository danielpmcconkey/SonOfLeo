module Business.CrossDomainOrchestration.InvestmentOrchestration

open NodaTime
open App.Utility.IAppError
open App.Utility.Result
open App.Utility.FieldUpdate
open App.Session
open Business.General
open Business.General.PersonComponent
open Business.General.Person
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Positions
open Business.FinancialServices.Positions.PositionsError
open Business.FinancialServices.Positions.PositionsComponent
open Business.CrossDomainOrchestration.PositionsLedgerLinks

// ---- Dimension Values ----

let private dimensionValueByName
    (context: Context.Context)
    (dimension: Dimension)
    (name: DimensionValueName)
    : Result<DimensionValue.DimensionValue, IAppError> =
    result {
        let! found = DimensionValue.fetchByDimensionAndName context dimension name
        match found with
        | Some dimensionValue -> return dimensionValue
        | None ->
            return!
                error (
                    PositionsDimensionValueNameDoesntMatch(dimension |> Dimension.toString, name |> DimensionValueName.value)
                )
    }

let private confirmDimensionValueNameFree
    (context: Context.Context)
    (dimension: Dimension)
    (name: DimensionValueName)
    (self: DimensionValueId option)
    : Result<unit, IAppError> =
    result {
        let! found = DimensionValue.fetchByDimensionAndName context dimension name
        match found with
        | Some dimensionValue when Some(dimensionValue |> DimensionValue.dimensionValueId) <> self ->
            return!
                error (
                    PositionsDimensionValueAlreadyExists(dimension |> Dimension.toString, name |> DimensionValueName.value)
                )
        | _ -> return ()
    }

let createDimensionValue
    (context: Context.Context)
    (dimension: Dimension)
    (name: DimensionValueName)
    : Result<DimensionValue.DimensionValue, IAppError> =
    let instant = context |> Context.getInitiationInstant
    let dimensionValue = DimensionValue.create (DimensionValueId.create ()) dimension name instant instant
    result {
        do! confirmDimensionValueNameFree context dimension name None
        do! dimensionValue |> DimensionValue.persist context
        return dimensionValue
    }

let renameDimensionValue
    (context: Context.Context)
    (dimension: Dimension)
    (currentName: DimensionValueName)
    (newName: DimensionValueName)
    : Result<DimensionValue.DimensionValue, IAppError> =
    result {
        let! dimensionValue = dimensionValueByName context dimension currentName
        do! confirmDimensionValueNameFree context dimension newName (Some(dimensionValue |> DimensionValue.dimensionValueId))
        return! dimensionValue |> DimensionValue.dimensionValueId |> DimensionValue.rename context newName
    }

let listDimensionValues (context: Context.Context) (dimension: Dimension) : Result<DimensionValue.DimensionValue list, IAppError> =
    DimensionValue.fetchByDimension context dimension
    |> Result.map (List.sortBy (DimensionValue.dimensionValueName >> DimensionValueName.value))

// ---- Securities ----

/// A Security as listed: the name of its value in each dimension that has one.
type SecurityView = {
    security: Security.Security
    dimensionValueNames: Map<Dimension, string>
}

let fetchSecurityByName (context: Context.Context) (name: SecurityName) : Result<Security.Security, IAppError> =
    result {
        let! found = Security.fetchByName context name
        match found with
        | Some security -> return security
        | None -> return! error (PositionsSecurityNameDoesntMatch(name |> SecurityName.value))
    }

let private confirmSecurityNameFree
    (context: Context.Context)
    (name: SecurityName)
    (self: SecurityId option)
    : Result<unit, IAppError> =
    result {
        let! found = Security.fetchByName context name
        match found with
        | Some security when Some(security |> Security.securityId) <> self ->
            return! error (PositionsSecurityNameAlreadyExists(name |> SecurityName.value))
        | _ -> return ()
    }

let private confirmTickerFree (context: Context.Context) (ticker: Ticker option) (self: SecurityId option) =
    match ticker with
    | None -> Ok()
    | Some t ->
        result {
            let! found = Security.fetchByTicker context t
            match found with
            | Some security when Some(security |> Security.securityId) <> self ->
                return! error (PositionsTickerAlreadyExists(t |> Ticker.value))
            | _ -> return ()
        }

let private confirmEachDimensionOnce (dimensions: Dimension list) : Result<unit, IAppError> =
    match dimensions |> List.countBy id |> List.tryFind (fun (_, count) -> count > 1) with
    | Some(dimension, _) -> error (PositionsSecurityDimensionGivenTwice(dimension |> Dimension.toString))
    | None -> Ok()

// Each value is looked up within the dimension it is given for, so a value can only ever land in its own dimension's
// slot.
let private resolveDimensionValues
    (context: Context.Context)
    (givenValues: (Dimension * DimensionValueName option) list)
    : Result<Map<Dimension, DimensionValueId option>, IAppError> =
    result {
        do! givenValues |> List.map fst |> confirmEachDimensionOnce
        let! resolved =
            givenValues
            |> List.map (fun (dimension, name) ->
                match name with
                | None -> Ok(dimension, None)
                | Some n ->
                    dimensionValueByName context dimension n
                    |> Result.map (fun dv -> dimension, Some(dv |> DimensionValue.dimensionValueId)))
            |> convertListOfResultsToResultsList
        return resolved |> Map.ofList
    }

let createSecurity
    (context: Context.Context)
    (name: SecurityName)
    (ticker: Ticker option)
    (dimensionValues: (Dimension * DimensionValueName) list)
    : Result<Security.Security, IAppError> =
    let instant = context |> Context.getInitiationInstant
    result {
        do! confirmSecurityNameFree context name None
        do! confirmTickerFree context ticker None
        let! resolved = dimensionValues |> List.map (fun (d, n) -> d, Some n) |> resolveDimensionValues context
        let assigned = resolved |> Map.toList |> List.choose (fun (d, v) -> v |> Option.map (fun id -> d, id)) |> Map.ofList
        let security = Security.create (SecurityId.create ()) name ticker assigned instant instant
        do! security |> Security.persist context
        return security
    }

/// A dimension given with Some name is set to that value; with None it is cleared; a dimension not given is unchanged.
let updateSecurity
    (context: Context.Context)
    (currentName: SecurityName)
    (nameUpdate: FieldUpdate<SecurityName>)
    (tickerUpdate: FieldUpdate<Ticker option>)
    (dimensionValueUpdates: (Dimension * DimensionValueName option) list)
    : Result<Security.Security, IAppError> =
    result {
        let! security = fetchSecurityByName context currentName
        let self = Some(security |> Security.securityId)
        do!
            match nameUpdate with
            | SetTo newName -> confirmSecurityNameFree context newName self
            | NoChange -> Ok()
        do!
            match tickerUpdate with
            | SetTo newTicker -> confirmTickerFree context newTicker self
            | NoChange -> Ok()
        let! resolved = resolveDimensionValues context dimensionValueUpdates
        return!
            Security.update
                context
                { securityIdToUpdate = security |> Security.securityId
                  securityNameUpdate = nameUpdate
                  tickerUpdate = tickerUpdate
                  dimensionValueUpdates = resolved }
    }

let private dimensionValueNamesById (context: Context.Context) : Result<Map<DimensionValueId, string>, IAppError> =
    DimensionValue.fetchAll context
    |> Result.map (
        List.map (fun dv -> dv |> DimensionValue.dimensionValueId, dv |> DimensionValue.dimensionValueName |> DimensionValueName.value)
        >> Map.ofList
    )

let private viewWith (valueNames: Map<DimensionValueId, string>) (security: Security.Security) : SecurityView =
    { security = security
      dimensionValueNames = security |> Security.dimensionValues |> Map.map (fun _ valueId -> valueNames |> Map.find valueId) }

let viewSecurity (context: Context.Context) (security: Security.Security) : Result<SecurityView, IAppError> =
    dimensionValueNamesById context |> Result.map (fun valueNames -> viewWith valueNames security)

let listSecurities (context: Context.Context) : Result<SecurityView list, IAppError> =
    result {
        let! securities = Security.fetchAll context
        let! valueNames = dimensionValueNamesById context
        return securities |> List.sortBy (Security.securityName >> SecurityName.value) |> List.map (viewWith valueNames)
    }

// ---- Investment Accounts ----

type NewInvestmentAccount = {
    name: InvestmentAccountName
    institution: Institution
    accountGroup: AccountGroup
    taxTreatment: TaxTreatment
    owners: PersonName list
    activityPeriod: ActivityPeriod.ActivityPeriod
    ledgerAccountId: AccountId option
}

/// ownersUpdate carries the complete new set of owners.
type InvestmentAccountUpdate = {
    currentName: InvestmentAccountName
    nameUpdate: FieldUpdate<InvestmentAccountName>
    institutionUpdate: FieldUpdate<Institution>
    accountGroupUpdate: FieldUpdate<AccountGroup>
    taxTreatmentUpdate: FieldUpdate<TaxTreatment>
    ownersUpdate: FieldUpdate<PersonName list>
    activeBeginUpdate: FieldUpdate<LocalDate>
    activeEndUpdate: FieldUpdate<LocalDate option>
    ledgerAccountIdUpdate: FieldUpdate<AccountId option>
}

/// An Investment Account as listed: its owners' names and its linked ledger account's code and name.
type InvestmentAccountView = {
    investmentAccount: InvestmentAccount.InvestmentAccount
    ownerNames: string list
    ledgerAccountCodeAndName: (string * string) option
}

let fetchInvestmentAccountByName
    (context: Context.Context)
    (name: InvestmentAccountName)
    : Result<InvestmentAccount.InvestmentAccount, IAppError> =
    result {
        let! found = InvestmentAccount.fetchByName context name
        match found with
        | Some account -> return account
        | None -> return! error (PositionsInvestmentAccountNameDoesntMatch(name |> InvestmentAccountName.value))
    }

let private confirmInvestmentAccountNameFree
    (context: Context.Context)
    (name: InvestmentAccountName)
    (self: InvestmentAccountId option)
    : Result<unit, IAppError> =
    result {
        let! found = InvestmentAccount.fetchByName context name
        match found with
        | Some account when Some(account |> InvestmentAccount.investmentAccountId) <> self ->
            return! error (PositionsInvestmentAccountNameAlreadyExists(name |> InvestmentAccountName.value))
        | _ -> return ()
    }

let private confirmInvestmentAccountLink
    (context: Context.Context)
    (self: InvestmentAccountId)
    (ledgerAccountId: AccountId)
    : Result<unit, IAppError> =
    result {
        let! account = ledgerAccountId |> fetchLedgerAccount context
        do!
            match account |> Account.accountType, account |> Account.accountSubType with
            | AccountType.Asset, Some AccountSubtype.Investment -> Ok()
            | _ ->
                let accountType, subtype = describeType account
                error (PositionsInvestmentLedgerAccountNotAssetInvestment(codeOf account, accountType, subtype))
        let! linked = ledgerAccountId |> InvestmentAccount.fetchByLedgerAccountId context
        match linked with
        | Some other when other |> InvestmentAccount.investmentAccountId <> self ->
            let name = other |> InvestmentAccount.investmentAccountName |> InvestmentAccountName.value
            return! error (PositionsInvestmentLedgerAccountAlreadyLinked(codeOf account, name))
        | _ -> return ()
    }

let private resolveOwners
    (context: Context.Context)
    (accountName: InvestmentAccountName)
    (taxTreatment: TaxTreatment)
    (owners: PersonName list)
    : Result<Set<PersonId>, IAppError> =
    let shownName = accountName |> InvestmentAccountName.value
    let ownerNames = owners |> List.map PersonName.value
    result {
        do! if owners |> List.isEmpty then error (PositionsInvestmentAccountHasNoOwners shownName) else Ok()
        do!
            match ownerNames |> List.countBy id |> List.tryFind (fun (_, count) -> count > 1) with
            | Some(repeated, _) -> error (PositionsInvestmentAccountOwnerRepeated(shownName, repeated))
            | None -> Ok()
        let! persons =
            owners
            |> List.map (PersonOrchestration.fetchPersonByName context)
            |> convertListOfResultsToResultsList
        do!
            if persons.Length > 1 && not (taxTreatment |> TaxTreatment.allowsJointOwnership) then
                error (
                    PositionsInvestmentAccountOwnersNotAllowed(shownName, taxTreatment |> TaxTreatment.toString, persons.Length)
                )
            else
                Ok()
        return persons |> List.map personId |> Set.ofList
    }

let createInvestmentAccount
    (context: Context.Context)
    (newAccount: NewInvestmentAccount)
    : Result<InvestmentAccount.InvestmentAccount, IAppError> =
    let instant = context |> Context.getInitiationInstant
    let investmentAccountId = InvestmentAccountId.create ()
    result {
        do! confirmInvestmentAccountNameFree context newAccount.name None
        let! owners = resolveOwners context newAccount.name newAccount.taxTreatment newAccount.owners
        do!
            match newAccount.ledgerAccountId with
            | Some ledgerAccountId -> ledgerAccountId |> confirmInvestmentAccountLink context investmentAccountId
            | None -> Ok()
        let account =
            InvestmentAccount.create
                investmentAccountId
                newAccount.name
                newAccount.institution
                newAccount.accountGroup
                newAccount.taxTreatment
                owners
                newAccount.activityPeriod
                newAccount.ledgerAccountId
                instant
                instant
        do! account |> InvestmentAccount.persist context
        return account
    }

let private confirmHoldingsAllowTaxTreatment
    (context: Context.Context)
    (account: InvestmentAccount.InvestmentAccount)
    (taxTreatment: TaxTreatment)
    : Result<unit, IAppError> =
    result {
        let! holdings = account |> InvestmentAccount.investmentAccountId |> Holding.fetchByInvestmentAccount context
        let offending =
            holdings |> List.filter (fun h -> not (h |> Holding.basisMethod |> BasisMethod.isAllowedFor taxTreatment))
        if not (offending |> List.isEmpty) then
            let! securityNames =
                offending
                |> List.map (fun h ->
                    h |> Holding.securityId |> Security.fetchById context |> Result.map (Security.securityName >> SecurityName.value))
                |> convertListOfResultsToResultsList
            return!
                error (
                    PositionsTaxTreatmentChangeBreaksHoldings(
                        account |> InvestmentAccount.investmentAccountName |> InvestmentAccountName.value,
                        taxTreatment |> TaxTreatment.toString,
                        securityNames |> List.sort
                    )
                )
    }

// Only a Roth account's snapshot carries a contribution basis, and a recorded basis is never cleared, so an account
// whose snapshots carry one stays Roth.
let private confirmNoContributionBasisStranded
    (context: Context.Context)
    (account: InvestmentAccount.InvestmentAccount)
    (taxTreatment: TaxTreatment)
    : Result<unit, IAppError> =
    match account |> InvestmentAccount.taxTreatment, taxTreatment with
    | TaxTreatment.Roth, TaxTreatment.Roth -> Ok()
    | TaxTreatment.Roth, _ ->
        result {
            let! snapshots =
                account |> InvestmentAccount.investmentAccountId |> AccountSnapshotHeader.fetchByInvestmentAccount context
            let datesWithBasis =
                snapshots
                |> List.filter (AccountSnapshotHeader.contributionBasis >> Option.isSome)
                |> List.map AccountSnapshotHeader.snapshotDate
            if not (datesWithBasis |> List.isEmpty) then
                return!
                    error (
                        PositionsTaxTreatmentChangeStrandsContributionBasis(
                            account |> InvestmentAccount.investmentAccountName |> InvestmentAccountName.value,
                            datesWithBasis |> List.min,
                            datesWithBasis |> List.max
                        )
                    )
        }
    | _ -> Ok()

let private confirmPeriodKeepsSnapshots
    (context: Context.Context)
    (account: InvestmentAccount.InvestmentAccount)
    (activityPeriod: ActivityPeriod.ActivityPeriod)
    : Result<unit, IAppError> =
    result {
        let! snapshots = account |> InvestmentAccount.investmentAccountId |> AccountSnapshotHeader.fetchByInvestmentAccount context
        let offendingDates =
            snapshots
            |> List.map AccountSnapshotHeader.snapshotDate
            |> List.filter (fun d -> not (activityPeriod |> ActivityPeriod.isActive d))
        if not (offendingDates |> List.isEmpty) then
            return!
                error (
                    PositionsActivePeriodExcludesSnapshots(
                        account |> InvestmentAccount.investmentAccountName |> InvestmentAccountName.value,
                        offendingDates |> List.min,
                        offendingDates |> List.max
                    )
                )
    }

let updateInvestmentAccount
    (context: Context.Context)
    (accountUpdate: InvestmentAccountUpdate)
    : Result<InvestmentAccount.InvestmentAccount, IAppError> =
    result {
        let! account = fetchInvestmentAccountByName context accountUpdate.currentName
        let self = account |> InvestmentAccount.investmentAccountId
        do!
            match accountUpdate.nameUpdate with
            | SetTo newName -> confirmInvestmentAccountNameFree context newName (Some self)
            | NoChange -> Ok()
        let resultingName = accountUpdate.nameUpdate |> valueOrCurrent (account |> InvestmentAccount.investmentAccountName)
        let resultingTreatment = accountUpdate.taxTreatmentUpdate |> valueOrCurrent (account |> InvestmentAccount.taxTreatment)
        let! ownersUpdate =
            match accountUpdate.ownersUpdate with
            | SetTo owners -> resolveOwners context resultingName resultingTreatment owners |> Result.map SetTo
            | NoChange ->
                let currentOwnerCount = account |> InvestmentAccount.owners |> Set.count
                if currentOwnerCount > 1 && not (resultingTreatment |> TaxTreatment.allowsJointOwnership) then
                    error (
                        PositionsInvestmentAccountOwnersNotAllowed(
                            resultingName |> InvestmentAccountName.value,
                            resultingTreatment |> TaxTreatment.toString,
                            currentOwnerCount
                        )
                    )
                else
                    Ok NoChange
        do!
            match accountUpdate.taxTreatmentUpdate with
            | SetTo newTreatment -> confirmHoldingsAllowTaxTreatment context account newTreatment
            | NoChange -> Ok()
        do!
            match accountUpdate.taxTreatmentUpdate with
            | SetTo newTreatment -> confirmNoContributionBasisStranded context account newTreatment
            | NoChange -> Ok()
        let! activityPeriodUpdate =
            match accountUpdate.activeBeginUpdate, accountUpdate.activeEndUpdate with
            | NoChange, NoChange -> Ok NoChange
            | beginUpdate, endUpdate ->
                let current = account |> InvestmentAccount.activityPeriod
                result {
                    let! period =
                        ActivityPeriod.create
                            (beginUpdate |> valueOrCurrent (current |> ActivityPeriod.activeBegin))
                            (endUpdate |> valueOrCurrent (current |> ActivityPeriod.activeEnd))
                            ActivityPeriod.NotConsideredAvailableBeforeBeginDate
                    do! confirmPeriodKeepsSnapshots context account period
                    return SetTo period
                }
        do!
            match accountUpdate.ledgerAccountIdUpdate with
            | SetTo(Some ledgerAccountId) -> ledgerAccountId |> confirmInvestmentAccountLink context self
            | _ -> Ok()
        return!
            InvestmentAccount.update
                context
                { investmentAccountIdToUpdate = self
                  investmentAccountNameUpdate = accountUpdate.nameUpdate
                  institutionUpdate = accountUpdate.institutionUpdate
                  accountGroupUpdate = accountUpdate.accountGroupUpdate
                  taxTreatmentUpdate = accountUpdate.taxTreatmentUpdate
                  ownersUpdate = ownersUpdate
                  activityPeriodUpdate = activityPeriodUpdate
                  ledgerAccountIdUpdate = accountUpdate.ledgerAccountIdUpdate }
    }

let viewInvestmentAccount
    (context: Context.Context)
    (account: InvestmentAccount.InvestmentAccount)
    : Result<InvestmentAccountView, IAppError> =
    result {
        let! ownerNames = account |> InvestmentAccount.owners |> PersonOrchestration.personNamesOf context
        let! ledger =
            account
            |> InvestmentAccount.ledgerAccountId
            |> convertOptionToDesiredTypeWithFallibleConverter (ledgerAccountCodeAndName context)
        return { investmentAccount = account; ownerNames = ownerNames; ledgerAccountCodeAndName = ledger }
    }

let listInvestmentAccounts (context: Context.Context) : Result<InvestmentAccountView list, IAppError> =
    result {
        let! accounts = InvestmentAccount.fetchAll context
        return!
            accounts
            |> List.sortBy (InvestmentAccount.investmentAccountName >> InvestmentAccountName.value)
            |> List.map (viewInvestmentAccount context)
            |> convertListOfResultsToResultsList
    }

// ---- Holdings ----

/// A Holding as listed, with its account's and Security's names.
type HoldingView = {
    holding: Holding.Holding
    investmentAccountName: string
    securityName: string
}

let private confirmBasisMethodAllowed
    (account: InvestmentAccount.InvestmentAccount)
    (security: Security.Security)
    (basisMethod: BasisMethod option)
    : Result<unit, IAppError> =
    let taxTreatment = account |> InvestmentAccount.taxTreatment
    if basisMethod |> BasisMethod.isAllowedFor taxTreatment then
        Ok()
    else
        error (
            PositionsBasisMethodNotAllowed(
                account |> InvestmentAccount.investmentAccountName |> InvestmentAccountName.value,
                security |> Security.securityName |> SecurityName.value,
                taxTreatment |> TaxTreatment.toString
            )
        )

let createHolding
    (context: Context.Context)
    (accountName: InvestmentAccountName)
    (securityName: SecurityName)
    (basisMethod: BasisMethod option)
    : Result<Holding.Holding, IAppError> =
    let instant = context |> Context.getInitiationInstant
    result {
        let! account = fetchInvestmentAccountByName context accountName
        let! security = fetchSecurityByName context securityName
        let accountId = account |> InvestmentAccount.investmentAccountId
        let securityId = security |> Security.securityId
        let! existing = Holding.fetchByInvestmentAccountAndSecurity context accountId securityId
        do!
            if existing |> Option.isSome then
                error (
                    PositionsHoldingAlreadyExists(
                        accountName |> InvestmentAccountName.value,
                        securityName |> SecurityName.value
                    )
                )
            else
                Ok()
        do! confirmBasisMethodAllowed account security basisMethod
        let holding = Holding.create (HoldingId.create ()) accountId securityId basisMethod instant instant
        do! holding |> Holding.persist context
        return holding
    }

let changeHoldingBasisMethod
    (context: Context.Context)
    (accountName: InvestmentAccountName)
    (securityName: SecurityName)
    (basisMethod: BasisMethod option)
    : Result<Holding.Holding, IAppError> =
    result {
        let! account = fetchInvestmentAccountByName context accountName
        let! security = fetchSecurityByName context securityName
        let! existing =
            Holding.fetchByInvestmentAccountAndSecurity
                context
                (account |> InvestmentAccount.investmentAccountId)
                (security |> Security.securityId)
        let! holding =
            match existing with
            | Some holding -> Ok holding
            | None ->
                error (
                    PositionsHoldingDoesntExist(accountName |> InvestmentAccountName.value, securityName |> SecurityName.value)
                )
        do! confirmBasisMethodAllowed account security basisMethod
        return! holding |> Holding.holdingId |> Holding.updateBasisMethod context basisMethod
    }

/// Deletes the Holding and returns it as it stood before deletion.
let deleteHolding
    (context: Context.Context)
    (accountName: InvestmentAccountName)
    (securityName: SecurityName)
    : Result<Holding.Holding, IAppError> =
    let shownAccount = accountName |> InvestmentAccountName.value
    let shownSecurity = securityName |> SecurityName.value
    result {
        let! account = fetchInvestmentAccountByName context accountName
        let! security = fetchSecurityByName context securityName
        let! existing =
            Holding.fetchByInvestmentAccountAndSecurity
                context
                (account |> InvestmentAccount.investmentAccountId)
                (security |> Security.securityId)
        let! holding =
            match existing with
            | Some holding -> Ok holding
            | None -> error (PositionsHoldingDoesntExist(shownAccount, shownSecurity))
        let holdingId = holding |> Holding.holdingId
        let! referenced = holdingId |> AccountSnapshotLine.existsForHolding context
        do! if referenced then error (PositionsHoldingReferencedBySnapshots(shownAccount, shownSecurity)) else Ok()
        do! holdingId |> Holding.delete context
        return holding
    }

let viewHolding (context: Context.Context) (holding: Holding.Holding) : Result<HoldingView, IAppError> =
    result {
        let! account = holding |> Holding.investmentAccountId |> InvestmentAccount.fetchById context
        let! security = holding |> Holding.securityId |> Security.fetchById context
        return
            { holding = holding
              investmentAccountName = account |> InvestmentAccount.investmentAccountName |> InvestmentAccountName.value
              securityName = security |> Security.securityName |> SecurityName.value }
    }

let listHoldings (context: Context.Context) (accountName: InvestmentAccountName option) : Result<HoldingView list, IAppError> =
    result {
        let! holdings =
            match accountName with
            | Some name ->
                fetchInvestmentAccountByName context name
                |> Result.bind (InvestmentAccount.investmentAccountId >> Holding.fetchByInvestmentAccount context)
            | None -> Holding.fetchAll context
        let! views = holdings |> List.map (viewHolding context) |> convertListOfResultsToResultsList
        return views |> List.sortBy (fun v -> v.investmentAccountName, v.securityName)
    }
