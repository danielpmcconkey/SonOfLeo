module Business.CrossDomainOrchestration.InvestmentAccountOrchestration

open App.Utility.IAppError
open App.Utility.Result
open App.Utility.FieldUpdate
open App.Session
open Business.General
open Business.General.PersonComponent
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Positions
open Business.FinancialServices.Positions.PositionsError
open Business.FinancialServices.Positions.PositionsComponent
open Business.CrossDomainOrchestration.PositionsLedgerLinks

/// An Investment Account as listed: its owners' names and its linked ledger account's code and name.
type InvestmentAccountView = {
    investmentAccount: InvestmentAccount.InvestmentAccount
    ownerNames: string list
    ledgerAccountCodeAndName: (string * string) option
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

let private confirmJointOwnershipAllowed
    (accountName: InvestmentAccountName)
    (taxTreatment: TaxTreatment)
    (ownerCount: int)
    : Result<unit, IAppError> =
    if ownerCount > 1 && not (taxTreatment |> TaxTreatment.allowsJointOwnership) then
        error (
            PositionsInvestmentAccountOwnersNotAllowed(
                accountName |> InvestmentAccountName.value,
                taxTreatment |> TaxTreatment.toString,
                ownerCount
            )
        )
    else
        Ok()

/// Confirms the owners given for an Investment Account: at least one, no Person twice, each an existing Person. Returns
/// them as the account's set of owners.
let ownerSetOf
    (context: Context.Context)
    (accountName: InvestmentAccountName)
    (owners: PersonId list)
    : Result<Set<PersonId>, IAppError> =
    let shownName = accountName |> InvestmentAccountName.value
    result {
        do! if owners |> List.isEmpty then error (PositionsInvestmentAccountHasNoOwners shownName) else Ok()
        let! persons = owners |> List.map (Person.fetchById context) |> convertListOfResultsToResultsList
        do!
            match
                persons
                |> List.map (Person.personName >> PersonName.value)
                |> List.countBy id
                |> List.tryFind (fun (_, count) -> count > 1)
            with
            | Some(repeated, _) -> error (PositionsInvestmentAccountOwnerRepeated(shownName, repeated))
            | None -> Ok()
        return owners |> Set.ofList
    }

let constructNewAndPersist
    (context: Context.Context)
    (name: InvestmentAccountName)
    (institution: Institution)
    (accountGroup: AccountGroup)
    (taxTreatment: TaxTreatment)
    (owners: PersonId list)
    (activityPeriod: ActivityPeriod.ActivityPeriod)
    (ledgerAccountId: AccountId option)
    : Result<InvestmentAccount.InvestmentAccount, IAppError> =
    let instant = context |> Context.getInitiationInstant
    let investmentAccountId = InvestmentAccountId.create ()
    result {
        do! confirmInvestmentAccountNameFree context name None
        let! ownerSet = ownerSetOf context name owners
        do! confirmJointOwnershipAllowed name taxTreatment ownerSet.Count
        do!
            match ledgerAccountId with
            | Some id -> id |> confirmInvestmentAccountLink context investmentAccountId
            | None -> Ok()
        let account =
            InvestmentAccount.create
                investmentAccountId
                name
                institution
                accountGroup
                taxTreatment
                ownerSet
                activityPeriod
                ledgerAccountId
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

/// ownersUpdate carries the complete new set of owners.
let updateInvestmentAccount
    (context: Context.Context)
    (fieldUpdates: InvestmentAccount.InvestmentAccountFieldUpdates)
    : Result<InvestmentAccount.InvestmentAccount, IAppError> =
    result {
        let self = fieldUpdates.investmentAccountIdToUpdate
        let! account = self |> InvestmentAccount.fetchById context
        do!
            match fieldUpdates.investmentAccountNameUpdate with
            | SetTo newName -> confirmInvestmentAccountNameFree context newName (Some self)
            | NoChange -> Ok()
        let resultingName =
            fieldUpdates.investmentAccountNameUpdate |> valueOrCurrent (account |> InvestmentAccount.investmentAccountName)
        let resultingTreatment = fieldUpdates.taxTreatmentUpdate |> valueOrCurrent (account |> InvestmentAccount.taxTreatment)
        let! resultingOwners =
            match fieldUpdates.ownersUpdate with
            | SetTo owners -> owners |> Set.toList |> ownerSetOf context resultingName
            | NoChange -> Ok(account |> InvestmentAccount.owners)
        do! confirmJointOwnershipAllowed resultingName resultingTreatment resultingOwners.Count
        do!
            match fieldUpdates.taxTreatmentUpdate with
            | SetTo newTreatment -> confirmHoldingsAllowTaxTreatment context account newTreatment
            | NoChange -> Ok()
        do!
            match fieldUpdates.taxTreatmentUpdate with
            | SetTo newTreatment -> confirmNoContributionBasisStranded context account newTreatment
            | NoChange -> Ok()
        do!
            match fieldUpdates.activityPeriodUpdate with
            | SetTo period -> confirmPeriodKeepsSnapshots context account period
            | NoChange -> Ok()
        do!
            match fieldUpdates.ledgerAccountIdUpdate with
            | SetTo(Some ledgerAccountId) -> ledgerAccountId |> confirmInvestmentAccountLink context self
            | _ -> Ok()
        return! InvestmentAccount.update context fieldUpdates
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
